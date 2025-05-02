using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class WFCBuildingGenerator : MonoBehaviour
{
    [Header("Module Definitions")]
    public List<WFCModule> allModules = new List<WFCModule>();

    [Header("Generation Parameters")]
    public int minSegments = 8;
    public int maxSegments = 20;
    public int minHeight = 2;
    public int maxHeight = 5;

    [Header("Closure Parameters")]
    [Tooltip("How close (in meters) the end point must be to the start point to *attempt* choosing a closing module.")]
    public float closureDistanceThreshold = 2.0f;
    [Tooltip("Max angle difference (degrees) for the final segment's direction relative to pointing at the start. (Currently used loosely in simple closure check).")]
    public float closureAngleThreshold = 25.0f; // Increased tolerance slightly
    [Tooltip("Safety break: Max attempts to place a single segment before giving up.")]
    public int maxPlacementAttempts = 50;
    [Tooltip("Safety break: Max total segments generated if loop doesn't close naturally.")]
    public int absoluteMaxSegments = 100; // Prevent runaway generation

    [Header("Placement Settings")]
    public Transform generationParent;
    public Vector3 startPosition = Vector3.zero;
    public Quaternion startRotation = Quaternion.identity; // Allow defining start rotation

    // --- Internal State ---
    private List<PlacedModuleInfo> placedModules = new List<PlacedModuleInfo>();
    private Vector3 currentPosition;
    private Quaternion currentRotation;
    private SocketType requiredInputSocket; // What the *next* module's input must be compatible with

    private struct PlacedModuleInfo
    {
        public WFCModule module;
        public Vector3 position;
        public Quaternion rotation;
        public int height; // Number of middle floors
    }

    public void GenerateBuilding()
    {
        ClearPreviousGeneration();
        InitializeGeneration();
        BuildFootprint();
        InstantiateBuilding();
    }

    public void ClearPreviousGeneration()
    {
        // Use a temporary list to avoid issues while iterating and destroying
        List<GameObject> childrenToDestroy = new List<GameObject>();
        Transform parent = (generationParent != null) ? generationParent : transform;
        foreach (Transform child in parent)
        {
            // Optional: Add a check here if you have other specific children you want to keep
            // e.g., if (!child.CompareTag("KeepMe")) childrenToDestroy.Add(child.gameObject);
            childrenToDestroy.Add(child.gameObject);
        }

        foreach (GameObject child in childrenToDestroy)
        {
            // Use DestroyImmediate in Editor, Destroy in Play mode
            if (Application.isEditor && !Application.isPlaying)
                DestroyImmediate(child);
            else
                Destroy(child);
        }

        placedModules.Clear();
    }


    void InitializeGeneration()
    {
        placedModules.Clear();
        currentPosition = startPosition;
        currentRotation = startRotation; // Use configurable start rotation
        requiredInputSocket = SocketType.Straight; // Buildings typically start with a straight segment's output requirement
    }

    void BuildFootprint()
    {
        // Use a cap based on absoluteMaxSegments, but try for random length first
        int targetSegments = Random.Range(minSegments, maxSegments + 1);
        int placementAttempts = 0; // Attempts for the current segment
        int totalSegmentsPlaced = 0; // Total segments placed so far

        // --- Define constants related to the specific corner geometry ---
        // Ensure this 'L' matches the actual length/width of your segments
        const float L = 0.8f;

        while (totalSegmentsPlaced < targetSegments && totalSegmentsPlaced < absoluteMaxSegments)
        {
            if (placementAttempts >= maxPlacementAttempts)
            {
                Debug.LogError($"Failed to find a compatible module for socket {requiredInputSocket} after {maxPlacementAttempts} attempts at segment {totalSegmentsPlaced}. Stopping.");
                break;
            }

            List<WFCModule> possibleModules = FindCompatibleModules(requiredInputSocket);

            if (possibleModules.Count == 0)
            {
                Debug.LogError($"No compatible modules found for required input socket type {requiredInputSocket} at segment {totalSegmentsPlaced}. Check module definitions and CanConnect logic. Stopping generation.");
                return; // Critical failure
            }

            // --- Closure Check ---
            bool shouldAttemptClosure = (totalSegmentsPlaced >= minSegments - 1) &&
                                       Vector3.Distance(currentPosition, startPosition) < closureDistanceThreshold * 2.0f;

            WFCModule chosenModule = null;

            if (shouldAttemptClosure)
            {
                // Pass the *current* state to TryFindClosingModule, as it needs to simulate placement
                chosenModule = TryFindClosingModule(possibleModules);
            }

            // --- If not attempting closure, or closure failed, select normally ---
            if (chosenModule == null)
            {
                chosenModule = SelectRandomWeightedModule(possibleModules);
                if (chosenModule == null)
                {
                    Debug.LogError($"SelectRandomWeightedModule returned null despite having options. Check weights/logic.");
                    placementAttempts++;
                    continue;
                }
            }

            // --- MODULE PLACEMENT AND STATE UPDATE ---

            // 1. Store the position/rotation *where this module will be placed*
            Vector3 placedPosition = currentPosition;
            Quaternion placedRotation = currentRotation;

            // 2. Calculate the state (position, rotation) for the START of the *next* segment
            Vector3 nextPosition;
            Quaternion nextRotation = placedRotation * Quaternion.Euler(0, chosenModule.placementRotationY, 0); // Rotation is always applied

            // Check if the module we just chose requires special offset handling
            if (chosenModule.isSpecial45DegreeCorner)
            {
                // Use the derived trigonometric offset for the special 45-degree corner
                float angleY = chosenModule.placementRotationY; // Get the specific rotation angle (e.g., 45)

                // Calculate the special offset vector *in the local space of the placed corner module*
                // This vector points from the corner's pivot to where the *next* module's pivot should be.
                Vector3 localOffset = (Vector3.left * (L / 2.0f))
                                    + (Quaternion.Euler(0, angleY, 0) * Vector3.left * (1.5f * L));

                // Convert the local offset to a world-space offset using the corner's placement rotation
                // and add it to the corner's placement position to find the next module's position.
                nextPosition = placedPosition + (placedRotation * localOffset);

                // --- Optional Debug Visualization ---
#if UNITY_EDITOR // Only run this code in the Unity Editor
                // Draw a line showing the calculated offset for debugging
                Debug.DrawLine(placedPosition, nextPosition, Color.cyan, 10f);
                // Draw the corner's forward direction
                Debug.DrawRay(placedPosition, placedRotation * Vector3.forward, Color.blue, 10f);
                // Draw the corner's left direction (the axis the offset is initially based on)
                Debug.DrawRay(placedPosition, placedRotation * Vector3.left, Color.red, 10f);
                // Draw the direction the *next* module will face
                Debug.DrawRay(nextPosition, nextRotation * Vector3.forward, Color.green, 10f);
#endif
            }
            else // Assume it's a standard module (like a straight piece)
            {
                // Use the simple segmentLength logic: move along the current 'left' direction
                // Ensure the straight module's 'segmentLength' field is set correctly (e.g., to L = 0.8)
                nextPosition = placedPosition + (placedRotation * Vector3.left * chosenModule.segmentLength);

                // --- Optional Debug Visualization ---
#if UNITY_EDITOR
                Debug.DrawLine(placedPosition, nextPosition, Color.yellow, 10f);
                Debug.DrawRay(placedPosition, placedRotation * Vector3.forward, Color.blue, 10f);
                Debug.DrawRay(nextPosition, nextRotation * Vector3.forward, Color.green, 10f);
#endif
            }

            // 3. Record the module that was just placed, using its placement position/rotation
            int chosenHeight = Random.Range(minHeight, maxHeight + 1);
            placedModules.Add(new PlacedModuleInfo
            {
                module = chosenModule,
                position = placedPosition, // Use the position where it was placed
                rotation = placedRotation, // Use the rotation it was placed with
                height = chosenHeight
            });
            totalSegmentsPlaced++;

            // 4. Update the generator's state to the calculated position/rotation for the NEXT segment
            currentPosition = nextPosition;
            currentRotation = nextRotation;
            requiredInputSocket = chosenModule.outputSocket; // Set the requirement for the next module


            // --- Check if the loop is now closed (using the updated state) ---
            if (IsLoopClosed()) // Checks if the 'currentPosition/Rotation' meets closure criteria
            {
                Debug.Log($"Loop closed successfully after {totalSegmentsPlaced} segments.");
                return; // Finished footprint generation
            }

            // Reset placement attempt counter for the next segment
            placementAttempts = 0;

        } // End of while loop

        // --- Post-Loop Logging ---
        if (totalSegmentsPlaced >= absoluteMaxSegments)
        {
            Debug.LogWarning($"Reached absolute maximum segments ({absoluteMaxSegments}) without closing loop.");
        }
        else if (totalSegmentsPlaced < minSegments)
        {
            Debug.LogWarning($"Generation stopped early ({totalSegmentsPlaced} segments) before reaching minimum ({minSegments}). Check for compatibility issues.");
        }
        else if (!IsLoopClosed())
        {
            Debug.LogWarning($"Finished placing {totalSegmentsPlaced} segments without closing the loop. Distance to start: {Vector3.Distance(currentPosition, startPosition):F2}m");
        }
    }

    List<WFCModule> FindCompatibleModules(SocketType inputSocketNeeded)
    {
        // Find all modules whose INPUT socket is compatible with the PREVIOUS module's OUTPUT requirement
        return allModules.Where(m => m != null && CanConnect(requiredInputSocket, m.inputSocket)).ToList();
    }

    // Define connection logic: Previous Module's Output -> Current Module's Input
    bool CanConnect(SocketType previousOutputSocket, SocketType currentInputSocket)
    {
        // Based on user description: Straight can connect to Straight or Corner.
        // Corner must connect to Straight.
        if (previousOutputSocket == SocketType.Straight)
        {
            // A straight segment can be followed by another Straight or a Corner
            return currentInputSocket == SocketType.Straight || currentInputSocket == SocketType.Corner;
        }
        else if (previousOutputSocket == SocketType.Corner)
        {
            // A corner segment must be followed by a Straight segment
            return currentInputSocket == SocketType.Straight;
        }
        else
        {
            // Fallback/Error case
            Debug.LogError($"Unhandled previousOutputSocket type: {previousOutputSocket}");
            return false;
        }
    }


    WFCModule SelectRandomWeightedModule(List<WFCModule> modules)
    {
        if (modules == null || modules.Count == 0) return null;

        float totalWeight = modules.Sum(m => Mathf.Max(0.1f, m.probabilityWeight)); // Ensure weight is at least slightly positive
        float randomValue = Random.Range(0, totalWeight);
        float currentWeight = 0;

        foreach (var module in modules)
        {
            currentWeight += Mathf.Max(0.1f, module.probabilityWeight);
            if (randomValue <= currentWeight)
            {
                return module;
            }
        }
        return modules.LastOrDefault(); // Fallback
    }

    // REMEMBER: Also modify TryFindClosingModule if needed, as it simulates placement
    // It needs to perform the *same* conditional logic (special corner vs normal)
    // when calculating the 'nextPos' and 'nextRot' during its simulation.

    // Updated TryFindClosingModule - Example (Needs integration)
    WFCModule TryFindClosingModule(List<WFCModule> possibleModules)
    {
        float bestDist = float.MaxValue;
        WFCModule bestModule = null;
        const float L = 0.8f; // Make sure L is defined/accessible here too

        // Current state where the potential closing module would be placed
        Vector3 placementPos = currentPosition;
        Quaternion placementRot = currentRotation;

        foreach (var module in possibleModules)
        {
            // --- Simulate placement to find the position/rotation AFTER this module ---
            Vector3 simulatedNextPos;
            Quaternion simulatedNextRot = placementRot * Quaternion.Euler(0, module.placementRotationY, 0);

            if (module.isSpecial45DegreeCorner)
            {
                float angleY = module.placementRotationY;
                Vector3 localOffset = (Vector3.left * (L / 2.0f)) + (Quaternion.Euler(0, angleY, 0) * Vector3.left * (1.5f * L));
                simulatedNextPos = placementPos + (placementRot * localOffset);
            }
            else // Normal module
            {
                simulatedNextPos = placementPos + (placementRot * Vector3.left * module.segmentLength);
            }
            // --- End Simulation ---


            float distToStart = Vector3.Distance(simulatedNextPos, startPosition);

            // Check 1: Is the simulated resulting position close enough?
            if (distToStart < closureDistanceThreshold)
            {
                // Check 2: Is the output socket compatible with the start requirement?
                bool compatibleWithStart = CanConnect(module.outputSocket, SocketType.Straight);

                if (compatibleWithStart)
                {
                    // Optional: Check Angle (using simulatedNextRot and simulatedNextPos) if desired

                    if (distToStart < bestDist)
                    {
                        bestDist = distToStart;
                        bestModule = module;
                    }
                }
            }
        }

        if (bestModule != null)
        {
            Debug.Log($"Found potential closing module: {bestModule.moduleName}. Simulated end distance: {bestDist:F2}m.");
        }

        return bestModule; // Return the best closing module found, or null
    }

    // Check if the loop is considered closed based on the CURRENT state
    // This is called AFTER updating position/rotation from the last placed module.
    bool IsLoopClosed() // No parameter needed, checks current state vs start state
    {
        if (placedModules.Count == 0) return false; // Can't be closed if nothing is placed

        float finalDistance = Vector3.Distance(currentPosition, startPosition);

        // Get the output socket of the very last module placed
        SocketType lastOutputSocket = placedModules.Last().module.outputSocket;

        // Check if the last module's output is compatible with the first module's input requirement (Straight)
        bool socketsCompatible = CanConnect(lastOutputSocket, SocketType.Straight);

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
        buildingRoot.transform.position = startPosition; // Set root to start position
        // buildingRoot.transform.rotation = startRotation; // Apply start rotation to root if desired
        if (generationParent != null)
        {
            buildingRoot.transform.SetParent(generationParent, worldPositionStays: true); // Parent but keep world pos/rot
            buildingRoot.transform.position = startPosition; // Re-apply position relative to parent if needed
            buildingRoot.transform.rotation = startRotation; // Re-apply rotation relative to parent if needed
        }


        foreach (var placedInfo in placedModules)
        {
            WFCModule module = placedInfo.module;
            if (module == null) continue;

            GameObject segmentRoot = new GameObject($"Segment_{module.moduleName}");
            segmentRoot.transform.position = placedInfo.position;
            segmentRoot.transform.rotation = placedInfo.rotation;
            segmentRoot.transform.SetParent(buildingRoot.transform, worldPositionStays: true); // Parent maintaining world orientation

            float currentY = 0f;
            float segmentMaxHeight = 0f; // Track max height for this segment for better stacking

            // --- Instantiate Floor Pieces ---
            // Helper function to instantiate and update Y position
            InstantiateFloorPiece(module.GetRandomPrefab(module.groundFloorPrefabs), "Ground", module.moduleName, segmentRoot.transform, ref currentY, ref segmentMaxHeight);

            if (module.middleFloorPrefabs != null && module.middleFloorPrefabs.Count > 0)
            {
                for (int i = 0; i < placedInfo.height; i++)
                {
                    InstantiateFloorPiece(module.GetRandomPrefab(module.middleFloorPrefabs), $"Middle_{i}", module.moduleName, segmentRoot.transform, ref currentY, ref segmentMaxHeight);
                }
            }
            else if (placedInfo.height > 0) // Only warn if height > 0 but no prefabs
            {
                Debug.LogWarning($"Module {module.moduleName} missing Middle Floor Prefabs, but height was {placedInfo.height}");
            }

            InstantiateFloorPiece(module.GetRandomPrefab(module.mansardFloorPrefabs), "Mansard", module.moduleName, segmentRoot.transform, ref currentY, ref segmentMaxHeight);
            InstantiateFloorPiece(module.atticRoofPrefab, "Attic", module.moduleName, segmentRoot.transform, ref currentY, ref segmentMaxHeight);

            // Optional: Add a collider encompassing the segment height?
            // BoxCollider col = segmentRoot.AddComponent<BoxCollider>();
            // col.center = new Vector3(0, segmentMaxHeight / 2f, 0); // Adjust Z based on prefab depth
            // col.size = new Vector3(1, segmentMaxHeight, 1); // Adjust X, Z based on prefab width/depth
        }
        Debug.Log($"Instantiated building with {placedModules.Count} segments.");
    }

    // Updated instantiation helper
    void InstantiateFloorPiece(GameObject prefab, string floorName, string moduleName, Transform parent, ref float currentY, ref float segmentMaxHeight)
    {
        if (prefab == null)
        {
            // Don't warn for missing attic, but do for others if expected
            if (floorName != "Attic")
            {
                // Only warn if it's a type that *should* exist based on lists potentially being non-empty
                // This check is implicit now because GetRandomPrefab returns null if list empty.
                // We could add more specific warnings here if needed.
                // Debug.LogWarning($"Module {moduleName} missing {floorName} Floor Prefab variation.");
            }
            return;
        }

        GameObject instance = Instantiate(prefab, parent);
        instance.transform.localPosition = new Vector3(0, currentY, 0);
        instance.transform.localRotation = Quaternion.identity;
        instance.name = $"{floorName}_{prefab.name}"; // More descriptive name

        // --- CRITICAL: Height Calculation ---
        float prefabHeight = GetPrefabHeight(instance, prefab.name);
        currentY += prefabHeight;
        segmentMaxHeight = Mathf.Max(segmentMaxHeight, currentY); // Update max Y for this segment
    }

    // Centralized Height Calculation - IMPROVE THIS METHOD!
    float GetPrefabHeight(GameObject instance, string prefabName)
    {
        // METHOD 1: Bounds (Often unreliable for non-uniform/complex meshes)
        Renderer rend = instance.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            // Check if bounds are valid (sometimes they are zero/tiny on first frame)
            if (rend.bounds.size.y > 0.01f)
            {
                // Debug.Log($"Height for {prefabName} from Bounds: {rend.bounds.size.y}");
                return rend.bounds.size.y;
            }
        }

        // METHOD 2: Collider (More reliable if setup correctly)
        Collider coll = instance.GetComponentInChildren<Collider>();
        if (coll != null)
        {
            // Using bounds of the collider component
            if (coll.bounds.size.y > 0.01f)
            {
                // Debug.Log($"Height for {prefabName} from Collider: {coll.bounds.size.y}");
                return coll.bounds.size.y;
            }
        }

/*        // METHOD 3: Dedicated Script (MOST RELIABLE)
        // Add a simple script like 'PrefabHeightInfo' to your prefabs:
        // public class PrefabHeightInfo : MonoBehaviour { public float height = 3.0f; }
        PrefabHeightInfo heightInfo = instance.GetComponent<PrefabHeightInfo>();
        if (heightInfo != null)
        {
            // Debug.Log($"Height for {prefabName} from HeightInfo Script: {heightInfo.height}");
            return heightInfo.height;
        }*/


        // --- FALLBACK ---
        float defaultHeight = 3.0f; // Default guess
        Debug.LogWarning($"Could not determine height for prefab '{prefabName}'. Using default {defaultHeight}m. ADD a Collider or PrefabHeightInfo script for accuracy.", instance);
        return defaultHeight;
    }

    // Example script to add to your prefabs for reliable height
    // Create this as PrefabHeightInfo.cs
    // public class PrefabHeightInfo : MonoBehaviour {
    //     [Tooltip("The vertical size of this prefab piece for stacking.")]
    //     public float height = 3.0f;
    // }

} // End of WFCBuildingGenerator class