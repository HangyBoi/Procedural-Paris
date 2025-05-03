using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))] // Optional: If you want to generate a base mesh
public class PolygonBuildingGenerator : MonoBehaviour
{
    [Header("Polygon Definition")]
    public List<Vector3> vertices = new List<Vector3>() {
        new Vector3(0, 0, 0),
        new Vector3(0, 0, 5),
        new Vector3(5, 0, 5),
        new Vector3(5, 0, 0)
    }; // Local space vertices
    public float vertexSnapSize = 1.0f;
    public int minSideLengthUnits = 1;

    [Header("Building Settings")]
    public int middleFloors = 3; // Number of floors between ground and mansard/attic
    public float floorHeight = 3.0f; // Height of one standard floor
    public bool useMansardFloor = true;
    public bool useAtticFloor = true;
    public bool allowHeightVariation = false;
    [Range(0, 5)]
    public int maxHeightVariation = 1; // Max number of floors +/- from the base height per side

    // Add a setting to control this behaviour
    [Header("Facade Placement")]
    [Tooltip("Distribute facade scaling evenly across the side to fill gaps, instead of strict 1-unit placement.")]
    public bool scaleFacadesToFitSide = true; // Default to true for smoother results
    public float nominalFacadeWidth = 1.0f; // The ideal width of your facade prefabs

    [Header("Prefabs (Assign 1x1 Unit Facades)")]
    public List<GameObject> groundFloorPrefabs;
    public List<GameObject> middleFloorPrefabs;
    public List<GameObject> mansardFloorPrefabs; // Optional
    public List<GameObject> atticFloorPrefabs;  // Optional (Sloped)

    // Keep track of generated objects for easy clearing
    private GameObject generatedBuildingRoot;
    private const string ROOT_NAME = "Generated Building";

    // --- Public Methods (called by Editor script or manually) ---

    public void GenerateBuilding()
    {
        ClearBuilding();

        if (vertices.Count < 3)
        {
            Debug.LogWarning("Cannot generate building, polygon needs at least 3 vertices.");
            return;
        }

        generatedBuildingRoot = new GameObject(ROOT_NAME);
        generatedBuildingRoot.transform.SetParent(this.transform);
        generatedBuildingRoot.transform.localPosition = Vector3.zero;
        generatedBuildingRoot.transform.localRotation = Quaternion.identity;

        // --- Calculate Polygon Center (for normal check) ---
        Vector3 polygonCenter = Vector3.zero;
        if (vertices.Count > 0)
        {
            foreach (Vector3 v in vertices) polygonCenter += v;
            polygonCenter /= vertices.Count;
        }
        // ---

        // --- Generate Walls side by side ---
        for (int i = 0; i < vertices.Count; i++)
        {
            // --- Create Parent for this Side ---
            GameObject sideParent = new GameObject($"Side_{i}");
            sideParent.transform.SetParent(generatedBuildingRoot.transform, false); // Use worldPositionStays = false
                                                                                    // Set position relative to the building root (which is at local 0,0,0)
            sideParent.transform.localPosition = Vector3.zero;
            sideParent.transform.localRotation = Quaternion.identity;
            // ---

            Vector3 p1 = vertices[i];
            Vector3 p2 = vertices[(i + 1) % vertices.Count];

            Vector3 sideVector = p2 - p1;
            float sideDistance = sideVector.magnitude;
            Vector3 sideDirection = sideVector.normalized;

            if (sideDistance < 0.01f) continue; // Skip zero-length sides

            // --- Calculate Normal (Robust Check) ---
            // Cross product gives a perpendicular vector on the local XZ plane
            Vector3 sideNormal = Vector3.Cross(sideDirection, Vector3.up).normalized;
            // Check if it points away from the calculated polygon center
            Vector3 sideMidpoint = p1 + sideDirection * (sideDistance / 2f);
            Vector3 centerToMidpoint = sideMidpoint - polygonCenter;
            // Ensure the check considers only the XZ plane projection if center isn't at Y=0
            centerToMidpoint.y = 0;
            sideNormal.y = 0;
            if (Vector3.Dot(sideNormal.normalized, centerToMidpoint.normalized) < 0)
            {
                sideNormal *= -1; // Flip if pointing inwards relative to center
            }
            // --- End Normal Check ---


            // --- Determine Number of Segments and Actual Width ---
            // Calculate how many nominal units *would* fit
            int numSegments = Mathf.Max(minSideLengthUnits, Mathf.RoundToInt(sideDistance / nominalFacadeWidth));

            // Calculate the exact width each segment needs to be to perfectly fill sideDistance
            float actualSegmentWidth = sideDistance / numSegments;

            // If scaling is disabled, force segment width to nominal, potentially leaving gaps/overlaps handled visually
            if (!scaleFacadesToFitSide)
            {
                actualSegmentWidth = nominalFacadeWidth;
                // Recalculate numSegments based on strict placement if needed, though the original numSegments is usually fine.
                numSegments = Mathf.Max(minSideLengthUnits, Mathf.FloorToInt(sideDistance / nominalFacadeWidth));
                if (numSegments == 0 && minSideLengthUnits > 0) numSegments = minSideLengthUnits; // Ensure minimum
            }
            // ---

            // Determine height for this specific side if variation is enabled
            int currentMiddleFloors = middleFloors;
            if (allowHeightVariation)
            {
                // Use a deterministic seed based on side index for consistency if needed
                // Random.InitState(i); // Uncomment for consistent random height per side across regenerations
                currentMiddleFloors = Mathf.Max(0, middleFloors + Random.Range(-maxHeightVariation, maxHeightVariation + 1));
            }

            for (int j = 0; j < numSegments; j++)
            {
                // Calculate center position for this segment along the side
                // Use actualSegmentWidth for positioning
                Vector3 segmentBasePos = p1 + sideDirection * (actualSegmentWidth * (j + 0.5f));
                Quaternion segmentRotation = Quaternion.LookRotation(sideNormal);

                // Instantiate floors vertically
                float currentY = 0;

                // 1. Ground Floor
                InstantiateFloorSegment(groundFloorPrefabs, segmentBasePos, segmentRotation, sideParent.transform, actualSegmentWidth);
                currentY += floorHeight;

                // 2. Middle Floors
                for (int floor = 0; floor < currentMiddleFloors; floor++)
                {
                    Vector3 floorPos = segmentBasePos + Vector3.up * currentY;
                    InstantiateFloorSegment(middleFloorPrefabs, floorPos, segmentRotation, sideParent.transform, actualSegmentWidth);
                    currentY += floorHeight;
                }

                // 3. Mansard Floor
                if (useMansardFloor && mansardFloorPrefabs != null && mansardFloorPrefabs.Count > 0)
                {
                    Vector3 floorPos = segmentBasePos + Vector3.up * currentY;
                    InstantiateFloorSegment(mansardFloorPrefabs, floorPos, segmentRotation, sideParent.transform, actualSegmentWidth);
                    currentY += floorHeight;
                }

                // 4. Attic Floor
                if (useAtticFloor && atticFloorPrefabs != null && atticFloorPrefabs.Count > 0)
                {
                    Vector3 floorPos = segmentBasePos + Vector3.up * currentY;
                    InstantiateFloorSegment(atticFloorPrefabs, floorPos, segmentRotation, sideParent.transform, actualSegmentWidth);
                    // currentY += floorHeight; // No height increase needed
                }
            }
        }
    }

    public void ClearBuilding()
    {
        // Find children GameObjects named "GeneratedBuilding" under this transform
        while (transform.Find(ROOT_NAME) != null)
        {
            Transform existingRoot = transform.Find(ROOT_NAME);
            if (Application.isEditor && !Application.isPlaying)
            {
                DestroyImmediate(existingRoot.gameObject);
            }
            else
            {
                Destroy(existingRoot.gameObject);
            }
        }
        generatedBuildingRoot = null;
    }


    // --- Helper Methods ---

    private void InstantiateFloorSegment(List<GameObject> prefabList, Vector3 localPosition, Quaternion localRotation, Transform parent, float segmentWidth)
    {
        if (prefabList == null || prefabList.Count == 0) return;

        int randomIndex = Random.Range(0, prefabList.Count);
        GameObject prefab = prefabList[randomIndex];
        if (prefab == null) return;

        // Instantiate under the specific side's parent
        GameObject instance = Instantiate(prefab, parent);

        // Set local position and rotation relative to the side parent
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = localRotation;

        // Apply scaling based on the calculated segment width
        // Assumes original prefab is designed to be nominalFacadeWidth (e.g., 1.0) wide along its local X axis
        Vector3 originalScale = prefab.transform.localScale; // Use prefab's scale as base if needed, but often it's (1,1,1)
        float scaleFactor = segmentWidth / nominalFacadeWidth;
        instance.transform.localScale = new Vector3(originalScale.x * scaleFactor, originalScale.y, originalScale.z);

        // Note: Instantiating with parent and then setting local transforms is generally reliable.
        // The previous world space conversion is not needed here as we parent first.
    }

    // Snaps a single vertex position based on snapSize
    public Vector3 SnapVertex(Vector3 vertex)
    {
        // Snap each component (X, Y, Z) to the nearest multiple of vertexSnapSize
        // Keep Y at 0 for a flat polygon base, unless you want verticality in the base polygon
        return new Vector3(
            Mathf.Round(vertex.x / vertexSnapSize) * vertexSnapSize,
            0f, // Keep base polygon flat on local Y=0 plane
            Mathf.Round(vertex.z / vertexSnapSize) * vertexSnapSize
        );
    }
}