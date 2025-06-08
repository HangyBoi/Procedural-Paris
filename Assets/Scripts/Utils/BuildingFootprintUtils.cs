// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
// This script provides static utility methods for geometric calculations on 3D building footprints.
//

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A static utility class for geometric calculations specific to 3D building footprints,
/// typically operating on the XZ plane.
/// </summary>
public static class BuildingFootprintUtils
{
    /// <summary>
    /// Calculates the signed area of a polygon on the XZ plane using the Shoelace formula.
    /// The sign indicates the winding order: positive for Counter-Clockwise (CCW), negative for Clockwise (CW).
    /// </summary>
    /// <param name="vertexData">List of PolygonVertexData defining the footprint.</param>
    /// <returns>The signed area of the polygon.</returns>
    public static float CalculateSignedAreaXZ(List<PolygonVertexData> vertexData)
    {
        if (vertexData == null || vertexData.Count < 3) return 0f;

        float area = 0f;
        for (int i = 0; i < vertexData.Count; i++)
        {
            Vector3 p1 = vertexData[i].position;
            Vector3 p2 = vertexData[(i + 1) % vertexData.Count].position;

            // Shoelace formula component for the XZ plane: (x1*z2 - x2*z1)
            area += (p1.x * p2.z) - (p2.x * p1.z);
        }

        // The sum is twice the signed area.
        return area * 0.5f;
    }

    /// <summary>
    /// Calculates the signed area of a polygon on the XZ plane using the Shoelace formula.
    /// The sign indicates the winding order: positive for Counter-Clockwise (CCW), negative for Clockwise (CW).
    /// </summary>
    /// <param name="vertices">List of Vector3 points defining the polygon.</param>
    /// <returns>The signed area of the polygon.</returns>
    public static float CalculateSignedAreaXZ(List<Vector3> vertices)
    {
        if (vertices == null || vertices.Count < 3) return 0f;

        float area = 0f;
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 p1 = vertices[i];
            Vector3 p2 = vertices[(i + 1) % vertices.Count];

            area += (p1.x * p2.z) - (p2.x * p1.z);
        }

        return area * 0.5f;
    }

    /// <summary>
    /// Calculates the outward-facing normal for a side of a polygon on the XZ plane.
    /// The direction of "outward" is determined by the polygon's winding order.
    /// </summary>
    /// <param name="p1">Position of the side's starting vertex.</param>
    /// <param name="p2">Position of the side's ending vertex.</param>
    /// <param name="allVertexData">The complete footprint, used to determine winding order.</param>
    /// <returns>A normalized Vector3 representing the outward-facing normal on the XZ plane.</returns>
    public static Vector3 CalculateSideNormal(Vector3 p1, Vector3 p2, List<PolygonVertexData> allVertexData)
    {
        Vector3 sideDirection = (p2 - p1);
        sideDirection.y = 0; // Ensure the vector is flat on the XZ plane.

        // If the side has no length, return a default normal.
        if (sideDirection.sqrMagnitude < GeometryConstants.GeometricEpsilonSqr)
        {
            return Vector3.forward;
        }

        // Calculate a perpendicular vector. Rotating a 2D vector (x, z) by -90 degrees gives (z, -x).
        Vector3 initialNormal = new Vector3(sideDirection.z, 0, -sideDirection.x).normalized;

        // The initial normal assumes a CCW winding. If the winding is CW, we must flip it.
        float signedArea = CalculateSignedAreaXZ(allVertexData);
        if (signedArea < -GeometryConstants.GeometricEpsilon) // Clockwise winding
        {
            return -initialNormal;
        }

        // Otherwise, assume Counter-Clockwise (or zero area, which is degenerate).
        return initialNormal;
    }

    /// <summary>
    /// Calculates the geometric center (average of all vertex positions) of a polygon footprint.
    /// </summary>
    /// <param name="vertexData">List of PolygonVertexData defining the footprint.</param>
    /// <returns>The average Vector3 position of all vertices.</returns>
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