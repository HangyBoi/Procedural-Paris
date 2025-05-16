using UnityEngine;
using System.Collections.Generic;

public class RoofWindowGenerator
{
    private readonly PolygonBuildingGeneratorMain _settings;
    private readonly List<PolygonVertexData> _vertexData;
    private readonly List<PolygonSideData> _sideData;
    private readonly BuildingStyleSO _buildingStyle;

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

        GameObject mansardWindowsParent = null;
        if (_settings.placeMansardWindows && generatedRoofs.MansardRoofObject != null)
        {
            mansardWindowsParent = new GameObject("Mansard Windows");
            mansardWindowsParent.transform.SetParent(roofWindowsRoot, false);
        }

        GameObject atticWindowsParent = null;
        if (_settings.placeAtticWindows && generatedRoofs.AtticRoofObject != null)
        {
            atticWindowsParent = new GameObject("Attic Windows");
            atticWindowsParent.transform.SetParent(roofWindowsRoot, false);
        }

        for (int sideIdx = 0; sideIdx < _vertexData.Count; sideIdx++)
        {
            Vector3 p1_local = _vertexData[sideIdx].position;
            Vector3 p2_local = _vertexData[(sideIdx + 1) % _vertexData.Count].position;
            float sideDistance = Vector3.Distance(p1_local, p2_local);

            if (sideDistance < GeometryUtils.Epsilon) continue;

            int numSegments = CalculateNumSegments(sideDistance);
            float actualSegmentWidth = CalculateActualSegmentWidth(sideDistance, numSegments);

            for (int segmentIdx = 0; segmentIdx < numSegments; segmentIdx++)
            {
                if (_settings.placeMansardWindows && _settings.useMansardFloor && mansardWindowsParent != null && generatedRoofs.MansardRoofObject != null)
                {
                    PlaceWindowsOnSpecificRoof(
                        "Mansard",
                        generatedRoofs.MansardRoofObject,
                        sideIdx, segmentIdx, numSegments, actualSegmentWidth,
                        mansardWindowsParent.transform
                    );
                }

                if (_settings.placeAtticWindows && _settings.useAtticFloor && atticWindowsParent != null && generatedRoofs.AtticRoofObject != null)
                {
                    PlaceWindowsOnSpecificRoof(
                        "Attic",
                        generatedRoofs.AtticRoofObject,
                        sideIdx, segmentIdx, numSegments, actualSegmentWidth,
                        atticWindowsParent.transform
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
        Transform windowsParent)
    {
        MeshFilter targetRoofMF = roofObject.GetComponent<MeshFilter>();
        if (targetRoofMF == null || targetRoofMF.sharedMesh == null)
        {
            Debug.LogError($"{roofTypeName} GameObject for side {sideIndex} does not have a MeshFilter or mesh.");
            return;
        }
        Mesh targetRoofMesh = targetRoofMF.sharedMesh;
        Transform targetRoofTransform = roofObject.transform;

        List<GameObject> defaultWindowPrefabs = null;
        if (roofTypeName == "Mansard") defaultWindowPrefabs = _buildingStyle.defaultMansardWindowPrefabs;
        else if (roofTypeName == "Attic") defaultWindowPrefabs = _buildingStyle.defaultAtticWindowPrefabs;

        List<GameObject> windowPrefabsToUse = defaultWindowPrefabs;
        if (sideIndex < _sideData.Count && _sideData[sideIndex].useCustomStyle && _sideData[sideIndex].sideStylePreset != null)
        {
            SideStyleSO customStyle = _sideData[sideIndex].sideStylePreset;
            List<GameObject> customPrefabs = null;
            if (roofTypeName == "Mansard") customPrefabs = customStyle.mansardFloorPrefabs;
            else if (roofTypeName == "Attic") customPrefabs = customStyle.atticFloorPrefabs;

            if (customPrefabs != null && customPrefabs.Count > 0) windowPrefabsToUse = customPrefabs;
        }

        if (windowPrefabsToUse == null || windowPrefabsToUse.Count == 0) return;

        int N = _vertexData.Count;
        if (targetRoofMesh.vertexCount != N * 2)
        {
            Debug.LogError($"{roofTypeName} mesh vertex count ({targetRoofMesh.vertexCount}) on side {sideIndex} is incorrect for strip structure (expected {N * 2}). Skipping window placement for this side/roof.");
            return;
        }

        // Get the four corner vertices of the ENTIRE roof panel for the current side, in mesh-local space
        Vector3 vOuter1_meshLocal = targetRoofMesh.vertices[sideIndex];
        Vector3 vOuter2_meshLocal = targetRoofMesh.vertices[(sideIndex + 1) % N];
        Vector3 vInner1_meshLocal = targetRoofMesh.vertices[sideIndex + N]; // Inner vertex corresponding to vOuter1
        Vector3 vInner2_meshLocal = targetRoofMesh.vertices[((sideIndex + 1) % N) + N]; // Inner vertex corresponding to vOuter2

        // Transform these corner vertices to world space
        Vector3 vOuter1_world = targetRoofTransform.TransformPoint(vOuter1_meshLocal);
        Vector3 vOuter2_world = targetRoofTransform.TransformPoint(vOuter2_meshLocal);
        Vector3 vInner1_world = targetRoofTransform.TransformPoint(vInner1_meshLocal);
        Vector3 vInner2_world = targetRoofTransform.TransformPoint(vInner2_meshLocal);


        // Calculate the facade normal (outward direction of the base building wall for this side)
        Vector3 p1_base_local = _vertexData[sideIndex].position;
        Vector3 p2_base_local = _vertexData[(sideIndex + 1) % N].position;
        Vector3 facadeNormal_local = PolygonGeometry.CalculateSideNormal(p1_base_local, p2_base_local, _vertexData);
        Vector3 facadeNormal_world = _settings.transform.TransformDirection(facadeNormal_local);

        // Calculate the tangent direction along the outer (bottom) edge of the roof side
        Vector3 sideTangent_world_raw = vOuter2_world - vOuter1_world;
        Vector3 sideTangent_world;
        if (sideTangent_world_raw.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon)
        {
            // Fallback if outer edge has zero length (degenerate)
            sideTangent_world = targetRoofTransform.right; // Assume roof object's right
        }
        else
        {
            sideTangent_world = sideTangent_world_raw.normalized;
        }

        // Calculate a CONSISTENT slope direction for the ENTIRE roof side
        // This vector points from the midpoint of the outer edge to the midpoint of the inner edge
        Vector3 midOuterEdge_world = (vOuter1_world + vOuter2_world) * 0.5f;
        Vector3 midInnerEdge_world = (vInner1_world + vInner2_world) * 0.5f;
        Vector3 consistentSlopeDirection_world_raw = midInnerEdge_world - midOuterEdge_world;
        Vector3 consistentSlopeDirection_world;

        if (consistentSlopeDirection_world_raw.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon)
        {
            // Fallback if midpoints are coincident (e.g., roof side is flat or collapsed)
            // Use the roof object's local up, transformed to world space.
            consistentSlopeDirection_world = targetRoofTransform.up;
        }
        else
        {
            consistentSlopeDirection_world = consistentSlopeDirection_world_raw.normalized;
        }

        // Calculate a CONSISTENT surface normal for the ENTIRE roof side (for window inset)
        Vector3 consistentActualRoofSurfaceNormal_world_raw = Vector3.Cross(sideTangent_world, consistentSlopeDirection_world);
        Vector3 consistentActualRoofSurfaceNormal_world;
        if (consistentActualRoofSurfaceNormal_world_raw.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon)
        {
            // Fallback if sideTangent and consistentSlopeDirection are parallel (e.g. vertical roof face, sideTangent is vertical)
            // This indicates a degenerate roof panel. Default to roof object's up.
            consistentActualRoofSurfaceNormal_world = targetRoofTransform.up;
        }
        else
        {
            consistentActualRoofSurfaceNormal_world = consistentActualRoofSurfaceNormal_world_raw.normalized;
        }

        // Ensure the normal generally points "outward and upward"
        // A good reference is the average of the facade's outward normal and the roof's up direction
        Vector3 referenceOutwardUp = (facadeNormal_world + targetRoofTransform.up).normalized;
        if (referenceOutwardUp.sqrMagnitude > GeometryUtils.Epsilon && // Ensure reference is not zero
            Vector3.Dot(consistentActualRoofSurfaceNormal_world, referenceOutwardUp) < 0.0f)
        {
            consistentActualRoofSurfaceNormal_world = -consistentActualRoofSurfaceNormal_world;
        }


        // --- Calculate window position ---
        // Interpolate along the *edges of the entire roof side* to find the segment's specific outer/inner points
        float t_segment = (segmentIndexInSide + 0.5f) / totalSegmentsOnSide;
        Vector3 segmentOuterEdgeMidPt_world = Vector3.Lerp(vOuter1_world, vOuter2_world, t_segment);
        Vector3 segmentInnerEdgeMidPt_world = Vector3.Lerp(vInner1_world, vInner2_world, t_segment);

        // Anchor position is the midpoint of the segment's span on the roof surface
        Vector3 windowAnchorPos_world = (segmentOuterEdgeMidPt_world + segmentInnerEdgeMidPt_world) * 0.5f;

        // Final window position with inset using the consistent surface normal
        Vector3 windowPosition_world = windowAnchorPos_world + consistentActualRoofSurfaceNormal_world * _settings.roofWindowInset;

        // --- Calculate window rotation ---
        Vector3 windowUpVector = consistentSlopeDirection_world;
        // If facadeNormal and the chosen windowUpVector are parallel or anti-parallel,
        // LookRotation needs a different 'up' hint to avoid ambiguity. Use roof's transform.up.
        if (Vector3.Cross(facadeNormal_world, windowUpVector).sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon)
        {
            windowUpVector = targetRoofTransform.up;
        }
        Quaternion windowRotation_world = Quaternion.LookRotation(facadeNormal_world, windowUpVector);

        // --- END OF NEW/MODIFIED LOGIC ---


        GameObject prefab = windowPrefabsToUse[Random.Range(0, windowPrefabsToUse.Count)];
        if (prefab != null)
        {
            GameObject instance = Object.Instantiate(prefab, windowsParent);
            instance.transform.position = windowPosition_world;
            instance.transform.rotation = windowRotation_world;

            if (_settings.scaleRoofWindowsToFitSegment && _settings.nominalFacadeWidth > GeometryUtils.Epsilon)
            {
                Vector3 localScale = instance.transform.localScale;
                float scaleFactor = actualSegmentWidth / _settings.nominalFacadeWidth;
                instance.transform.localScale = new Vector3(localScale.x * scaleFactor, localScale.y, localScale.z);
            }
            instance.name = $"{roofTypeName}Window_{sideIndex}_{segmentIndexInSide}";
        }
    }
}