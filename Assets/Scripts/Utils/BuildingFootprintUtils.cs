using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Utility class for geometric calculations specific to 3D building footprints
/// defined by PolygonVertexData, typically operating on the XZ plane.
/// </summary>
public static class BuildingFootprintUtils
{
    /// <summary>
    /// Calculates the signed area of a polygon defined by PolygonVertexData on the XZ plane.
    /// A positive area indicates Counter-Clockwise (CCW) vertex order,
    /// a negative area indicates Clockwise (CW) order.
    /// </summary>
    /// <param name="vertexData">List of PolygonVertexData defining the footprint.</param>
    /// <returns>The signed area of the polygon on the XZ plane.</returns>
    public static float CalculateSignedAreaXZ(List<PolygonVertexData> vertexData)
    {
        if (vertexData == null || vertexData.Count < 3) return 0f;

        float area = 0f;
        for (int i = 0; i < vertexData.Count; i++)
        {
            Vector3 p1 = vertexData[i].position;
            Vector3 p2 = vertexData[(i + 1) % vertexData.Count].position;

            // Shoelace formula component for XZ plane: (x1*z2 - x2*z1)
            area += (p1.x * p2.z) - (p2.x * p1.z);
        }
        // The sum is twice the signed area.
        return area / 2.0f;
    }

    /// <summary>
    /// Calculates the outward-facing normal for a side of a polygon on the XZ plane.
    /// The polygon is defined by PolygonVertexData. Winding order determines "outward".
    /// </summary>
    /// <param name="p1_pos">Position of the start vertex of the side (on XZ plane).</param>
    /// <param name="p2_pos">Position of the end vertex of the side (on XZ plane).</param>
    /// <param name="fullFootprintVertexData">The complete list of polygon vertices, used to determine winding order via signed area.</param>
    /// <returns>The outward-facing normal vector (Vector3 with Y=0).</returns>
    public static Vector3 CalculateSideNormal(Vector3 p1, Vector3 p2, List<PolygonVertexData> allVertexData)
    {
        Vector3 sideDirection = (p2 - p1);
        sideDirection.y = 0; // Project onto XZ plane

        if (sideDirection.sqrMagnitude < GeometryConstants.GeometricEpsilon * GeometryConstants.GeometricEpsilon)
        {
            // If side length is negligible, default normal (e.g., if p1 and p2 are coincident)
            return Vector3.forward;
        }
        sideDirection.Normalize();

        // Perpendicular vector on XZ plane (right-hand rule with Y-up gives normal to the right of direction)
        Vector3 initialNormal = new Vector3(sideDirection.z, 0, -sideDirection.x); // Equivalent to Cross(sideDirection, Vector3.up) and normalizing

        float signedArea = CalculateSignedAreaXZ(allVertexData);

        if (signedArea < -GeometryConstants.GeometricEpsilon) // Clockwise
        {
            return -initialNormal;
        }
        else // Counter-Clockwise or zero area
        {
            return initialNormal;
        }
    }

    /// <summary>
    /// Calculates the geometric center (average of vertex positions) of a polygon footprint.
    /// </summary>
    /// <param name="vertexData">List of PolygonVertexData defining the footprint.</param>
    /// <returns>The average Vector3 position of the vertices.</returns>
    public static Vector3 CalculatePolygonCentroid(List<PolygonVertexData> vertexData)
    {
        if (vertexData == null || vertexData.Count == 0) return Vector3.zero;

        Vector3 center = Vector3.zero;
        foreach (var vd in vertexData)
        {
            center += vd.position;
        }
        return center / vertexData.Count;
    }
}