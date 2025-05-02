using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class WFCBuildingGenerator : MonoBehaviour
{
    [Header("Module Definitions")]
    public List<WFCModule> allModules = new List<WFCModule>();

    [Header("Generation Parameters")]
    public int minSegments = 8;
    public int maxSegments = 20; // Max footprint complexity
    public int minHeight = 2; // Min number of middle floors
    public int maxHeight = 5; // Max number of middle floors

    [Header("Closure Parameters")]
    [Tooltip("How close the end point needs to be to the start point to attempt closing.")]
    public float closureDistanceThreshold = 2.0f;
    [Tooltip("Maximum angle difference (degrees) allowed between the final segment's direction and the direction needed to face the start point.")]
    public float closureAngleThreshold = 15.0f; // Allow some tolerance
    public int maxPlacementAttempts = 50; // Prevent infinite loops if closure is impossible

    [Header("Placement Settings")]
    public Transform generationParent; // Optional: Parent generated buildings under this transform
    public Vector3 startPosition = Vector3.zero;

    // --- Internal State ---
    private List<PlacedModuleInfo> placedModules = new List<PlacedModuleInfo>();
    private Vector3 currentPosition;
    private Quaternion currentRotation;
    private SocketType requiredInputSocket; // What the *next* module needs to connect to

    // Helper struct to keep track of placed modules and their transforms
    private struct PlacedModuleInfo
    {
        public WFCModule module;
        public Vector3 position;
        public Quaternion rotation;
        public int height; // Number of middle floors
    }

    // Call this function to start generation (e.g., from an editor button)
    public void GenerateBuilding()
    {
        ClearPreviousGeneration();
        InitializeGeneration();
        BuildFootprint();
        InstantiateBuilding();
    }

    public void ClearPreviousGeneration()
    {
        if (generationParent == null)
        {
            // If no parent specified, try to find and destroy objects tagged appropriately
            // Or simply destroy children of this generator's transform
            foreach (Transform child in transform)
            {
                // Add a check here if you have other children you want to keep
                DestroyImmediate(child.gameObject); // Use Destroy in Play mode
            }
        }
        else
        {
            // Destroy children of the specified parent
            foreach (Transform child in generationParent)
            {
                DestroyImmediate(child.gameObject); // Use Destroy in Play mode
            }
        }
        placedModules.Clear();
    }


    void InitializeGeneration()
    {
        placedModules.Clear();
        currentPosition = startPosition;
        currentRotation = Quaternion.identity;

        // Start with a module that expects a 'Straight' input (or define a specific start module type)
        requiredInputSocket = SocketType.Straight; // Assumption: Buildings start straight
    }

    void BuildFootprint()
    {
        int segmentsToPlace = Random.Range(minSegments, maxSegments + 1);
        int attempts = 0;

        for (int i = 0; i < segmentsToPlace && attempts < maxPlacementAttempts; i++)
        {
            attempts++;
            List<WFCModule> possibleModules = FindCompatibleModules(requiredInputSocket);

            if (possibleModules.Count == 0)
            {
                Debug.LogError($"No compatible modules found for socket type {requiredInputSocket} at segment {i}. Stopping generation.");
                return; // Failed
            }

            // --- Closure Check (Attempt to close loop if near the end and geometrically possible) ---
            bool tryingToClose = (i >= minSegments - 1); // Only try closing after minimum length
            WFCModule chosenModule = null;

            if (tryingToClose)
            {
                chosenModule = TryFindClosingModule(possibleModules);
            }

            // --- If not closing or closure module not found, pick randomly based on weight ---
            if (chosenModule == null)
            {
                chosenModule = SelectRandomWeightedModule(possibleModules);
                if (chosenModule == null)
                {
                    Debug.LogError($"Failed to select a module (check weights?) for socket {requiredInputSocket}. Stopping generation.");
                    return; // Should not happen if possibleModules is not empty
                }
            }

            // --- Place the chosen module (logically) ---
            int chosenHeight = Random.Range(minHeight, maxHeight + 1);
            placedModules.Add(new PlacedModuleInfo
            {
                module = chosenModule,
                position = currentPosition,
                rotation = currentRotation,
                height = chosenHeight
            });

            // --- Update position and rotation for the NEXT segment ---
            // Move forward along the current segment's direction
            currentPosition += currentRotation * Vector3.forward * chosenModule.segmentLength;
            // Apply the rotation defined by the placed module
            currentRotation *= Quaternion.Euler(0, chosenModule.placementRotationY, 0);

            // Set the requirement for the next module's input socket
            requiredInputSocket = chosenModule.outputSocket;

            // --- Check if we successfully closed the loop ---
            if (tryingToClose && chosenModule != null && IsLoopClosed(chosenModule))
            {
                Debug.Log($"Loop closed successfully after {i + 1} segments.");
                return; // Finished footprint
            }

            // Reset attempt counter for the next segment if successful
            attempts = 0;
        }

        if (attempts >= maxPlacementAttempts)
        {
            Debug.LogWarning("Reached max placement attempts without closing the loop. Result might be incomplete or open.");
        }
        else if (placedModules.Count < segmentsToPlace)
        {
            // This case can happen if loop closes early
            Debug.Log($"Loop closed early after {placedModules.Count} segments.");
        }
        else
        {
            Debug.LogWarning("Finished placing max segments without closing the loop. Result will be open.");
        }
    }

    List<WFCModule> FindCompatibleModules(SocketType inputSocketNeeded)
    {
        // Simple compatibility: input socket must match. Extend this logic if needed.
        // For example, allow 'Straight' input to connect to any 'Corner' output, etc.
        return allModules.Where(m => CanConnect(inputSocketNeeded, m.inputSocket)).ToList();

        // --- Example of more complex compatibility rule ---
        // return allModules.Where(m => {
        //     if (inputSocketNeeded == SocketType.Straight) {
        //         // Straight output can connect to Straight input OR Corner inputs
        //         return m.inputSocket == SocketType.Straight ||
        //                m.inputSocket == SocketType.CornerIn45 || // Assuming corners always expect straight input
        //                m.inputSocket == SocketType.CornerOut45;
        //     } else {
        //         // Corner outputs (like CornerOut45) might only connect to specific inputs (like Straight)
        //         return m.inputSocket == SocketType.Straight; // Example: corners must be followed by straight
        //     }
        // }).ToList();
    }

    // Define your connection logic here
    bool CanConnect(SocketType previousOutput, SocketType currentInput)
    {
        // Simplest: Exact match required
        // return previousOutput == currentInput;

        // Flexible: Allow Straight output to connect to ANY input type
        // Allow Corner outputs only connect to Straight inputs
        if (previousOutput == SocketType.Straight)
        {
            return true; // Straight can lead into anything
        }
        else
        {
            // Any corner type must be followed by a straight segment
            return currentInput == SocketType.Straight;
        }
    }


    WFCModule SelectRandomWeightedModule(List<WFCModule> modules)
    {
        float totalWeight = modules.Sum(m => m.probabilityWeight);
        float randomValue = Random.Range(0, totalWeight);
        float currentWeight = 0;

        foreach (var module in modules)
        {
            currentWeight += module.probabilityWeight;
            if (randomValue <= currentWeight)
            {
                return module;
            }
        }
        return modules.LastOrDefault(); // Fallback
    }

    WFCModule TryFindClosingModule(List<WFCModule> possibleModules)
    {
        Vector3 directionToStart = (startPosition - currentPosition).normalized;
        float distanceToStart = Vector3.Distance(currentPosition, startPosition);

        if (distanceToStart > closureDistanceThreshold)
        {
            return null; // Too far away to close
        }

        foreach (var module in possibleModules)
        {
            // Calculate where this module would end up and its final orientation
            Quaternion nextRotation = currentRotation * Quaternion.Euler(0, module.placementRotationY, 0);
            Vector3 nextPosition = currentPosition + currentRotation * Vector3.forward * module.segmentLength; // Position AFTER this segment

            // Check if the final orientation (nextRotation) points towards the start
            Vector3 finalForward = nextRotation * Vector3.forward;
            float angleDifference = Vector3.Angle(finalForward, directionToStart);

            // Check if the module's length roughly matches the remaining distance
            // And if the angle allows connection back to the start module's expected input
            // And if the output socket of this potential closing module is compatible
            // with the input socket of the VERY FIRST module placed (needs adjustment for rotation).

            // Simplified check: Is the next position close enough?
            // Is the angle after placement reasonably aligned to point back?
            // Is the module's output socket compatible with the initial required input socket?

            float finalDist = Vector3.Distance(nextPosition, startPosition);
            // A more robust check involves the sockets of the first and last module.
            // The last module's output socket, rotated by the final rotation, must match the
            // first module's input socket type (which we assumed was SocketType.Straight).
            bool socketsMatch = CanConnect(module.outputSocket, SocketType.Straight); // Assuming start always needs Straight


            // Let's use a simpler check for now: distance and socket type
            if (finalDist < closureDistanceThreshold * 0.5f && socketsMatch) // Use smaller threshold for final segment
            {
                Debug.Log($"Found potential closing module: {module.moduleName}. Final distance: {finalDist}");
                // Ideally, you would adjust the last segment's length/rotation slightly
                // to guarantee perfect closure, but for now, we just select it.
                return module;
            }

            // --- More Accurate Angle Check (Optional - Requires more careful geometry) ---
            // This estimates if placing the module would align the *next* segment towards start
            // Vector3 directionAfterPlacement = nextRotation * Vector3.forward;
            // float angleToStartAfterPlace = Vector3.Angle(directionAfterPlacement, (startPosition - nextPosition).normalized);
            // if (angleToStartAfterPlace < closureAngleThreshold && socketsMatch) { ... }
        }

        return null; // No suitable closing module found
    }

    // Check if the loop is considered closed based on the last placed module
    // This is a simplified check after placement.
    bool IsLoopClosed(WFCModule lastModule)
    {
        float finalDistance = Vector3.Distance(currentPosition, startPosition);
        bool socketsCompatible = CanConnect(lastModule.outputSocket, SocketType.Straight); // Check against initial requirement

        // Debug.Log($"Checking closure: Distance={finalDistance}, Sockets OK={socketsCompatible}");

        return finalDistance < closureDistanceThreshold && socketsCompatible;
    }


    void InstantiateBuilding()
    {
        if (placedModules.Count == 0)
        {
            Debug.LogWarning("No modules were placed. Nothing to instantiate.");
            return;
        }

        GameObject buildingRoot = new GameObject("ProceduralBuilding");
        if (generationParent != null)
        {
            buildingRoot.transform.SetParent(generationParent);
        }
        buildingRoot.transform.position = startPosition; // Set root position if needed


        foreach (var placedInfo in placedModules)
        {
            WFCModule module = placedInfo.module;
            if (module == null) continue;

            // Create a parent object for this vertical column/segment
            GameObject segmentRoot = new GameObject($"Segment_{module.moduleName}");
            segmentRoot.transform.position = placedInfo.position;
            segmentRoot.transform.rotation = placedInfo.rotation;
            segmentRoot.transform.SetParent(buildingRoot.transform);


            float currentY = 0f;

            // 1. Ground Floor
            if (module.groundFloorPrefab != null)
            {
                InstantiateFloorPiece(module.groundFloorPrefab, segmentRoot.transform, ref currentY);
            }
            else
            {
                Debug.LogWarning($"Module {module.moduleName} missing Ground Floor Prefab");
            }

            // 2. Middle Floors
            if (module.middleFloorPrefabs != null && module.middleFloorPrefabs.Count > 0)
            {
                for (int i = 0; i < placedInfo.height; i++)
                {
                    // Pick a random middle floor variant
                    GameObject prefab = module.middleFloorPrefabs[Random.Range(0, module.middleFloorPrefabs.Count)];
                    InstantiateFloorPiece(prefab, segmentRoot.transform, ref currentY);
                }
            }
            else
            {
                Debug.LogWarning($"Module {module.moduleName} missing Middle Floor Prefabs");
            }


            // 3. Mansard Floor
            if (module.mansardFloorPrefab != null)
            {
                InstantiateFloorPiece(module.mansardFloorPrefab, segmentRoot.transform, ref currentY);
            }
            else
            {
                Debug.LogWarning($"Module {module.moduleName} missing Mansard Floor Prefab");
            }


            // 4. Attic/Roof
            if (module.atticRoofPrefab != null)
            {
                InstantiateFloorPiece(module.atticRoofPrefab, segmentRoot.transform, ref currentY);
            }
            else
            {
                Debug.LogWarning($"Module {module.moduleName} missing Attic Roof Prefab");
            }
        }
        Debug.Log($"Instantiated building with {placedModules.Count} segments.");
    }

    void InstantiateFloorPiece(GameObject prefab, Transform parent, ref float currentY)
    {
        if (prefab == null) return;

        GameObject instance = Instantiate(prefab, parent);
        instance.transform.localPosition = new Vector3(0, currentY, 0);
        instance.transform.localRotation = Quaternion.identity; // Prefabs should be oriented correctly

        // --- IMPORTANT: Determine Height Offset ---
        // You NEED a reliable way to know the height of each prefab piece
        // to stack them correctly. Using a simple fixed offset here,
        // **ADJUST THIS BASED ON YOUR ASSET DIMENSIONS!**
        float prefabHeight = 3.0f; // EXAMPLE VALUE - Get this from Mesh Bounds, Collider, or a script on the prefab

        // Try getting height from renderer bounds
        Renderer rend = instance.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            prefabHeight = rend.bounds.size.y;
            // Note: bounds.size can be inaccurate for complex models or if not centered.
            // A dedicated script on the prefab providing its height is more reliable.
        }
        else
        {
            Debug.LogWarning($"Prefab {prefab.name} has no renderer to determine height. Using default {prefabHeight}m.");
        }
        if (prefabHeight <= 0.1f) prefabHeight = 3.0f; // Sanity check for zero/small bounds


        currentY += prefabHeight;
    }
}