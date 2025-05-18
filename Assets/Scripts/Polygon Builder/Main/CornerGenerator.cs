using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Responsible for generating corner elements (e.g., chimneys, quoins) of the building.
/// </summary>
public class CornerGenerator
{
    private readonly PolygonBuildingGeneratorMain _settings;
    private readonly List<PolygonVertexData> _vertexData;
    private readonly BuildingStyleSO _buildingStyle;

    public CornerGenerator(PolygonBuildingGeneratorMain settings, List<PolygonVertexData> vertexData, BuildingStyleSO buildingStyle)
    {
        _settings = settings;
        _vertexData = vertexData;
        _buildingStyle = buildingStyle;
    }

    /// <summary>
    /// Generates all corner elements for the building and parents them to cornerRoot.
    /// </summary>
    public void GenerateAllCorners(Transform cornerRoot)
    {
        if (_vertexData.Count < 3 || _buildingStyle == null) return;

        bool hasCornerBodyPrefabs = _buildingStyle.defaultChimneyBodyPrefabs != null && _buildingStyle.defaultChimneyBodyPrefabs.Count > 0;
        bool hasCornerCapPrefabs = _settings.useCornerCaps && _buildingStyle.defaultChimneyCapPrefabs != null && _buildingStyle.defaultChimneyCapPrefabs.Count > 0;

        if (!hasCornerBodyPrefabs && !hasCornerCapPrefabs) return; // Nothing to generate if no prefabs for either

        float chimneyBodyPrefabHeight = _settings.floorHeight; // Assuming chimney body prefabs are designed for one floorHeight
        float pivotOffsetVertical = chimneyBodyPrefabHeight * 0.5f; // Pivot offset to center prefabs vertically

        for (int i = 0; i < _vertexData.Count; i++)
        {
            if (!_vertexData[i].addCornerElement) continue;

            int prevI = (i + _vertexData.Count - 1) % _vertexData.Count;
            Vector3 currentPos_local = _vertexData[i].position;
            Vector3 prevPos_local = _vertexData[prevI].position;
            Vector3 nextPos_local = _vertexData[(i + 1) % _vertexData.Count].position;

            CalculateCornerTransform(prevPos_local, currentPos_local, nextPos_local,
                                     out Vector3 cornerBaseHorizontalPos_local, out Quaternion baseCornerRotation_local);

            float currentElementBaseY_local = 0; // Tracks the Y position for the base of the *next* element
            float cornerElementWidth = _settings.nominalFacadeWidth;

            int totalBodySegments = 1; // For ground floor
            totalBodySegments += _settings.middleFloors;

            // Add a segment for the mansard level if it exists and has height
            if (_settings.useMansardFloor && _settings.mansardRiseHeight > GeometryUtils.Epsilon)
            {
                totalBodySegments += 1;
            }

            // Add a segment for the attic level if it exists and has height
            if (_settings.useAtticFloor && _settings.atticRiseHeight > GeometryUtils.Epsilon)
            {
                totalBodySegments += 1;
            }

            // --- Corner Element Bodies (e.g., Chimneys) ---
            if (hasCornerBodyPrefabs)
            {
                for (int segmentIndex = 0; segmentIndex < totalBodySegments; segmentIndex++)
                {
                    // LOCAL SPACE pivot position for the current segment
                    Vector3 segmentPivotPos_local = cornerBaseHorizontalPos_local + Vector3.up * (currentElementBaseY_local + pivotOffsetVertical);

                    // Convert to WORLD SPACE
                    Vector3 segmentPivotPos_world = _settings.transform.TransformPoint(segmentPivotPos_local);
                    Quaternion baseCornerRotation_world = _settings.transform.rotation * baseCornerRotation_local;

                    PrefabInstantiator.InstantiateSegment(
                        _buildingStyle.defaultChimneyBodyPrefabs, segmentPivotPos_world, baseCornerRotation_world,
                        cornerRoot, cornerElementWidth, true,
                        false, _settings.nominalFacadeWidth);

                    currentElementBaseY_local += chimneyBodyPrefabHeight; // Advance base for the next segment
                }
            }
            else
            {
                // If there are no body prefabs, but we might place a cap,
                // the cap should be placed as if the bodies existed.
                // So, calculate where the top of these hypothetical bodies would be.
                currentElementBaseY_local = chimneyBodyPrefabHeight * totalBodySegments;
            }


            // --- Corner Caps ---
            // The cap is placed on top of all body segments (real or hypothetical).
            // currentElementBaseY_local now represents the Y where the base of the cap should be.
            if (hasCornerCapPrefabs)
            {
                // LOCAL SPACE cap position (pivot at base of cap)
                Vector3 capPosition_local = cornerBaseHorizontalPos_local + Vector3.up * currentElementBaseY_local;

                // Convert to WORLD SPACE
                Vector3 capPosition_world = _settings.transform.TransformPoint(capPosition_local);
                Quaternion baseCornerRotation_world = _settings.transform.rotation * baseCornerRotation_local;

                PrefabInstantiator.InstantiateSegment(
                    _buildingStyle.defaultChimneyCapPrefabs, capPosition_world, baseCornerRotation_world,
                    cornerRoot, cornerElementWidth, true,
                    false, _settings.nominalFacadeWidth);
            }
        }
    }

    /// <summary>
    /// Calculates the local position and rotation for a corner element.
    /// The rotation bisects the angle of the corner, and an offset can be applied.
    /// </summary>
    private void CalculateCornerTransform(Vector3 p1_prev_local, Vector3 p2_current_local, Vector3 p3_next_local,
                                          out Vector3 cornerPos_local, out Quaternion cornerRot_local)
    {
        Vector3 sideNormalPrev = PolygonGeometry.CalculateSideNormal(p1_prev_local, p2_current_local, _vertexData);
        Vector3 sideNormalNext = PolygonGeometry.CalculateSideNormal(p2_current_local, p3_next_local, _vertexData);
        Vector3 cornerFacingDirection = (sideNormalPrev + sideNormalNext).normalized;

        if (cornerFacingDirection.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon)
        {
            if (sideNormalNext.sqrMagnitude > GeometryUtils.Epsilon * GeometryUtils.Epsilon)
                cornerFacingDirection = sideNormalNext;
            else if (sideNormalPrev.sqrMagnitude > GeometryUtils.Epsilon * GeometryUtils.Epsilon)
                cornerFacingDirection = sideNormalPrev;
            else
            {
                Vector3 dirIn = (p2_current_local - p1_prev_local).normalized;
                if (dirIn.sqrMagnitude > GeometryUtils.Epsilon * GeometryUtils.Epsilon)
                    cornerFacingDirection = new Vector3(dirIn.z, 0, -dirIn.x).normalized;
                else
                {
                    Vector3 dirOut = (p3_next_local - p2_current_local).normalized;
                    if (dirOut.sqrMagnitude > GeometryUtils.Epsilon * GeometryUtils.Epsilon)
                        cornerFacingDirection = new Vector3(dirOut.z, 0, -dirOut.x).normalized;
                    else
                        cornerFacingDirection = Vector3.forward;
                }
            }
        }

        cornerRot_local = Quaternion.LookRotation(cornerFacingDirection);
        Vector3 localOffsetVector = Vector3.forward * _settings.cornerElementForwardOffset;
        Vector3 buildingLocalSpaceOffset = cornerRot_local * localOffsetVector;
        cornerPos_local = p2_current_local + buildingLocalSpaceOffset;
    }
}