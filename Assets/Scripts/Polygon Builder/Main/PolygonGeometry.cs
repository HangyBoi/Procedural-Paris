using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Utility class for geometric calculations specific to the building's polygon footprint.
/// </summary>
public static class PolygonGeometry
{
    /// <summary>
    /// Calculates the signed area of the polygon.
    /// Positive for counter-clockwise (CCW), negative for clockwise (CW) vertex order on the XZ plane.
    /// </summary>
    public static float CalculateSignedArea(List<PolygonVertexData> vertexData)
    {
        if (vertexData == null || vertexData.Count < 3) return 0f;
        float area = 0f;
        for (int i = 0; i < vertexData.Count; i++)
        {
            Vector3 p1 = vertexData[i].position;
            Vector3 p2 = vertexData[(i + 1) % vertexData.Count].position;
            // Using X and Z coordinates for 2D area calculation on the ground plane
            area += (p1.x * p2.z) - (p2.x * p1.z);
        }
        return area / 2.0f;
    }

    /// <summary>
    /// Calculates the outward-facing normal for a side of the polygon on the XZ plane.
    /// Takes into account the polygon's winding order to ensure the normal points outwards.
    /// </summary>
    /// <param name="p1">Start vertex of the side (local space).</param>
    /// <param name="p2">End vertex of the side (local space).</param>
    /// <param name="allVertexData">The complete list of polygon vertices, used to determine winding order.</param>
    /// <returns>The outward-facing normal vector.</returns>
    public static Vector3 CalculateSideNormal(Vector3 p1, Vector3 p2, List<PolygonVertexData> allVertexData)
    {
        Vector3 sideDirection = (p2 - p1);
        sideDirection.y = 0; // Project onto XZ plane

        if (sideDirection.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon)
        {
            // If side length is negligible, default normal (e.g., if p1 and p2 are coincident)
            return Vector3.forward;
        }
        sideDirection.Normalize();

        // Perpendicular vector on XZ plane (right-hand rule with Y-up gives normal to the right of direction)
        Vector3 initialNormal = new Vector3(sideDirection.z, 0, -sideDirection.x); // Equivalent to Cross(sideDirection, Vector3.up) and normalizing

        float signedArea = CalculateSignedArea(allVertexData);

        if (signedArea < -GeometryUtils.Epsilon) // Clockwise
        {
            return -initialNormal;
        }
        else // Counter-Clockwise or zero area
        {
            return initialNormal;
        }
    }

    /// <summary>
    /// Calculates the geometric center (average of vertex positions) of the polygon.
    /// </summary>
    public static Vector3 CalculatePolygonCenter(List<PolygonVertexData> vertexData)
    {
        Vector3 center = Vector3.zero;
        if (vertexData != null && vertexData.Count > 0)
        {
            foreach (var vd in vertexData) center += vd.position;
            center /= vertexData.Count;
        }
        return center;
    }
}