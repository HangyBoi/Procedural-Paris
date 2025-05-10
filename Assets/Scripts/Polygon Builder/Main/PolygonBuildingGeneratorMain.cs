using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(MeshFilter))]
public class PolygonBuildingGeneratorMain : MonoBehaviour
{
    [Header("Polygon Definition")]
    public List<PolygonVertexData> vertexData = new List<PolygonVertexData>() {
        new() { position = new Vector3(0, 0, 0), addCornerElement = true },
        new() { position = new Vector3(0, 0, 5), addCornerElement = true },
        new() { position = new Vector3(5, 0, 5), addCornerElement = true },
        new() { position = new Vector3(5, 0, 0), addCornerElement = true }
    };
    public List<PolygonSideData> sideData = new List<PolygonSideData>();
    public float vertexSnapSize = 1.0f;
    public int minSideLengthUnits = 1;

    [Header("Building Settings")]
    public int middleFloors = 3;
    public float floorHeight = 3.0f; // CRITICAL: Assumed to be the VERTICAL rise of Ground/Middle floor prefabs for centering

    [Header("Facade Placement")]
    public bool scaleFacadesToFitSide = true;
    public float nominalFacadeWidth = 1.0f;

    [Header("Default Prefabs")]
    public List<GameObject> defaultGroundFloorPrefabs;
    public List<GameObject> defaultMiddleFloorPrefabs;

    [Header("Corner Elements")]
    public List<GameObject> cornerElementPrefabs;
    public bool useCornerCaps = true;
    public List<GameObject> cornerCapPrefabs;
    public float cornerElementForwardOffset = 0.0f;

    [Header("Procedural Mansard Roof")]
    public bool useMansardFloor = true;
    public Material mansardMaterial;
    public float mansardSlopeHorizontalDistance = 1.5f;
    public float mansardRiseHeight = 2.0f;
    public float mansardAngleFromVerticalDegrees = 10.0f;

    [Header("Procedural Attic Roof")]
    public bool useAtticFloor = true;
    public Material atticMaterial;
    public float atticSlopeHorizontalDistance = 1.0f;
    public float atticRiseHeight = 1.5f;
    public float atticAngleFromVerticalDegrees = 50.0f;

    [Header("Top Roof Settings")]
    public float flatRoofEdgeOffset = 0.0f;
    public Material roofMaterial;
    public float roofUvScale = 1.0f;

    private GameObject generatedBuildingRoot;
    private const string ROOT_NAME = "Generated Building";
    private const string CORNERS_NAME = "Corner Elements";
    private const string ROOF_FLAT_NAME = "Procedural Flat Roof Cap";
    private const string MANSARD_FLOOR_NAME = "Procedural Mansard Floor";
    private const string ATTIC_FLOOR_NAME = "Procedural Attic Floor";


#if UNITY_EDITOR
    [HideInInspector] public Mesh _debugFlatRoofMesh;
    [HideInInspector] public Transform _debugFlatRoofTransform;
    [HideInInspector] public Mesh _debugMansardMesh;
    [HideInInspector] public Transform _debugMansardTransform;
    [HideInInspector] public Mesh _debugAtticMesh;
    [HideInInspector] public Transform _debugAtticTransform;
#endif

    public void GenerateBuilding()
    {
        ClearBuilding();
        SynchronizeSideData();
        if (vertexData.Count < 3)
        {
            Debug.LogWarning("Cannot generate building: Polygon requires at least 3 vertices.");
            return;
        }

        generatedBuildingRoot = new GameObject(ROOT_NAME);
        generatedBuildingRoot.transform.SetParent(this.transform, false);

        GenerateFacades();
        GenerateCornerElements();
        GenerateRoof();
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
        _debugFlatRoofMesh = null;
        _debugFlatRoofTransform = null;
        _debugMansardMesh = null;
        _debugMansardTransform = null;
        _debugAtticMesh = null;
        _debugAtticTransform = null;
#endif
    }

    void GenerateFacades()
    {
        float pivotOffsetVertical = floorHeight * 0.5f;

        for (int i = 0; i < vertexData.Count; i++)
        {
            GameObject sideParent = new GameObject($"Side_{i}");
            sideParent.transform.SetParent(generatedBuildingRoot.transform, false);

            Vector3 p1 = vertexData[i].position;
            Vector3 p2 = vertexData[(i + 1) % vertexData.Count].position;
            Vector3 sideVector = p2 - p1;
            float sideDistance = sideVector.magnitude;

            if (sideDistance < GeometryUtils.Epsilon) continue;

            Vector3 sideDirection = sideVector.normalized;
            Vector3 sideNormal = CalculateSideNormal(p1, p2);

            int numSegments = CalculateNumSegments(sideDistance);
            float actualSegmentWidth = CalculateSegmentWidth(sideDistance, numSegments);

            GetSidePrefabLists(i, out var currentGround, out var currentMiddle);

            for (int j = 0; j < numSegments; j++)
            {
                Vector3 segmentBaseHorizontalPos = p1 + sideDirection * (actualSegmentWidth * (j + 0.5f));
                Quaternion baseSegmentRotation = Quaternion.LookRotation(sideNormal);
                float currentBottomY = 0;

                Vector3 groundFloorPivotPosition = segmentBaseHorizontalPos + Vector3.up * (currentBottomY + pivotOffsetVertical);
                InstantiateFacadeSegment(currentGround, groundFloorPivotPosition, baseSegmentRotation, sideParent.transform, actualSegmentWidth, false);
                currentBottomY += floorHeight;

                for (int floor = 0; floor < middleFloors; floor++)
                {
                    Vector3 middleFloorPivotPosition = segmentBaseHorizontalPos + Vector3.up * (currentBottomY + pivotOffsetVertical);
                    InstantiateFacadeSegment(currentMiddle, middleFloorPivotPosition, baseSegmentRotation, sideParent.transform, actualSegmentWidth, false);
                    currentBottomY += floorHeight;
                }
            }
        }
    }

    void GenerateCornerElements()
    {
        bool hasCornerBodyPrefabs = cornerElementPrefabs != null && cornerElementPrefabs.Count > 0;
        bool hasCornerCapPrefabs = useCornerCaps && cornerCapPrefabs != null && cornerCapPrefabs.Count > 0;

        if (!hasCornerBodyPrefabs && !hasCornerCapPrefabs) return;

        GameObject cornersParent = new GameObject(CORNERS_NAME);
        cornersParent.transform.SetParent(generatedBuildingRoot.transform, false);

        float pivotOffsetVertical = floorHeight * 0.5f;

        for (int i = 0; i < vertexData.Count; i++)
        {
            if (!vertexData[i].addCornerElement) continue;

            int prevI = (i + vertexData.Count - 1) % vertexData.Count;
            Vector3 currentPosRaw = vertexData[i].position;
            Vector3 prevPos = vertexData[prevI].position;
            Vector3 nextPos = vertexData[(i + 1) % vertexData.Count].position;

            CalculateCornerTransform(prevPos, currentPosRaw, nextPos, out Vector3 cornerBaseHorizontalPos, out Quaternion baseCornerRotation);

            float nominalCurrentElementBaseY = 0;
            float cornerWidth = nominalFacadeWidth;

            if (hasCornerBodyPrefabs)
            {
                Vector3 groundCornerPivotPos = cornerBaseHorizontalPos + Vector3.up * (nominalCurrentElementBaseY + pivotOffsetVertical);
                InstantiateFacadeSegment(cornerElementPrefabs, groundCornerPivotPos, baseCornerRotation, cornersParent.transform, cornerWidth, true);
                nominalCurrentElementBaseY += floorHeight;

                for (int floor = 0; floor < middleFloors; floor++)
                {
                    Vector3 middleCornerPivotPos = cornerBaseHorizontalPos + Vector3.up * (nominalCurrentElementBaseY + pivotOffsetVertical);
                    InstantiateFacadeSegment(cornerElementPrefabs, middleCornerPivotPos, baseCornerRotation, cornersParent.transform, cornerWidth, true);
                    nominalCurrentElementBaseY += floorHeight;
                }
            }

            if (hasCornerCapPrefabs)
            {
                float capBaseY = floorHeight;
                capBaseY += middleFloors * floorHeight;

                Vector3 capPosition = cornerBaseHorizontalPos + Vector3.up * capBaseY;
                InstantiateFacadeSegment(cornerCapPrefabs, capPosition, baseCornerRotation, cornersParent.transform, cornerWidth, true);
            }
        }
    }

    void GenerateRoof()
    {
        float wallTopHeight = CalculateTotalWallTopHeight();

        List<Vector3> currentOuterEdgeLoop = new List<Vector3>();
        foreach (var vd in vertexData)
        {
            currentOuterEdgeLoop.Add(new Vector3(vd.position.x, wallTopHeight, vd.position.z));
        }

        if (currentOuterEdgeLoop.Count < 3)
        {
            Debug.LogWarning("Cannot generate roof: Base polygon has less than 3 vertices.");
            return;
        }

        // --- Generate Mansard Floor Mesh (if enabled) ---
        if (useMansardFloor && mansardSlopeHorizontalDistance > GeometryUtils.Epsilon)
        {
            List<Vector3> innerMansardEdgeLoop = CalculateInnerRoofEdge(currentOuterEdgeLoop, mansardSlopeHorizontalDistance, mansardRiseHeight);

            if (innerMansardEdgeLoop != null && innerMansardEdgeLoop.Count >= 3)
            {
                GenerateStripMeshData(currentOuterEdgeLoop, innerMansardEdgeLoop,
                                      out List<Vector3> mansardMeshVertices,
                                      out List<int> mansardMeshTriangles,
                                      out List<Vector2> mansardMeshUVs);
                Material mat = mansardMaterial != null ? mansardMaterial : roofMaterial;
                GameObject mansardObj = CreateMeshObject(mansardMeshVertices, mansardMeshTriangles, mansardMeshUVs, mat, "MansardFloorMesh", MANSARD_FLOOR_NAME, generatedBuildingRoot.transform);
                currentOuterEdgeLoop = innerMansardEdgeLoop;
#if UNITY_EDITOR
                if (mansardObj != null)
                {
                    _debugMansardMesh = mansardObj.GetComponent<MeshFilter>()?.sharedMesh;
                    _debugMansardTransform = mansardObj.transform;
                }
#endif
            }
            else
            {
                Debug.LogWarning("Failed to calculate inner mansard edge loop or not enough vertices, skipping mansard mesh generation.");
            }
        }

        // --- Generate Attic Floor Mesh (if enabled) ---
        if (useAtticFloor && atticSlopeHorizontalDistance > GeometryUtils.Epsilon)
        {
            List<Vector3> innerAtticEdgeLoop = CalculateInnerRoofEdge(currentOuterEdgeLoop, atticSlopeHorizontalDistance, atticRiseHeight);

            if (innerAtticEdgeLoop != null && innerAtticEdgeLoop.Count >= 3)
            {
                GenerateStripMeshData(currentOuterEdgeLoop, innerAtticEdgeLoop,
                                      out List<Vector3> atticMeshVertices,
                                      out List<int> atticMeshTriangles,
                                      out List<Vector2> atticMeshUVs);
                Material mat = atticMaterial != null ? atticMaterial : roofMaterial;
                GameObject atticObj = CreateMeshObject(atticMeshVertices, atticMeshTriangles, atticMeshUVs, mat, "AtticFloorMesh", ATTIC_FLOOR_NAME, generatedBuildingRoot.transform);
                currentOuterEdgeLoop = innerAtticEdgeLoop;
#if UNITY_EDITOR
                if (atticObj != null)
                {
                    _debugAtticMesh = atticObj.GetComponent<MeshFilter>()?.sharedMesh;
                    _debugAtticTransform = atticObj.transform;
                }
#endif
            }
            else
            {
                Debug.LogWarning("Failed to calculate inner attic edge loop or not enough vertices, skipping attic mesh generation.");
            }
        }

        // --- Generate Final Top Roof Cap (Flat) ---
        List<Vector3> flatRoofPerimeter;
        if (Mathf.Abs(flatRoofEdgeOffset) > GeometryUtils.Epsilon)
        {
            flatRoofPerimeter = CalculateInnerRoofEdge(currentOuterEdgeLoop, -flatRoofEdgeOffset, 0f);
            if (flatRoofPerimeter == null || flatRoofPerimeter.Count < 3)
            {
                Debug.LogWarning("Flat roof offset calculation failed, using un-offseted perimeter.");
                flatRoofPerimeter = new List<Vector3>(currentOuterEdgeLoop);
            }
        }
        else
        {
            flatRoofPerimeter = new List<Vector3>(currentOuterEdgeLoop);
        }


        if (flatRoofPerimeter.Count < 3)
        {
            Debug.LogWarning("Cannot generate flat roof cap: Less than 3 perimeter vertices.");
#if UNITY_EDITOR
            _debugFlatRoofMesh = null;
            _debugFlatRoofTransform = null;
#endif
            return;
        }

        List<int> capMeshTriangles; // Declare here to be used after the if
        if (!GeometryUtils.TriangulatePolygonEarClipping(flatRoofPerimeter, out capMeshTriangles))
        {
            Debug.LogError("Flat Roof cap triangulation failed.");
#if UNITY_EDITOR
            _debugFlatRoofMesh = null;
            _debugFlatRoofTransform = null;
#endif
            return;
        }

        List<Vector3> capMeshVertices = flatRoofPerimeter;
        List<Vector2> capMeshUVs = CalculatePlanarUVs(capMeshVertices, roofUvScale);

        GameObject roofObject = CreateMeshObject(capMeshVertices, capMeshTriangles, capMeshUVs, roofMaterial, "FlatRoofCapMesh", ROOF_FLAT_NAME, generatedBuildingRoot.transform);

#if UNITY_EDITOR
        if (roofObject != null)
        {
            _debugFlatRoofMesh = roofObject.GetComponent<MeshFilter>()?.sharedMesh;
            _debugFlatRoofTransform = roofObject.transform;
        }
        else
        {
            _debugFlatRoofMesh = null;
            _debugFlatRoofTransform = null;
        }
#endif
    }


    List<Vector3> CalculateInnerRoofEdge(List<Vector3> outerLoop, float horizontalDistance, float riseHeight)
    {
        if (outerLoop == null || outerLoop.Count < 3)
        {
            Debug.LogError("CalculateInnerRoofEdge: Outer loop is null or has less than 3 vertices.");
            return null;
        }

        if (vertexData.Count != outerLoop.Count)
        {
            Debug.LogError($"CalculateInnerRoofEdge: VertexData count ({vertexData.Count}) mismatch with outerLoop count ({outerLoop.Count}). This indicates an issue with loop generation logic.");
            // Fallback or more robust error handling might be needed.
            // For now, if counts mismatch, the logic might produce unexpected offsets.
        }

        List<Vector3> innerVertices = new List<Vector3>(outerLoop.Count);
        int n = outerLoop.Count;

        for (int i = 0; i < n; i++)
        {
            Vector3 p1_base = vertexData[(i + n - 1) % n].position;
            Vector3 p2_base = vertexData[i].position;
            Vector3 p3_base = vertexData[(i + 1) % n].position;

            Vector3 p2_outer = outerLoop[i];

            Vector3 sideDirPrev_base = (p2_base - p1_base).normalized;
            Vector3 sideDirNext_base = (p3_base - p2_base).normalized;
            Vector3 normalPrev_base = CalculateSideNormal(p1_base, p2_base);
            Vector3 normalNext_base = CalculateSideNormal(p2_base, p3_base);

            Vector3 innerVertexPosXZ;

            if (Mathf.Abs(horizontalDistance) < GeometryUtils.Epsilon)
            {
                innerVertexPosXZ = new Vector3(p2_outer.x, 0, p2_outer.z);
            }
            else
            {
                Vector3 lineOriginPrev_XZ = new Vector3(p1_base.x, 0, p1_base.z) - normalPrev_base * horizontalDistance;
                Vector3 lineDirPrev_XZ = sideDirPrev_base;

                Vector3 lineOriginNext_XZ = new Vector3(p2_base.x, 0, p2_base.z) - normalNext_base * horizontalDistance;
                Vector3 lineDirNext_XZ = sideDirNext_base;

                if (GeometryUtils.LineLineIntersection(lineOriginPrev_XZ, lineDirPrev_XZ, lineOriginNext_XZ, lineDirNext_XZ, out innerVertexPosXZ))
                {
                    // Intersection found
                }
                else
                {
                    Vector3 avgNormal = (normalPrev_base + normalNext_base).normalized;
                    if (avgNormal.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon)
                    {
                        avgNormal = new Vector3(sideDirPrev_base.z, 0, -sideDirPrev_base.x);
                    }
                    innerVertexPosXZ = new Vector3(p2_outer.x, 0, p2_outer.z) - avgNormal * horizontalDistance;
                }
            }

            float innerCornerY = p2_outer.y + riseHeight;
            innerVertices.Add(new Vector3(innerVertexPosXZ.x, innerCornerY, innerVertexPosXZ.z));
        }
        return innerVertices;
    }

    void GenerateStripMeshData(List<Vector3> outerVertices, List<Vector3> innerVertices,
                               out List<Vector3> meshVertices, out List<int> meshTriangles, out List<Vector2> meshUVs)
    {
        meshVertices = new List<Vector3>(); // Initialize out parameter
        meshTriangles = new List<int>();   // Initialize out parameter
        meshUVs = new List<Vector2>();     // Initialize out parameter

        if (outerVertices.Count != innerVertices.Count || outerVertices.Count < 3)
        {
            Debug.LogError("GenerateStripMeshData: Vertex count mismatch or insufficient vertices.");
            return;
        }

        meshVertices.AddRange(outerVertices);
        meshVertices.AddRange(innerVertices);

        foreach (Vector3 v in meshVertices)
        {
            meshUVs.Add(new Vector2(v.x * roofUvScale, v.z * roofUvScale));
        }

        int N = outerVertices.Count;
        for (int i = 0; i < N; i++)
        {
            int currentOuter = i;
            int nextOuter = (i + 1) % N;
            int currentInner = i + N;
            int nextInner = ((i + 1) % N) + N;

            meshTriangles.Add(currentOuter);
            meshTriangles.Add(nextOuter);
            meshTriangles.Add(nextInner);

            meshTriangles.Add(currentOuter);
            meshTriangles.Add(nextInner);
            meshTriangles.Add(currentInner);
        }
    }

    List<Vector2> CalculatePlanarUVs(List<Vector3> vertices, float uvScale)
    {
        List<Vector2> uvs = new List<Vector2>(vertices.Count);
        foreach (Vector3 v in vertices)
        {
            uvs.Add(new Vector2(v.x * uvScale, v.z * uvScale));
        }
        return uvs;
    }

    GameObject CreateMeshObject(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, Material material, string meshName, string objectName, Transform parent)
    {
        if (vertices.Count == 0 || triangles.Count == 0)
        {
            Debug.LogWarning($"Attempted to create empty mesh: {objectName}");
            return null;
        }
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
        return meshObject;
    }

    void CalculateCornerTransform(Vector3 p1_prev, Vector3 p2_current, Vector3 p3_next, out Vector3 cornerPos, out Quaternion cornerRot)
    {
        Vector3 sideNormalPrev = CalculateSideNormal(p1_prev, p2_current);
        Vector3 sideNormalNext = CalculateSideNormal(p2_current, p3_next);
        Vector3 cornerFacingDirection = (sideNormalPrev + sideNormalNext).normalized;

        if (cornerFacingDirection.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon)
        {
            Vector3 dir1 = (p2_current - p1_prev).normalized;
            cornerFacingDirection = sideNormalNext;
            if (cornerFacingDirection.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon)
            {
                cornerFacingDirection = Quaternion.Euler(0, 90, 0) * dir1;
                if (cornerFacingDirection.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon)
                {   
                    cornerFacingDirection = Vector3.forward;
                }
            }
        }
        cornerRot = Quaternion.LookRotation(cornerFacingDirection);
        Vector3 localOffset = Vector3.forward * cornerElementForwardOffset;
        Vector3 worldOffset = cornerRot * localOffset;
        cornerPos = p2_current + worldOffset;
    }

    float CalculateTotalWallTopHeight()
    {
        float height = 0;
        height += floorHeight;
        height += middleFloors * floorHeight;
        return height;
    }

    void GetSidePrefabLists(int sideIndex, out List<GameObject> ground, out List<GameObject> middle)
    {
        ground = defaultGroundFloorPrefabs;
        middle = defaultMiddleFloorPrefabs;

        if (sideIndex < 0 || sideIndex >= sideData.Count)
        {
            Debug.LogError($"sideIndex {sideIndex} out of bounds for sideData. Falling back to defaults.");
            return;
        }

        PolygonSideData currentSideSettings = sideData[sideIndex];
        if (currentSideSettings.useCustomStyle && currentSideSettings.sideStylePreset != null)
        {
            SideStyleSO style = currentSideSettings.sideStylePreset;
            ground = (style.groundFloorPrefabs != null && style.groundFloorPrefabs.Count > 0) ? style.groundFloorPrefabs : defaultGroundFloorPrefabs;
            middle = (style.middleFloorPrefabs != null && style.middleFloorPrefabs.Count > 0) ? style.middleFloorPrefabs : defaultMiddleFloorPrefabs;
        }
    }

    void InstantiateFacadeSegment(List<GameObject> prefabList, Vector3 worldPosition, Quaternion worldRotation, Transform parent, float segmentWidth, bool isCorner)
    {
        if (prefabList == null || prefabList.Count == 0) return;
        int randomIndex = Random.Range(0, prefabList.Count);
        GameObject prefab = prefabList[randomIndex];
        if (prefab == null) return;

        GameObject instance = Instantiate(prefab, parent);
        instance.transform.position = worldPosition;
        instance.transform.rotation = worldRotation;

        if (!isCorner && scaleFacadesToFitSide && nominalFacadeWidth > GeometryUtils.Epsilon && Mathf.Abs(segmentWidth - nominalFacadeWidth) > GeometryUtils.Epsilon)
        {
            Vector3 localScale = instance.transform.localScale;
            float scaleFactor = segmentWidth / nominalFacadeWidth;
            instance.transform.localScale = new Vector3(localScale.x * scaleFactor, localScale.y, localScale.z);
        }
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
            Vector3 p2 = vertexData[(i + 1) % vertexData.Count].position;
            area += (p1.x * p2.z) - (p2.x * p1.z);
        }
        return area / 2.0f;
    }

    public int CalculateNumSegments(float sideDistance)
    {
        if (nominalFacadeWidth <= GeometryUtils.Epsilon) return Mathf.Max(1, minSideLengthUnits > 0 ? minSideLengthUnits : 1);
        int num;
        if (scaleFacadesToFitSide)
        {
            num = Mathf.Max(minSideLengthUnits > 0 ? minSideLengthUnits : 1, Mathf.RoundToInt(sideDistance / nominalFacadeWidth));
        }
        else
        {
            num = Mathf.Max(minSideLengthUnits > 0 ? minSideLengthUnits : 1, Mathf.FloorToInt(sideDistance / nominalFacadeWidth));
        }
        return Mathf.Max(1, num);
    }

    float CalculateSegmentWidth(float sideDistance, int numSegments)
    {
        if (numSegments == 0) return nominalFacadeWidth;
        return scaleFacadesToFitSide ? (sideDistance / numSegments) : nominalFacadeWidth;
    }

    public Vector3 CalculateSideNormal(Vector3 p1, Vector3 p2)
    {
        Vector3 sideDirection = (p2 - p1).normalized;
        sideDirection.y = 0;

        if (sideDirection.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon)
        {
            return Vector3.forward;
        }
        sideDirection.Normalize();

        Vector3 initialNormal = Vector3.Cross(sideDirection, Vector3.up).normalized;
        float signedArea = CalculateSignedArea();

        if (signedArea > GeometryUtils.Epsilon) return -initialNormal;
        else return initialNormal;
    }

    public void SynchronizeSideData()
    {
        if (vertexData == null) vertexData = new List<PolygonVertexData>();
        if (sideData == null) sideData = new List<PolygonSideData>();
        int requiredCount = vertexData.Count;
        while (sideData.Count < requiredCount) sideData.Add(new PolygonSideData());
        while (sideData.Count > requiredCount && sideData.Count > 0) sideData.RemoveAt(sideData.Count - 1);
    }

    void OnValidate()
    {
        SynchronizeSideData();
        middleFloors = Mathf.Max(0, middleFloors);
        floorHeight = Mathf.Max(0.1f, floorHeight);
        nominalFacadeWidth = Mathf.Max(0.1f, nominalFacadeWidth);
        minSideLengthUnits = Mathf.Max(0, minSideLengthUnits);

        mansardSlopeHorizontalDistance = Mathf.Max(0, mansardSlopeHorizontalDistance);
        mansardRiseHeight = Mathf.Max(0, mansardRiseHeight);
        atticSlopeHorizontalDistance = Mathf.Max(0, atticSlopeHorizontalDistance);
        atticRiseHeight = Mathf.Max(0, atticRiseHeight);
    }

    public Vector3 SnapVertexPosition(Vector3 vertexPos)
    {
        if (vertexSnapSize <= GeometryUtils.Epsilon) return new Vector3(vertexPos.x, 0f, vertexPos.z);
        return new Vector3(
            Mathf.Round(vertexPos.x / vertexSnapSize) * vertexSnapSize,
            0f,
            Mathf.Round(vertexPos.z / vertexSnapSize) * vertexSnapSize
        );
    }
}
