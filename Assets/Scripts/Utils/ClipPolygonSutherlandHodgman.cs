using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Implements the Sutherland-Hodgman polygon clipping algorithm for 2D polygons against a rectangular clipping window.
/// </summary>
public static class ClipPolygonSutherlandHodgman
{
    /// <summary>
    /// Clips a subject polygon against a rectangular clipping window (clipRect).
    /// </summary>
    /// <param name="subjectPolygon">The polygon to be clipped, defined by a list of Vector2 vertices in order.</param>
    /// <param name="clipRect">The AABB Rect defining the clipping window.</param>
    /// <returns>A new list of Vector2 vertices representing the clipped polygon, or null if the polygon is entirely outside or becomes degenerate.</returns>
    public static List<Vector2> GetIntersectedPolygon(List<Vector2> subjectPolygon, Rect clipRect)
    {
        if (subjectPolygon == null || subjectPolygon.Count < 3)
            return null;

        List<Vector2> clippedPolygon = new List<Vector2>(subjectPolygon);

        // Edge 1: Bottom edge (y = clipRect.yMin), from (xMin, yMin) to (xMax, yMin)
        // "Inside" means y >= clipRect.yMin.
        clippedPolygon = ClipAgainstEdge(clippedPolygon,
                                         new Vector2(clipRect.xMin, clipRect.yMin),
                                         new Vector2(clipRect.xMax, clipRect.yMin));
        if (clippedPolygon == null || clippedPolygon.Count < 3) return null;

        // Edge 2: Right edge (x = clipRect.xMax), from (xMax, yMin) to (xMax, yMax)
        // "Inside" means x <= clipRect.xMax.
        clippedPolygon = ClipAgainstEdge(clippedPolygon,
                                         new Vector2(clipRect.xMax, clipRect.yMin),
                                         new Vector2(clipRect.xMax, clipRect.yMax));
        if (clippedPolygon == null || clippedPolygon.Count < 3) return null;

        // Edge 3: Top edge (y = clipRect.yMax), from (xMax, yMax) to (xMin, yMax)
        // "Inside" means y <= clipRect.yMax.
        clippedPolygon = ClipAgainstEdge(clippedPolygon,
                                         new Vector2(clipRect.xMax, clipRect.yMax),
                                         new Vector2(clipRect.xMin, clipRect.yMax));
        if (clippedPolygon == null || clippedPolygon.Count < 3) return null;

        // Edge 4: Left edge (x = clipRect.xMin), from (xMin, yMax) to (xMin, yMin)
        // "Inside" means x >= clipRect.xMin.
        clippedPolygon = ClipAgainstEdge(clippedPolygon,
                                         new Vector2(clipRect.xMin, clipRect.yMax),
                                         new Vector2(clipRect.xMin, clipRect.yMin));

        return (clippedPolygon != null && clippedPolygon.Count >= 3) ? clippedPolygon : null;
    }

    /// <summary>
    /// Clips a polygon against a single infinite clipping edge.
    /// </summary>
    /// <param name="subjectPolygon">The input polygon vertices.</param>
    /// <param name="clipEdgeP1">Start point of the clipping edge vector.</param>
    /// <param name="clipEdgeP2">End point of the clipping edge vector. Defines direction; "inside" is to its left.</param>
    /// <returns>The list of vertices of the polygon clipped against this single edge.</returns>
    private static List<Vector2> ClipAgainstEdge(List<Vector2> subjectPolygon, Vector2 clipEdgeP1, Vector2 clipEdgeP2)
    {
        List<Vector2> outputList = new List<Vector2>();
        if (subjectPolygon.Count == 0) return outputList;

        // 's' is the start point of the current subject polygon edge being processed.
        Vector2 s = subjectPolygon[subjectPolygon.Count - 1];

        for (int i = 0; i < subjectPolygon.Count; i++)
        {
            // 'e' is the end point of the current subject polygon edge.
            Vector2 e = subjectPolygon[i];

            bool s_inside = IsInsideClipEdge(clipEdgeP1, clipEdgeP2, s);
            bool e_inside = IsInsideClipEdge(clipEdgeP1, clipEdgeP2, e);

            if (s_inside && e_inside)
            {
                // Case 1: Both points are inside. Add 'e'.
                outputList.Add(e);
            }
            else if (s_inside && !e_inside)
            {
                // Case 2: Edge goes from inside to outside. Add intersection point.
                outputList.Add(CalculateIntersectionPoint(clipEdgeP1, clipEdgeP2, s, e));
            }
            else if (!s_inside && e_inside)
            {
                // Case 3: Edge goes from outside to inside. Add intersection point, then add 'e'.
                outputList.Add(CalculateIntersectionPoint(clipEdgeP1, clipEdgeP2, s, e));
                outputList.Add(e);
            }
            // Case 4: Both points are outside (do nothing)

            s = e; // Move to the next edge
        }
        return outputList;
    }

    /// <summary>
    /// Checks if a point 'p' is "inside" a clipping edge defined by directed line segment p1->p2.
    /// "Inside" means to the left of or on the line. Uses 2D cross product.
    /// (p2.x-p1.x)*(p.y-p1.y) - (p2.y-p1.y)*(p.x-p1.x) >= 0 for "left of or on".
    /// </summary>
    private static bool IsInsideClipEdge(Vector2 clipEdgeP1, Vector2 clipEdgeP2, Vector2 p)
    {
        // Cross product: (clipEdgeP2 - clipEdgeP1) x (p - clipEdgeP1)
        // For 2D vectors v1=(x1,y1), v2=(x2,y2), cross product z-component is x1*y2 - y1*x2.
        // Here, v1 = clipEdgeP2 - clipEdgeP1, v2 = p - clipEdgeP1.
        float cross_product_z = (clipEdgeP2.x - clipEdgeP1.x) * (p.y - clipEdgeP1.y) -
                                (clipEdgeP2.y - clipEdgeP1.y) * (p.x - clipEdgeP1.x);
        // Point is inside if cross_product_z >= 0 (or >= -epsilon for robustness).
        return cross_product_z >= -GeometryConstants.GeometricEpsilon;
    }

    /// <summary>
    /// Calculates the intersection point of two 2D line segments: (seg1_p1 - seg1_p2) and (seg2_p3 - seg2_p4).
    /// This assumes lines are not parallel (denominator check).
    /// Used here for finding where a polygon edge (s-e) intersects a clipping edge (clipEdgeP1-clipEdgeP2).
    /// </summary>
    private static Vector2 CalculateIntersectionPoint(Vector2 clipEdgeP1, Vector2 clipEdgeP2, Vector2 polyEdgeS, Vector2 polyEdgeE)
    {
        // Line 1 (clipping edge): clipEdgeP1 + t * (clipEdgeP2 - clipEdgeP1)
        // Line 2 (polygon edge): polyEdgeS   + u * (polyEdgeE  - polyEdgeS)

        float dx_clip = clipEdgeP2.x - clipEdgeP1.x;
        float dy_clip = clipEdgeP2.y - clipEdgeP1.y;
        float dx_poly = polyEdgeE.x - polyEdgeS.x;
        float dy_poly = polyEdgeE.y - polyEdgeS.y;

        // Denominator for parametric solution: (dx_poly * dy_clip) - (dy_poly * dx_clip)
        float determinant = (dx_clip * dy_poly) - (dy_clip * dx_poly);

        if (Mathf.Abs(determinant) < GeometryConstants.GeometricEpsilon)
        {
            // Lines are parallel or collinear.
            // Returning polyEdgeS (or polyEdgeE) is a fallback.
            return polyEdgeS;
        }

        float t_numerator = (polyEdgeS.x - clipEdgeP1.x) * dy_poly - (polyEdgeS.y - clipEdgeP1.y) * dx_poly;
        float t = t_numerator / determinant; // This 't' is for the clipping edge: clipEdgeP1 + t * (clipEdgeP2 - clipEdgeP1)

        // Calculate intersection point using parameter 't' for the clipping edge line
        return new Vector2(clipEdgeP1.x + t * dx_clip, clipEdgeP1.y + t * dy_clip);
    }
}
