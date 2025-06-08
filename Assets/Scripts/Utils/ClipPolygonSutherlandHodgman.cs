// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting,Optimization and Code Cleanup were done by AI*
//
// This script implements the Sutherland-Hodgman algorithm for clipping a 2D polygon against an axis-aligned rectangle.
//

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A static utility class that implements the Sutherland-Hodgman algorithm to clip a
/// 2D polygon against a rectangular clipping window.
/// </summary>
public static class ClipPolygonSutherlandHodgman
{
    /// <summary>
    /// Clips a subject polygon against a rectangular clipping window.
    /// The algorithm processes the polygon against each of the four clipping edges sequentially.
    /// </summary>
    /// <param name="subjectPolygon">The polygon to be clipped, defined by a list of vertices in order.</param>
    /// <param name="clipRect">The axis-aligned Rect defining the clipping window.</param>
    /// <returns>A new list of vertices for the clipped polygon, or null if it's outside or becomes degenerate.</returns>
    public static List<Vector2> GetIntersectedPolygon(List<Vector2> subjectPolygon, Rect clipRect)
    {
        if (subjectPolygon == null || subjectPolygon.Count < 3) return null;

        var clippedPolygon = new List<Vector2>(subjectPolygon);

        // Define the four clipping edges of the rectangle in a counter-clockwise order.
        // This ensures that the "inside" of each edge is consistently defined by a left-hand rule.
        Vector2 p1 = new Vector2(clipRect.xMin, clipRect.yMin);
        Vector2 p2 = new Vector2(clipRect.xMax, clipRect.yMin);
        Vector2 p3 = new Vector2(clipRect.xMax, clipRect.yMax);
        Vector2 p4 = new Vector2(clipRect.xMin, clipRect.yMax);

        // Clip against the bottom edge.
        clippedPolygon = ClipAgainstEdge(clippedPolygon, p1, p2);
        if (clippedPolygon.Count < 3) return null;

        // Clip against the right edge.
        clippedPolygon = ClipAgainstEdge(clippedPolygon, p2, p3);
        if (clippedPolygon.Count < 3) return null;

        // Clip against the top edge.
        clippedPolygon = ClipAgainstEdge(clippedPolygon, p3, p4);
        if (clippedPolygon.Count < 3) return null;

        // Clip against the left edge.
        clippedPolygon = ClipAgainstEdge(clippedPolygon, p4, p1);

        return (clippedPolygon.Count >= 3) ? clippedPolygon : null;
    }

    /// <summary>
    /// Clips a polygon against a single infinite clipping edge.
    /// </summary>
    /// <param name="subjectPolygon">The input polygon vertices.</param>
    /// <param name="clipEdgeP1">Start point of the clipping edge vector.</param>
    /// <param name="clipEdgeP2">End point of the clipping edge vector. "Inside" is to its left.</param>
    /// <returns>The list of vertices of the polygon clipped against this single edge.</returns>
    private static List<Vector2> ClipAgainstEdge(List<Vector2> subjectPolygon, Vector2 clipEdgeP1, Vector2 clipEdgeP2)
    {
        var outputList = new List<Vector2>();
        if (subjectPolygon.Count == 0) return outputList;

        // 's' is the start point of the current subject polygon edge. Initialize with the last vertex.
        Vector2 s = subjectPolygon[subjectPolygon.Count - 1];

        foreach (Vector2 e in subjectPolygon)
        {
            // 'e' is the end point of the current subject polygon edge.
            bool s_inside = IsInsideClipEdge(clipEdgeP1, clipEdgeP2, s);
            bool e_inside = IsInsideClipEdge(clipEdgeP1, clipEdgeP2, e);

            // Case 1: Both points are inside -> only add the end point 'e'.
            if (s_inside && e_inside)
            {
                outputList.Add(e);
            }
            // Case 2: Start is inside, end is outside -> add the intersection point.
            else if (s_inside && !e_inside)
            {
                outputList.Add(CalculateIntersectionPoint(clipEdgeP1, clipEdgeP2, s, e));
            }
            // Case 3: Start is outside, end is inside -> add the intersection point, then the end point 'e'.
            else if (!s_inside && e_inside)
            {
                outputList.Add(CalculateIntersectionPoint(clipEdgeP1, clipEdgeP2, s, e));
                outputList.Add(e);
            }
            // Case 4: Both points are outside -> do nothing.

            s = e; // Advance to the next edge.
        }
        return outputList;
    }

    /// <summary>
    /// Checks if a point 'p' is "inside" a clipping edge defined by the directed line p1->p2.
    /// </summary>
    /// <remarks>
    /// "Inside" means to the left of or on the line. This is determined using the 2D cross product's Z-component.
    /// A positive or zero result means the point 'p' is not to the right of the directed edge.
    /// `(p2.x-p1.x)*(p.y-p1.y) - (p2.y-p1.y)*(p.x-p1.x) >= 0`
    /// </remarks>
    private static bool IsInsideClipEdge(Vector2 clipEdgeP1, Vector2 clipEdgeP2, Vector2 p)
    {
        float crossProductZ = (clipEdgeP2.x - clipEdgeP1.x) * (p.y - clipEdgeP1.y) -
                              (clipEdgeP2.y - clipEdgeP1.y) * (p.x - clipEdgeP1.x);

        // Point is considered inside if it's on the line or to its left.
        return crossProductZ >= -GeometryConstants.GeometricEpsilon;
    }

    /// <summary>
    /// Calculates the intersection point of two infinite lines defined by segments (clipEdgeP1, clipEdgeP2) and (polyEdgeS, polyEdgeE).
    /// </summary>
    private static Vector2 CalculateIntersectionPoint(Vector2 clipEdgeP1, Vector2 clipEdgeP2, Vector2 polyEdgeS, Vector2 polyEdgeE)
    {
        // Define the direction vectors for both lines.
        Vector2 clipEdgeDir = clipEdgeP2 - clipEdgeP1;
        Vector2 polyEdgeDir = polyEdgeE - polyEdgeS;

        // Calculate the determinant of the system of linear equations.
        // If zero, the lines are parallel or collinear.
        float determinant = (clipEdgeDir.x * polyEdgeDir.y) - (clipEdgeDir.y * polyEdgeDir.x);
        if (Mathf.Abs(determinant) < GeometryConstants.GeometricEpsilon)
        {
            // Fallback for parallel lines. The context of the algorithm means this is a rare edge case.
            return polyEdgeS;
        }

        // Solve for the parametric variable 't' for the clipping edge line.
        // Line equation: P = clipEdgeP1 + t * clipEdgeDir
        Vector2 deltaStart = polyEdgeS - clipEdgeP1;
        float t = ((deltaStart.x * polyEdgeDir.y) - (deltaStart.y * polyEdgeDir.x)) / determinant;

        // Calculate the intersection point using the found parameter 't'.
        return clipEdgeP1 + t * clipEdgeDir;
    }
}