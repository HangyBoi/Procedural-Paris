using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class MansardCornerSet
{
    [Tooltip("The approximate interior angle (in degrees) of the polygon corner this prefab set is designed for. E.g., 90 for a right angle between two walls.")]
    public float targetCornerAngle = 90.0f;
    [Tooltip("Tolerance for matching this angle (in degrees). E.g., if 15, it matches angles from target-15 to target+15.")]
    public float angleTolerance = 15.0f;

    [Tooltip("The main prefab for this corner. Assumed to be a complete corner assembly (e.g., pre-assembled front, left, right parts if applicable). Its Z-axis should face along the corner's bisector if rotated by baseCornerRotation. Pivot ideally at its base and horizontally centered for the intended floorHeight slot.")]
    public GameObject cornerAssemblyPrefab;

    [Tooltip("How much this corner assembly extends from the mathematical polygon vertex point ALONG the wall towards the NEXT vertex. This side's regular facades will start after this distance.")]
    public float coverageAlongNextSide; // e.g. how much it covers along side V_current -> V_next

    [Tooltip("How much this corner assembly extends from the mathematical polygon vertex point ALONG the wall towards the PREVIOUS vertex. The previous side's regular facades will end before this distance from V_current.")]
    public float coverageAlongPrevSide; // e.g. how much it covers along side V_prev -> V_current
}

[RequireComponent(typeof(MeshFilter))]
public class PolygonBuildingGeneratorAdj : MonoBehaviour
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

    [Header("Building Style")]
    [Tooltip("Assign a Building Style ScriptableObject to define default prefabs and corner configurations.")]
    public BuildingStyleSO buildingStyle;

    [Header("Building Settings")]
    public int middleFloors = 3;
    public float floorHeight = 10.0f;
    public bool useMansardFloor = true;
    public float mansardAngleFromVerticalDegrees = 10.0f;
    public bool useAtticFloor = true;
    public float atticAngleFromVerticalDegrees = 50.0f;

    [Header("Facade Placement")]
    public bool scaleFacadesToFitSide = true;
    public float nominalFacadeWidth = 1.0f;

    [Header("Corner Elements")]
    public bool useCornerCaps = true;
    public float cornerElementForwardOffset = 0.0f;

    [Header("Mansard Roof Dedicated Corners")]
    public bool useDedicatedMansardCorners = true;
    private Dictionary<int, MansardCornerSet> _activeMansardCorners;

    [Header("Roof Settings")]
    public float flatRoofEdgeOffset = 0.0f;
    public Material roofMaterial;
    public float roofUvScale = 1.0f;

    private GameObject generatedBuildingRoot;
    private const string ROOT_NAME = "Generated Building";
    private const string CORNERS_NAME = "Corner Elements";
    private const string ROOF_FLAT_NAME = "Procedural Flat Roof";
    private const string DEDICATED_WALL_CORNERS_NAME = "Dedicated Wall Corners";

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
        if (buildingStyle == null)
        {
            Debug.LogWarning("Building Style SO not assigned. Facades and dedicated corners might not generate as expected.", this);
        }

        generatedBuildingRoot = new GameObject(ROOT_NAME);
        generatedBuildingRoot.transform.SetParent(this.transform, false);

        _activeMansardCorners = new Dictionary<int, MansardCornerSet>();

        PrecomputeCornerData();
        GenerateFacades();
        GenerateDedicatedWallCorners();
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
        _activeMansardCorners?.Clear();

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

            Vector3 p1_vertex = vertexData[i].position;
            int nextVertexIndex = (i + 1) % vertexData.Count;
            Vector3 p2_vertex = vertexData[nextVertexIndex].position;

            Vector3 sideVector = p2_vertex - p1_vertex;
            float originalSideDistance = sideVector.magnitude;

            if (originalSideDistance < GeometryUtils.Epsilon) continue;

            Vector3 sideDirection = sideVector.normalized;
            Vector3 sideNormal = CalculateSideNormal(p1_vertex, p2_vertex);

            GetSidePrefabLists(i, out var currentGround, out var currentMiddle, out var currentMansard, out var currentAttic);

            // --- Adjustments for dedicated corners ---
            float mansardStartCoverage = 0f;
            float mansardEndCoverage = 0f;

            MansardCornerSet startCornerSet = null;
            bool hasStartMansardCorner = useMansardFloor && useDedicatedMansardCorners && _activeMansardCorners.TryGetValue(i, out startCornerSet);
            if (hasStartMansardCorner)
            {
                if (startCornerSet != null)
                {
                    mansardStartCoverage = startCornerSet.coverageAlongNextSide;
                }
            }

            MansardCornerSet endCornerSet = null;
            bool hasEndMansardCorner = useMansardFloor && useDedicatedMansardCorners && _activeMansardCorners.TryGetValue(nextVertexIndex, out endCornerSet);
            if (hasEndMansardCorner)
            {
                if (endCornerSet != null)
                {
                    mansardEndCoverage = endCornerSet.coverageAlongPrevSide;
                }
            }

            float currentBottomY = 0;

            // --- Ground Floor (No dedicated corner adjustments here, but could be added) ---
            InstantiateSideSegments(p1_vertex, sideDirection, sideNormal, originalSideDistance, 0f, 0f,
                                    currentGround, sideParent.transform, pivotOffsetVertical, currentBottomY, false);
            currentBottomY += floorHeight;

            // --- Middle Floors (No dedicated corner adjustments here) ---
            for (int floor = 0; floor < middleFloors; floor++)
            {
                InstantiateSideSegments(p1_vertex, sideDirection, sideNormal, originalSideDistance, 0f, 0f,
                                        currentMiddle, sideParent.transform, pivotOffsetVertical, currentBottomY, false);
                currentBottomY += floorHeight;
            }

            // --- Mansard Floor (With dedicated corner adjustments) ---
            if (useMansardFloor)
            {
                InstantiateSideSegments(p1_vertex, sideDirection, sideNormal, originalSideDistance,
                                        mansardStartCoverage, mansardEndCoverage,
                                        currentMansard, sideParent.transform, pivotOffsetVertical, currentBottomY, false);
                currentBottomY += floorHeight;
            }

            // --- Attic Floor (Placeholder for similar adjustments) ---
            if (useAtticFloor)
            {
                // TODO: Add atticStartCoverage, atticEndCoverage logic if using dedicated attic corners
                InstantiateSideSegments(p1_vertex, sideDirection, sideNormal, originalSideDistance, 0f, 0f, // Update coverage later
                                        currentAttic, sideParent.transform, pivotOffsetVertical, currentBottomY, false);
                // currentBottomY += floorHeight; // Only if attic contributes to overall height calculation this way
            }
        }
    }

    // New helper method to instantiate segments for a side, considering coverage
    void InstantiateSideSegments(Vector3 sideStartVertex, Vector3 sideDirection, Vector3 sideNormal,
                                 float originalSideDistance, float startCoverage, float endCoverage,
                                 List<GameObject> prefabs, Transform parent,
                                 float pivotOffsetY, float baseLevelY, bool isCornerElement) // isCornerElement not really used here, but kept for signature similarity
    {
        if (prefabs == null || prefabs.Count == 0) return;

        Vector3 effectiveSideStartPos = sideStartVertex + sideDirection * startCoverage;
        float effectiveSideDistance = originalSideDistance - startCoverage - endCoverage;

        if (effectiveSideDistance < nominalFacadeWidth * 0.1f && effectiveSideDistance < minSideLengthUnits * nominalFacadeWidth) // If too short, skip
        {
            if (effectiveSideDistance < 0) Debug.LogWarning($"Side {parent.name} has negative effective length ({effectiveSideDistance.ToString("F2")}) after corner coverage. Check coverage values. Original: {originalSideDistance}, StartCov: {startCoverage}, EndCov: {endCoverage}");
            return;
        }

        int numSegments = CalculateNumSegments(effectiveSideDistance);
        if (numSegments <= 0 && effectiveSideDistance > GeometryUtils.Epsilon) numSegments = 1; // Ensure at least one segment if there's some space
        else if (numSegments <= 0) return;


        float actualSegmentWidth = CalculateSegmentWidth(effectiveSideDistance, numSegments);
        Quaternion baseSegmentRotation = Quaternion.LookRotation(sideNormal);

        for (int j = 0; j < numSegments; j++)
        {
            Vector3 segmentBaseHorizontalPos = effectiveSideStartPos + sideDirection * (actualSegmentWidth * (j + 0.5f));
            Vector3 segmentPivotPosition = segmentBaseHorizontalPos + Vector3.up * (baseLevelY + pivotOffsetY);
            InstantiateFacadeSegment(prefabs, segmentPivotPosition, baseSegmentRotation, parent, actualSegmentWidth, false); // false for isCorner
        }
    }

    void PrecomputeCornerData()
    {
        if (vertexData.Count < 3) return;

        for (int i = 0; i < vertexData.Count; i++)
        {

            int prevI = (i + vertexData.Count - 1) % vertexData.Count;
            Vector3 currentPos = vertexData[i].position;
            Vector3 prevPos = vertexData[prevI].position;
            Vector3 nextPos = vertexData[(i + 1) % vertexData.Count].position;

            float interiorAngle = CalculateInteriorCornerAngle(prevPos, currentPos, nextPos);

            if (useDedicatedMansardCorners && buildingStyle.mansardCornerSets.Count > 0)
            {
                MansardCornerSet matchedSet = FindMatchingMansardCornerSet(buildingStyle.mansardCornerSets, interiorAngle);
                if (matchedSet != null)
                {
                    _activeMansardCorners[i] = matchedSet;
                }
            }
            // Add similar logic for _activeAtticCorners here if/when implemented
        }
    }

    MansardCornerSet FindMatchingMansardCornerSet(List<MansardCornerSet> sets, float angle)
    {
        MansardCornerSet bestMatch = null;
        float minDiff = float.MaxValue;

        foreach (var set in sets)
        {
            if (set.cornerAssemblyPrefab == null) continue; // Skip if no prefab assigned

            float diff = Mathf.Abs(angle - set.targetCornerAngle);
            if (diff <= set.angleTolerance)
            {
                if (diff < minDiff) // Prefer closer matches
                {
                    minDiff = diff;
                    bestMatch = set;
                }
            }
        }
        return bestMatch;
    }

    public float CalculateInteriorCornerAngle(Vector3 pPrev, Vector3 pCurr, Vector3 pNext)
    {
        // Ensure we are working on the XZ plane
        Vector2 prev = new Vector2(pPrev.x, pPrev.z);
        Vector2 curr = new Vector2(pCurr.x, pCurr.z);
        Vector2 next = new Vector2(pNext.x, pNext.z);

        Vector2 v1 = (prev - curr).normalized;
        Vector2 v2 = (next - curr).normalized;

        if (v1.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon ||
            v2.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon)
        {
            return 180f;
        }

        float angle = Vector2.Angle(v1, v2);

        float crossProductSign = (v1.x * v2.y) - (v1.y * v2.x);

        float polygonWinding = CalculateSignedArea();

        // If polygon is CCW (signedArea > 0):
        //  - A positive crossProductSign (v1 to v2 is a "left" turn in 2D screen space if Y is up) means it's concave.
        //  - A negative crossProductSign (v1 to v2 is a "right" turn) means it's convex.
        // If polygon is CW (signedArea < 0):
        //  - The interpretation of convexity/concavity flips.

        bool isConcave = false;
        if (Mathf.Abs(polygonWinding) > GeometryUtils.Epsilon)
        {
            if (polygonWinding > 0) // CCW polygon
            {
                if (crossProductSign > GeometryUtils.Epsilon) isConcave = true;
            }
            else // CW polygon
            {
                if (crossProductSign < -GeometryUtils.Epsilon) isConcave = true;
            }
        }

        if (isConcave)
        {
            return 360f - angle;
        }
        else
        {

            return angle;
        }
    }

    void GenerateDedicatedWallCorners()
    {
        if (!useDedicatedMansardCorners /* && !useDedicatedAtticCorners (add later) */)
        {
            // If no dedicated corner types are enabled, no need to proceed.
            return;
        }
        if (_activeMansardCorners.Count == 0 /* && _activeAtticCorners.Count == 0 (add later) */)
        {
            // If no corners matched any prefabs during PrecomputeCornerData
            return;
        }


        GameObject dedicatedCornersParent = new GameObject(DEDICATED_WALL_CORNERS_NAME);
        dedicatedCornersParent.transform.SetParent(generatedBuildingRoot.transform, false);

        float pivotOffsetVertical = floorHeight * 0.5f; // Assuming dedicated corners also have centered pivots for their floorHeight slot

        for (int i = 0; i < vertexData.Count; i++)
        {
            // We iterate through ALL vertices, as walls meet at every vertex.
            int prevI = (i + vertexData.Count - 1) % vertexData.Count;
            Vector3 currentPosRaw = vertexData[i].position;
            Vector3 prevPos = vertexData[prevI].position;
            Vector3 nextPos = vertexData[(i + 1) % vertexData.Count].position;

            CalculateCornerTransform(prevPos, currentPosRaw, nextPos, out Vector3 cornerBaseHorizontalPos, out Quaternion baseCornerRotation);

            // --- Mansard Dedicated Wall Corner ---
            if (useMansardFloor && useDedicatedMansardCorners)
            {
                if (_activeMansardCorners.TryGetValue(i, out MansardCornerSet selectedSet))
                {
                    if (selectedSet.cornerAssemblyPrefab != null)
                    {
                        // Calculate Y position for the base of the Mansard floor level
                        float mansardFloorSlotBaseY = floorHeight; // Ground floor height
                        mansardFloorSlotBaseY += middleFloors * floorHeight; // Middle floors height

                        Vector3 mansardCornerPivotPos = cornerBaseHorizontalPos + Vector3.up * (mansardFloorSlotBaseY + pivotOffsetVertical);

                        GameObject cornerAssembly = Instantiate(selectedSet.cornerAssemblyPrefab, dedicatedCornersParent.transform);
                        cornerAssembly.name = $"Mansard_WallCorner_{i}_{selectedSet.cornerAssemblyPrefab.name}";
                        cornerAssembly.transform.localPosition = mansardCornerPivotPos; // Use localPosition
                        cornerAssembly.transform.localRotation = baseCornerRotation;   // Use localRotation
                        // Scaling is assumed to be part of the prefab for dedicated corners.
                    }
                }
            }

            // --- Attic Dedicated Wall Corner (Placeholder for future) ---
            // if (useAtticFloor && useDedicatedAtticCorners)
            // {
            //     if (_activeAtticCorners.TryGetValue(i, out AtticCornerSet selectedAtticSet)) // Assuming an AtticCornerSet type
            //     {
            //         if (selectedAtticSet.cornerAssemblyPrefab != null)
            //         {
            //             float atticFloorSlotBaseY = floorHeight; // Ground
            //             atticFloorSlotBaseY += middleFloors * floorHeight; // Middles
            //             if (useMansardFloor) atticFloorSlotBaseY += floorHeight; // Mansard
            //
            //             Vector3 atticCornerPivotPos = cornerBaseHorizontalPos + Vector3.up * (atticFloorSlotBaseY + pivotOffsetVertical);
            //             // Instantiate attic corner assembly
            //         }
            //     }
            // }
        }
    }


    void GenerateCornerElements()
    {
        bool hasChimneyBodyPrefabs = buildingStyle != null && buildingStyle.defaultChimneyBodyPrefabs != null && buildingStyle.defaultChimneyBodyPrefabs.Count > 0;
        bool hasChimneyCapPrefabs = useCornerCaps && buildingStyle != null && buildingStyle.defaultChimneyCapPrefabs != null && buildingStyle.defaultChimneyCapPrefabs.Count > 0;

        if (!hasChimneyBodyPrefabs && !hasChimneyCapPrefabs)
        {
            Debug.Log("No generic corner body or cap prefabs assigned; skipping chimney generation.");
            return;
        }

        GameObject chimneyParent = new GameObject(CORNERS_NAME);
        chimneyParent.transform.SetParent(generatedBuildingRoot.transform, false);

        float pivotOffsetVertical = floorHeight * 0.5f;

        for (int i = 0; i < vertexData.Count; i++)
        {
            // ONLY build a chimney stack if this vertex is marked for it.
            if (!vertexData[i].addCornerElement) continue;

            int prevI = (i + vertexData.Count - 1) % vertexData.Count;
            Vector3 currentPosRaw = vertexData[i].position;
            Vector3 prevPos = vertexData[prevI].position;
            Vector3 nextPos = vertexData[(i + 1) % vertexData.Count].position;

            CalculateCornerTransform(prevPos, currentPosRaw, nextPos, out Vector3 cornerBaseHorizontalPos, out Quaternion baseCornerRotation);
            float cornerWidth = nominalFacadeWidth; // Default width for chimney elements

            float nominalCurrentElementBaseY = 0; // Reset Y for each chimney stack

            // Ground Floor Chimney Segment
            if (hasChimneyBodyPrefabs)
            {
                Vector3 groundCornerPivotPos = cornerBaseHorizontalPos + Vector3.up * (nominalCurrentElementBaseY + pivotOffsetVertical);
                InstantiateFacadeSegment(buildingStyle.defaultChimneyBodyPrefabs, groundCornerPivotPos, baseCornerRotation, chimneyParent.transform, cornerWidth, true);
            }
            nominalCurrentElementBaseY += floorHeight;

            // Middle Floor Chimney Segments
            if (hasChimneyBodyPrefabs)
            {
                for (int floor = 0; floor < middleFloors; floor++)
                {
                    Vector3 middleCornerPivotPos = cornerBaseHorizontalPos + Vector3.up * (nominalCurrentElementBaseY + pivotOffsetVertical);
                    InstantiateFacadeSegment(buildingStyle.defaultChimneyBodyPrefabs, middleCornerPivotPos, baseCornerRotation, chimneyParent.transform, cornerWidth, true);
                    nominalCurrentElementBaseY += floorHeight;
                }
            }
            else
            {
                nominalCurrentElementBaseY += floorHeight * middleFloors; // Still advance Y if no prefabs
            }

            // Mansard Floor Chimney Segment (Uses generic cornerElementPrefabs)
            if (useMansardFloor)
            {
                if (hasChimneyBodyPrefabs)
                {
                    Vector3 mansardCornerPivotPos = cornerBaseHorizontalPos + Vector3.up * (nominalCurrentElementBaseY + pivotOffsetVertical);
                    InstantiateFacadeSegment(buildingStyle.defaultChimneyBodyPrefabs, mansardCornerPivotPos, baseCornerRotation, chimneyParent.transform, cornerWidth, true);
                }
                nominalCurrentElementBaseY += floorHeight;
            }

            // Attic Floor Chimney Segment (Uses generic cornerElementPrefabs)
            if (useAtticFloor)
            {
                if (hasChimneyBodyPrefabs)
                {
                    Vector3 atticCornerPivotPos = cornerBaseHorizontalPos + Vector3.up * (nominalCurrentElementBaseY + pivotOffsetVertical);
                    //InstantiateFacadeSegment(buildingStyle.defaultChimneyBodyPrefabs, groundCornerPivotPos, baseCornerRotation, chimneyParent.transform, cornerWidth, true);
                }
                //nominalCurrentElementBaseY += floorHeight; // Advance Y for cap placement
            }

            // Chimney Cap
            if (hasChimneyCapPrefabs)
            {
                Vector3 capPosition = cornerBaseHorizontalPos + Vector3.up * nominalCurrentElementBaseY;
                InstantiateFacadeSegment(buildingStyle.defaultChimneyCapPrefabs, capPosition, baseCornerRotation, chimneyParent.transform, cornerWidth, true);
            }
        }
    }

    void InstantiateFacadeSegment(List<GameObject> prefabList, Vector3 localPosition, Quaternion localRotation, Transform parent, float segmentWidth, bool isCorner)
    {
        if (prefabList == null || prefabList.Count == 0) return;
        int randomIndex = Random.Range(0, prefabList.Count);
        GameObject prefab = prefabList[randomIndex];
        if (prefab == null) return;

        GameObject instance = Instantiate(prefab, parent);
        instance.transform.localPosition = localPosition; // Use localPosition
        instance.transform.localRotation = localRotation; // Use localRotation

        if (!isCorner && scaleFacadesToFitSide && nominalFacadeWidth > GeometryUtils.Epsilon && Mathf.Abs(segmentWidth - nominalFacadeWidth) > GeometryUtils.Epsilon)
        {
            Vector3 localScale = instance.transform.localScale;
            float scaleFactor = segmentWidth / nominalFacadeWidth;
            instance.transform.localScale = new Vector3(localScale.x * scaleFactor, localScale.y, localScale.z);
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

        if (!GeometryUtils.TriangulatePolygonEarClipping(roofPerimeter, out List<int> meshTriangles))
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

            if (Mathf.Abs(edgeOffset) > GeometryUtils.Epsilon)
            {
                Vector3 lineOriginPrev_XZ = new Vector3(p1_base.x, 0, p1_base.z) + normalPrev_XZ * edgeOffset;
                Vector3 lineOriginNext_XZ = new Vector3(p2_base.x, 0, p2_base.z) + normalNext_XZ * edgeOffset;

                if (GeometryUtils.LineLineIntersection(lineOriginPrev_XZ, sideDirPrev_XZ, lineOriginNext_XZ, sideDirNext_XZ, out vertexPosXZ_Calculated))
                {
                    // Intersection found. vertexPosXZ_Calculated is on XZ plane.
                }
                else
                {
                    Vector3 avgNormal_XZ = (normalPrev_XZ + normalNext_XZ).normalized;
                    if (avgNormal_XZ.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon)
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
        if (cornerFacingDirection.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon)
        {
            Vector3 dir1 = (p2_current - p1_prev).normalized;
            Vector3 dir2 = (p3_next - p2_current).normalized;

            if (Vector3.Dot(dir1, dir2) < -0.99f)
            {
                cornerFacingDirection = -((p1_prev - p2_current).normalized + (p3_next - p2_current).normalized).normalized;
                if (cornerFacingDirection.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon)
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
        float height = 0;
        // Ground floor: vertical height is floorHeight
        height += floorHeight;
        // Middle floors: vertical height is floorHeight per floor
        height += middleFloors * floorHeight;

        // Mansard floor: actual vertical rise depends on angle
        if (useMansardFloor)
        {
            // Calculate actual vertical rise for mansard
            float mansardActualVerticalRise = floorHeight * Mathf.Cos(mansardAngleFromVerticalDegrees * Mathf.Deg2Rad);
            height += mansardActualVerticalRise;
        }
        // Attic floor: actual vertical rise depends on angle
        if (useAtticFloor)
        {
            // Calculate actual vertical rise for attic
            float atticActualVerticalRise = floorHeight * Mathf.Cos(atticAngleFromVerticalDegrees * Mathf.Deg2Rad);
            height += atticActualVerticalRise;
        }
        return height;
    }

    void GetSidePrefabLists(int sideIndex, out List<GameObject> ground, out List<GameObject> middle, out List<GameObject> mansard, out List<GameObject> attic)
    {
        // Initialize with empty lists as a true default if nothing is found
        List<GameObject> styleGround = null;
        List<GameObject> styleMiddle = null;
        List<GameObject> styleMansard = null;
        List<GameObject> styleAttic = null;

        // 1. Try Side-specific SideStyleSO
        if (sideIndex >= 0 && sideIndex < sideData.Count)
        {
            PolygonSideData currentSideSettings = sideData[sideIndex];
            if (currentSideSettings.useCustomStyle && currentSideSettings.sideStylePreset != null)
            {
                SideStyleSO sidePreset = currentSideSettings.sideStylePreset;
                if (sidePreset.groundFloorPrefabs != null && sidePreset.groundFloorPrefabs.Count > 0) styleGround = sidePreset.groundFloorPrefabs;
                if (sidePreset.middleFloorPrefabs != null && sidePreset.middleFloorPrefabs.Count > 0) styleMiddle = sidePreset.middleFloorPrefabs;
                if (sidePreset.mansardFloorPrefabs != null && sidePreset.mansardFloorPrefabs.Count > 0) styleMansard = sidePreset.mansardFloorPrefabs;
                if (sidePreset.atticFloorPrefabs != null && sidePreset.atticFloorPrefabs.Count > 0) styleAttic = sidePreset.atticFloorPrefabs;
            }
        }

        // 2. Fallback to BuildingStyleSO if side-specific lists were not populated or not used
        if (buildingStyle != null)
        {
            if (styleGround == null && buildingStyle.defaultGroundFloorPrefabs != null && buildingStyle.defaultGroundFloorPrefabs.Count > 0) styleGround = buildingStyle.defaultGroundFloorPrefabs;
            if (styleMiddle == null && buildingStyle.defaultMiddleFloorPrefabs != null && buildingStyle.defaultMiddleFloorPrefabs.Count > 0) styleMiddle = buildingStyle.defaultMiddleFloorPrefabs;
            if (styleMansard == null && buildingStyle.defaultMansardFloorPrefabs != null && buildingStyle.defaultMansardFloorPrefabs.Count > 0) styleMansard = buildingStyle.defaultMansardFloorPrefabs;
            if (styleAttic == null && buildingStyle.defaultAtticFloorPrefabs != null && buildingStyle.defaultAtticFloorPrefabs.Count > 0) styleAttic = buildingStyle.defaultAtticFloorPrefabs;
        }

        // Assign the determined lists (or null if none found, which InstantiateFacadeSegment handles)
        ground = styleGround;
        middle = styleMiddle;
        mansard = styleMansard;
        attic = styleAttic;

        // Optional: Log if any list ends up null or empty
        if ((ground == null || ground.Count == 0) && (styleGround == null && (buildingStyle?.defaultGroundFloorPrefabs == null || buildingStyle.defaultGroundFloorPrefabs.Count == 0)))
            Debug.LogWarning($"Side {sideIndex}: No Ground floor prefabs found from SideStyleSO or BuildingStyleSO.");
        // Similar warnings for other floor types if desired
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
        if (nominalFacadeWidth <= GeometryUtils.Epsilon) return Mathf.Max(1, minSideLengthUnits);
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

        if (sideDirection.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon) return Vector3.forward;

        Vector3 initialNormal = Vector3.Cross(sideDirection, Vector3.up).normalized; // This will be on XZ plane
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
        middleFloors = Mathf.Max(0, middleFloors);
        floorHeight = Mathf.Max(0.1f, floorHeight);
        nominalFacadeWidth = Mathf.Max(0.1f, nominalFacadeWidth);
        minSideLengthUnits = Mathf.Max(0, minSideLengthUnits);
        // Removed angle clamping as they are no longer used by the script
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