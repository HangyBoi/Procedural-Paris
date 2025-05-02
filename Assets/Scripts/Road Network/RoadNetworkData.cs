using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

// Class to hold the generated road network and plot data
public class RoadNetworkData : MonoBehaviour
{
    // List of polygons, each polygon is a list of vertices defining a block/plot
    public List<List<Vector3>> plotPolygons = new List<List<Vector3>>();

    // Optional: Store intersections and segments for visualization/debugging
    public List<Vector3> intersectionPoints = new List<Vector3>();
    public List<(Vector3, Vector3)> roadSegments = new List<(Vector3, Vector3)>(); // Segments between intersections/endpoints

    public void ClearData()
    {
        plotPolygons.Clear();
        intersectionPoints.Clear();
        roadSegments.Clear();
    }

    // --- Gizmo Drawing ---
    void OnDrawGizmos()
    {
        // Draw Road Segments
        Gizmos.color = Color.red;
        foreach (var segment in roadSegments)
        {
            Gizmos.DrawLine(segment.Item1, segment.Item2);
        }

        // Draw Intersections
        Gizmos.color = Color.yellow;
        foreach (Vector3 intersection in intersectionPoints)
        {
            Gizmos.DrawSphere(intersection, 0.2f); // Adjust size as needed
        }

        // Draw Plot Polygons
        Gizmos.color = Color.cyan;
        int plotIndex = 0;
        foreach (var plot in plotPolygons)
        {
            if (plot == null || plot.Count < 3) continue;
            for (int i = 0; i < plot.Count; i++)
            {
                Vector3 p1 = plot[i];
                Vector3 p2 = plot[(i + 1) % plot.Count]; // Wrap around
                Gizmos.DrawLine(p1, p2);

                // Optional: Draw plot center index
                Vector3 center = CalculatePolygonCenter(plot);
                UnityEditor.Handles.Label(center, $"Plot {plotIndex}");
            }
            plotIndex++;
        }
    }

    // Helper to calculate center for labeling (requires UnityEditor namespace)
    Vector3 CalculatePolygonCenter(List<Vector3> polygon)
    {
        if (polygon == null || polygon.Count == 0) return Vector3.zero;
        Vector3 center = Vector3.zero;
        foreach (Vector3 p in polygon) { center += p; }
        return center / polygon.Count;
    }
}