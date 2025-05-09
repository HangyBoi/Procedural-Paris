// GeometryUtilsAdj.cs
using UnityEngine;
using System.Collections.Generic;

public static class GeometryUtilsAdj
{
    public const float Epsilon = 1e-5f; // A small value for float comparisons

    public static bool LineLineIntersection(Vector3 line1Origin, Vector3 line1Direction,
                                            Vector3 line2Origin, Vector3 line2Direction,
                                            out Vector3 intersectionPoint)
    {
        // --- Placeholder: Implement 2D line intersection on XZ plane ---
        // This function should find the intersection of two lines defined by origin and direction.
        // It's crucial for calculating mitered corners for the roof offset.
        // Return true if intersection exists, false otherwise.
        // intersectionPoint = ...;

        // Simplified Example (not robust for all cases, especially parallel lines):
        Vector3 lineVec1 = line1Direction;
        Vector3 lineVec2 = line2Direction;
        Vector3 lineOrg1 = new Vector3(line1Origin.x, 0, line1Origin.z); // Project to XZ
        Vector3 lineOrg2 = new Vector3(line2Origin.x, 0, line2Origin.z); // Project to XZ

        Vector3 L1_End = lineOrg1 + lineVec1; // Assume direction is normalized and scale by a large number if not
        Vector3 L2_End = lineOrg2 + lineVec2;

        float denominator = (L1_End.x - lineOrg1.x) * (L2_End.z - lineOrg2.z) - (L1_End.z - lineOrg1.z) * (L2_End.x - lineOrg2.x);

        if (Mathf.Abs(denominator) < Epsilon) // Lines are parallel or collinear
        {
            intersectionPoint = Vector3.zero;
            return false;
        }

        float t = ((lineOrg1.x - lineOrg2.x) * (L2_End.z - lineOrg2.z) - (lineOrg1.z - lineOrg2.z) * (L2_End.x - lineOrg2.x)) / denominator;
        // float u = -((L1_End.x - lineOrg1.x) * (lineOrg1.z - lineOrg2.z) - (L1_End.z - lineOrg1.z) * (lineOrg1.x - lineOrg2.x)) / denominator;

        intersectionPoint = lineOrg1 + t * lineVec1;
        // Set Y to be consistent if needed, though this function primarily solves for XZ
        intersectionPoint.y = line1Origin.y; // Or average, or specific height. For roof, it's usually fixed.
        return true;
        // --- End Placeholder ---
    }

    public static bool TriangulatePolygonEarClipping(List<Vector3> polygonVertices, out List<int> triangles)
    {
        // --- Placeholder: Implement Ear Clipping triangulation algorithm ---
        // This function takes a list of 2D (or 3D on a plane) polygon vertices
        // and outputs a list of triangle indices for a simple polygon.
        // triangles = ...;
        triangles = new List<int>();
        if (polygonVertices == null || polygonVertices.Count < 3)
        {
            Debug.LogError("Triangulation failed: Not enough vertices.");
            return false;
        }

        List<Vector3> verts = new List<Vector3>(polygonVertices);
        List<int> indices = new List<int>();
        for (int i = 0; i < verts.Count; i++)
        {
            indices.Add(i);
        }

        int n = verts.Count;
        if (n < 3) return false;

        while (n > 3)
        {
            bool earFound = false;
            for (int i = 0; i < n; i++)
            {
                int prev = (i == 0) ? (n - 1) : (i - 1);
                int next = (i == n - 1) ? 0 : (i + 1);

                Vector3 p_prev = verts[indices[prev]];
                Vector3 p_curr = verts[indices[i]];
                Vector3 p_next = verts[indices[next]];

                // Check if vertex is convex (using 2D cross product on XZ plane)
                float crossProduct = (p_curr.x - p_prev.x) * (p_next.z - p_curr.z) -
                                     (p_curr.z - p_prev.z) * (p_next.x - p_curr.x);

                // Assuming CCW polygon, convex vertices have positive cross product.
                // If your polygon can be CW, you'll need to determine winding first.
                // For simplicity, this example might need adjustment for CW polygons or use signed area to determine winding.
                bool isConvex = crossProduct > Epsilon;


                if (isConvex)
                {
                    bool isEar = true;
                    for (int j = 0; j < n; j++)
                    {
                        if (j == prev || j == i || j == next) continue;
                        Vector3 pt = verts[indices[j]];
                        if (IsPointInTriangle(pt, p_prev, p_curr, p_next))
                        {
                            isEar = false;
                            break;
                        }
                    }

                    if (isEar)
                    {
                        triangles.Add(indices[prev]);
                        triangles.Add(indices[i]);
                        triangles.Add(indices[next]);

                        indices.RemoveAt(i);
                        n--;
                        earFound = true;
                        break;
                    }
                }
            }
            if (!earFound)
            {
                // Fallback for complex cases or if no ear found (could be due to collinear points, degeneracies, or CW winding issues)
                // A robust triangulation library would be better for production.
                // Simple fan for remaining polygon if stuck:
                // Debug.LogWarning("Ear clipping got stuck or polygon is complex. Using fallback for remaining points.");
                for (int i = 1; i < n - 1; i++)
                {
                    triangles.Add(indices[0]);
                    triangles.Add(indices[i]);
                    triangles.Add(indices[i + 1]);
                }
                break; // Exit while loop
            }
        }
        // Add the last triangle
        if (n == 3)
        {
            triangles.Add(indices[0]);
            triangles.Add(indices[1]);
            triangles.Add(indices[2]);
        }
        // Debug.LogWarning("TriangulatePolygonEarClipping is a basic implementation. Consider a robust library for complex polygons.");
        return triangles.Count > 0;
        // --- End Placeholder ---
    }

    // Helper for basic ear clipping (point in triangle test on XZ plane)
    private static bool IsPointInTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        // Project to XZ plane
        Vector2 p_xz = new Vector2(p.x, p.z);
        Vector2 a_xz = new Vector2(a.x, a.z);
        Vector2 b_xz = new Vector2(b.x, b.z);
        Vector2 c_xz = new Vector2(c.x, c.z);

        float s = a_xz.y * c_xz.x - a_xz.x * c_xz.y + (c_xz.y - a_xz.y) * p_xz.x + (a_xz.x - c_xz.x) * p_xz.y;
        float t = a_xz.x * b_xz.y - a_xz.y * b_xz.x + (a_xz.y - b_xz.y) * p_xz.x + (b_xz.x - a_xz.x) * p_xz.y;

        if ((s < 0) != (t < 0) && s != 0 && t != 0)
            return false;

        float A = -b_xz.y * c_xz.x + a_xz.y * (c_xz.x - b_xz.x) + a_xz.x * (b_xz.y - c_xz.y) + b_xz.x * c_xz.y;
        if (A < 0.0)
        {
            s = -s;
            t = -t;
            A = -A;
        }
        return s > 0 && t > 0 && (s + t) <= A;
    }
}