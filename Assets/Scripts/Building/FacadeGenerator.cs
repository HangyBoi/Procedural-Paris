// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
//  This script is responsible for generating facade elements (ground and middle floors) of a building in a procedural generation context.
//  It uses the provided settings, vertex data, and side data to create facade segments based on the building's polygon shape and style preferences.
//

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Responsible for generating facade elements (ground, middle floors) of the building.
/// </summary>
public class FacadeGenerator
{
    private readonly PolygonBuildingGenerator _settings;
    private readonly List<PolygonVertexData> _vertexData;
    private readonly List<PolygonSideData> _sideData;
    private readonly BuildingStyleSO _buildingStyle;
    private readonly GeneratedBuildingElements _elementsStore;

    // Pre-calculated vertical offset for placing prefabs, assuming their pivot is at the base.
    private readonly float _pivotOffsetY;

    public FacadeGenerator(PolygonBuildingGenerator settings, List<PolygonVertexData> vertexData, List<PolygonSideData> sideData, BuildingStyleSO buildingStyle, GeneratedBuildingElements elementsStore)
    {
        _settings = settings;
        _vertexData = vertexData;
        _sideData = sideData;
        _buildingStyle = buildingStyle;
        _elementsStore = elementsStore;

        _pivotOffsetY = _settings.floorHeight * 0.5f;
    }

    /// <summary>
    /// Generates all facade elements for the building and parents them to a root transform.
    /// Iterates through each side of the building polygon and generates the required facade segments.
    /// </summary>
    /// <param name="facadeRoot">The parent transform for all generated facade elements.</param>
    public void GenerateAllFacades(Transform facadeRoot)
    {
        // A building side requires at least two vertices.
        if (_vertexData.Count < 2) return;

        for (int i = 0; i < _vertexData.Count; i++)
        {
            GenerateFacadesForSide(i, facadeRoot);
        }
    }

    /// <summary>
    /// Generates all facade segments for a single side of the building.
    /// </summary>
    /// <param name="sideIndex">The index of the side to generate facades for.</param>
    /// <param name="facadeRoot">The root transform for all facade elements.</param>
    private void GenerateFacadesForSide(int sideIndex, Transform facadeRoot)
    {
        // Get the vertices that define the current side.
        Vector3 p1_local = _vertexData[sideIndex].position;
        Vector3 p2_local = _vertexData[(sideIndex + 1) % _vertexData.Count].position;

        Vector3 sideVector = p2_local - p1_local;
        float sideDistance = sideVector.magnitude;

        // Skip sides that are too small to generate anything on.
        if (sideDistance < GeometryConstants.GeometricEpsilon) return;

        SideElementGroup currentSideGroup = _elementsStore.facadeElementsPerSide.Find(sg => sg.sideIndex == sideIndex);
        if (currentSideGroup == null) return;

        // Create a parent object for this side's elements to keep the hierarchy organized.
        GameObject sideParent = new GameObject($"Side_{sideIndex}_Facades");
        sideParent.transform.SetParent(facadeRoot, false);

        // Determine the side's orientation.
        Vector3 sideDirection = sideVector.normalized;
        Vector3 sideNormal = BuildingFootprintUtils.CalculateSideNormal(p1_local, p2_local, _vertexData);
        Quaternion baseSegmentRotation_local = Quaternion.LookRotation(sideNormal);

        // Calculate segmentation based on side length and desired facade width.
        int numSegments = CalculateNumSegments(sideDistance);
        float actualSegmentWidth = CalculateActualSegmentWidth(sideDistance, numSegments);

        // Retrieve the appropriate prefab lists for this side (default or custom style).
        GetSidePrefabLists(sideIndex, out var groundPrefabs, out var middlePrefabs);

        // Generate segments for this side.
        for (int j = 0; j < numSegments; j++)
        {
            // Calculate the base position for this segment in local space.
            Vector3 segmentBaseHorizontalPos_local = p1_local + sideDirection * (actualSegmentWidth * (j + 0.5f));
            float currentBottomY = 0f;

            // --- Instantiate Ground Floor ---
            InstantiateFacadeFloor(groundPrefabs, segmentBaseHorizontalPos_local, baseSegmentRotation_local, currentBottomY,
                                   sideParent.transform, actualSegmentWidth, currentSideGroup.groundFacadeSegments);
            currentBottomY += _settings.floorHeight;

            // --- Instantiate Middle Floors ---
            for (int floor = 0; floor < _settings.middleFloors; floor++)
            {
                InstantiateFacadeFloor(middlePrefabs, segmentBaseHorizontalPos_local, baseSegmentRotation_local, currentBottomY,
                                       sideParent.transform, actualSegmentWidth, currentSideGroup.middleFacadeSegments);
                currentBottomY += _settings.floorHeight;
            }
        }
    }

    /// <summary>
    /// Instantiates and configures a single facade segment for one floor.
    /// </summary>
    private void InstantiateFacadeFloor(List<GameObject> prefabs, Vector3 localSegmentBasePos, Quaternion localSegmentRotation,
        float yLevel, Transform parent, float segmentWidth, List<GameObject> storageList)
    {
        if (prefabs == null || prefabs.Count == 0) return;

        // Calculate final position and rotation in world space.
        Vector3 localPivotPosition = localSegmentBasePos + Vector3.up * (yLevel + _pivotOffsetY);
        Vector3 worldPivotPosition = _settings.transform.TransformPoint(localPivotPosition);
        Quaternion worldRotation = _settings.transform.rotation * localSegmentRotation;

        GameObject instance = PrefabInstantiator.InstantiateSegment(
            prefabs, worldPivotPosition, worldRotation, parent, segmentWidth,
            false, _settings.scaleFacadesToFitSide, _settings.nominalFacadeWidth);

        ProcessPrefabRandomizer(instance);

        if (instance != null)
        {
            storageList.Add(instance);
        }
    }

    /// <summary>
    /// Finds and executes a PrefabPropRandomizer on the given instance, if it exists.
    /// </summary>
    private void ProcessPrefabRandomizer(GameObject instance)
    {
        if (instance == null) return;

        var propRandomizer = instance.GetComponent<PrefabPropRandomizer>();
        if (propRandomizer != null)
        {
            propRandomizer.RandomizeProps();

#if UNITY_EDITOR
            // Mark objects as dirty to ensure changes are saved in the editor scene.
            UnityEditor.EditorUtility.SetDirty(propRandomizer);
            UnityEditor.EditorUtility.SetDirty(instance);
            foreach (var prop in propRandomizer.optionalProps)
            {
                if (prop != null) UnityEditor.EditorUtility.SetDirty(prop);
            }
#endif
        }
    }

    /// <summary>
    /// Determines which prefab lists to use for a given side (custom style or defaults from BuildingStyleSO).
    /// </summary>
    private void GetSidePrefabLists(int sideIndex, out List<GameObject> ground, out List<GameObject> middle)
    {
        // Start with default styles from the main building settings.
        ground = _buildingStyle.defaultGroundFloorPrefabs;
        middle = _buildingStyle.defaultMiddleFloorPrefabs;

        bool canUseCustomStyle = !_settings.useConsistentStyleForAllSides && sideIndex >= 0 && sideIndex < _sideData.Count;
        if (!canUseCustomStyle) return;

        // If allowed, check for a custom style override on this specific side.
        PolygonSideData currentSideSettings = _sideData[sideIndex];
        if (currentSideSettings.useCustomStyle && currentSideSettings.sideStylePreset != null)
        {
            SideStyleSO styleSO = currentSideSettings.sideStylePreset;
            if (styleSO.groundFloorPrefabs != null && styleSO.groundFloorPrefabs.Count > 0)
                ground = styleSO.groundFloorPrefabs;
            if (styleSO.middleFloorPrefabs != null && styleSO.middleFloorPrefabs.Count > 0)
                middle = styleSO.middleFloorPrefabs;
        }
    }

    /// <summary>
    /// Calculates the number of facade segments for a side based on its length and nominal width.
    /// </summary>
    private int CalculateNumSegments(float sideDistance)
    {
        if (_settings.nominalFacadeWidth <= GeometryConstants.GeometricEpsilon) return 1;

        int num;
        int minSegments = Mathf.Max(1, _settings.minSideLengthUnits);

        if (_settings.scaleFacadesToFitSide)
        {
            // When scaling, round to the nearest whole number of segments.
            num = Mathf.RoundToInt(sideDistance / _settings.nominalFacadeWidth);
        }
        else
        {
            // When not scaling, fit as many fixed-width segments as possible.
            num = Mathf.FloorToInt(sideDistance / _settings.nominalFacadeWidth);
        }
        return Mathf.Max(minSegments, num);
    }

    /// <summary>
    /// Calculates the actual width of each segment.
    /// If scaling, it's sideDistance / numSegments. Otherwise, it's the nominalFacadeWidth.
    /// </summary>
    private float CalculateActualSegmentWidth(float sideDistance, int numSegments)
    {
        if (numSegments == 0) return _settings.nominalFacadeWidth; // Avoid division by zero.
        return _settings.scaleFacadesToFitSide ? (sideDistance / numSegments) : _settings.nominalFacadeWidth;
    }
}