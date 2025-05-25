using UnityEngine;
using System.Collections.Generic;
using MIConvexHull;

/// <summary>
/// Utility class for 2D polygon and geometric calculations, primarily using Vector2.
/// Also includes methods interacting with MIConvexHull types.
/// </summary>
public static class PolygonUtils
{
    /// <summary>
    /// Calculates the 2D circumcenter of a triangle defined by three MIConvexHull IVertex points.
    /// The Z-coordinate of the IVertex positions is ignored.
    /// </summary>
    /// <param name="p1">The first vertex of the triangle.</param>
    /// <param name="p2">The second vertex of the triangle.</param>
    /// <param name="p3">The third vertex of the triangle.</param>
    /// <returns>A double array [x, y] representing the circumcenter, or null if points are collinear.</returns>
    public static double[] CalculateCircumcenter(IVertex p1, IVertex p2, IVertex p3)
    {
        // Extract 2D coordinates (assuming X and Y are the relevant dimensions)
        double ax = p1.Position[0];
        double ay = p1.Position[1];
        double bx = p2.Position[0];
        double by = p2.Position[1];
        double cx = p3.Position[0];
        double cy = p3.Position[1];

        // Squared lengths from origin (used in the formula)
        double aSq = ax * ax + ay * ay;
        double bSq = bx * bx + by * by;
        double cSq = cx * cx + cy * cy;

        // Denominator D for the circumcenter formula.
        // This is related to twice the signed area of the triangle. If D is near zero, points are collinear.
        double D = 2.0 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));

        if (System.Math.Abs(D) < GeometryConstants.HighPrecisionEpsilon) // Using a specific epsilon for this critical check
        {
            return null; // Points are collinear, no unique circumcircle
        }

        // The Cartesian coordinates numerators (taken from Wikipedia) for X and Y of the circumcenter U = (Ux, Uy) are:
        double ux = (aSq * (by - cy) + bSq * (cy - ay) + cSq * (ay - by)) / D;
        double uy = (aSq * (cx - bx) + bSq * (ax - cx) + cSq * (bx - ax)) / D;

        return new double[] { ux, uy };
    }

    /// <summary>
    /// Orders a list of 2D vertices polygonally (counter-clockwise) around a given center point.
    /// </summary>
    /// <param name="vertices">The list of Vector2 vertices to order.</param>
    /// <param name="centerPoint">The reference point around which to sort the vertices.</param>
    /// <returns>A new list containing the sorted vertices, or the original list if fewer than 3 vertices.</returns>
    public static List<Vector2> OrderVerticesOfPolygon(List<Vector2> vertices, Vector2 centerPoint) // Changed centerPoint to Vector2
    {
        if (vertices == null || vertices.Count < 3)
        {
            return vertices; // Not enough vertices to form a meaningful polygon or to sort
        }

        // Create a new list to avoid modifying the original if it's passed around elsewhere
        List<Vector2> sortedVertices = new(vertices);

        // Sort vertices by the angle they make with the centerPoint.
        // Mathf.Atan2(y, x) returns the angle in radians between the positive X-axis and the point (x, y).
        sortedVertices.Sort((v1, v2) =>
        {
            double angle1 = Mathf.Atan2(v1.y - centerPoint.y, v1.x - centerPoint.x);
            double angle2 = Mathf.Atan2(v2.y - centerPoint.y, v2.x - centerPoint.x);
            return angle1.CompareTo(angle2); // Sorts in ascending order of angle (CCW)
        });

        return sortedVertices;
    }

    /// <summary>
    /// Shrinks a 2D polygon by moving its vertices towards its centroid by a specified distance.
    /// This is a basic shrinking method and may not preserve shape perfectly or handle self-intersections.
    /// </summary>
    /// <param name="polygon">The list of Vector2 vertices defining the polygon.</param>
    /// <param name="distance">The distance to shrink inwards. Must be positive.</param>
    /// <returns>A new list of shrunk vertices, or null if shrinking fails (e.g., distance too large, degenerate polygon).</returns>
    public static List<Vector2> ShrinkPolygonBasic(List<Vector2> polygon, float distance)
    {
        if (polygon == null || polygon.Count < 3) return null;
        if (distance <= GeometryConstants.GeometricEpsilon) return new List<Vector2>(polygon); // No shrinking needed or negligible distance

        // Calculate centroid (average of vertices)
        Vector2 centroid = Vector2.zero;
        foreach (var v in polygon) centroid += v;
        centroid /= polygon.Count;

        List<Vector2> shrunkPolygon = new List<Vector2>();
        for (int i = 0; i < polygon.Count; ++i)
        {
            Vector2 vertex = polygon[i];
            Vector2 directionToCentroid = centroid - vertex;

            // Check for degenerate case: vertex is at centroid
            if (directionToCentroid.sqrMagnitude < GeometryConstants.GeometricEpsilon * GeometryConstants.GeometricEpsilon)
            {
                // This implies a very small or degenerate polygon, or centroid coincides with a vertex.
                // Returning null is safer as shrinking is ill-defined.
                return null;
            }

            float distanceToCentroid = directionToCentroid.magnitude;

            // Check if the shrink distance is too large
            if (distanceToCentroid < distance - GeometryConstants.GeometricEpsilon) // Allow slight negative if distanceToCentroid is almost equal to distance
            {
                // Shrinking by this distance would cause the vertex to cross the centroid or invert.
                return null;
            }
            shrunkPolygon.Add(vertex + directionToCentroid.normalized * distance);
        }

        // Check if shrunk polygon is still valid (e.g. not self-intersecting, still has area)
        // For basic shrink, just check vertex count.
        if (shrunkPolygon.Count < 3) return null;

        return shrunkPolygon;
    }

    /// <summary>
    /// Calculates the area of a 2D polygon defined by a list of Vector2 vertices.
    /// Uses the shoelace formula. The result is always non-negative.
    /// </summary>
    /// <param name="vertices">The list of Vector2 vertices defining the polygon, assumed to be ordered.</param>
    /// <returns>The absolute area of the polygon.</returns>
    public static float CalculatePolygonArea(List<Vector2> vertices)
    {
        if (vertices == null || vertices.Count < 3) return 0f;

        float area = 0f;
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector2 p1 = vertices[i];
            Vector2 p2 = vertices[(i + 1) % vertices.Count]; // Wrap around for the last segment

            // Shoelace formula component: (x1*y2 - x2*y1)
            area += (p1.x * p2.y) - (p2.x * p1.y);
        }
        // The sum is twice the signed area. Absolute value gives the geometric area.
        return Mathf.Abs(area / 2.0f);
    }

    /// <summary>
    /// Validates a 2D polygon plot based on minimum side length, minimum interior angle, and minimum area for VISUALLY EFFECTIVE FOOTPRINT GENERATION
    /// </summary>
    /// <param name="plotVertices">The list of Vector2 vertices defining the polygon, assumed to be ordered.</param>
    /// <param name="minSideLength">Minimum allowed length for any side.</param>
    /// <param name="minAngleDegrees">Minimum allowed interior angle at any vertex (in degrees).</param>
    /// <param name="minArea">Minimum allowed area for the polygon.</param>
    /// <returns>True if the plot meets all criteria, false otherwise.</returns>
    public static bool ValidatePlotGeometry(List<Vector2> plotVertices, float minSideLength, float minAngleDegrees, float minArea)
    {
        if (plotVertices == null || plotVertices.Count < 3)
        {
            return false; // Not a valid polygon
        }

        // 1. Check Area
        float area = CalculatePolygonArea(plotVertices);
        if (area < minArea - GeometryConstants.GeometricEpsilon) // Use epsilon for float comparison
        {
            return false; // Area too small
        }

        int n = plotVertices.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 pCurr = plotVertices[i];
            Vector2 pNext = plotVertices[(i + 1) % n];
            Vector2 pPrev = plotVertices[(i + n - 1) % n]; // Previous vertex (wraps around)

            // 2. Check Side Length (for side pCurr to pNext)
            float sideLength = Vector2.Distance(pCurr, pNext);
            if (sideLength < minSideLength - GeometryConstants.GeometricEpsilon)
            {
                return false; // Side too short
            }

            // 3. Check Interior Angle (at pCurr, formed by edges pPrev-pCurr and pNext-pCurr)
            // Vector from current vertex to previous vertex
            Vector2 edge1 = pPrev - pCurr;
            // Vector from current vertex to next vertex
            Vector2 edge2 = pNext - pCurr;

            // Vector2.Angle returns the unsigned angle between 0 and 180 degrees.
            // This is suitable for checking interior angles of a simple polygon
            // if the vertices are ordered (e.g., CCW).
            float angle = Vector2.Angle(edge1, edge2);

            if (angle < minAngleDegrees - GeometryConstants.GeometricEpsilon)
            {
                return false; // Angle too acute
            }

            // Optional: Could also check for reflex angles (angle > 180) if the polygon is not guaranteed to be convex,
            // or very flat angles (angle close to 180) if those are also undesirable.
            // For typical Voronoi cells (convex), this check for acute angles is primary.
        }
        return true; // All checks passed
    }
}