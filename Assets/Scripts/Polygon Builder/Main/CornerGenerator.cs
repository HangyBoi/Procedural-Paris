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

        if (!hasCornerBodyPrefabs && !hasCornerCapPrefabs) return; // Nothing to generate

        // Pivot offset to center prefabs vertically, assuming pivot is at base
        float pivotOffsetVertical = _settings.floorHeight * 0.5f;

        for (int i = 0; i < _vertexData.Count; i++)
        {
            if (!_vertexData[i].addCornerElement) continue;

            int prevI = (i + _vertexData.Count - 1) % _vertexData.Count;
            Vector3 currentPos_local = _vertexData[i].position;
            Vector3 prevPos_local = _vertexData[prevI].position;
            Vector3 nextPos_local = _vertexData[(i + 1) % _vertexData.Count].position;

            // Calculate the position and orientation for the corner element
            CalculateCornerTransform(prevPos_local, currentPos_local, nextPos_local,
                                     out Vector3 cornerBaseHorizontalPos_local, out Quaternion baseCornerRotation_local);

            float currentElementBaseY = 0;
            float cornerElementWidth = _settings.nominalFacadeWidth;


            // --- Corner Element Bodies (e.g., Chimneys) ---
            if (hasCornerBodyPrefabs)
            {
                // Ground floor level corner element LOCAL position
                Vector3 groundCornerPivotPos_local = cornerBaseHorizontalPos_local + Vector3.up * (currentElementBaseY + pivotOffsetVertical);

                // Convert to WORLD SPACE
                Vector3 groundCornerPivotPos_world = _settings.transform.TransformPoint(groundCornerPivotPos_local);
                Quaternion baseCornerRotation_world = _settings.transform.rotation * baseCornerRotation_local;

                PrefabInstantiator.InstantiateSegment(_buildingStyle.defaultChimneyBodyPrefabs, groundCornerPivotPos_world, baseCornerRotation_world,
                                                      cornerRoot, cornerElementWidth, true,
                                                      false, _settings.nominalFacadeWidth);
                currentElementBaseY += _settings.floorHeight;

                // Middle floor levels corner elements
                for (int floor = 0; floor < _settings.middleFloors; floor++)
                {
                    // LOCAL SPACE pivot position
                    Vector3 middleCornerPivotPos_local = cornerBaseHorizontalPos_local + Vector3.up * (currentElementBaseY + pivotOffsetVertical);

                    // Convert to WORLD SPACE
                    Vector3 middleCornerPivotPos_world = _settings.transform.TransformPoint(middleCornerPivotPos_local);
                    // baseCornerRotation_world is the same as for ground floor

                    PrefabInstantiator.InstantiateSegment(_buildingStyle.defaultChimneyBodyPrefabs, middleCornerPivotPos_world, baseCornerRotation_world,
                                                          cornerRoot, cornerElementWidth, true,
                                                          false, _settings.nominalFacadeWidth);
                    currentElementBaseY += _settings.floorHeight;
                }
            }

            // --- Corner Caps ---
            if (hasCornerCapPrefabs)
            {
                float capBaseY = _settings.floorHeight * (1 + _settings.middleFloors);
                // LOCAL SPACE cap position
                Vector3 capPosition_local = cornerBaseHorizontalPos_local + Vector3.up * capBaseY;

                // Convert to WORLD SPACE
                Vector3 capPosition_world = _settings.transform.TransformPoint(capPosition_local);
                Quaternion baseCornerRotation_world = _settings.transform.rotation * baseCornerRotation_local; // Same world rotation

                PrefabInstantiator.InstantiateSegment(_buildingStyle.defaultChimneyCapPrefabs, capPosition_world, baseCornerRotation_world,
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
        // Normals of the sides meeting at the current vertex
        Vector3 sideNormalPrev = PolygonGeometry.CalculateSideNormal(p1_prev_local, p2_current_local, _vertexData);
        Vector3 sideNormalNext = PolygonGeometry.CalculateSideNormal(p2_current_local, p3_next_local, _vertexData);

        // Bisector of the angle between the two normals (points outward from the corner)
        Vector3 cornerFacingDirection = (sideNormalPrev + sideNormalNext).normalized;

        // Handle collinear cases (straight wall) where normals might cancel out or be identical
        if (cornerFacingDirection.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon)
        {
            cornerFacingDirection = sideNormalNext;
            if (cornerFacingDirection.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon) // Still zero?
            {
                // Fallback if sideNormalNext was also zero (e.g. p2_current == p3_next)
                // Use normal perpendicular to the incoming segment
                Vector3 dirIn = (p2_current_local - p1_prev_local).normalized;
                cornerFacingDirection = new Vector3(dirIn.z, 0, -dirIn.x); // Perpendicular
                if (cornerFacingDirection.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon)
                {
                    cornerFacingDirection = Vector3.forward; // Absolute fallback
                }
            }
        }

        cornerRot_local = Quaternion.LookRotation(cornerFacingDirection);

        // Apply forward offset along the corner's facing direction
        Vector3 localOffset = Vector3.forward * _settings.cornerElementForwardOffset; // Z-forward in corner's local space
        Vector3 worldSpaceOffsetFromCornerPoint = cornerRot_local * localOffset;
        cornerPos_local = p2_current_local + worldSpaceOffsetFromCornerPoint;
    }
}