using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Responsible for generating facade elements (ground, middle floors) of the building.
/// </summary>
public class FacadeGenerator
{
    private readonly PolygonBuildingGenerator _settings;
    private readonly List<PolygonVertexData> _vertexData;
    private readonly List<PolygonSideData> _sideData;
    private readonly BuildingStyleSO _buildingStyle;

    public FacadeGenerator(PolygonBuildingGenerator settings, List<PolygonVertexData> vertexData, List<PolygonSideData> sideData, BuildingStyleSO buildingStyle)
    {
        _settings = settings;
        _vertexData = vertexData;
        _sideData = sideData;
        _buildingStyle = buildingStyle;
    }

    /// <summary>
    /// Generates all facade elements for the building and parents them to facadeRoot.
    /// </summary>
    public void GenerateAllFacades(Transform facadeRoot)
    {
        if (_vertexData.Count < 2) return; // Need at least two vertices to form a side

        // Pivot offset to center prefabs vertically, assuming pivot is at base of prefab
        float pivotOffsetVertical = _settings.floorHeight * 0.5f;

        for (int i = 0; i < _vertexData.Count; i++)
        {
            GameObject sideParent = new GameObject($"Side_{i}_Facades");
            sideParent.transform.SetParent(facadeRoot, false);

            Vector3 p1_local = _vertexData[i].position;
            Vector3 p2_local = _vertexData[(i + 1) % _vertexData.Count].position;
            Vector3 sideVector = p2_local - p1_local;
            float sideDistance = sideVector.magnitude;

            if (sideDistance < GeometryUtils.Epsilon) continue; // Skip zero-length sides

            Vector3 sideDirection = sideVector.normalized;
            // Calculate normal using the polygon's winding order for consistent outward direction
            Vector3 sideNormal = PolygonGeometry.CalculateSideNormal(p1_local, p2_local, _vertexData);

            int numSegments = CalculateNumSegments(sideDistance);
            float actualSegmentWidth = CalculateActualSegmentWidth(sideDistance, numSegments);

            GetSidePrefabLists(i, out var currentGroundPrefabs, out var currentMiddlePrefabs);

            for (int j = 0; j < numSegments; j++)
            {
                // Base position for this segment (center of the segment on the ground) - LOCAL SPACE
                Vector3 segmentBaseHorizontalPos_local = p1_local + sideDirection * (actualSegmentWidth * (j + 0.5f));
                // Rotation for this segment - LOCAL SPACE
                Quaternion baseSegmentRotation_local = Quaternion.LookRotation(sideNormal);
                float currentBottomY = 0;

                // --- Ground Floor ---
                if (currentGroundPrefabs != null && currentGroundPrefabs.Count > 0)
                {
                    // LOCAL SPACE pivot position
                    Vector3 groundFloorPivotPosition_local = segmentBaseHorizontalPos_local + Vector3.up * (currentBottomY + pivotOffsetVertical);

                    // Convert to WORLD SPACE for instantiation
                    Vector3 groundFloorPivotPosition_world = _settings.transform.TransformPoint(groundFloorPivotPosition_local);
                    Quaternion baseSegmentRotation_world = _settings.transform.rotation * baseSegmentRotation_local;

                    PrefabInstantiator.InstantiateSegment(currentGroundPrefabs, groundFloorPivotPosition_world, baseSegmentRotation_world,
                                                          sideParent.transform, actualSegmentWidth, false,
                                                          _settings.scaleFacadesToFitSide, _settings.nominalFacadeWidth);
                }
                currentBottomY += _settings.floorHeight;

                // --- Middle Floors ---
                if (currentMiddlePrefabs != null && currentMiddlePrefabs.Count > 0)
                {
                    for (int floor = 0; floor < _settings.middleFloors; floor++)
                    {
                        // LOCAL SPACE pivot position
                        Vector3 middleFloorPivotPosition_local = segmentBaseHorizontalPos_local + Vector3.up * (currentBottomY + pivotOffsetVertical);

                        // Convert to WORLD SPACE for instantiation
                        Vector3 middleFloorPivotPosition_world = _settings.transform.TransformPoint(middleFloorPivotPosition_local);
                        Quaternion baseSegmentRotation_world = _settings.transform.rotation * baseSegmentRotation_local;

                        PrefabInstantiator.InstantiateSegment(currentMiddlePrefabs, middleFloorPivotPosition_world, baseSegmentRotation_world,
                                                              sideParent.transform, actualSegmentWidth, false,
                                                              _settings.scaleFacadesToFitSide, _settings.nominalFacadeWidth);
                        currentBottomY += _settings.floorHeight;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Determines which prefab lists to use for a given side (custom style or defaults from BuildingStyleSO).
    /// </summary>
    private void GetSidePrefabLists(int sideIndex, out List<GameObject> ground, out List<GameObject> middle)
    {
        // Start with defaults from the main BuildingStyleSO
        ground = _buildingStyle.defaultGroundFloorPrefabs;
        middle = _buildingStyle.defaultMiddleFloorPrefabs;

        if (sideIndex < 0 || sideIndex >= _sideData.Count)
        {
            Debug.LogWarning($"FacadeGenerator: sideIndex {sideIndex} out of bounds for sideData. Using default prefabs.");
            return;
        }

        PolygonSideData currentSideSettings = _sideData[sideIndex];
        if (currentSideSettings.useCustomStyle && currentSideSettings.sideStylePreset != null)
        {
            SideStyleSO styleSO = currentSideSettings.sideStylePreset;
            // Override with style-specific prefabs if they are assigned and non-empty
            if (styleSO.groundFloorPrefabs != null && styleSO.groundFloorPrefabs.Count > 0)
                ground = styleSO.groundFloorPrefabs;
            if (styleSO.middleFloorPrefabs != null && styleSO.middleFloorPrefabs.Count > 0)
                middle = styleSO.middleFloorPrefabs;
            // Note: Mansard and Attic prefabs from SideStyleSO are not used here yet,
            // as facade instantiation on sloped roofs is not yet implemented.
        }
    }

    /// <summary>
    /// Calculates the number of facade segments for a side based on its length and nominal width.
    /// </summary>
    private int CalculateNumSegments(float sideDistance)
    {
        if (_settings.nominalFacadeWidth <= GeometryUtils.Epsilon)
            return Mathf.Max(1, _settings.minSideLengthUnits > 0 ? _settings.minSideLengthUnits : 1);

        int num;
        if (_settings.scaleFacadesToFitSide)
        {
            // If scaling, round to nearest whole number of segments, ensuring minimum
            num = Mathf.Max(_settings.minSideLengthUnits > 0 ? _settings.minSideLengthUnits : 1,
                            Mathf.RoundToInt(sideDistance / _settings.nominalFacadeWidth));
        }
        else
        {
            // If not scaling, fit as many nominal-width segments as possible, ensuring minimum
            num = Mathf.Max(_settings.minSideLengthUnits > 0 ? _settings.minSideLengthUnits : 1,
                            Mathf.FloorToInt(sideDistance / _settings.nominalFacadeWidth));
        }
        return Mathf.Max(1, num); // Ensure at least one segment
    }

    /// <summary>
    /// Calculates the actual width of each segment.
    /// If scaling, it's sideDistance / numSegments. Otherwise, it's the nominalFacadeWidth.
    /// </summary>
    private float CalculateActualSegmentWidth(float sideDistance, int numSegments)
    {
        if (numSegments == 0) return _settings.nominalFacadeWidth; // Avoid division by zero
        return _settings.scaleFacadesToFitSide ? (sideDistance / numSegments) : _settings.nominalFacadeWidth;
    }
}