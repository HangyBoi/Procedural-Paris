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
}