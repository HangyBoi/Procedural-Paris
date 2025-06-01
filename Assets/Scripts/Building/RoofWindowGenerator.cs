using UnityEngine;
using System.Collections.Generic;

public class RoofWindowGenerator
{
    private readonly PolygonBuildingGenerator _settings;
    private readonly List<PolygonVertexData> _vertexData;
    private readonly List<PolygonSideData> _sideData;
    private readonly BuildingStyleSO _buildingStyle;
    private readonly GeneratedBuildingElements _elementsStore;

    public RoofWindowGenerator(PolygonBuildingGenerator settings, List<PolygonVertexData> vertexData, List<PolygonSideData> sideData, BuildingStyleSO buildingStyle, GeneratedBuildingElements elementsStore)
    {
        _settings = settings;
        _vertexData = vertexData;
        _sideData = sideData;
        _buildingStyle = buildingStyle;
        _elementsStore = elementsStore;
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

            if (sideDistance < GeometryConstants.GeometricEpsilon) continue;

            int numSegments = CalculateNumSegments(sideDistance);
            float actualSegmentWidth = CalculateActualSegmentWidth(sideDistance, numSegments);

            bool skipFirstSegment = true; // Always skip the first segment
            bool skipLastSegment = true;  // Always skip the last segment

            if (numSegments <= 2) // Or <= 2 if you want at least one window even if ends are skipped
            {
                skipFirstSegment = false;
                skipLastSegment = false;
            }

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
        if (_settings.nominalFacadeWidth <= GeometryConstants.GeometricEpsilon)
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
        Vector3 facadeNormal_local = BuildingFootprintUtils.CalculateSideNormal(p1_base_local, p2_base_local, _vertexData);

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

            if (roofTypeName == "Mansard") _elementsStore.allMansardWindows.Add(instance);
            else if (roofTypeName == "Attic") _elementsStore.allAtticWindows.Add(instance);

            if (_settings.scaleRoofWindowsToFitSegment && _settings.nominalFacadeWidth > GeometryConstants.GeometricEpsilon && actualSegmentWidth > GeometryConstants.GeometricEpsilon)
            {
                Vector3 localScale = instance.transform.localScale;
                float scaleFactor = actualSegmentWidth / _settings.nominalFacadeWidth;
                instance.transform.localScale = new Vector3(localScale.x * scaleFactor, localScale.y, localScale.z);
            }
            instance.name = $"{roofTypeName}Window_{sideIndex}_{segmentIndexInSide}";
        }
    }
}