using System.Collections.Generic;
using UnityEngine;

public static class GeometryUtils
{
    // Finds the intersection point of two 2D lines defined by point and direction.
    // Returns true if intersection exists, false otherwise (parallel lines).
    // Ignores the Y component.

    public static bool LineLineIntersection(Vector3 p1, Vector3 dir1, Vector3 p2, Vector3 dir2, out Vector3 intersection)
    {
        intersection = Vector3.zero;

        float dx1 = dir1.x;
        float dz1 = dir1.z;
        float dx2 = dir2.x;
        float dz2 = dir2.z;

        float determinant = (dz2 * dx1) - (dx2 * dz1);

        // Check if lines are parallel (determinant is close to zero)
        if (Mathf.Abs(determinant) < 1e-6)
        {
            return false; // Lines are parallel or coincident
        }

        float t = ((p2.x - p1.x) * dz1 - (p2.z - p1.z) * dx1) / determinant; // Parametric distance for line 2

        // Calculate intersection point using line 2's parameters
        intersection = new Vector3(p2.x + dx2 * t, 0, p2.z + dz2 * t); // Y is set to 0
        return true;
    }

    public const float Epsilon = 1e-5f; // Make sure Epsilon is accessible if needed elsewhere

    private static float CrossProduct2D(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        return (p2.x - p1.x) * (p3.z - p1.z) - (p2.z - p1.z) * (p3.x - p1.x);
    }

    public static bool PointInTriangle(Vector3 pt, Vector3 v1, Vector3 v2, Vector3 v3)
    {
        // ... (Implementation is likely correct, keep as is) ...
        float d1, d2, d3;
        bool has_neg, has_pos;
        d1 = CrossProduct2D(pt, v1, v2);
        d2 = CrossProduct2D(pt, v2, v3);
        d3 = CrossProduct2D(pt, v3, v1);
        has_neg = (d1 < -Epsilon) || (d2 < -Epsilon) || (d3 < -Epsilon);
        has_pos = (d1 > Epsilon) || (d2 > Epsilon) || (d3 > Epsilon);
        return !(has_neg && has_pos);
    }


    // --- Ear Clipping Triangulation ---

    public static bool TriangulatePolygonEarClipping(List<Vector3> vertices, out List<int> triangles)
    {
        triangles = new List<int>();
        if (vertices == null || vertices.Count < 3)
        {
            Debug.LogError("Ear Clipping: Need at least 3 vertices.");
            return false;
        }

        List<int> indices = new List<int>(vertices.Count);
        for (int i = 0; i < vertices.Count; i++) { indices.Add(i); }

        float signedArea = 0f;
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 p1 = vertices[i];
            Vector3 p2 = vertices[(i + 1) % vertices.Count];
            signedArea += (p1.x * p2.z) - (p2.x * p1.z);
        }
        // Determine if the input polygon vertex order is Clockwise
        bool isClockwise = signedArea < 0f;

        int remainingVertices = indices.Count;
        int currentIndex = 0;
        int loopSafetyCounter = 0;
        int maxLoops = remainingVertices * remainingVertices * 2; // Increased safety margin slightly

        while (remainingVertices > 3 && loopSafetyCounter++ < maxLoops)
        {
            int prevVIndex = currentIndex % remainingVertices;
            int currVIndex = (currentIndex + 1) % remainingVertices;
            int nextVIndex = (currentIndex + 2) % remainingVertices;

            int prevIndex = indices[prevVIndex];
            int currIndex = indices[currVIndex];
            int nextIndex = indices[nextVIndex];

            Vector3 pPrev = vertices[prevIndex];
            Vector3 pCurr = vertices[currIndex];
            Vector3 pNext = vertices[nextIndex];

            float crossProd = CrossProduct2D(pPrev, pCurr, pNext);
            // An ear vertex must be convex relative to the polygon's winding order.
            // If CW, cross product should be negative (left turn is convex).
            // If CCW, cross product should be positive (left turn is convex).
            bool isConvex = isClockwise ? (crossProd < -Epsilon) : (crossProd > Epsilon);

            bool isEar = isConvex;
            if (isConvex)
            {
                // Check for other vertices inside the potential ear triangle
                for (int i = 0; i < remainingVertices; i++)
                {
                    // Don't check the triangle's own vertices
                    if (i == prevVIndex || i == currVIndex || i == nextVIndex) continue;

                    int testIndex = indices[i];
                    if (PointInTriangle(vertices[testIndex], pPrev, pCurr, pNext))
                    {
                        isEar = false; // Not an ear, contains another vertex
                        break;
                    }
                }
            }

            if (isEar)
            {
                // --- THIS IS THE CORE FIX ---
                // Add the triangle indices ensuring CCW order for Unity normals.
                if (isClockwise)
                {
/*                  // Input was CW, so reverse order B, C to get CCW (A, C, B)
                    triangles.Add(prevIndex);
                    triangles.Add(nextIndex); // C
                    triangles.Add(currIndex); // B*/

                    triangles.Add(prevIndex);
                    triangles.Add(currIndex); // B
                    triangles.Add(nextIndex); // C


                }
                else // Input was CCW
                {
/*                  // Input was CCW, so natural order (A, B, C) is already CCW
                    triangles.Add(prevIndex);
                    triangles.Add(currIndex); // B
                    triangles.Add(nextIndex); // C*/

                    triangles.Add(prevIndex);
                    triangles.Add(nextIndex); // C
                    triangles.Add(currIndex); // B
                }
                // --- END CORE FIX ---

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
            // --- APPLY CONSISTENT FIX HERE TOO ---
            if (isClockwise)
            {
/*              // Input was CW, reverse order 1, 2 to get CCW (0, 2, 1)
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
            // --- END CONSISTENT FIX ---
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
}