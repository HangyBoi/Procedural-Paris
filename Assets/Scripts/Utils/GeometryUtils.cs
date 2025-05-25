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
    /// This is equivalent to the Z component of (p2-p1) x (p3-p1).
    /// Result > 0 if p3 is to the left of the directed line p1-p2.
    /// Result < 0 if p3 is to the right.
    /// Result = 0 if p1, p2, p3 are collinear.
    /// </summary>
    /// <param name="p1">The origin point of the vectors.</param>
    /// <param name="p2">The end point of the first vector (p2-p1).</param>
    /// <param name="p3">The end point of the second vector (p3-p1).</param>
    /// <returns>The scalar value of the 2D cross product on the XZ plane.</returns>
    public static float CrossProduct2D_XZ(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        // (P2.x - P1.x) * (P3.z - P1.z) - (P2.z - P1.z) * (P3.x - P1.x)
        return (p2.x - p1.x) * (p3.z - p1.z) - (p2.z - p1.z) * (p3.x - p1.x);
    }

    /// <summary>
    /// Determines if a point lies inside a triangle defined by three vertices on the XZ plane.
    /// Uses the barycentric coordinate method by checking the sign of cross products.
    /// </summary>
    /// <param name="pt">The point to test.</param>
    /// <param name="v1">First vertex of the triangle.</param>
    /// <param name="v2">Second vertex of the triangle.</param>
    /// <param name="v3">Third vertex of the triangle.</param>
    /// <returns>True if the point is inside or on the boundary of the triangle, false otherwise.</returns>
    public static bool PointInTriangleXZ(Vector3 pt, Vector3 v1, Vector3 v2, Vector3 v3)
    {
        // Calculate cross products for each edge with the point.
        // If the point is on the same side of all three edges (or on an edge), it's inside.
        float d1 = CrossProduct2D_XZ(pt, v1, v2);
        float d2 = CrossProduct2D_XZ(pt, v2, v3);
        float d3 = CrossProduct2D_XZ(pt, v3, v1);

        // Check if all cross products have the same sign (or are zero).
        // has_neg: true if any cross product is significantly negative.
        // has_pos: true if any cross product is significantly positive.
        bool has_neg = (d1 < -GeometryConstants.GeometricEpsilon) || (d2 < -GeometryConstants.GeometricEpsilon) || (d3 < -GeometryConstants.GeometricEpsilon);
        bool has_pos = (d1 > GeometryConstants.GeometricEpsilon) || (d2 > GeometryConstants.GeometricEpsilon) || (d3 > GeometryConstants.GeometricEpsilon);

        // If signs are mixed (both significantly positive and significantly negative exist), the point is outside.
        return !(has_neg && has_pos);
    }


    /// <summary>
    /// Triangulates a simple 2D polygon (on XZ plane) using the Ear Clipping algorithm.
    /// Handles both Clockwise (CW) and Counter-Clockwise (CCW) input vertex order.
    /// Outputs triangles with CCW winding order, suitable for Unity meshes.
    /// </summary>
    /// <param name="vertices">A list of Vector3 vertices defining the polygon, ordered sequentially on the XZ plane.</param>
    /// <param name="triangles">Output list of integer indices forming the triangles (3 indices per triangle).</param>
    /// <returns>True if triangulation was successful, false otherwise (e.g., invalid polygon, self-intersecting).</returns>
    public static bool TriangulatePolygonEarClipping(List<Vector3> vertices, out List<int> triangles)
    {
        triangles = new List<int>();
        if (vertices == null || vertices.Count < 3)
        {
            Debug.LogError("Ear Clipping: Polygon requires at least 3 vertices.");
            return false;
        }

        // Create a working list of vertex indices
        List<int> indices = new List<int>(vertices.Count);
        for (int i = 0; i < vertices.Count; i++) { indices.Add(i); }

        // Determine polygon winding order (on XZ plane) using shoelace formula for signed area.
        float signedArea = 0f;
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 p1 = vertices[i];
            Vector3 p2 = vertices[(i + 1) % vertices.Count];
            // Area component: (x1*z2 - x2*z1)
            signedArea += (p1.x * p2.z) - (p2.x * p1.z);
        }
        // Note: Shoelace formula is area = 0.5 * sum. Here, only sign of sum matters.
        bool isInputClockwise = signedArea < 0f; // Negative area means clockwise for XZ plane with Y-up

        int remainingVertices = indices.Count;
        int currentIndex = 0;   // Pointer to the potential tip of the ear triangle in the 'indices' list.
        int loopSafetyCounter = 0;  // Safety counter to prevent infinite loops for invalid polygons.
        int maxLoops = remainingVertices * remainingVertices * 2;   // Heuristic limit

        while (remainingVertices > 3 && loopSafetyCounter++ < maxLoops)
        {
            if (remainingVertices == 3) break; // Last triangle handled outside loop

            // Get indices of the three consecutive vertices forming a potential ear.
            int prevVIndex = currentIndex % remainingVertices;
            int currVIndex = (currentIndex + 1) % remainingVertices;
            int nextVIndex = (currentIndex + 2) % remainingVertices;

            // Get the actual indices in the original 'vertices' list.
            int prevIndex = indices[prevVIndex];
            int currIndex = indices[currVIndex];
            int nextIndex = indices[nextVIndex];

            Vector3 pPrev = vertices[prevIndex];
            Vector3 pCurr = vertices[currIndex];
            Vector3 pNext = vertices[nextIndex];

            // An ear vertex must be convex relative to the polygon's winding order.
            // If CW, cross product should be negative (left turn is convex).
            // If CCW, cross product should be positive (left turn is convex).
            float crossProduct = CrossProduct2D_XZ(pPrev, pCurr, pNext);
            bool isConvex;
            if (isInputClockwise)
            {
                // For a CW polygon, a convex vertex makes a "right turn" (P->Q->R).
                // The cross product PQR will be negative (R is to the right of PQ).
                isConvex = crossProduct < -GeometryConstants.GeometricEpsilon;
            }
            else // Input is CCW
            {
                // For a CCW polygon, a convex vertex makes a "left turn" (P->Q->R).
                // The cross product PQR will be positive (R is to the left of PQ).
                isConvex = crossProduct > GeometryConstants.GeometricEpsilon;
            }

            bool isEar = isConvex;
            if (isConvex)
            {
                // Check for other vertices inside the potential ear triangle
                for (int i = 0; i < remainingVertices; i++)
                {
                    int testVertexLocalIdx = i;
                    // Skip vertices that form the triangle PQR itself.
                    if (testVertexLocalIdx == prevIndex ||
                        testVertexLocalIdx == currVIndex ||
                        testVertexLocalIdx == nextVIndex)
                    {
                        continue;
                    }

                    int testIndex = indices[i];
                    if (PointInTriangleXZ(vertices[testIndex], pPrev, pCurr, pNext))
                    {
                        isEar = false; // Not an ear, contains another vertex
                        break;
                    }
                }
            }

            if (isEar)
            {
                // Add the triangle indices ensuring CCW order for Unity normals.
                if (isInputClockwise)
                {
/*                    // Input was CW, so reverse order B, C to get CCW (A, C, B)
                    triangles.Add(prevIndex);
                    triangles.Add(nextIndex); // C
                    triangles.Add(currIndex); // B*/

                    triangles.Add(prevIndex);
                    triangles.Add(currIndex); // B
                    triangles.Add(nextIndex); // C


                }
                else // Input was CCW
                {
/*                    // Input was CCW, so natural order (A, B, C) is already CCW
                    triangles.Add(prevIndex);
                    triangles.Add(currIndex); // B
                    triangles.Add(nextIndex); // C*/

                    triangles.Add(prevIndex);
                    triangles.Add(nextIndex); // C
                    triangles.Add(currIndex); // B
                }

                // Remove the middle vertex index of the clipped ear
                indices.RemoveAt(currVIndex);
                remainingVertices--;

                // Reset index to re-evaluate from the start, as the polygon shape changed
                currentIndex = 0; // Or: currentIndex = (currentIndex) % remainingVertices; // Adjust if needed
                loopSafetyCounter = 0; // Reset safety counter
            }
            else // Not an ear, move to the next vertex
            {
                currentIndex++;
                if (currentIndex >= remainingVertices && remainingVertices > 3) // Check needed?
                {
                    Debug.LogError($"Ear Clipping: Failed to find an ear after checking all vertices ({remainingVertices} remaining). LoopCounter: {loopSafetyCounter}. Polygon might be invalid.");
                    return false;
                }
            }
        }

        // Add the last remaining triangle (should always have 3 vertices left if valid)
        if (remainingVertices == 3)
        {
            if (isInputClockwise)
            {
/*                // Input was CW, reverse order 1, 2 to get CCW (0, 2, 1)
                triangles.Add(indices[0]);
                triangles.Add(indices[2]); // 2
                triangles.Add(indices[1]); // 1*/

                // Try outputting natural order (0, 1, 2), which would be CW
                triangles.Add(indices[0]);
                triangles.Add(indices[1]); // 1
                triangles.Add(indices[2]); // 2
            }
            else // Input was CCW
            {
/*              // Input was CCW, so natural order (0, 1, 2) is already CCW
                triangles.Add(indices[0]);
                triangles.Add(indices[1]); // 1
                triangles.Add(indices[2]); // 2*/

                // Try outputting reversed order (0, 2, 1), which would be CW
                triangles.Add(indices[0]);
                triangles.Add(indices[2]); // 2
                triangles.Add(indices[1]); // 1
            }

        }
        else if (loopSafetyCounter >= maxLoops)
        {
            Debug.LogError($"Ear Clipping: Hit safety counter limit ({loopSafetyCounter}/{maxLoops}). Failed to triangulate.");
            return false;
        }
        else if (remainingVertices != 3)
        {
            Debug.LogError($"Ear Clipping: Ended with {remainingVertices} vertices instead of 3. Triangulation failed.");
            return false;
        }

        return true; // Triangulation successful
    }

    /// <summary>
    /// <param name="p1">A point on the first line.</param>
    /// <param name="dir1">Direction vector of the first line (Y component ignored).</param>
    /// <param name="p2">A point on the second line.</param>
    /// <param name="dir2">Direction vector of the second line (Y component ignored).</param>
    /// <param name="intersection">Output parameter for the intersection point (Y component will be 0).</param>
    /// <returns>True if the lines intersect (are not parallel), false otherwise.</returns>
    /// </summary>
    public static bool LineLineIntersection(Vector3 p1, Vector3 dir1, Vector3 p2, Vector3 dir2, out Vector3 intersection)
    {
        intersection = Vector3.zero;

        // Using X and Z components for 2D line intersection.
        // Line 1: P1 + t*D1 => x = p1.x + t*dir1.x, z = p1.z + t*dir1.z
        // Line 2: P2 + u*D2 => x = p2.x + u*dir2.x, z = p2.z + u*dir2.z
        // Solve for t and u. We only need one to find the intersection point.
        float determinant = (dir2.z * dir1.x) - (dir2.x * dir1.z);

        if (Mathf.Abs(determinant) < GeometryConstants.GeometricEpsilon)
        {
            return false; // Lines are parallel or coincident
        }

        float dx_points = p2.x - p1.x;
        float dz_points = p2.z - p1.z;

        float u_numerator = (dx_points * dir1.z) - (dz_points * dir1.x);
        float u = u_numerator / determinant;

        // Calculate intersection point using parameter u for line 2
        intersection.x = p2.x + u * dir2.x;
        intersection.y = 0; // Intersection on XZ plane
        intersection.z = p2.z + u * dir2.z;

        return true;
    }
}