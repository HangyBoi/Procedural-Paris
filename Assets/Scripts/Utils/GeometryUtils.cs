// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
// This script provides a set of general-purpose 3D geometric utility functions.
//

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides general 3D geometric utility functions, such as line intersections and polygon triangulation.
/// Operations are often performed on the XZ plane.
/// </summary>
public static class GeometryUtils
{
    /// <summary>
    /// Calculates the 2D cross product of three points (p1, p2, p3) on the XZ plane.
    /// This is equivalent to the Z component of the 3D cross product (p2-p1) x (p3-p1).
    /// </summary>
    /// <returns>
    /// > 0 if p3 is to the left of the directed line p1->p2.
    /// < 0 if p3 is to the right.
    /// = 0 if the points are collinear.
    /// </returns>
    public static float CrossProduct2D_XZ(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        return (p2.x - p1.x) * (p3.z - p1.z) - (p2.z - p1.z) * (p3.x - p1.x);
    }

    /// <summary>
    /// Determines if a point lies inside a triangle defined by three vertices on the XZ plane.
    /// Uses the barycentric coordinate method by checking if the point is on the same side of all three edges.
    /// </summary>
    /// <returns>True if the point is inside or on the boundary of the triangle, false otherwise.</returns>
    public static bool PointInTriangleXZ(Vector3 pt, Vector3 v1, Vector3 v2, Vector3 v3)
    {
        // Calculate cross products for the point relative to each triangle edge.
        float d1 = CrossProduct2D_XZ(v1, v2, pt);
        float d2 = CrossProduct2D_XZ(v2, v3, pt);
        float d3 = CrossProduct2D_XZ(v3, v1, pt);

        // If the point is inside, all cross products will have the same sign (or be zero).
        bool hasNegative = (d1 < -GeometryConstants.GeometricEpsilon) || (d2 < -GeometryConstants.GeometricEpsilon) || (d3 < -GeometryConstants.GeometricEpsilon);
        bool hasPositive = (d1 > GeometryConstants.GeometricEpsilon) || (d2 > GeometryConstants.GeometricEpsilon) || (d3 > GeometryConstants.GeometricEpsilon);

        // The point is outside if there's a mix of positive and negative signs.
        return !(hasNegative && hasPositive);
    }

    /// <summary>
    /// Triangulates a simple 2D polygon (on the XZ plane) using the Ear Clipping algorithm.
    /// This implementation handles both Clockwise (CW) and Counter-Clockwise (CCW) polygons
    /// and preserves the specific winding order logic from the original implementation.
    /// </summary>
    /// <param name="vertices">A list of Vector3 vertices defining the polygon in order.</param>
    /// <param name="triangles">The output list of integer indices forming the triangles.</param>
    /// <returns>True if triangulation was successful, false otherwise.</returns>
    public static bool TriangulatePolygonEarClipping(List<Vector3> vertices, out List<int> triangles)
    {
        triangles = new List<int>();
        if (vertices == null || vertices.Count < 3)
        {
            return false;
        }

        var indices = new List<int>();
        for (int i = 0; i < vertices.Count; i++)
        {
            indices.Add(i);
        }

        // Determine polygon winding order to correctly identify convex vertices ("ears").
        float signedArea = BuildingFootprintUtils.CalculateSignedAreaXZ(vertices);
        bool isInputClockwise = signedArea < 0f;

        int remainingVertices = indices.Count;
        int loopSafetyCounter = 0;
        int maxLoops = remainingVertices * 2; // Safety break for invalid polygons.

        while (remainingVertices > 2 && loopSafetyCounter++ < maxLoops)
        {
            bool earFound = false;
            for (int i = 0; i < remainingVertices; i++)
            {
                int prevIdx = (i == 0) ? remainingVertices - 1 : i - 1;
                int currIdx = i;
                int nextIdx = (i + 1) % remainingVertices;

                Vector3 pPrev = vertices[indices[prevIdx]];
                Vector3 pCurr = vertices[indices[currIdx]];
                Vector3 pNext = vertices[indices[nextIdx]];

                // Check if the current vertex is convex. The rule depends on the polygon's winding order.
                float crossProduct = CrossProduct2D_XZ(pPrev, pCurr, pNext);
                bool isConvex = isInputClockwise
                    ? (crossProduct < -GeometryConstants.GeometricEpsilon) // For CW, a "right turn" is convex.
                    : (crossProduct > GeometryConstants.GeometricEpsilon);  // For CCW, a "left turn" is convex.

                if (!isConvex) continue;

                // Check if any other remaining vertex lies inside this potential ear triangle.
                bool isEar = true;
                for (int j = 0; j < remainingVertices; j++)
                {
                    if (j == prevIdx || j == currIdx || j == nextIdx) continue;

                    if (PointInTriangleXZ(vertices[indices[j]], pPrev, pCurr, pNext))
                    {
                        isEar = false; // Another vertex is inside, so it's not a valid ear.
                        break;
                    }
                }

                if (isEar)
                {
                    // Add the triangle indices based on the original winding order logic.
                    if (isInputClockwise)
                    {
                        //triangles.Add(indices[prevIdx]);
                        //triangles.Add(indices[nextIdx]);
                        //triangles.Add(indices[currIdx]);


                        triangles.Add(indices[prevIdx]);
                        triangles.Add(indices[currIdx]);
                        triangles.Add(indices[nextIdx]);
                    }
                    else // Input was CCW
                    {
                        //triangles.Add(indices[prevIdx]);
                        //triangles.Add(indices[currIdx]);
                        //triangles.Add(indices[nextIdx]);

                        triangles.Add(indices[prevIdx]);
                        triangles.Add(indices[nextIdx]);
                        triangles.Add(indices[currIdx]);
                    }

                    // "Clip" the ear by removing its tip from the working list.
                    indices.RemoveAt(currIdx);
                    remainingVertices--;
                    earFound = true;
                    break; // Restart the loop on the now-smaller polygon.
                }
            }

            if (!earFound && remainingVertices > 2)
            {
                Debug.LogError($"Ear Clipping: Failed to find a valid ear with {remainingVertices} vertices remaining. Polygon may be self-intersecting or invalid.");
                return false;
            }
        }

        return triangles.Count > 0;
    }

    /// <summary>
    /// Calculates the intersection point of two infinite 2D lines on the XZ plane.
    /// </summary>
    /// <param name="p1">A point on the first line.</param>
    /// <param name="dir1">Direction vector of the first line.</param>
    /// <param name="p2">A point on the second line.</param>
    /// <param name="dir2">Direction vector of the second line.</param>
    /// <param name="intersection">The output intersection point on the XZ plane.</param>
    /// <returns>True if the lines intersect (are not parallel), false otherwise.</returns>
    public static bool LineLineIntersection(Vector3 p1, Vector3 dir1, Vector3 p2, Vector3 dir2, out Vector3 intersection)
    {
        intersection = Vector3.zero;

        // The determinant of the system of linear equations for the intersection.
        float determinant = (dir1.x * dir2.z) - (dir1.z * dir2.x);
        if (Mathf.Abs(determinant) < GeometryConstants.GeometricEpsilon)
        {
            return false; // Lines are parallel or coincident.
        }

        // Solve for the parametric variable 'u' for the second line: P = p2 + u * dir2
        Vector3 delta = p1 - p2;
        float u_numerator = (delta.z * dir1.x) - (delta.x * dir1.z);
        float u = u_numerator / determinant;

        // Calculate the intersection point using the parameter 'u'.
        intersection = p2 + u * dir2;
        intersection.y = 0; // Ensure the result is on the XZ plane.

        return true;
    }
}