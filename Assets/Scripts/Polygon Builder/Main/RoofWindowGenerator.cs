using UnityEngine;
using System.Collections.Generic;

public class RoofWindowGenerator
{
    private readonly PolygonBuildingGeneratorMain _settings;
    private readonly List<PolygonVertexData> _vertexData;
    private readonly List<PolygonSideData> _sideData;
    private readonly BuildingStyleSO _buildingStyle;

    private const float ACUTE_ANGLE_THRESHOLD_DEGREES = 90.0f;

    public RoofWindowGenerator(PolygonBuildingGeneratorMain settings, List<PolygonVertexData> vertexData, List<PolygonSideData> sideData, BuildingStyleSO buildingStyle)
    {
        _settings = settings;
        _vertexData = vertexData;
        _sideData = sideData;
        _buildingStyle = buildingStyle;
    }

    public void GenerateAllWindows(Transform roofWindowsRoot, GeneratedRoofObjects generatedRoofs)
    {
        if (!_settings.placeMansardWindows && !_settings.placeAtticWindows) return;
        if (_vertexData.Count < 3) return;

        // --- Mansard Window Setup ---
        GameObject mansardWindowsParent = null;
        bool canGenerateMansardWindows = _settings.placeMansardWindows &&
                                         _settings.useMansardFloor &&
                                         generatedRoofs.MansardRoofObject != null &&
                                         _settings.mansardRiseHeight >= _settings.minMansardRiseForWindows &&
                                         _settings.mansardSlopeHorizontalDistance <= _settings.maxMansardHDistForWindows;

        if (canGenerateMansardWindows)
        {
            mansardWindowsParent = new GameObject("Mansard Windows");
            mansardWindowsParent.transform.SetParent(roofWindowsRoot, false);
        }
        else if (_settings.placeMansardWindows && _settings.useMansardFloor) // Log if general conditions met but thresholds failed
        {
            if (generatedRoofs.MansardRoofObject == null)
                Debug.LogWarning("RoofWindowGenerator: Mansard windows enabled, but Mansard Roof Object is null.");
            else if (_settings.mansardRiseHeight < _settings.minMansardRiseForWindows)
                Debug.Log($"RoofWindowGenerator: Mansard windows skipped. Mansard Rise Height ({_settings.mansardRiseHeight}) is less than MinRiseForWindows ({_settings.minMansardRiseForWindows}).");
            else if (_settings.mansardSlopeHorizontalDistance < _settings.maxMansardHDistForWindows)
                Debug.Log($"RoofWindowGenerator: Mansard windows skipped. Mansard Slope Horizontal Distance ({_settings.mansardSlopeHorizontalDistance}) is less than MinHDistForWindows ({_settings.maxMansardHDistForWindows}).");
        }

        // --- Attic Window Setup ---
        GameObject atticWindowsParent = null;
        bool canGenerateAtticWindows = _settings.placeAtticWindows &&
                                       _settings.useAtticFloor &&
                                       generatedRoofs.AtticRoofObject != null &&
                                       _settings.atticRiseHeight >= _settings.minAtticRiseForWindows &&
                                       _settings.atticSlopeHorizontalDistance <= _settings.maxAtticHDistForWindows;

        if (canGenerateAtticWindows)
        {
            atticWindowsParent = new GameObject("Attic Windows");
            atticWindowsParent.transform.SetParent(roofWindowsRoot, false);
        }
        else if (_settings.placeAtticWindows && _settings.useAtticFloor) // Log if general conditions met but thresholds failed
        {
            if (generatedRoofs.AtticRoofObject == null)
                Debug.LogWarning("RoofWindowGenerator: Attic windows enabled, but Attic Roof Object is null.");
            else if (_settings.atticRiseHeight < _settings.minAtticRiseForWindows)
                Debug.Log($"RoofWindowGenerator: Attic windows skipped. Attic Rise Height ({_settings.atticRiseHeight}) is less than MinRiseForWindows ({_settings.minAtticRiseForWindows}).");
            else if (_settings.atticSlopeHorizontalDistance < _settings.maxAtticHDistForWindows)
                Debug.Log($"RoofWindowGenerator: Attic windows skipped. Attic Slope Horizontal Distance ({_settings.atticSlopeHorizontalDistance}) is less than MinHDistForWindows ({_settings.maxAtticHDistForWindows}).");
        }


        int N = _vertexData.Count;

        for (int sideIdx = 0; sideIdx < N; sideIdx++)
        {
            Vector3 p1_local = _vertexData[sideIdx].position;
            Vector3 p2_local = _vertexData[(sideIdx + 1) % N].position;
            float sideDistance = Vector3.Distance(p1_local, p2_local);

            if (sideDistance < GeometryUtils.Epsilon) continue;

            int numSegments = CalculateNumSegments(sideDistance);
            float actualSegmentWidth = CalculateActualSegmentWidth(sideDistance, numSegments);

            bool skipFirstSegment = _vertexData[sideIdx].addCornerElement; // Basic check
            if (skipFirstSegment && numSegments > 1) // Only check angle if it's a multi-segment side and has a corner element
            {
                int prevVertexIndex = (sideIdx + N - 1) % N;
                Vector3 p_prev = _vertexData[prevVertexIndex].position; // Vertex before the start of current side
                Vector3 p_curr = p1_local;             // Start vertex of current side
                Vector3 p_next = p2_local;             // End vertex of current side

                Vector3 dir1 = (p_curr - p_prev).normalized;
                Vector3 dir2 = (p_next - p_curr).normalized; // This is the current side's direction

                // We need the outgoing vector from p_curr to p_next (current side)
                // and the incoming vector from p_prev to p_curr
                // Angle between -dir1 and dir2 (vector from prev to curr, and curr to next)
                float angleAtCorner = Vector3.Angle(-dir1, dir2);

                if (angleAtCorner < ACUTE_ANGLE_THRESHOLD_DEGREES)
                {
                    skipFirstSegment = true; // Confirmed skip due to acute angle
                }
                // else, if angle is >= 90, the _vertexData[sideIdx].addCornerElement still applies
            }


            // Check angle at the end vertex of the current side (vertexData[(sideIdx + 1) % N])
            int endVertexIndexOfSide = (sideIdx + 1) % N;
            bool skipLastSegment = _vertexData[endVertexIndexOfSide].addCornerElement; // Basic check
            if (skipLastSegment && numSegments > 1) // Only check angle if it's a multi-segment side and has a corner element
            {
                Vector3 p_prev = p1_local;                // Start vertex of current side
                Vector3 p_curr = p2_local;                // End vertex of current side
                Vector3 p_next = _vertexData[(endVertexIndexOfSide + 1) % N].position; // Vertex after the end of current side

                Vector3 dir1 = (p_curr - p_prev).normalized; // This is the current side's direction
                Vector3 dir2 = (p_next - p_curr).normalized;

                // Angle between -dir1 (vector from prev to curr) and dir2 (vector from curr to next)
                float angleAtCorner = Vector3.Angle(-dir1, dir2);

                if (angleAtCorner < ACUTE_ANGLE_THRESHOLD_DEGREES)
                {
                    skipLastSegment = true; // Confirmed skip due to acute angle
                }
                // else, if angle is >= 90, the _vertexData[endVertexIndexOfSide].addCornerElement still applies
            }
            // --- END OF MODIFIED CORNER SKIPPING LOGIC ---

            for (int segmentIdx = 0; segmentIdx < numSegments; segmentIdx++)
            {
                if (skipFirstSegment && segmentIdx == 0) continue;
                if (skipLastSegment && segmentIdx == numSegments - 1) continue;

                if (canGenerateMansardWindows) // Use the pre-calculated boolean
                {
                    PlaceWindowsOnSpecificRoof(
                        "Mansard",
                        generatedRoofs.MansardRoofObject,
                        sideIdx, segmentIdx, numSegments, actualSegmentWidth,
                        mansardWindowsParent.transform,
                        _settings.mansardSlopeHorizontalDistance,
                        _settings.mansardWindowInset
                    );
                }

                if (canGenerateAtticWindows) // Use the pre-calculated boolean
                {
                    PlaceWindowsOnSpecificRoof(
                        "Attic",
                        generatedRoofs.AtticRoofObject,
                        sideIdx, segmentIdx, numSegments, actualSegmentWidth,
                        atticWindowsParent.transform,
                        _settings.atticSlopeHorizontalDistance,
                        _settings.atticWindowInset
                    );
                }
            }
        }
    }

    private int CalculateNumSegments(float sideDistance)
    {
        if (_settings.nominalFacadeWidth <= GeometryUtils.Epsilon)
            return Mathf.Max(1, _settings.minSideLengthUnits > 0 ? _settings.minSideLengthUnits : 1);

        int num = (_settings.scaleFacadesToFitSide)
            ? Mathf.Max(_settings.minSideLengthUnits > 0 ? _settings.minSideLengthUnits : 1, Mathf.RoundToInt(sideDistance / _settings.nominalFacadeWidth))
            : Mathf.Max(_settings.minSideLengthUnits > 0 ? _settings.minSideLengthUnits : 1, Mathf.FloorToInt(sideDistance / _settings.nominalFacadeWidth));
        return Mathf.Max(1, num);
    }

    private float CalculateActualSegmentWidth(float sideDistance, int numSegments)
    {
        if (numSegments == 0) return _settings.nominalFacadeWidth;
        return _settings.scaleFacadesToFitSide ? (sideDistance / numSegments) : _settings.nominalFacadeWidth;
    }

    private void PlaceWindowsOnSpecificRoof(
        string roofTypeName,
        GameObject roofObject,
        int sideIndex,
        int segmentIndexInSide,
        int totalSegmentsOnSide,
        float actualSegmentWidth,
        Transform windowsParent,
        float roofSlopeHorizontalDistance,
        float currentRoofWindowInset)
    {
        // MeshFilter and MeshCollider checks already in place from previous version
        MeshCollider roofCollider = roofObject.GetComponent<MeshCollider>();
        if (roofCollider == null)
        {
            Debug.LogError($"{roofTypeName} GameObject for side {sideIndex} does not have a MeshCollider. Cannot place windows via raycast.");
            return;
        }

        List<GameObject> windowPrefabsToUse = null;
        if (roofTypeName == "Mansard") windowPrefabsToUse = _buildingStyle.defaultMansardWindowPrefabs;
        else if (roofTypeName == "Attic") windowPrefabsToUse = _buildingStyle.defaultAtticWindowPrefabs;

        if (sideIndex < _sideData.Count && _sideData[sideIndex].useCustomStyle && _sideData[sideIndex].sideStylePreset != null)
        {
            SideStyleSO customStyle = _sideData[sideIndex].sideStylePreset;
            List<GameObject> customPrefabs = null;
            if (roofTypeName == "Mansard" && customStyle.mansardFloorPrefabs != null && customStyle.mansardFloorPrefabs.Count > 0)
                customPrefabs = customStyle.mansardFloorPrefabs;
            else if (roofTypeName == "Attic" && customStyle.atticFloorPrefabs != null && customStyle.atticFloorPrefabs.Count > 0)
                customPrefabs = customStyle.atticFloorPrefabs;

            if (customPrefabs != null) windowPrefabsToUse = customPrefabs;
        }

        if (windowPrefabsToUse == null || windowPrefabsToUse.Count == 0) return;


        int N = _vertexData.Count;
        Vector3 p1_base_local = _vertexData[sideIndex].position;
        Vector3 p2_base_local = _vertexData[(sideIndex + 1) % N].position;

        Vector3 sideVector_local = p2_base_local - p1_base_local;
        Vector3 sideDirection_local = sideVector_local.normalized;
        Vector3 facadeNormal_local = PolygonGeometry.CalculateSideNormal(p1_base_local, p2_base_local, _vertexData);

        Vector3 segmentOuterEdgeHorizontalCenter_local = p1_base_local + sideDirection_local * (actualSegmentWidth * (segmentIndexInSide + 0.5f));
        float desiredInwardHorizontalShift = roofSlopeHorizontalDistance * 0.5f;
        Vector3 windowRaycastOriginHorizontal_local = segmentOuterEdgeHorizontalCenter_local - facadeNormal_local * desiredInwardHorizontalShift;
        Vector3 horizontalPositionForRaycast_world = _settings.transform.TransformPoint(windowRaycastOriginHorizontal_local);

        float buildingBaseY_world = _settings.transform.position.y;
        float totalWallHeight_local = (_settings.middleFloors + 1) * _settings.floorHeight;
        float estimatedMaxRoofRise_local = 0;
        if (_settings.useMansardFloor) estimatedMaxRoofRise_local += _settings.mansardRiseHeight;
        if (_settings.useAtticFloor) estimatedMaxRoofRise_local += _settings.atticRiseHeight;
        float raycastOriginY_world = buildingBaseY_world + totalWallHeight_local + estimatedMaxRoofRise_local;

        Vector3 raycastOrigin_world = new Vector3(
            horizontalPositionForRaycast_world.x,
            raycastOriginY_world,
            horizontalPositionForRaycast_world.z
        );

        Ray ray = new Ray(raycastOrigin_world, Vector3.down);
        RaycastHit hit;
        float raycastDistance = raycastOriginY_world - (buildingBaseY_world - 1f);

        if (!roofCollider.Raycast(ray, out hit, raycastDistance))
        {
            return;
        }

        Vector3 windowAnchorPos_world = hit.point;
        Vector3 roofSurfaceNormal_world = hit.normal;
        Vector3 facadeNormal_world = _settings.transform.TransformDirection(facadeNormal_local);

        // --- MODIFIED INSET LOGIC ---
        // Inset along the facade's normal (window's Z-axis), effectively pushing it "horizontally" into the building.
        // A positive roofWindowInset pushes the window inwards from its raycasted position.
        Vector3 windowPosition_world = windowAnchorPos_world - facadeNormal_world * currentRoofWindowInset;

        Quaternion windowRotation_world = Quaternion.LookRotation(facadeNormal_world, roofSurfaceNormal_world);

        GameObject prefab = windowPrefabsToUse[Random.Range(0, windowPrefabsToUse.Count)];
        if (prefab != null)
        {
            GameObject instance = Object.Instantiate(prefab, windowsParent);
            instance.transform.position = windowPosition_world;
            instance.transform.rotation = windowRotation_world;

            if (_settings.scaleRoofWindowsToFitSegment && _settings.nominalFacadeWidth > GeometryUtils.Epsilon && actualSegmentWidth > GeometryUtils.Epsilon)
            {
                Vector3 localScale = instance.transform.localScale;
                float scaleFactor = actualSegmentWidth / _settings.nominalFacadeWidth;
                instance.transform.localScale = new Vector3(localScale.x * scaleFactor, localScale.y, localScale.z);
            }
            instance.name = $"{roofTypeName}Window_{sideIndex}_{segmentIndexInSide}";
        }
    }
}