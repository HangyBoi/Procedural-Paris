using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(MeshFilter))]
public class PolygonBuildingGenerator : MonoBehaviour
{
    [Header("Polygon Definition")]
    public List<PolygonVertexData> vertexData = new List<PolygonVertexData>() {
        new() { position = new Vector3(0, 0, 0) },
        new() { position = new Vector3(0, 0, 5) },
        new() { position = new Vector3(5, 0, 5) },
        new() { position = new Vector3(5, 0, 0) }
    };
    public List<PolygonSideData> sideData = new List<PolygonSideData>();
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
    public bool scaleFacadesToFitSide = true;
    public float nominalFacadeWidth = 1.0f;

    [Header("Default Prefabs")]
    public List<GameObject> defaultGroundFloorPrefabs;
    public List<GameObject> defaultMiddleFloorPrefabs;
    public List<GameObject> defaultMansardFloorPrefabs;
    public List<GameObject> defaultAtticFloorPrefabs;

    [Header("Corner Elements")]
    public List<GameObject> cornerElementPrefabs;
    public List<GameObject> cornerCapPrefabs;
    public float cornerElementForwardOffset = 0.0f;

    [Header("Roof Settings")]
    public bool generateSlopedRoof = true;
    public bool generateRoofTopCap = true;
    public float roofSlopeHorizontalDistance = 2.0f;
    public float roofRiseHeight = 1.5f;
    public float flatRoofEdgeOffset = 0.0f;
    public Material roofMaterial;
    public Material roofTopCapMaterial;
    public float roofUvScale = 1.0f;

    // --- Private Fields ---
    private GameObject generatedBuildingRoot;
    private const string ROOT_NAME = "Generated Building";
    private const string CORNERS_NAME = "Corner Elements";
    private const string ROOF_SLOPED_NAME = "Procedural Sloped Roof";
    private const string ROOF_FLAT_NAME = "Procedural Flat Roof";
    private const string ROOF_CAP_NAME = "Procedural Roof Cap";

#if UNITY_EDITOR
    // --- Make Debug Data Public for Editor Access ---
    [HideInInspector] public List<Vector3> _debugOuterRoofVertices;
    [HideInInspector] public List<Vector3> _debugInnerRoofVertices;
    [HideInInspector] public Mesh _debugSlopedRoofMesh;
    [HideInInspector] public Mesh _debugFlatRoofMesh;
    [HideInInspector] public Mesh _debugRoofCapMesh;
    [HideInInspector] public Transform _debugSlopedRoofTransform;
    [HideInInspector] public Transform _debugFlatRoofTransform;
    [HideInInspector] public Transform _debugRoofCapTransform;
#endif

    // --- Core Generation Logic ---
    public void GenerateBuilding()
    {
        ClearBuilding();
        SynchronizeSideData();
        if (vertexData.Count < 3) return;

        generatedBuildingRoot = new GameObject(ROOT_NAME);
        generatedBuildingRoot.transform.SetParent(this.transform, false);

        Vector3 polygonCenter = CalculatePolygonCenter();
        int[] sideMiddleFloors = CalculateSideHeights(); // Calculate heights once

        GenerateFacades(sideMiddleFloors, polygonCenter);
        GenerateCornerElements(sideMiddleFloors, polygonCenter); // Pass center if needed
        GenerateRoof(sideMiddleFloors, polygonCenter);         // Pass center if needed
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

#if UNITY_EDITOR
        // Clear debug data when clearing the building
        _debugOuterRoofVertices = null;
        _debugInnerRoofVertices = null;
        _debugSlopedRoofMesh = null;
        _debugFlatRoofMesh = null;
        _debugRoofCapMesh = null;
        _debugSlopedRoofTransform = null;
        _debugFlatRoofTransform = null;
        _debugRoofCapTransform = null;
#endif
    }


    // --- Sub-Generation Functions ---

    void GenerateFacades(int[] sideMiddleFloors, Vector3 polygonCenter)
    {
        for (int i = 0; i < vertexData.Count; i++) // Loop through sides
        {
            GameObject sideParent = new GameObject($"Side_{i}");
            sideParent.transform.SetParent(generatedBuildingRoot.transform, false);

            Vector3 p1 = vertexData[i].position;
            Vector3 p2 = vertexData[(i + 1) % vertexData.Count].position;
            Vector3 sideVector = p2 - p1;
            float sideDistance = sideVector.magnitude;
            if (sideDistance < 0.01f) continue;
            Vector3 sideDirection = sideVector.normalized;

            Vector3 sideNormal = CalculateSideNormal(p1, p2, polygonCenter);
            int numSegments = CalculateNumSegments(sideDistance);
            float actualSegmentWidth = CalculateSegmentWidth(sideDistance, numSegments);
            int currentMiddleFloors = sideMiddleFloors[i];

            // Get prefab lists for this side
            GetSidePrefabLists(i, out var currentGround, out var currentMiddle, out var currentMansard, out var currentAttic);

            // Loop through horizontal segments on this side
            for (int j = 0; j < numSegments; j++)
            {
                // Calculate base position and rotation for this segment
                Vector3 segmentBasePos = p1 + sideDirection * (actualSegmentWidth * (j + 0.5f));
                Quaternion segmentRotation = Quaternion.LookRotation(sideNormal);

                // Build vertical stack for this segment
                float currentY = 0; // Reset Y for each NEW vertical stack (at segment j)

                // Ground Floor for this segment
                InstantiateFacadeSegment(currentGround, segmentBasePos, segmentRotation, sideParent.transform, actualSegmentWidth);
                currentY += floorHeight;

                // Middle Floors for this segment
                for (int floor = 0; floor < currentMiddleFloors; floor++)
                {
                    InstantiateFacadeSegment(currentMiddle, segmentBasePos + Vector3.up * currentY, segmentRotation, sideParent.transform, actualSegmentWidth);
                    currentY += floorHeight;
                }

                // Mansard Floor for this segment (if used)
                if (useMansardFloor)
                {
                    InstantiateFacadeSegment(currentMansard, segmentBasePos + Vector3.up * currentY, segmentRotation, sideParent.transform, actualSegmentWidth);
                    currentY += floorHeight;
                }

                // Attic Floor for this segment (if used)
                if (useAtticFloor)
                {
                    InstantiateFacadeSegment(currentAttic, segmentBasePos + Vector3.up * currentY, segmentRotation, sideParent.transform, actualSegmentWidth);
                    // No Y increment after the top floor
                }
            }
        }
    }

    void GenerateCornerElements(int[] sideMiddleFloors, Vector3 polygonCenter)
    {
        bool hasCornerPrefabs = cornerElementPrefabs != null && cornerElementPrefabs.Count > 0;
        bool hasCapPrefabs = cornerCapPrefabs != null && cornerCapPrefabs.Count > 0;
        if (!hasCornerPrefabs && !hasCapPrefabs) return;

        GameObject cornersParent = new GameObject(CORNERS_NAME);
        cornersParent.transform.SetParent(generatedBuildingRoot.transform, false);

        for (int i = 0; i < vertexData.Count; i++)
        {
            if (!vertexData[i].addCornerElement) continue;

            int prevI = (i + vertexData.Count - 1) % vertexData.Count;
            int nextI = (i + 1) % vertexData.Count;
            Vector3 currentPosRaw = vertexData[i].position;
            Vector3 prevPos = vertexData[prevI].position;
            Vector3 nextPos = vertexData[nextI].position;

            // Calculate corner rotation and offset position using helpers
            CalculateCornerTransform(prevPos, currentPosRaw, nextPos, polygonCenter, out Vector3 cornerBasePos, out Quaternion cornerRotation);

            // Determine Height
            int cornerMiddleFloors = Mathf.Max(sideMiddleFloors[prevI], sideMiddleFloors[i]);

            // Instantiate Stack
            float currentY = 0;
            float cornerWidth = nominalFacadeWidth;
            int regularSegmentsBeforeCap = CalculateRegularCornerSegments(cornerMiddleFloors, hasCapPrefabs);

            // Instantiate Ground, Middle, Mansard, Attic segments (if needed)
            if (hasCornerPrefabs && 0 < regularSegmentsBeforeCap) { InstantiateFacadeSegment(cornerElementPrefabs, cornerBasePos + Vector3.up * currentY, cornerRotation, cornersParent.transform, cornerWidth); }
            currentY += floorHeight;
            for (int floor = 0; floor < cornerMiddleFloors; floor++) { if (hasCornerPrefabs && 1 + floor < regularSegmentsBeforeCap) { InstantiateFacadeSegment(cornerElementPrefabs, cornerBasePos + Vector3.up * currentY, cornerRotation, cornersParent.transform, cornerWidth); } currentY += floorHeight; }
            if (useMansardFloor) { if (hasCornerPrefabs && 1 + cornerMiddleFloors < regularSegmentsBeforeCap) { InstantiateFacadeSegment(cornerElementPrefabs, cornerBasePos + Vector3.up * currentY, cornerRotation, cornersParent.transform, cornerWidth); } currentY += floorHeight; }
            if (useAtticFloor) { if (hasCornerPrefabs && 1 + cornerMiddleFloors + (useMansardFloor ? 1 : 0) < regularSegmentsBeforeCap) { InstantiateFacadeSegment(cornerElementPrefabs, cornerBasePos + Vector3.up * currentY, cornerRotation, cornersParent.transform, cornerWidth); } /* No Y increment */ }

            // Instantiate Cap
            if (hasCapPrefabs)
            {
                float capY = cornerBasePos.y + floorHeight * regularSegmentsBeforeCap;
                InstantiateFacadeSegment(cornerCapPrefabs, cornerBasePos + Vector3.up * capY, cornerRotation, cornersParent.transform, cornerWidth);
            }
        }
    }

    void GenerateRoof(int[] sideMiddleFloors, Vector3 polygonCenter)
    {
        if (generateSlopedRoof)
        {
            GenerateRoofMesh_Sloped(sideMiddleFloors, polygonCenter);
        }
        else
        {
            GenerateRoofMesh_Flat(sideMiddleFloors, polygonCenter);
        }
    }

    // --- Roof Generation Helpers ---

    void GenerateRoofMesh_Flat(int[] sideMiddleFloors, Vector3 polygonCenter)
    {
        List<Vector3> roofVertices = CalculateRoofPerimeterVertices(sideMiddleFloors, polygonCenter, flatRoofEdgeOffset);
        if (roofVertices == null || roofVertices.Count < 3) return;
        GenerateFanMeshData(roofVertices, out List<Vector3> meshVertices, out List<int> meshTriangles, out List<Vector2> meshUVs);

        // Create GameObject and store mesh/transform for Gizmos
        GameObject roofObject = CreateMeshObject(meshVertices, meshTriangles, meshUVs, roofMaterial, "FlatRoofMesh", ROOF_FLAT_NAME, generatedBuildingRoot.transform);

#if UNITY_EDITOR
        _debugFlatRoofMesh = roofObject.GetComponent<MeshFilter>()?.sharedMesh; // Use sharedMesh in editor
        _debugFlatRoofTransform = roofObject.transform;
        // Clear other debug lists if switching modes
        _debugOuterRoofVertices = null;
        _debugInnerRoofVertices = null;
        _debugSlopedRoofMesh = null;
        _debugRoofCapMesh = null;
        _debugSlopedRoofTransform = null;
        _debugRoofCapTransform = null;
#endif
    }

    void GenerateRoofMesh_Sloped(int[] sideMiddleFloors, Vector3 polygonCenter)
    {
        if (vertexData.Count < 3 || roofSlopeHorizontalDistance <= 0.01f) return;
        CalculateSlopedRoofEdges(sideMiddleFloors, polygonCenter, out List<Vector3> outerVertices, out List<Vector3> innerVertices);
        if (outerVertices == null || innerVertices == null || outerVertices.Count < 3) return;

#if UNITY_EDITOR
        // Store vertices for Gizmos *before* generating mesh
        _debugOuterRoofVertices = new List<Vector3>(outerVertices); // Make copies
        _debugInnerRoofVertices = new List<Vector3>(innerVertices);
        // Clear flat roof debug info
        _debugFlatRoofMesh = null;
        _debugFlatRoofTransform = null;
        _debugRoofCapMesh = null;
        _debugRoofCapTransform = null;
#endif

        GenerateStripMeshData(outerVertices, innerVertices, out List<Vector3> meshVertices, out List<int> meshTriangles, out List<Vector2> meshUVs);
        GameObject slopedRoofObject = CreateMeshObject(meshVertices, meshTriangles, meshUVs, roofMaterial, "SlopedRoofMesh", ROOF_SLOPED_NAME, generatedBuildingRoot.transform);

#if UNITY_EDITOR
        _debugSlopedRoofMesh = slopedRoofObject.GetComponent<MeshFilter>()?.sharedMesh;
        _debugSlopedRoofTransform = slopedRoofObject.transform;
#endif

        if (generateRoofTopCap && innerVertices.Count >= 3)
        {
            GenerateFanMeshData(innerVertices, out List<Vector3> capVertices, out List<int> capTriangles, out List<Vector2> capUVs, true);
            GameObject capObject = CreateMeshObject(capVertices, capTriangles, capUVs, roofTopCapMaterial ?? roofMaterial, "RoofCapMesh", ROOF_CAP_NAME, generatedBuildingRoot.transform);
#if UNITY_EDITOR
            _debugRoofCapMesh = capObject.GetComponent<MeshFilter>()?.sharedMesh;
            _debugRoofCapTransform = capObject.transform;
#endif
        }
#if UNITY_EDITOR
        else
        { // Ensure cap debug info is cleared if cap isn't generated
            _debugRoofCapMesh = null;
            _debugRoofCapTransform = null;
        }
#endif
    }

    // --- Mesh Data Generation ---

    // Calculates vertices for a flat roof perimeter or sloped roof outer edge
    List<Vector3> CalculateRoofPerimeterVertices(int[] sideMiddleFloors, Vector3 polygonCenter, float edgeOffset)
    {
        List<Vector3> vertices = new List<Vector3>();
        for (int i = 0; i < vertexData.Count; i++)
        {
            int prevI = (i + vertexData.Count - 1) % vertexData.Count;
            Vector3 p1 = vertexData[prevI].position;
            Vector3 p2 = vertexData[i].position;
            Vector3 p3 = vertexData[(i + 1) % vertexData.Count].position;

            Vector3 sideDirPrev = (p2 - p1).normalized;
            Vector3 sideDirNext = (p3 - p2).normalized;
            Vector3 normalPrev = CalculateSideNormal(p1, p2, polygonCenter);
            Vector3 normalNext = CalculateSideNormal(p2, p3, polygonCenter);

            Vector3 vertexPosXZ;
            // Find intersection of offset lines
            if (Mathf.Abs(edgeOffset) > 0.01f) // Only intersect if offset exists
            {
                Vector3 lineOriginPrev = p1 + normalPrev * edgeOffset;
                Vector3 lineOriginNext = p2 + normalNext * edgeOffset;
                if (!GeometryUtils.LineLineIntersection(lineOriginPrev, sideDirPrev, lineOriginNext, sideDirNext, out vertexPosXZ))
                {
                    // Fallback for parallel lines
                    Vector3 avgNormal = (normalPrev + normalNext).normalized;
                    if (avgNormal == Vector3.zero) avgNormal = normalPrev;
                    vertexPosXZ = p2 + avgNormal * edgeOffset;
                }
            }
            else
            {
                vertexPosXZ = p2; // No offset, use original vertex XZ
            }

            // Calculate height
            int cornerMiddleFloors = Mathf.Max(sideMiddleFloors[prevI], sideMiddleFloors[i]);
            float cornerY = CalculateCornerHeight(cornerMiddleFloors);

            vertices.Add(new Vector3(vertexPosXZ.x, cornerY, vertexPosXZ.z));
        }
        return vertices;
    }

    // Calculates both outer and inner edge vertices for sloped roof
    // In CalculateSlopedRoofEdges function

    void CalculateSlopedRoofEdges(int[] sideMiddleFloors, Vector3 polygonCenter,
                                   out List<Vector3> outerVertices, out List<Vector3> innerVertices)
    {
        outerVertices = new List<Vector3>();
        innerVertices = new List<Vector3>();

        for (int i = 0; i < vertexData.Count; i++)
        {
            int prevI = (i + vertexData.Count - 1) % vertexData.Count;
            Vector3 p1_base = vertexData[prevI].position;
            Vector3 p2_base = vertexData[i].position;
            Vector3 p3_base = vertexData[(i + 1) % vertexData.Count].position;

            // Outer Vertex (No horizontal offset, calculated height)
            int outerCornerMiddleFloors = Mathf.Max(sideMiddleFloors[prevI], sideMiddleFloors[i]);
            float outerCornerY = CalculateCornerHeight(outerCornerMiddleFloors);
            outerVertices.Add(new Vector3(p2_base.x, outerCornerY, p2_base.z));

            // Inner Vertex (Horizontal offset, calculated height + rise)
            Vector3 sideDirPrev = (p2_base - p1_base).normalized;
            Vector3 sideDirNext = (p3_base - p2_base).normalized;
            Vector3 normalPrev = CalculateSideNormal(p1_base, p2_base, polygonCenter);
            Vector3 normalNext = CalculateSideNormal(p2_base, p3_base, polygonCenter);

            Vector3 innerVertexPosXZ;
            // *** FIX: Subtract offset distance to move inwards ***
            Vector3 innerLineOriginPrev = p1_base - normalPrev * roofSlopeHorizontalDistance; // Subtracted
            Vector3 innerLineOriginNext = p2_base - normalNext * roofSlopeHorizontalDistance; // Subtracted
            // Find intersection of INWARD offset lines
            if (!GeometryUtils.LineLineIntersection(innerLineOriginPrev, sideDirPrev, innerLineOriginNext, sideDirNext, out innerVertexPosXZ))
            {
                // Fallback for parallel lines: Offset inwards
                Vector3 avgNormal = (normalPrev + normalNext).normalized;
                if (avgNormal == Vector3.zero) avgNormal = normalPrev;
                innerVertexPosXZ = p2_base - avgNormal * roofSlopeHorizontalDistance; // Offset inwards
            }

            float innerCornerY = outerCornerY + roofRiseHeight;
            innerVertices.Add(new Vector3(innerVertexPosXZ.x, innerCornerY, innerVertexPosXZ.z));
        }
    }

    // Generates mesh data using center fan triangulation
    void GenerateFanMeshData(List<Vector3> perimeterVertices,
                              out List<Vector3> meshVertices, out List<int> meshTriangles, out List<Vector2> meshUVs,
                              bool forceFlatY = false)
    {
        meshVertices = new List<Vector3>();
        meshTriangles = new List<int>();
        meshUVs = new List<Vector2>();

        float finalY = perimeterVertices.Min(v => v.y); // Use Min Y if forcing flat, otherwise highest Y
        Vector3 center = Vector3.zero;
        foreach (var p in perimeterVertices) center += p;
        center /= perimeterVertices.Count;
        center.y = forceFlatY ? finalY : perimeterVertices.Max(v => v.y); // Choose Y based on flag

        meshVertices.Add(center);
        meshUVs.Add(new Vector2(center.x / roofUvScale, center.z / roofUvScale));

        foreach (Vector3 p in perimeterVertices)
        {
            Vector3 vertexToAdd = forceFlatY ? new Vector3(p.x, finalY, p.z) : p;
            meshVertices.Add(vertexToAdd);
            meshUVs.Add(new Vector2(p.x / roofUvScale, p.z / roofUvScale));
        }

        int centerIndex = 0;
        for (int i = 0; i < perimeterVertices.Count; i++)
        {
            int indexA = i + 1;
            int indexB = (i + 1) % perimeterVertices.Count + 1;
            // Correct Winding (Counter-Clockwise):
            meshTriangles.Add(centerIndex);
            meshTriangles.Add(indexA);
            meshTriangles.Add(indexB);
        }
    }

    // Generates mesh data for the strips connecting outer and inner roof loops
    void GenerateStripMeshData(List<Vector3> outerVertices, List<Vector3> innerVertices,
                                out List<Vector3> meshVertices, out List<int> meshTriangles, out List<Vector2> meshUVs)
    {
        meshVertices = new List<Vector3>();
        meshTriangles = new List<int>();
        meshUVs = new List<Vector2>();

        meshVertices.AddRange(outerVertices);
        meshVertices.AddRange(innerVertices);

        // Simple planar UVs for now
        foreach (Vector3 v in meshVertices)
        {
            meshUVs.Add(new Vector2(v.x * roofUvScale, v.z * roofUvScale)); // Project onto XZ plane
        }

        int N = outerVertices.Count;
        for (int i = 0; i < N; i++)
        {
            int currentOuter = i;
            int nextOuter = (i + 1) % N;
            int currentInner = i + N;
            int nextInner = (i + 1) % N + N;

            // Correct Winding for Triangle 1: currentOuter, nextInner, nextOuter

            meshTriangles.Add(currentOuter); meshTriangles.Add(nextOuter); meshTriangles.Add(nextInner); 

            // Correct Winding for Triangle 2: currentOuter, currentInner, nextInner

            meshTriangles.Add(currentOuter); meshTriangles.Add(nextInner); meshTriangles.Add(currentInner); 
        }
    }

    // Creates the actual mesh and GameObject
    GameObject CreateMeshObject(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, Material material, string meshName, string objectName, Transform parent)
    {
        Mesh mesh = new Mesh { name = meshName };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject meshObject = new GameObject(objectName);
        meshObject.transform.SetParent(parent, false);
        MeshFilter mf = meshObject.AddComponent<MeshFilter>();
        mf.mesh = mesh;
        MeshRenderer mr = meshObject.AddComponent<MeshRenderer>();
        mr.material = material;

        // Add this line
        return meshObject;
    }


    // --- Corner Calculation Helpers ---

    void CalculateCornerTransform(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 polygonCenter, out Vector3 cornerPos, out Quaternion cornerRot)
    {
        Vector3 sideNormalPrev = CalculateSideNormal(p1, p2, polygonCenter);
        Vector3 sideNormalNext = CalculateSideNormal(p2, p3, polygonCenter);

        Vector3 avgNormal = (sideNormalPrev + sideNormalNext).normalized;
        if (avgNormal == Vector3.zero) avgNormal = sideNormalNext; // Fallback

        cornerRot = Quaternion.LookRotation(avgNormal);

        Vector3 localOffset = Vector3.forward * cornerElementForwardOffset;
        Vector3 worldOffset = cornerRot * localOffset;
        cornerPos = p2 + worldOffset;
    }

    int CalculateRegularCornerSegments(int cornerMiddleFloors, bool placeCap)
    {
        int segments = 1 + cornerMiddleFloors;
        if (useMansardFloor) segments++;
        if (useAtticFloor) segments++;
        if (placeCap) segments = Mathf.Max(0, segments - 1);
        return segments;
    }


    // --- Height and Prefab Helpers ---

    int[] CalculateSideHeights()
    {
        int[] sideMiddleFloors = new int[vertexData.Count];
        for (int i = 0; i < vertexData.Count; i++)
        {
            int height = middleFloors; // Start with default
            if (allowHeightVariation)
            {
                height = Mathf.Max(0, middleFloors + Random.Range(-maxHeightVariation, maxHeightVariation + 1));
            }
            sideMiddleFloors[i] = height;
        }
        return sideMiddleFloors;
    }

    float CalculateCornerHeight(int middleFloors) // Helper moved here
    {
        float cornerY = floorHeight; // Ground
        cornerY += middleFloors * floorHeight; // Middle
        if (useMansardFloor) cornerY += floorHeight;
        //if (useAtticFloor) cornerY += floorHeight;
        return cornerY;
    }

    void GetSidePrefabLists(int sideIndex, out List<GameObject> ground, out List<GameObject> middle, out List<GameObject> mansard, out List<GameObject> attic)
    {
        PolygonSideData currentSideSettings = sideData[sideIndex];
        if (currentSideSettings.useCustomStyle && currentSideSettings.sideStylePreset != null)
        {
            SideStyleSO style = currentSideSettings.sideStylePreset;
            ground = style.groundFloorPrefabs ?? defaultGroundFloorPrefabs;
            middle = style.middleFloorPrefabs ?? defaultMiddleFloorPrefabs;
            mansard = style.mansardFloorPrefabs ?? defaultMansardFloorPrefabs;
            attic = style.atticFloorPrefabs ?? defaultAtticFloorPrefabs;
        }
        else
        {
            ground = defaultGroundFloorPrefabs;
            middle = defaultMiddleFloorPrefabs;
            mansard = defaultMansardFloorPrefabs;
            attic = defaultAtticFloorPrefabs;
        }
    }


    // --- Instantiation and Utility Helpers ---

    void InstantiateFacadeSegment(List<GameObject> prefabList, Vector3 worldPosition, Quaternion worldRotation, Transform parent, float segmentWidth) // Renamed parameters for clarity
    {
        if (prefabList == null || prefabList.Count == 0) return;
        int randomIndex = Random.Range(0, prefabList.Count);
        GameObject prefab = prefabList[randomIndex];
        if (prefab == null) return;

        // Instantiate at world pos/rot initially for correct prefab orientation relative to world
        GameObject instance = Instantiate(prefab, worldPosition, worldRotation, parent);

        // Apply scaling if needed
        if (scaleFacadesToFitSide && Mathf.Abs(segmentWidth - nominalFacadeWidth) > 0.01f)
        {
            Vector3 localScale = instance.transform.localScale; // Get current local scale
            float scaleFactor = segmentWidth / nominalFacadeWidth;
            // Scale relative to the nominal width, applying to instance's current X scale
            instance.transform.localScale = new Vector3(localScale.x * scaleFactor, localScale.y, localScale.z);
        }
        // Optionally, you might want to reset local position/rotation after parenting if the prefab pivot isn't centered
        // instance.transform.localPosition = parent.InverseTransformPoint(worldPosition);
        // instance.transform.localRotation = Quaternion.Inverse(parent.rotation) * worldRotation;
    }

    // Overload for corner elements where position/rotation is already calculated correctly relative to parent
    void InstantiateFacadeSegment(List<GameObject> prefabList, Vector3 localPosition, Quaternion localRotation, Transform parent, float segmentWidth, bool isCorner = true)
    {
        if (!isCorner)
        { // Fallback to the other method if not explicitly a corner
            InstantiateFacadeSegment(prefabList, parent.TransformPoint(localPosition), parent.rotation * localRotation, parent, segmentWidth);
            return;
        }

        if (prefabList == null || prefabList.Count == 0) return;
        int randomIndex = Random.Range(0, prefabList.Count);
        GameObject prefab = prefabList[randomIndex];
        if (prefab == null) return;

        GameObject instance = Instantiate(prefab, parent);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = localRotation;
        // No scaling typically applied to corners
    }

    Vector3 CalculatePolygonCenter()
    {
        Vector3 center = Vector3.zero;
        if (vertexData.Count > 0)
        {
            foreach (var vd in vertexData) center += vd.position;
            center /= vertexData.Count;
        }
        return center;
    }

    float CalculateSignedArea()
    {
        if (vertexData == null || vertexData.Count < 3) return 0f;

        float area = 0f;
        for (int i = 0; i < vertexData.Count; i++)
        {
            Vector3 p1 = vertexData[i].position;
            Vector3 p2 = vertexData[(i + 1) % vertexData.Count].position; // Wrap around
            // Shoelace formula component for XZ plane
            area += (p1.x * p2.z) - (p2.x * p1.z);
        }
        return area / 2.0f;
    }

    int CalculateNumSegments(float sideDistance)
    {
        int num = Mathf.Max(minSideLengthUnits, Mathf.RoundToInt(sideDistance / nominalFacadeWidth));
        if (!scaleFacadesToFitSide)
        {
            num = Mathf.Max(minSideLengthUnits, Mathf.FloorToInt(sideDistance / nominalFacadeWidth));
            if (num == 0 && minSideLengthUnits > 0) num = minSideLengthUnits;
        }
        return num;
    }

    float CalculateSegmentWidth(float sideDistance, int numSegments)
    {
        return scaleFacadesToFitSide ? (sideDistance / numSegments) : nominalFacadeWidth;
    }

    Vector3 CalculateSideNormal(Vector3 p1, Vector3 p2, Vector3 polygonCenter)
    {
        Vector3 sideDirection = (p2 - p1).normalized;
        Vector3 sideNormal = Vector3.Cross(sideDirection, Vector3.up).normalized;
        Vector3 sideMidpoint = p1 + sideDirection * Vector3.Distance(p1, p2) / 2f;
        Vector3 centerToMidpoint = sideMidpoint - polygonCenter;
        centerToMidpoint.y = 0;
        Vector3 checkNormal = sideNormal; checkNormal.y = 0;
        if (Vector3.Dot(checkNormal.normalized, centerToMidpoint.normalized) < -0.01f)
        {
            sideNormal *= -1;
        }
        return sideNormal;
    }

/*    Vector3 CalculateSideNormal(Vector3 p1, Vector3 p2, Vector3 polygonCenter *//*polygonCenter no longer needed here*//*)
    {
        Vector3 sideDirection = (p2 - p1).normalized;
        if (sideDirection == Vector3.zero) return Vector3.forward; // Avoid issues with zero-length sides

        // Calculate the perpendicular vector using cross product.
        // This points "right" relative to sideDirection on the XZ plane.
        Vector3 initialNormal = Vector3.Cross(sideDirection, Vector3.up).normalized;

        // Determine the polygon's winding order based on signed area
        float signedArea = CalculateSignedArea();

        // Define what "outward" means based on winding order.
        // Convention: Assume Counter-Clockwise (CCW) winding means outward is "left" (Cross(up, dir)),
        // and Clockwise (CW) means outward is "right" (Cross(dir, up)).
        // Our initialNormal IS Cross(dir, up), which is outward for CW.

        if (signedArea > Mathf.Epsilon) // Polygon is Counter-Clockwise (Positive Area)
        {
            // initialNormal currently points "right" (inward for CCW). Flip it to point "left" (outward).
            return -initialNormal;
            // Alternatively, calculate the other cross product: return Vector3.Cross(Vector3.up, sideDirection).normalized;
        }
        else if (signedArea < -Mathf.Epsilon) // Polygon is Clockwise (Negative Area)
        {
            // initialNormal currently points "right" (outward for CW). Use it directly.
            return initialNormal;
        }
        else
        {
            // Area is zero (collinear points or error). Fallback using the old center check or default.
            // Let's fallback to the initial calculation, although this case shouldn't happen for valid polygons.
            Debug.LogWarning("Polygon area is close to zero, normal calculation might be unreliable.");
            return initialNormal;
            // Old Center Check Fallback (if needed):
            // Vector3 sideMidpoint = p1 + sideDirection * Vector3.Distance(p1, p2) / 2f;
            // Vector3 centerToMidpoint = sideMidpoint - polygonCenter; centerToMidpoint.y = 0;
            // Vector3 checkNormal = initialNormal; checkNormal.y=0;
            // if (Vector3.Dot(checkNormal.normalized, centerToMidpoint.normalized) < -0.01f) return -initialNormal;
            // else return initialNormal;
        }
    }*/

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