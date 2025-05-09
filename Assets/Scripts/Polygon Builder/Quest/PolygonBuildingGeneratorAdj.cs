// PolygonBuildingGeneratorAdj.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(MeshFilter))]
public class PolygonBuildingGeneratorAdj : MonoBehaviour
{
    [Header("Polygon Definition")]
    public List<PolygonVertexDataAdj> vertexData = new List<PolygonVertexDataAdj>() {
        new() { position = new Vector3(0, 0, 0), addCornerElement = true },
        new() { position = new Vector3(0, 0, 5), addCornerElement = true },
        new() { position = new Vector3(5, 0, 5), addCornerElement = true },
        new() { position = new Vector3(5, 0, 0), addCornerElement = true }
    };
    public List<PolygonSideDataAdj> sideData = new List<PolygonSideDataAdj>();
    public float vertexSnapSize = 1.0f;
    public int minSideLengthUnits = 1;

    [Header("Building Settings")]
    public int middleFloors = 3;
    public float floorHeight = 3.0f; // CRITICAL: Assumed to be the VERTICAL rise of EACH floor type
    public bool useMansardFloor = true;
    // public float mansardAngleDegrees = 10.0f; // Script will not use this if prefabs are pre-rotated
    public bool useAtticFloor = true;
    // public float atticAngleDegrees = 50.0f;   // Script will not use this if prefabs are pre-rotated

    [Header("Facade Placement")]
    public bool scaleFacadesToFitSide = true;
    public float nominalFacadeWidth = 1.0f;

    [Header("Default Prefabs")]
    public List<GameObject> defaultGroundFloorPrefabs;
    public List<GameObject> defaultMiddleFloorPrefabs;
    public List<GameObject> defaultMansardFloorPrefabs; // These should be pre-rotated & centered for floorHeight
    public List<GameObject> defaultAtticFloorPrefabs;   // These should be pre-rotated & centered for floorHeight

    [Header("Corner Elements")]
    public List<GameObject> cornerElementPrefabs; // Also assumed pre-rotated if they are mansard/attic types
    public bool useCornerCaps = true;
    public List<GameObject> cornerCapPrefabs;
    public float cornerElementForwardOffset = 0.0f;

    [Header("Roof Settings")]
    public float flatRoofEdgeOffset = 0.0f;
    public Material roofMaterial;
    public float roofUvScale = 1.0f;

    private GameObject generatedBuildingRoot;
    private const string ROOT_NAME = "Generated Building";
    private const string CORNERS_NAME = "Corner Elements";
    private const string ROOF_FLAT_NAME = "Procedural Flat Roof";

#if UNITY_EDITOR
    [HideInInspector] public Mesh _debugFlatRoofMesh;
    [HideInInspector] public Transform _debugFlatRoofTransform;
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
#endif
    }

    void GenerateFacades()
    {
        float pivotOffsetVertical = floorHeight * 0.5f; // For centered pivots

        for (int i = 0; i < vertexData.Count; i++)
        {
            GameObject sideParent = new GameObject($"Side_{i}");
            sideParent.transform.SetParent(generatedBuildingRoot.transform, false);

            Vector3 p1 = vertexData[i].position;
            Vector3 p2 = vertexData[(i + 1) % vertexData.Count].position;
            Vector3 sideVector = p2 - p1;
            float sideDistance = sideVector.magnitude;

            if (sideDistance < GeometryUtilsAdj.Epsilon) continue;

            Vector3 sideDirection = sideVector.normalized;
            Vector3 sideNormal = CalculateSideNormal(p1, p2);

            int numSegments = CalculateNumSegments(sideDistance);
            float actualSegmentWidth = CalculateSegmentWidth(sideDistance, numSegments);

            GetSidePrefabLists(i, out var currentGround, out var currentMiddle, out var currentMansard, out var currentAttic);

            for (int j = 0; j < numSegments; j++)
            {
                Vector3 segmentBaseHorizontalPos = p1 + sideDirection * (actualSegmentWidth * (j + 0.5f));
                Quaternion baseSegmentRotation = Quaternion.LookRotation(sideNormal); // Only outward rotation
                float currentBottomY = 0;

                // Ground Floor
                Vector3 groundFloorPivotPosition = segmentBaseHorizontalPos + Vector3.up * (currentBottomY + pivotOffsetVertical);
                InstantiateFacadeSegment(currentGround, groundFloorPivotPosition, baseSegmentRotation, sideParent.transform, actualSegmentWidth, false);
                currentBottomY += floorHeight;

                // Middle Floors
                for (int floor = 0; floor < middleFloors; floor++)
                {
                    Vector3 middleFloorPivotPosition = segmentBaseHorizontalPos + Vector3.up * (currentBottomY + pivotOffsetVertical);
                    InstantiateFacadeSegment(currentMiddle, middleFloorPivotPosition, baseSegmentRotation, sideParent.transform, actualSegmentWidth, false);
                    currentBottomY += floorHeight;
                }

                // Mansard Floor - Assuming prefab is pre-rotated and centered for floorHeight
                if (useMansardFloor)
                {
                    Vector3 mansardFloorPivotPosition = segmentBaseHorizontalPos + Vector3.up * (currentBottomY + pivotOffsetVertical);
                    InstantiateFacadeSegment(currentMansard, mansardFloorPivotPosition, baseSegmentRotation, sideParent.transform, actualSegmentWidth, false);
                    currentBottomY += floorHeight;
                }

                // Attic Floor - Assuming prefab is pre-rotated and centered for floorHeight
                if (useAtticFloor)
                {
                    Vector3 atticFloorPivotPosition = segmentBaseHorizontalPos + Vector3.up * (currentBottomY + pivotOffsetVertical);
                    InstantiateFacadeSegment(currentAttic, atticFloorPivotPosition, baseSegmentRotation, sideParent.transform, actualSegmentWidth, false);
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
        float pivotOffsetVertical = floorHeight * 0.5f; // Assuming corner elements also use centered pivots for floorHeight

        for (int i = 0; i < vertexData.Count; i++)
        {
            if (!vertexData[i].addCornerElement) continue;

            int prevI = (i + vertexData.Count - 1) % vertexData.Count;
            Vector3 currentPosRaw = vertexData[i].position;
            Vector3 prevPos = vertexData[prevI].position;
            Vector3 nextPos = vertexData[(i + 1) % vertexData.Count].position;

            CalculateCornerTransform(prevPos, currentPosRaw, nextPos, out Vector3 cornerBaseHorizontalPos, out Quaternion baseCornerRotation); // Only outward rotation

            float currentBottomY = 0;
            float cornerWidth = nominalFacadeWidth;

            if (hasCornerBodyPrefabs)
            {
                // Ground Floor Corner
                Vector3 groundCornerPivotPos = cornerBaseHorizontalPos + Vector3.up * (currentBottomY + pivotOffsetVertical);
                InstantiateFacadeSegment(cornerElementPrefabs, groundCornerPivotPos, baseCornerRotation, cornersParent.transform, cornerWidth, true);
                currentBottomY += floorHeight;

                // Middle Floor Corners
                for (int floor = 0; floor < middleFloors; floor++)
                {
                    Vector3 middleCornerPivotPos = cornerBaseHorizontalPos + Vector3.up * (currentBottomY + pivotOffsetVertical);
                    InstantiateFacadeSegment(cornerElementPrefabs, middleCornerPivotPos, baseCornerRotation, cornersParent.transform, cornerWidth, true);
                    currentBottomY += floorHeight;
                }

                // Mansard Floor Corner - Assuming cornerElementPrefabs for mansard are pre-rotated & centered
                if (useMansardFloor)
                {
                    Vector3 mansardCornerPivotPos = cornerBaseHorizontalPos + Vector3.up * (currentBottomY + pivotOffsetVertical);
                    // If cornerElementPrefabs list contains specific mansard/attic prefabs, they should be pre-rotated.
                    // If cornerElementPrefabs are generic straight pieces, this is fine.
                    // If you use SideStyleSO for corners, that would give more control. For now, assume generic or pre-rotated.
                    InstantiateFacadeSegment(cornerElementPrefabs, mansardCornerPivotPos, baseCornerRotation, cornersParent.transform, cornerWidth, true);
                    currentBottomY += floorHeight;
                }

                // Attic Floor Corner - Assuming cornerElementPrefabs for attic are pre-rotated & centered
                if (useAtticFloor)
                {
                    Vector3 atticCornerPivotPos = cornerBaseHorizontalPos + Vector3.up * (currentBottomY + pivotOffsetVertical);
                    InstantiateFacadeSegment(cornerElementPrefabs, atticCornerPivotPos, baseCornerRotation, cornersParent.transform, cornerWidth, true);
                    currentBottomY += floorHeight;
                }
            }

            if (hasCornerCapPrefabs)
            {
                Vector3 capPosition = cornerBaseHorizontalPos + Vector3.up * currentBottomY;
                InstantiateFacadeSegment(cornerCapPrefabs, capPosition, baseCornerRotation, cornersParent.transform, cornerWidth, true);
            }
        }
    }

    void GenerateRoof()
    {
        GenerateRoofMesh_Flat();
    }

    void GenerateRoofMesh_Flat()
    {
        float totalWallTopHeight = CalculateTotalWallTopHeight();
        List<Vector3> roofPerimeter = CalculateFlatRoofPerimeterVertices(totalWallTopHeight, flatRoofEdgeOffset); // Renamed back

        if (roofPerimeter == null || roofPerimeter.Count < 3)
        {
            Debug.LogWarning("Cannot generate flat roof: Less than 3 perimeter vertices.");
            return;
        }

        if (!GeometryUtilsAdj.TriangulatePolygonEarClipping(roofPerimeter, out List<int> meshTriangles))
        {
            Debug.LogError("Flat Roof triangulation failed.");
#if UNITY_EDITOR
            _debugFlatRoofMesh = null;
            _debugFlatRoofTransform = null;
#endif
            return;
        }

        List<Vector3> meshVertices = roofPerimeter;
        List<Vector2> meshUVs = new List<Vector2>(meshVertices.Count);
        foreach (Vector3 v in meshVertices)
        {
            meshUVs.Add(new Vector2(v.x * roofUvScale, v.z * roofUvScale));
        }

        GameObject roofObject = CreateMeshObject(meshVertices, meshTriangles, meshUVs, roofMaterial, "FlatRoofMesh", ROOF_FLAT_NAME, generatedBuildingRoot.transform);

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

    // Using your robust method for calculating roof perimeter vertices
    List<Vector3> CalculateFlatRoofPerimeterVertices(float roofYPosition, float edgeOffset)
    {
        List<Vector3> vertices = new List<Vector3>();
        if (vertexData.Count < 3) return vertices;

        for (int i = 0; i < vertexData.Count; i++)
        {
            int prevI = (i + vertexData.Count - 1) % vertexData.Count;
            Vector3 p1_base = vertexData[prevI].position;
            Vector3 p2_base = vertexData[i].position;
            Vector3 p3_base = vertexData[(i + 1) % vertexData.Count].position;

            Vector3 sideDirPrev_XZ = (new Vector3(p2_base.x, 0, p2_base.z) - new Vector3(p1_base.x, 0, p1_base.z)).normalized;
            Vector3 sideDirNext_XZ = (new Vector3(p3_base.x, 0, p3_base.z) - new Vector3(p2_base.x, 0, p2_base.z)).normalized;

            Vector3 normalPrev_XZ = CalculateSideNormal(p1_base, p2_base); // Should already be XZ if p1/p2 Y is 0
            normalPrev_XZ.y = 0; normalPrev_XZ.Normalize();
            Vector3 normalNext_XZ = CalculateSideNormal(p2_base, p3_base); // Should already be XZ
            normalNext_XZ.y = 0; normalNext_XZ.Normalize();


            Vector3 vertexPosXZ_Calculated;

            if (Mathf.Abs(edgeOffset) > GeometryUtilsAdj.Epsilon)
            {
                Vector3 lineOriginPrev_XZ = new Vector3(p1_base.x, 0, p1_base.z) + normalPrev_XZ * edgeOffset;
                Vector3 lineOriginNext_XZ = new Vector3(p2_base.x, 0, p2_base.z) + normalNext_XZ * edgeOffset;

                if (GeometryUtilsAdj.LineLineIntersection(lineOriginPrev_XZ, sideDirPrev_XZ, lineOriginNext_XZ, sideDirNext_XZ, out vertexPosXZ_Calculated))
                {
                    // Intersection found. vertexPosXZ_Calculated is on XZ plane.
                }
                else
                {
                    Vector3 avgNormal_XZ = (normalPrev_XZ + normalNext_XZ).normalized;
                    if (avgNormal_XZ.sqrMagnitude < GeometryUtilsAdj.Epsilon * GeometryUtilsAdj.Epsilon)
                    {
                        avgNormal_XZ = new Vector3(sideDirPrev_XZ.z, 0, -sideDirPrev_XZ.x); // Perpendicular fallback
                    }
                    vertexPosXZ_Calculated = new Vector3(p2_base.x, 0, p2_base.z) + avgNormal_XZ * edgeOffset;
                }
            }
            else
            {
                vertexPosXZ_Calculated = new Vector3(p2_base.x, 0, p2_base.z);
            }
            vertices.Add(new Vector3(vertexPosXZ_Calculated.x, roofYPosition, vertexPosXZ_Calculated.z));
        }
        return vertices;
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
        if (cornerFacingDirection.sqrMagnitude < GeometryUtilsAdj.Epsilon * GeometryUtilsAdj.Epsilon)
        {
            Vector3 dir1 = (p2_current - p1_prev).normalized;
            Vector3 dir2 = (p3_next - p2_current).normalized;

            if (Vector3.Dot(dir1, dir2) < -0.99f)
            {
                cornerFacingDirection = -((p1_prev - p2_current).normalized + (p3_next - p2_current).normalized).normalized;
                if (cornerFacingDirection.sqrMagnitude < GeometryUtilsAdj.Epsilon * GeometryUtilsAdj.Epsilon)
                {
                    cornerFacingDirection = sideNormalNext;
                }
            }
            else
            {
                cornerFacingDirection = Quaternion.Euler(0, 90, 0) * dir1;
            }
        }
        cornerRot = Quaternion.LookRotation(cornerFacingDirection);

        Vector3 localOffset = Vector3.forward * cornerElementForwardOffset;
        Vector3 worldOffset = cornerRot * localOffset;
        cornerPos = p2_current + worldOffset;
    }

    float CalculateTotalWallTopHeight()
    {
        // This calculation assumes 'floorHeight' is the VERTICAL rise for each floor type.
        // The angles of mansard/attic prefabs (if any, now assumed to be pre-rotated in prefab)
        // do not change the Y-coordinate of the top of that floor's allocated vertical space.
        float height = 0;
        height += floorHeight;
        height += middleFloors * floorHeight;
        if (useMansardFloor) height += floorHeight;
        if (useAtticFloor) height += floorHeight;
        return height;
    }

    void GetSidePrefabLists(int sideIndex, out List<GameObject> ground, out List<GameObject> middle, out List<GameObject> mansard, out List<GameObject> attic)
    {
        if (sideIndex < 0 || sideIndex >= sideData.Count)
        {
            Debug.LogError($"sideIndex {sideIndex} out of bounds. Falling back to defaults.");
            ground = defaultGroundFloorPrefabs;
            middle = defaultMiddleFloorPrefabs;
            mansard = defaultMansardFloorPrefabs;
            attic = defaultAtticFloorPrefabs;
            return;
        }

        PolygonSideDataAdj currentSideSettings = sideData[sideIndex];
        if (currentSideSettings.useCustomStyle && currentSideSettings.sideStylePreset != null)
        {
            SideStyleSOAdj style = currentSideSettings.sideStylePreset;
            ground = style.groundFloorPrefabs != null && style.groundFloorPrefabs.Count > 0 ? style.groundFloorPrefabs : defaultGroundFloorPrefabs;
            middle = style.middleFloorPrefabs != null && style.middleFloorPrefabs.Count > 0 ? style.middleFloorPrefabs : defaultMiddleFloorPrefabs;
            mansard = style.mansardFloorPrefabs != null && style.mansardFloorPrefabs.Count > 0 ? style.mansardFloorPrefabs : defaultMansardFloorPrefabs;
            attic = style.atticFloorPrefabs != null && style.atticFloorPrefabs.Count > 0 ? style.atticFloorPrefabs : defaultAtticFloorPrefabs;
        }
        else
        {
            ground = defaultGroundFloorPrefabs;
            middle = defaultMiddleFloorPrefabs;
            mansard = defaultMansardFloorPrefabs;
            attic = defaultAtticFloorPrefabs;
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

        if (!isCorner && scaleFacadesToFitSide && nominalFacadeWidth > GeometryUtilsAdj.Epsilon && Mathf.Abs(segmentWidth - nominalFacadeWidth) > GeometryUtilsAdj.Epsilon)
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

    int CalculateNumSegments(float sideDistance)
    {
        if (nominalFacadeWidth <= GeometryUtilsAdj.Epsilon) return Mathf.Max(1, minSideLengthUnits);
        int num;
        if (scaleFacadesToFitSide)
        {
            num = Mathf.Max(minSideLengthUnits, Mathf.RoundToInt(sideDistance / nominalFacadeWidth));
        }
        else
        {
            num = Mathf.Max(minSideLengthUnits, Mathf.FloorToInt(sideDistance / nominalFacadeWidth));
        }
        return Mathf.Max(1, num);
    }

    float CalculateSegmentWidth(float sideDistance, int numSegments)
    {
        if (numSegments == 0) return nominalFacadeWidth;
        return scaleFacadesToFitSide ? (sideDistance / numSegments) : nominalFacadeWidth;
    }

    Vector3 CalculateSideNormal(Vector3 p1, Vector3 p2)
    {
        Vector3 sideDirection = (p2 - p1).normalized;
        // Ensure Y is 0 for sideDirection if p1/p2 might have Y components (though vertexData should be Y=0)
        sideDirection.y = 0;
        sideDirection.Normalize();

        if (sideDirection.sqrMagnitude < GeometryUtilsAdj.Epsilon * GeometryUtilsAdj.Epsilon) return Vector3.forward;

        Vector3 initialNormal = Vector3.Cross(sideDirection, Vector3.up).normalized; // This will be on XZ plane
        float signedArea = CalculateSignedArea();

        if (signedArea > GeometryUtilsAdj.Epsilon) return -initialNormal;
        else return initialNormal;
    }

    public void SynchronizeSideData()
    {
        if (vertexData == null) vertexData = new List<PolygonVertexDataAdj>();
        if (sideData == null) sideData = new List<PolygonSideDataAdj>();
        int requiredCount = vertexData.Count;
        while (sideData.Count < requiredCount) sideData.Add(new PolygonSideDataAdj());
        while (sideData.Count > requiredCount && sideData.Count > 0) sideData.RemoveAt(sideData.Count - 1);
    }

    void OnValidate()
    {
        SynchronizeSideData();
        middleFloors = Mathf.Max(0, middleFloors);
        floorHeight = Mathf.Max(0.1f, floorHeight);
        nominalFacadeWidth = Mathf.Max(0.1f, nominalFacadeWidth);
        minSideLengthUnits = Mathf.Max(0, minSideLengthUnits);
        // Removed angle clamping as they are no longer used by the script
    }

    public Vector3 SnapVertexPosition(Vector3 vertexPos)
    {
        if (vertexSnapSize <= GeometryUtilsAdj.Epsilon) return new Vector3(vertexPos.x, 0f, vertexPos.z);
        return new Vector3(
            Mathf.Round(vertexPos.x / vertexSnapSize) * vertexSnapSize,
            0f,
            Mathf.Round(vertexPos.z / vertexSnapSize) * vertexSnapSize
        );
    }
}