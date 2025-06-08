// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
//  This script is responsible for generating window elements on procedural roof surfaces, specifically for Mansard and Attic roofs.
//

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Responsible for generating and placing windows on procedural roof surfaces.
/// </summary>
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

    /// <summary>
    /// Main entry point to generate all enabled roof windows (Mansard and/or Attic).
    /// </summary>
    /// <param name="roofWindowsRoot">The parent transform for all window parent containers.</param>
    /// <param name="generatedRoofs">References to the generated roof GameObjects.</param>
    public void GenerateAllWindows(Transform roofWindowsRoot, GeneratedRoofObjects generatedRoofs)
    {
        if ((!_settings.placeMansardWindows && !_settings.placeAtticWindows) || _vertexData.Count < 3) return;

        // --- Setup Window Layers ---
        var mansardContext = SetupWindowLayer(
            "Mansard", roofWindowsRoot, generatedRoofs.MansardRoofObject,
            _settings.placeMansardWindows, _settings.useMansardFloor,
            _settings.mansardRiseHeight, _settings.minMansardRiseForWindows,
            _settings.mansardSlopeHorizontalDistance, _settings.maxMansardHDistForWindows,
            _settings.mansardWindowInset, _buildingStyle.defaultMansardWindowPrefabs);

        var atticContext = SetupWindowLayer(
            "Attic", roofWindowsRoot, generatedRoofs.AtticRoofObject,
            _settings.placeAtticWindows, _settings.useAtticFloor,
            _settings.atticRiseHeight, _settings.minAtticRiseForWindows,
            _settings.atticSlopeHorizontalDistance, _settings.maxAtticHDistForWindows,
            _settings.atticWindowInset, _buildingStyle.defaultAtticWindowPrefabs);

        if (!mansardContext.canGenerate && !atticContext.canGenerate) return;

        // Iterate through each side of the building to place windows segment by segment.
        for (int sideIdx = 0; sideIdx < _vertexData.Count; sideIdx++)
        {
            GenerateWindowsForSide(sideIdx, mansardContext, atticContext);
        }
    }

    /// <summary>
    /// Sets up the context for a specific window layer (Mansard or Attic), checking if generation is possible.
    /// </summary>
    /// <returns>A context object containing placement information for the layer.</returns>
    private WindowLayerContext SetupWindowLayer(string name, Transform root, GameObject roofObj, bool placeFlag, bool useFlag, float rise, float minRise, float hDist, float maxHDist, float inset, List<GameObject> prefabs)
    {
        var context = new WindowLayerContext(name, roofObj, inset, prefabs);
        context.canGenerate = placeFlag && useFlag && roofObj != null && rise >= minRise && hDist <= maxHDist;

        if (context.canGenerate)
        {
            var parentGo = new GameObject($"{name} Windows");
            parentGo.transform.SetParent(root, false);
            context.parent = parentGo.transform;
        }
        return context;
    }

    /// <summary>
    /// Generates all window segments for a single side of the building.
    /// </summary>
    private void GenerateWindowsForSide(int sideIdx, WindowLayerContext mansardContext, WindowLayerContext atticContext)
    {
        Vector3 p1_local = _vertexData[sideIdx].position;
        Vector3 p2_local = _vertexData[(sideIdx + 1) % _vertexData.Count].position;
        float sideDistance = Vector3.Distance(p1_local, p2_local);

        if (sideDistance < GeometryConstants.GeometricEpsilon) return;

        // Calculate segmentation to match the facade segments.
        int numSegments = CalculateNumSegments(sideDistance);
        float actualSegmentWidth = CalculateActualSegmentWidth(sideDistance, numSegments);

        // Skip placing windows on the first and last segments of a side to avoid corner crowding.
        bool skipFirstSegment = numSegments > 1;
        bool skipLastSegment = numSegments > 1;

        for (int segmentIdx = 0; segmentIdx < numSegments; segmentIdx++)
        {
            if ((skipFirstSegment && segmentIdx == 0) || (skipLastSegment && segmentIdx == numSegments - 1)) continue;

            // Place windows if the respective layers are enabled.
            if (mansardContext.canGenerate)
                PlaceWindowOnSegment(sideIdx, segmentIdx, actualSegmentWidth, mansardContext);

            if (atticContext.canGenerate)
                PlaceWindowOnSegment(sideIdx, segmentIdx, actualSegmentWidth, atticContext);
        }
    }

    /// <summary>
    /// Places a single window on a roof segment by raycasting to find the surface.
    /// </summary>
    private void PlaceWindowOnSegment(int sideIndex, int segmentIndex, float segmentWidth, WindowLayerContext context)
    {
        // Determine correct prefabs to use (default vs. side-specific override).
        var prefabsToUse = GetPrefabsForSide(sideIndex, context.defaultPrefabs, context.name);
        if (prefabsToUse == null || prefabsToUse.Count == 0) return;

        // Calculate the ray for this specific window segment.
        if (!TryCalculateWindowRaycast(sideIndex, segmentIndex, segmentWidth, out Ray ray, out Vector3 facadeNormal_local)) return;

        // Raycast against the roof mesh to find the placement point.
        var roofCollider = context.roofObject.GetComponent<MeshCollider>();
        if (roofCollider == null || !roofCollider.Raycast(ray, out RaycastHit hit, 500f)) return;

        // A window was successfully placed, now instantiate it.
        InstantiateAndPlaceWindow(hit, facadeNormal_local, segmentWidth, sideIndex, segmentIndex, prefabsToUse, context);
    }

    /// <summary>
    /// Calculates the world-space ray used to find a window's position on the roof surface.
    /// </summary>
    private bool TryCalculateWindowRaycast(int sideIndex, int segmentIndex, float segmentWidth, out Ray ray, out Vector3 facadeNormal_local)
    {
        ray = default;
        Vector3 p1_base_local = _vertexData[sideIndex].position;
        Vector3 p2_base_local = _vertexData[(sideIndex + 1) % _vertexData.Count].position;

        Vector3 sideDirection_local = (p2_base_local - p1_base_local).normalized;
        facadeNormal_local = BuildingFootprintUtils.CalculateSideNormal(p1_base_local, p2_base_local, _vertexData);

        // Center point of the segment on the outer wall edge.
        Vector3 segmentCenter_local = p1_base_local + sideDirection_local * (segmentWidth * (segmentIndex + 0.5f));

        // Shift the origin inwards to be above the sloped roof surface.
        Vector3 rayOriginHorizontal_local = segmentCenter_local - facadeNormal_local * (_settings.nominalFacadeWidth * 0.5f);
        Vector3 rayOriginHorizontal_world = _settings.transform.TransformPoint(rayOriginHorizontal_local);

        // Start the raycast from well above the highest possible roof point.
        float buildingTopY = _settings.transform.position.y + (_settings.middleFloors + 1) * _settings.floorHeight + _settings.mansardRiseHeight + _settings.atticRiseHeight + 10f;
        Vector3 rayOrigin_world = new Vector3(rayOriginHorizontal_world.x, buildingTopY, rayOriginHorizontal_world.z);

        ray = new Ray(rayOrigin_world, Vector3.down);
        return true;
    }

    /// <summary>
    /// Instantiates and configures a window prefab at the raycast hit location.
    /// </summary>
    private void InstantiateAndPlaceWindow(RaycastHit hit, Vector3 facadeNormal_local, float segmentWidth, int sideIndex, int segmentIndex, List<GameObject> prefabs, WindowLayerContext context)
    {
        Vector3 facadeNormal_world = _settings.transform.TransformDirection(facadeNormal_local);

        // Apply an inset from the roof surface along the facade's normal.
        Vector3 windowPosition_world = hit.point - facadeNormal_world * context.windowInset;
        Quaternion windowRotation_world = Quaternion.LookRotation(facadeNormal_world, hit.normal);

        GameObject prefab = prefabs[Random.Range(0, prefabs.Count)];
        GameObject instance = Object.Instantiate(prefab, windowPosition_world, windowRotation_world, context.parent);
        instance.name = $"{context.name}Window_{sideIndex}_{segmentIndex}";

        // Store reference and scale if needed.
        if (context.name == "Mansard") _elementsStore.allMansardWindows.Add(instance);
        else if (context.name == "Attic") _elementsStore.allAtticWindows.Add(instance);

        if (_settings.scaleRoofWindowsToFitSegment && _settings.nominalFacadeWidth > GeometryConstants.GeometricEpsilon)
        {
            float scaleFactor = segmentWidth / _settings.nominalFacadeWidth;
            instance.transform.localScale = new Vector3(instance.transform.localScale.x * scaleFactor, instance.transform.localScale.y, instance.transform.localScale.z);
        }
    }

    /// <summary>
    /// Selects the correct list of prefabs for a given side, checking for custom style overrides.
    /// </summary>
    private List<GameObject> GetPrefabsForSide(int sideIndex, List<GameObject> defaultPrefabs, string roofTypeName)
    {
        // Check for a custom style defined on this specific side.
        bool hasCustomStyle = !_settings.useConsistentStyleForAllSides && sideIndex < _sideData.Count &&
                              _sideData[sideIndex].useCustomStyle && _sideData[sideIndex].sideStylePreset != null;

        if (hasCustomStyle)
        {
            var customStyle = _sideData[sideIndex].sideStylePreset;
            if (roofTypeName == "Mansard" && customStyle.mansardFloorPrefabs?.Count > 0)
                return customStyle.mansardFloorPrefabs;
            if (roofTypeName == "Attic" && customStyle.atticFloorPrefabs?.Count > 0)
                return customStyle.atticFloorPrefabs;
        }

        // Fallback to the default style.
        return defaultPrefabs;
    }

    private int CalculateNumSegments(float sideDistance)
    {
        if (_settings.nominalFacadeWidth <= GeometryConstants.GeometricEpsilon) return 1;
        int minSegments = Mathf.Max(1, _settings.minSideLengthUnits);
        int num = _settings.scaleFacadesToFitSide
            ? Mathf.RoundToInt(sideDistance / _settings.nominalFacadeWidth)
            : Mathf.FloorToInt(sideDistance / _settings.nominalFacadeWidth);
        return Mathf.Max(minSegments, num);
    }

    private float CalculateActualSegmentWidth(float sideDistance, int numSegments)
    {
        if (numSegments == 0) return _settings.nominalFacadeWidth;
        return _settings.scaleFacadesToFitSide ? (sideDistance / numSegments) : _settings.nominalFacadeWidth;
    }

    // Helper struct to pass context for a window layer.
    private class WindowLayerContext
    {
        public string name;
        public bool canGenerate;
        public GameObject roofObject;
        public Transform parent;
        public float windowInset;
        public List<GameObject> defaultPrefabs;

        public WindowLayerContext(string name, GameObject roofObject, float inset, List<GameObject> defaultPrefabs)
        {
            this.name = name;
            this.roofObject = roofObject;
            this.windowInset = inset;
            this.defaultPrefabs = defaultPrefabs;
            this.canGenerate = false;
            this.parent = null;
        }
    }
}