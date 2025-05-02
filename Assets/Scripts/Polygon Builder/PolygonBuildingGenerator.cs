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

    [Header("Prefabs (Assign 1x1 Unit Facades)")]
    public List<GameObject> groundFloorPrefabs;
    public List<GameObject> middleFloorPrefabs;
    public List<GameObject> mansardFloorPrefabs; // Optional
    public List<GameObject> atticFloorPrefabs;  // Optional (Sloped)

    // Keep track of generated objects for easy clearing
    private GameObject generatedBuildingRoot;
    private const string ROOT_NAME = "GeneratedBuilding";

    // --- Public Methods (called by Editor script or manually) ---

    public void GenerateBuilding()
    {
        ClearBuilding(); // Clear previous generation

        if (vertices.Count < 3)
        {
            Debug.LogWarning("Cannot generate building, polygon needs at least 3 vertices.");
            return;
        }

        // Create a root object to hold all parts
        generatedBuildingRoot = new GameObject(ROOT_NAME);
        generatedBuildingRoot.transform.SetParent(this.transform);
        generatedBuildingRoot.transform.localPosition = Vector3.zero;
        generatedBuildingRoot.transform.localRotation = Quaternion.identity;

        int baseTotalFloors = 1 + middleFloors + (useMansardFloor ? 1 : 0) + (useAtticFloor ? 1 : 0);

        // --- Generate Walls side by side ---
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 p1 = vertices[i];
            Vector3 p2 = vertices[(i + 1) % vertices.Count]; // Wrap around for the last side

            Vector3 sideDirection = (p2 - p1).normalized;
            float sideDistance = Vector3.Distance(p1, p2);
            int numSegments = Mathf.Max(minSideLengthUnits, Mathf.RoundToInt(sideDistance / vertexSnapSize)); // Use snapped size for segment count

            if (numSegments <= 0) continue; // Skip zero-length sides

            float actualSegmentLength = sideDistance / numSegments; // How long each segment space actually is

            // Calculate outward normal (assuming polygon is roughly on XZ plane)
            // Ensure consistent winding order (e.g., clockwise) for correct normals
            Vector3 sideNormal = Vector3.Cross(sideDirection, Vector3.up).normalized;

            // --- Check if normal points outwards ---
            // Calculate polygon center (approximate)
            Vector3 center = Vector3.zero;
            foreach (Vector3 v in vertices) center += v;
            center /= vertices.Count;
            // Vector from center to side midpoint
            Vector3 centerToMidpoint = ((p1 + p2) / 2f) - center;
            // Flip normal if it points inwards
            if (Vector3.Dot(sideNormal, centerToMidpoint) < 0)
            {
                sideNormal *= -1;
            }
            // --- End Normal Check ---


            // Determine height for this specific side if variation is enabled
            int currentMiddleFloors = middleFloors;
            if (allowHeightVariation)
            {
                currentMiddleFloors = Mathf.Max(0, middleFloors + Random.Range(-maxHeightVariation, maxHeightVariation + 1));
            }
            int currentTotalFloors = 1 + currentMiddleFloors + (useMansardFloor ? 1 : 0) + (useAtticFloor ? 1 : 0);


            for (int j = 0; j < numSegments; j++)
            {
                // Calculate center position for this segment on the base polygon line
                Vector3 segmentBasePos = p1 + sideDirection * (actualSegmentLength * (j + 0.5f));
                Quaternion segmentRotation = Quaternion.LookRotation(sideNormal); // Prefabs should face +Z locally

                // Instantiate floors vertically for this segment
                float currentY = 0;

                // 1. Ground Floor
                InstantiateFloorSegment(groundFloorPrefabs, segmentBasePos, segmentRotation, generatedBuildingRoot.transform);
                currentY += floorHeight; // Move up for the next floor

                // 2. Middle Floors
                for (int floor = 0; floor < currentMiddleFloors; floor++)
                {
                    Vector3 floorPos = segmentBasePos + Vector3.up * currentY;
                    InstantiateFloorSegment(middleFloorPrefabs, floorPos, segmentRotation, generatedBuildingRoot.transform);
                    currentY += floorHeight;
                }

                // 3. Mansard Floor (Optional)
                if (useMansardFloor)
                {
                    if (mansardFloorPrefabs == null || mansardFloorPrefabs.Count == 0)
                    {
                        Debug.LogWarning("Mansard floor enabled but no prefabs assigned.");
                    }
                    else
                    {
                        Vector3 floorPos = segmentBasePos + Vector3.up * currentY;
                        InstantiateFloorSegment(mansardFloorPrefabs, floorPos, segmentRotation, generatedBuildingRoot.transform);
                        currentY += floorHeight; // Assume same height for simplicity
                    }
                }

                // 4. Attic Floor (Optional, Sloped)
                if (useAtticFloor)
                {
                    if (atticFloorPrefabs == null || atticFloorPrefabs.Count == 0)
                    {
                        Debug.LogWarning("Attic floor enabled but no prefabs assigned.");
                    }
                    else
                    {
                        Vector3 floorPos = segmentBasePos + Vector3.up * currentY;
                        // Attic might need different rotation/placement logic depending on prefab design
                        InstantiateFloorSegment(atticFloorPrefabs, floorPos, segmentRotation, generatedBuildingRoot.transform);
                        // currentY += floorHeight; // No height increase needed after the last floor
                    }
                }
            }
        }
    }

    public void ClearBuilding()
    {
        // Find the existing root object and destroy it
        Transform existingRoot = transform.Find(ROOT_NAME);
        if (existingRoot != null)
        {
            // Use DestroyImmediate in Editor, Destroy in Play mode
            if (Application.isEditor && !Application.isPlaying)
            {
                DestroyImmediate(existingRoot.gameObject);
            }
            else
            {
                Destroy(existingRoot.gameObject);
            }
        }
        generatedBuildingRoot = null; // Reset reference
    }


    // --- Helper Methods ---

    private void InstantiateFloorSegment(List<GameObject> prefabList, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (prefabList == null || prefabList.Count == 0)
        {
            // Debug.LogWarning($"Prefab list for floor type is empty."); // Can be spammy
            return;
        }

        int randomIndex = Random.Range(0, prefabList.Count);
        GameObject prefab = prefabList[randomIndex];

        if (prefab != null)
        {
            GameObject instance = Instantiate(prefab, position, rotation, parent);
            // Set position/rotation again AFTER parenting if prefab has unusual pivot/transform
            instance.transform.localPosition = position;
            instance.transform.localRotation = rotation;

            // Crucially, align the instantiated object's position TO the calculated position IN WORLD SPACE relative to parent
            instance.transform.position = parent.TransformPoint(position);
            instance.transform.rotation = parent.rotation * rotation;

        }
        else
        {
            Debug.LogWarning($"Prefab at index {randomIndex} is null.");
        }
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