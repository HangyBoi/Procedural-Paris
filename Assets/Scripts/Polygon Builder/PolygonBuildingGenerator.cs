using UnityEngine;
using System.Collections.Generic;

// Make sure PolygonVertexData and PolygonSideData scripts exist in your project

[RequireComponent(typeof(MeshFilter))]
public class PolygonBuildingGenerator : MonoBehaviour
{
    [Header("Polygon Definition")]
    // Replace List<Vector3> with List<PolygonVertexData>
    public List<PolygonVertexData> vertexData = new List<PolygonVertexData>() {
        new PolygonVertexData { position = new Vector3(0, 0, 0) },
        new PolygonVertexData { position = new Vector3(0, 0, 5) },
        new PolygonVertexData { position = new Vector3(5, 0, 5) },
        new PolygonVertexData { position = new Vector3(5, 0, 0) }
    };
    // Add List<PolygonSideData>
    public List<PolygonSideData> sideData = new List<PolygonSideData>(); // Will be synchronized by the editor script

    public float vertexSnapSize = 1.0f;
    public int minSideLengthUnits = 1;

    [Header("Building Settings")]
    public int middleFloors = 3;
    public float floorHeight = 3.0f;
    public bool useMansardFloor = true;
    public bool useAtticFloor = true;
    public bool allowHeightVariation = false;
    [Range(0, 5)] public int maxHeightVariation = 1;

    [Header("Facade Placement")]
    [Tooltip("Distribute facade scaling evenly across the side to fill gaps, instead of strict 1-unit placement.")]
    public bool scaleFacadesToFitSide = true;
    public float nominalFacadeWidth = 1.0f;

    [Header("Default Prefabs")] // Renamed for clarity
    public List<GameObject> defaultGroundFloorPrefabs;
    public List<GameObject> defaultMiddleFloorPrefabs;
    public List<GameObject> defaultMansardFloorPrefabs;
    public List<GameObject> defaultAtticFloorPrefabs;

    [Header("Corner Elements")]
    public List<GameObject> cornerElementPrefabs;
    // Define how high corner elements should go
    public enum CornerHeightMode { MatchShortest, MatchTallest, FullDefaultHeight }
    public CornerHeightMode cornerHeightMode = CornerHeightMode.MatchShortest;

    private GameObject generatedBuildingRoot;
    private const string ROOT_NAME = "Generated Building";
    private const string CORNERS_NAME = "Corner Elements";


    // Ensure sideData list count matches vertexData list count
    // Call this from OnValidate or the editor script to keep things synced
    public void SynchronizeSideData()
    {
        int requiredCount = vertexData.Count;
        // Add missing entries
        while (sideData.Count < requiredCount)
        {
            sideData.Add(new PolygonSideData());
        }
        // Remove excess entries
        while (sideData.Count > requiredCount && sideData.Count > 0)
        {
            sideData.RemoveAt(sideData.Count - 1);
        }
    }

    // Called when script values are changed in the inspector (Editor only)
    void OnValidate()
    {
        // Ensure sideData count matches vertexData count when edited in inspector
        if (vertexData == null) vertexData = new List<PolygonVertexData>();
        if (sideData == null) sideData = new List<PolygonSideData>();
        SynchronizeSideData();
    }


    public void GenerateBuilding()
    {
        ClearBuilding();
        SynchronizeSideData(); // Ensure lists are synced before generation

        if (vertexData.Count < 3)
        {
            Debug.LogWarning("Cannot generate building, polygon needs at least 3 vertices.");
            return;
        }

        generatedBuildingRoot = new GameObject(ROOT_NAME);
        generatedBuildingRoot.transform.SetParent(this.transform, false);

        // --- Calculate Polygon Center ---
        Vector3 polygonCenter = Vector3.zero;
        if (vertexData.Count > 0)
        {
            foreach (var vd in vertexData) polygonCenter += vd.position;
            polygonCenter /= vertexData.Count;
        }
        // ---

        // Store calculated side heights for corner generation
        int[] sideMiddleFloors = new int[vertexData.Count];


        // --- Generate Walls side by side ---
        for (int i = 0; i < vertexData.Count; i++)
        {
            GameObject sideParent = new GameObject($"Side_{i}");
            sideParent.transform.SetParent(generatedBuildingRoot.transform, false);

            PolygonVertexData v1 = vertexData[i];
            PolygonVertexData v2 = vertexData[(i + 1) % vertexData.Count]; // Wrap around
            Vector3 p1 = v1.position;
            Vector3 p2 = v2.position;

            Vector3 sideVector = p2 - p1;
            float sideDistance = sideVector.magnitude;
            Vector3 sideDirection = sideVector.normalized;

            if (sideDistance < 0.01f) continue;

            // --- Calculate Normal ---
            Vector3 sideNormal = Vector3.Cross(sideDirection, Vector3.up).normalized;
            Vector3 sideMidpoint = p1 + sideDirection * (sideDistance / 2f);
            Vector3 centerToMidpoint = sideMidpoint - polygonCenter;
            centerToMidpoint.y = 0;
            Vector3 checkNormal = sideNormal; checkNormal.y = 0; // Compare on XZ plane
            if (Vector3.Dot(checkNormal.normalized, centerToMidpoint.normalized) < -0.01f) // Add tolerance
            {
                sideNormal *= -1;
            }
            // ---

            // --- Determine Segments & Width ---
            int numSegments = Mathf.Max(minSideLengthUnits, Mathf.RoundToInt(sideDistance / nominalFacadeWidth));
            float actualSegmentWidth = scaleFacadesToFitSide ? (sideDistance / numSegments) : nominalFacadeWidth;
            if (!scaleFacadesToFitSide)
            {
                numSegments = Mathf.Max(minSideLengthUnits, Mathf.FloorToInt(sideDistance / nominalFacadeWidth));
                if (numSegments == 0 && minSideLengthUnits > 0) numSegments = minSideLengthUnits;
            }
            // ---

            // --- Determine Height & Prefab Lists for this Side ---
            int currentMiddleFloors = middleFloors; // Start with default
                                                    // Check side-specific override logic here if you added it to PolygonSideData
                                                    // if (sideData[i].overrideHeight) { currentMiddleFloors = sideData[i].sideMiddleFloors; } else ...
            if (allowHeightVariation)
            {
                // Apply global variation rule if no side-specific rule is active
                currentMiddleFloors = Mathf.Max(0, middleFloors + Random.Range(-maxHeightVariation, maxHeightVariation + 1));
            }
            sideMiddleFloors[i] = currentMiddleFloors; // Store for corner calculation

            // Determine which prefab lists to use
            PolygonSideData currentSideSettings = sideData[i];
            List<GameObject> currentGroundPrefabs = (currentSideSettings.overridePrefabs && currentSideSettings.groundFloorPrefabs.Count > 0) ? currentSideSettings.groundFloorPrefabs : defaultGroundFloorPrefabs;
            List<GameObject> currentMiddlePrefabs = (currentSideSettings.overridePrefabs && currentSideSettings.middleFloorPrefabs.Count > 0) ? currentSideSettings.middleFloorPrefabs : defaultMiddleFloorPrefabs;
            List<GameObject> currentMansardPrefabs = (currentSideSettings.overridePrefabs && currentSideSettings.mansardFloorPrefabs.Count > 0) ? currentSideSettings.mansardFloorPrefabs : defaultMansardFloorPrefabs;
            List<GameObject> currentAtticPrefabs = (currentSideSettings.overridePrefabs && currentSideSettings.atticFloorPrefabs.Count > 0) ? currentSideSettings.atticFloorPrefabs : defaultAtticFloorPrefabs;
            // ---

            for (int j = 0; j < numSegments; j++)
            {
                Vector3 segmentBasePos = p1 + sideDirection * (actualSegmentWidth * (j + 0.5f));
                Quaternion segmentRotation = Quaternion.LookRotation(sideNormal);
                float currentY = 0;

                // Ground
                InstantiateFloorSegment(currentGroundPrefabs, segmentBasePos, segmentRotation, sideParent.transform, actualSegmentWidth);
                currentY += floorHeight;
                // Middle
                for (int floor = 0; floor < currentMiddleFloors; floor++)
                {
                    InstantiateFloorSegment(currentMiddlePrefabs, segmentBasePos + Vector3.up * currentY, segmentRotation, sideParent.transform, actualSegmentWidth);
                    currentY += floorHeight;
                }
                // Mansard
                if (useMansardFloor)
                {
                    InstantiateFloorSegment(currentMansardPrefabs, segmentBasePos + Vector3.up * currentY, segmentRotation, sideParent.transform, actualSegmentWidth);
                    currentY += floorHeight;
                }
                // Attic
                if (useAtticFloor)
                {
                    InstantiateFloorSegment(currentAtticPrefabs, segmentBasePos + Vector3.up * currentY, segmentRotation, sideParent.transform, actualSegmentWidth);
                }
            }
        }

        // --- Generate Corner Elements ---
        GenerateCornerElements(sideMiddleFloors); // Pass calculated heights
    }


    void GenerateCornerElements(int[] sideHeights)
    {
        if (cornerElementPrefabs == null || cornerElementPrefabs.Count == 0) return; // Nothing to spawn

        GameObject cornersParent = new GameObject(CORNERS_NAME);
        cornersParent.transform.SetParent(generatedBuildingRoot.transform, false);

        for (int i = 0; i < vertexData.Count; i++)
        {
            if (!vertexData[i].addCornerElement) continue; // Skip if flag is false

            // Get vertex and its neighbours
            PolygonVertexData currentV = vertexData[i];
            PolygonVertexData prevV = vertexData[(i + vertexData.Count - 1) % vertexData.Count]; // Previous vertex (handles wrap around)
            PolygonVertexData nextV = vertexData[(i + 1) % vertexData.Count];                   // Next vertex

            Vector3 currentPos = currentV.position;
            Vector3 prevPos = prevV.position;
            Vector3 nextPos = nextV.position;

            // Calculate incoming and outgoing directions at the vertex
            Vector3 dirToPrev = (prevPos - currentPos).normalized;
            Vector3 dirToNext = (nextPos - currentPos).normalized;

            // Calculate the normals of the two sides meeting at this vertex
            // Side Prev->Current
            Vector3 sideNormalPrev = Vector3.Cross((currentPos - prevPos).normalized, Vector3.up).normalized;
            // Side Current->Next
            Vector3 sideNormalNext = Vector3.Cross(dirToNext, Vector3.up).normalized;

            // Robust Normal Check (ensure they point outwards) - Reuse logic from side generation if needed,
            // but often averaging is okay for corners unless polygon is very complex.
            // For simplicity, let's assume basic outward pointing here. A full check would involve polygon center again.

            // Calculate the corner's rotation: facing the average direction of the two outward normals
            Vector3 avgNormal = (sideNormalPrev + sideNormalNext).normalized;
            // We need to ensure this average normal points *outwards* from the corner angle
            Vector3 angleBisectorInternal = (-dirToPrev + dirToNext).normalized; // Vector pointing "into" the angle between sides
            if (Vector3.Dot(avgNormal, angleBisectorInternal) > 0) // If avgNormal points inwards with the bisector
            {
                avgNormal *= -1; // Flip it
            }

            Quaternion cornerRotation = Quaternion.LookRotation(avgNormal);


            // Determine the height of the corner element
            int prevSideIndex = (i + vertexData.Count - 1) % vertexData.Count;
            int nextSideIndex = i;
            int cornerMiddleFloors = 0;

            switch (cornerHeightMode)
            {
                case CornerHeightMode.MatchShortest:
                    cornerMiddleFloors = Mathf.Min(sideHeights[prevSideIndex], sideHeights[nextSideIndex]);
                    break;
                case CornerHeightMode.MatchTallest:
                    cornerMiddleFloors = Mathf.Max(sideHeights[prevSideIndex], sideHeights[nextSideIndex]);
                    break;
                case CornerHeightMode.FullDefaultHeight:
                default:
                    cornerMiddleFloors = middleFloors; // Use the global default
                    break;
            }

            // Instantiate corner elements vertically
            float currentY = 0;
            float cornerWidth = nominalFacadeWidth; // Assume corner pieces have standard width, no scaling needed usually

            // Ground Corner (Use first prefab or create a dedicated list?)
            InstantiateFloorSegment(cornerElementPrefabs, currentPos, cornerRotation, cornersParent.transform, cornerWidth);
            currentY += floorHeight;
            // Middle Corners
            for (int floor = 0; floor < cornerMiddleFloors; floor++)
            {
                InstantiateFloorSegment(cornerElementPrefabs, currentPos + Vector3.up * currentY, cornerRotation, cornersParent.transform, cornerWidth);
                currentY += floorHeight;
            }
            // Mansard Corner (Optional: use same prefabs or dedicated list)
            if (useMansardFloor)
            {
                InstantiateFloorSegment(cornerElementPrefabs, currentPos + Vector3.up * currentY, cornerRotation, cornersParent.transform, cornerWidth);
                currentY += floorHeight;
            }
            // Attic Corner (Optional: use same prefabs or dedicated list)
            if (useAtticFloor)
            {
                InstantiateFloorSegment(cornerElementPrefabs, currentPos + Vector3.up * currentY, cornerRotation, cornersParent.transform, cornerWidth);
            }
        }
    }

    public void ClearBuilding()
    {
        while (transform.Find(ROOT_NAME) != null)
        {
            Transform existingRoot = transform.Find(ROOT_NAME);
            if (Application.isEditor && !Application.isPlaying) DestroyImmediate(existingRoot.gameObject);
            else Destroy(existingRoot.gameObject);
        }
        generatedBuildingRoot = null;
    }

    private void InstantiateFloorSegment(List<GameObject> prefabList, Vector3 localPosition, Quaternion localRotation, Transform parent, float segmentWidth)
    {
        if (prefabList == null || prefabList.Count == 0) { /*Debug.LogWarning($"Prefab list empty for {parent.name}");*/ return; } // Avoid spam

        int randomIndex = Random.Range(0, prefabList.Count);
        GameObject prefab = prefabList[randomIndex];
        if (prefab == null) return;

        GameObject instance = Instantiate(prefab, parent);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = localRotation;

        // Only apply scaling if the feature is enabled *and* the width is different from nominal
        if (scaleFacadesToFitSide && Mathf.Abs(segmentWidth - nominalFacadeWidth) > 0.01f)
        {
            Vector3 originalScale = instance.transform.localScale; // Use instance's initial scale
            float scaleFactor = segmentWidth / nominalFacadeWidth;
            // Apply scaling only on X axis relative to nominal size
            instance.transform.localScale = new Vector3(originalScale.x * scaleFactor, originalScale.y, originalScale.z);
        }
        // else: Keep the original prefab scale (important for corners or when scaleFacadesToFitSide=false)

    }

    // Modify SnapVertex to work with PolygonVertexData
    public Vector3 SnapVertexPosition(Vector3 vertexPos)
    {
        return new Vector3(
            Mathf.Round(vertexPos.x / vertexSnapSize) * vertexSnapSize,
            0f,
            Mathf.Round(vertexPos.z / vertexSnapSize) * vertexSnapSize
        );
    }
}