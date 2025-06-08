// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
// This script provides utility methods for 2D polygon and geometric calculations.
//

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using MIConvexHull;

/// <summary>
/// A static utility class for 2D polygon and geometric calculations, primarily using Vector2.
/// Includes methods for calculating circumcenters, ordering vertices, offsetting, and validation.
/// </summary>
public static class PolygonUtils
{
    /// <summary>
    /// Calculates the 2D circumcenter of a triangle defined by three IVertex points.
    /// The Z-coordinate is ignored.
    /// </summary>
    /// <returns>A double array [x, y] for the circumcenter, or null if points are collinear.</returns>
    public static double[] CalculateCircumcenter(IVertex p1, IVertex p2, IVertex p3)
    {
        double p1x = p1.Position[0], p1y = p1.Position[1];
        double p2x = p2.Position[0], p2y = p2.Position[1];
        double p3x = p3.Position[0], p3y = p3.Position[1];

        // The determinant D is twice the signed area of the triangle. If D is near zero, the points are collinear.
        double D = 2.0 * (p1x * (p2y - p3y) + p2x * (p3y - p1y) + p3x * (p1y - p2y));
        if (System.Math.Abs(D) < GeometryConstants.HighPrecisionEpsilon)
        {
            return null; // Points are collinear, no unique circumcenter.
        }

        // Use squared lengths and Cartesian formula to find the circumcenter U = (Ux, Uy).
        double p1Sq = p1x * p1x + p1y * p1y;
        double p2Sq = p2x * p2x + p2y * p2y;
        double p3Sq = p3x * p3x + p3y * p3y;

        double ux = (p1Sq * (p2y - p3y) + p2Sq * (p3y - p1y) + p3Sq * (p1y - p2y)) / D;
        double uy = (p1Sq * (p3x - p2x) + p2Sq * (p1x - p3x) + p3Sq * (p2x - p1x)) / D;

        return new double[] { ux, uy };
    }

    /// <summary>
    /// Orders a list of 2D vertices polygonally (counter-clockwise) around a given center point.
    /// </summary>
    /// <param name="vertices">The list of Vector2 vertices to order.</param>
    /// <param name="centerPoint">The reference point to sort around.</param>
    /// <returns>A new list containing the sorted vertices.</returns>
    public static List<Vector2> OrderVerticesOfPolygon(List<Vector2> vertices, Vector2 centerPoint)
    {
        if (vertices == null || vertices.Count < 3) return vertices;

        var sortedVertices = new List<Vector2>(vertices);

        // Sort vertices by the angle they make with the center point using Atan2.
        sortedVertices.Sort((v1, v2) =>
        {
            double angle1 = Mathf.Atan2(v1.y - centerPoint.y, v1.x - centerPoint.x);
            double angle2 = Mathf.Atan2(v2.y - centerPoint.y, v2.x - centerPoint.x);
            return angle1.CompareTo(angle2);
        });

        return sortedVertices;
    }

    /// <summary>
    /// Creates a new polygon by offsetting each vertex towards the centroid.
    /// A positive distance shrinks the polygon, a negative distance expands it.
    /// </summary>
    /// <returns>A new list of offset vertices, or null if offsetting fails.</returns>
    public static List<Vector2> OffsetPolygonBasic(List<Vector2> polygon, float offsetDistance)
    {
        if (polygon == null || polygon.Count < 3) return null;
        if (Mathf.Abs(offsetDistance) < GeometryConstants.GeometricEpsilon) return new List<Vector2>(polygon);

        Vector2 centroid = Vector2.zero;
        foreach (var v in polygon) centroid += v;
        centroid /= polygon.Count;

        var offsetPolygon = new List<Vector2>(polygon.Count);
        foreach (var vertex in polygon)
        {
            Vector2 directionToCentroid = (centroid - vertex);
            // Safety check: If a vertex is too close to the centroid, offsetting is ill-defined.
            if (directionToCentroid.sqrMagnitude < GeometryConstants.GeometricEpsilonSqr) return null;

            // Safety check: If shrinking, ensure the offset distance doesn't cause the vertex to cross the centroid.
            if (offsetDistance > 0 && directionToCentroid.magnitude < offsetDistance) return null;

            offsetPolygon.Add(vertex + directionToCentroid.normalized * offsetDistance);
        }

        return offsetPolygon;
    }

    /// <summary>
    /// Calculates the area of a 2D polygon using the Shoelace formula.
    /// The result is always non-negative.
    /// </summary>
    public static float CalculatePolygonArea(List<Vector2> vertices)
    {
        if (vertices == null || vertices.Count < 3) return 0f;

        float area = 0f;
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector2 p1 = vertices[i];
            Vector2 p2 = vertices[(i + 1) % vertices.Count]; // Wrap around for the last segment.
            area += (p1.x * p2.y) - (p2.x * p1.y);
        }

        return Mathf.Abs(area * 0.5f);
    }

    /// <summary>
    /// Validates if a 2D polygon plot meets minimum geometric criteria for visual quality.
    /// </summary>
    /// <param name="plotVertices">The list of ordered vertices defining the polygon.</param>
    /// <param name="minSideLength">Minimum allowed length for any side.</param>
    /// <param name="minAngleDegrees">Minimum allowed interior angle at any vertex (in degrees).</param>
    /// <param name="minArea">Minimum allowed area for the polygon.</param>
    /// <returns>True if the plot meets all criteria, false otherwise.</returns>
    public static bool ValidatePlotGeometry(List<Vector2> plotVertices, float minSideLength, float minAngleDegrees, float minArea)
    {
        if (plotVertices == null || plotVertices.Count < 3) return false;

        // 1. Check Area
        if (CalculatePolygonArea(plotVertices) < minArea) return false;

        // 2. Check each side and vertex
        int n = plotVertices.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 pCurr = plotVertices[i];
            Vector2 pNext = plotVertices[(i + 1) % n];
            Vector2 pPrev = plotVertices[(i + n - 1) % n];

            // Check side length (from current to next vertex).
            if (Vector2.Distance(pCurr, pNext) < minSideLength) return false;

            // Check interior angle (at the current vertex).
            float angle = Vector2.Angle(pPrev - pCurr, pNext - pCurr);
            if (angle < minAngleDegrees) return false;
        }

        return true;
    }

    /// <summary>
    /// Snaps all vertices of a 2D polygon to a grid and removes consecutive duplicates.
    /// </summary>
    /// <returns>A new list of snapped vertices. May be null or have <3 vertices if snapping causes degeneration.</returns>
    public static List<Vector2> SnapPolygonVertices2D(List<Vector2> polygonVertices, float snapSize, bool removeDuplicateConsecutive = true)
    {
        if (polygonVertices == null || polygonVertices.Count == 0 || snapSize <= GeometryConstants.GeometricEpsilon)
        {
            return polygonVertices;
        }

        var snappedVertices = polygonVertices.Select(v => SnapVertexPosition2D(v, snapSize)).ToList();

        if (removeDuplicateConsecutive)
        {
            return RemoveConsecutiveDuplicates(snappedVertices);
        }

        return snappedVertices;
    }

    /// <summary>
    /// Snaps a 2D vector to the nearest point on a grid.
    /// </summary>
    public static Vector2 SnapVertexPosition2D(Vector2 vertexPos, float snapSize)
    {
        if (snapSize <= GeometryConstants.GeometricEpsilon) return vertexPos;
        return new Vector2(
            Mathf.Round(vertexPos.x / snapSize) * snapSize,
            Mathf.Round(vertexPos.y / snapSize) * snapSize
        );
    }

    /// <summary>
    /// Helper to filter a list of vertices, removing any vertex that is too close to the one preceding it.
    /// </summary>
    private static List<Vector2> RemoveConsecutiveDuplicates(List<Vector2> vertices)
    {
        if (vertices.Count < 2) return vertices;

        var uniqueVertices = new List<Vector2> { vertices[0] };

        // Remove consecutive duplicates from the main list.
        for (int i = 1; i < vertices.Count; i++)
        {
            if (Vector2.SqrMagnitude(vertices[i] - uniqueVertices.Last()) > GeometryConstants.GeometricEpsilonSqr)
            {
                uniqueVertices.Add(vertices[i]);
            }
        }

        // After cleaning, check if the first and last vertices are now the same.
        if (uniqueVertices.Count > 2 && Vector2.SqrMagnitude(uniqueVertices.Last() - uniqueVertices.First()) < GeometryConstants.GeometricEpsilonSqr)
        {
            uniqueVertices.RemoveAt(uniqueVertices.Count - 1);
        }

        return uniqueVertices;
    }
}