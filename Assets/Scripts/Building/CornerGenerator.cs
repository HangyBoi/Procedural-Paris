// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
//  This script is responsible for generating corner elements (e.g., chimneys, quoins) of a building in a procedural generation context.
//

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Responsible for generating corner elements (e.g., chimneys, quoins) of the building.
/// </summary>
public class CornerGenerator
{
    private readonly PolygonBuildingGenerator _settings;
    private readonly List<PolygonVertexData> _vertexData;
    private readonly BuildingStyleSO _buildingStyle;
    private readonly GeneratedBuildingElements _elementsStore;

    public CornerGenerator(PolygonBuildingGenerator settings, List<PolygonVertexData> vertexData, BuildingStyleSO buildingStyle, GeneratedBuildingElements elementsStore)
    {
        _settings = settings;
        _vertexData = vertexData;
        _buildingStyle = buildingStyle;
        _elementsStore = elementsStore;
    }

    /// <summary>
    /// Generates all corner elements for the building and parents them to a root transform.
    /// Iterates through vertices marked for corner element placement.
    /// </summary>
    /// <param name="cornerRoot">The parent transform for all generated corner elements.</param>
    public void GenerateAllCorners(Transform cornerRoot)
    {
        if (_vertexData.Count < 3 || _buildingStyle == null) return;

        bool hasBodyPrefabs = _buildingStyle.defaultChimneyBodyPrefabs?.Count > 0;
        bool hasCapPrefabs = _settings.useCornerCaps && _buildingStyle.defaultChimneyCapPrefabs?.Count > 0;

        // Exit if there are no prefabs to instantiate.
        if (!hasBodyPrefabs && !hasCapPrefabs) return;

        for (int i = 0; i < _vertexData.Count; i++)
        {
            if (_vertexData[i].addCornerElement)
            {
                GenerateCornerElementAtIndex(i, cornerRoot, hasBodyPrefabs, hasCapPrefabs);
            }
        }
    }

    /// <summary>
    /// Generates a single corner element (body segments and a cap) at a specified vertex index.
    /// </summary>
    /// <param name="index">The vertex index for the corner element.</param>
    /// <param name="cornerRoot">The parent transform for the new element.</param>
    /// <param name="hasBodyPrefabs">Whether body prefabs are available.</param>
    /// <param name="hasCapPrefabs">Whether cap prefabs are available and enabled.</param>
    private void GenerateCornerElementAtIndex(int index, Transform cornerRoot, bool hasBodyPrefabs, bool hasCapPrefabs)
    {
        // Get vertex positions to define the corner.
        int prevIndex = (index + _vertexData.Count - 1) % _vertexData.Count;
        Vector3 currentPos_local = _vertexData[index].position;
        Vector3 prevPos_local = _vertexData[prevIndex].position;
        Vector3 nextPos_local = _vertexData[(index + 1) % _vertexData.Count].position;

        // Calculate the position and orientation for the corner element.
        CalculateCornerTransform(prevPos_local, currentPos_local, nextPos_local,
                                 out Vector3 cornerBasePos_local, out Quaternion cornerRotation_local);

        float currentElementBaseY = 0f;
        float bodyPrefabHeight = _settings.floorHeight;

        // Calculate the total number of body segments needed to match the building's height.
        int totalBodySegments = 1 + _settings.middleFloors; // Ground floor + middle floors.
        if (_settings.useMansardFloor && _settings.mansardRiseHeight > GeometryConstants.GeometricEpsilon) totalBodySegments++;
        if (_settings.useAtticFloor && _settings.atticRiseHeight > GeometryConstants.GeometricEpsilon) totalBodySegments++;

        // --- Instantiate Corner Element Bodies ---
        if (hasBodyPrefabs)
        {
            float pivotOffsetY = bodyPrefabHeight * 0.5f;

            for (int i = 0; i < totalBodySegments; i++)
            {
                Vector3 segmentPivotPos_local = cornerBasePos_local + Vector3.up * (currentElementBaseY + pivotOffsetY);
                Vector3 segmentPivotPos_world = _settings.transform.TransformPoint(segmentPivotPos_local);
                Quaternion cornerRotation_world = _settings.transform.rotation * cornerRotation_local;

                GameObject bodyInstance = PrefabInstantiator.InstantiateSegment(
                    _buildingStyle.defaultChimneyBodyPrefabs, segmentPivotPos_world, cornerRotation_world,
                    cornerRoot, _settings.nominalFacadeWidth, true, false, _settings.nominalFacadeWidth);

                if (bodyInstance != null) _elementsStore.allCornerBodies.Add(bodyInstance);

                currentElementBaseY += bodyPrefabHeight;
            }
        }
        else
        {
            // If no body prefabs, calculate the height where the cap should be placed.
            currentElementBaseY = bodyPrefabHeight * totalBodySegments;
        }

        // --- Instantiate Corner Cap ---
        if (hasCapPrefabs)
        {
            // Place the cap on top of all body segments.
            Vector3 capPosition_local = cornerBasePos_local + Vector3.up * currentElementBaseY;
            Vector3 capPosition_world = _settings.transform.TransformPoint(capPosition_local);
            Quaternion cornerRotation_world = _settings.transform.rotation * cornerRotation_local;

            GameObject capInstance = PrefabInstantiator.InstantiateSegment(
                _buildingStyle.defaultChimneyCapPrefabs, capPosition_world, cornerRotation_world,
                cornerRoot, _settings.nominalFacadeWidth, true, false, _settings.nominalFacadeWidth);

            ProcessPrefabRandomizer(capInstance);
            if (capInstance != null) _elementsStore.allCornerCaps.Add(capInstance);
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
    /// Calculates the local position and rotation for a corner element.
    /// The rotation bisects the angle of the corner, and an offset can be applied.
    /// </summary>
    private void CalculateCornerTransform(Vector3 p1_prev_local, Vector3 p2_current_local, Vector3 p3_next_local,
                                          out Vector3 cornerPos_local, out Quaternion cornerRot_local)
    {
        // Get the normals of the two sides that form the corner.
        Vector3 sideNormalPrev = BuildingFootprintUtils.CalculateSideNormal(p1_prev_local, p2_current_local, _vertexData);
        Vector3 sideNormalNext = BuildingFootprintUtils.CalculateSideNormal(p2_current_local, p3_next_local, _vertexData);

        // The corner should face the average of the two side normals.
        Vector3 cornerFacingDirection = (sideNormalPrev + sideNormalNext).normalized;

        // Handle collinear or degenerate cases where normals might cancel out (e.g., a 180-degree corner).
        if (cornerFacingDirection.sqrMagnitude < GeometryConstants.GeometricEpsilonSqr)
        {
            // Fallback to one of the side normals if they are valid.
            if (sideNormalNext.sqrMagnitude > GeometryConstants.GeometricEpsilonSqr)
                cornerFacingDirection = sideNormalNext;
            else if (sideNormalPrev.sqrMagnitude > GeometryConstants.GeometricEpsilonSqr)
                cornerFacingDirection = sideNormalPrev;
            else
            {
                // If all else fails, compute a perpendicular direction from a side vector.
                Vector3 dir = (p3_next_local - p2_current_local).normalized;
                if (dir.sqrMagnitude < GeometryConstants.GeometricEpsilonSqr)
                    dir = (p2_current_local - p1_prev_local).normalized;

                cornerFacingDirection = dir.sqrMagnitude > GeometryConstants.GeometricEpsilonSqr
                    ? new Vector3(dir.z, 0, -dir.x)
                    : Vector3.forward;
            }
        }

        cornerRot_local = Quaternion.LookRotation(cornerFacingDirection);

        // Apply an optional forward offset from the corner vertex.
        Vector3 localOffsetVector = Vector3.forward * _settings.cornerElementForwardOffset;
        Vector3 worldSpaceOffset = cornerRot_local * localOffsetVector;
        cornerPos_local = p2_current_local + worldSpaceOffset;
    }
}