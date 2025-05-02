using UnityEngine;
using UnityEngine.Splines;      // Required for Spline handling
using System.Collections.Generic;
using System.Linq;              // Used for LINQ queries
using Unity.Mathematics;        // Required for float3 output

// Place this script on a dedicated Manager GameObject in your scene
public class SplineRoadProcessor : MonoBehaviour
{
    [Header("Processing Settings")]
    [Tooltip("How many segments to approximate each spline curve with for intersection checks.")]
    public int splineApproximationResolution = 10;
    [Tooltip("Minimum distance between points to be considered distinct intersections.")]
    public float intersectionMergeThreshold = 0.1f;
    [Tooltip("Subdivisions used for GetNearestPoint accuracy.")]
    public int nearestPointSubdivisions = 4; // Added control

    [Header("References")]
    [Tooltip("Drag the GameObject with the RoadNetworkData script here.")]
    public RoadNetworkData roadNetworkData;


    // Internal data structures for graph building
    private class GraphNode
    {
        public Vector3 position;
        public List<GraphNode> neighbours = new List<GraphNode>();
        public int id; // Unique ID for cycle finding
        public GraphNode(Vector3 pos, int id) { this.position = pos; this.id = id; }
    }

    private Dictionary<int, GraphNode> nodes = new Dictionary<int, GraphNode>();
    private int nextNodeId = 0;


    [ContextMenu("Process Spline Roads")] // Adds a right-click menu item in Inspector
    public void ProcessRoads()
    {
        if (roadNetworkData == null)
        {
            Debug.LogError("RoadNetworkData reference not set!");
            return;
        }

        // 0. Clear previous data
        roadNetworkData.ClearData();
        nodes.Clear();
        nextNodeId = 0;


        // 1. Find all active SplineContainers tagged as "Road" (or similar) or children of this object
        // For simplicity, let's assume roads are children of this GameObject
        var splineContainers = GetComponentsInChildren<SplineContainer>();
        if (splineContainers.Length < 2)
        {
            Debug.LogWarning("Need at least two SplineContainers (roads) to find intersections/plots.");
            return;
        }

        // 2. Approximate Splines and Find Intersections
        List<Vector3> rawIntersections = FindSplineIntersections(splineContainers);
        roadNetworkData.intersectionPoints = MergeNearbyPoints(rawIntersections, intersectionMergeThreshold);

        // 3. Build Graph (Nodes and Edges)
        BuildRoadGraph(splineContainers, roadNetworkData.intersectionPoints);

        // 4. Find Cycles (Plots)
        FindCyclesInGraph();

        // For debugging/visualization: Populate roadSegments in RoadNetworkData
        PopulateRoadSegmentsForGizmos();

        Debug.Log($"Processing complete. Found {roadNetworkData.intersectionPoints.Count} unique intersections and {roadNetworkData.plotPolygons.Count} potential plots.");
    }

    // --- Step 2: Intersection Finding ---
    private List<Vector3> FindSplineIntersections(SplineContainer[] containers)
    {
        List<Vector3> intersections = new List<Vector3>();
        // Compare every pair of splines
        for (int i = 0; i < containers.Length; i++)
        {
            for (int j = i + 1; j < containers.Length; j++)
            {
                if (containers[i].Spline == null || containers[j].Spline == null) continue;

                // Approximate both splines as polylines
                List<Vector3> polylineA = ApproximateSpline(containers[i].Spline, splineApproximationResolution);
                List<Vector3> polylineB = ApproximateSpline(containers[j].Spline, splineApproximationResolution);

                // Check for intersections between line segments of the polylines
                for (int segA = 0; segA < polylineA.Count - 1; segA++)
                {
                    for (int segB = 0; segB < polylineB.Count - 1; segB++)
                    {
                        if (LineSegmentIntersection(polylineA[segA], polylineA[segA + 1],
                                                  polylineB[segB], polylineB[segB + 1],
                                                  out Vector3 intersectionPoint))
                        {
                            intersections.Add(intersectionPoint);
                        }
                    }
                }
            }
        }
        return intersections;
    }

    private List<Vector3> ApproximateSpline(Spline spline, int resolution)
    {
        List<Vector3> points = new List<Vector3>();
        for (int i = 0; i <= resolution; i++)
        {
            float t = (float)i / resolution;
            // Ensure position is evaluated correctly relative to container's transform if needed
            Vector3 pos = spline.EvaluatePosition(t);
            // If splines are children, their position might be local. Transform if needed.
            // pos = transform.TransformPoint(pos); // Uncomment if splines are children and need world pos
            points.Add(pos);
        }
        return points;
    }

    private List<Vector3> MergeNearbyPoints(List<Vector3> points, float threshold)
    {
        List<Vector3> merged = new List<Vector3>();
        foreach (Vector3 p in points)
        {
            bool foundNearby = false;
            for (int i = 0; i < merged.Count; i++)
            {
                if (Vector3.Distance(p, merged[i]) < threshold)
                {
                    // Optional: Average the points instead of just skipping
                    // merged[i] = (merged[i] + p) * 0.5f;
                    foundNearby = true;
                    break;
                }
            }
            if (!foundNearby)
            {
                merged.Add(p);
            }
        }
        return merged;
    }

    // --- Step 3: Graph Building (REVISED) ---
    private void BuildRoadGraph(SplineContainer[] containers, List<Vector3> uniqueIntersectionPoints)
    {
        nodes.Clear();
        nextNodeId = 0;

        // === Step 3.1: Create Nodes for ALL significant points ===
        // Add nodes for unique intersections
        foreach (Vector3 point in uniqueIntersectionPoints)
        {
            AddNodeIfNew(point);
        }
        // Add nodes for unique start/end points of ALL splines
        foreach (var container in containers)
        {
            if (container.Spline == null || container.Spline.Count < 2) continue;
            container.Evaluate(0, out float3 worldStartPosF3, out _, out _);
            container.Evaluate(1, out float3 worldEndPosF3, out _, out _);
            AddNodeIfNew((Vector3)worldStartPosF3);
            AddNodeIfNew((Vector3)worldEndPosF3);
        }
        // Now 'nodes' dictionary contains unique nodes for all intersections, starts, and ends


        // === Step 3.2: Define Valid Segments Along Each Spline ===
        var validSegments = new HashSet<(GraphNode, GraphNode)>(); // Store pairs of connected nodes

        foreach (var container in containers)
        {
            if (container.Spline == null || container.Spline.Count < 2) continue;

            // Find which existing nodes lie on *this* spline
            List<GraphNode> nodesOnThisSpline = new List<GraphNode>();
            foreach (var node in nodes.Values)
            {
                // Check distance from node position to the spline
                float dist = SplineUtility.GetNearestPoint(
                    container.Spline,
                    (float3)node.position,
                    out _, // Don't need nearest point itself here
                    out _, // Don't need t value *yet*
                    splineApproximationResolution,
                    nearestPointSubdivisions
                );

                // Adjust threshold maybe slightly larger for spline check than point merging
                if (dist < intersectionMergeThreshold * 2.5f)
                {
                    nodesOnThisSpline.Add(node);
                }
            }

            if (nodesOnThisSpline.Count < 2) continue; // Need at least two points to form a segment

            // Sort these nodes based on their t-value along this specific spline
            nodesOnThisSpline = nodesOnThisSpline.OrderBy(node => {
                SplineUtility.GetNearestPoint(
                    container.Spline, (float3)node.position, out _, out float tValue,
                    splineApproximationResolution, nearestPointSubdivisions
                );
                return tValue;
            }).ToList();

            // === Step 3.3: Store Segments between consecutive sorted nodes ===
            for (int i = 0; i < nodesOnThisSpline.Count - 1; i++)
            {
                GraphNode nodeA = nodesOnThisSpline[i];
                GraphNode nodeB = nodesOnThisSpline[i + 1];

                // Ensure we store the pair consistently (e.g., smaller ID first) to avoid duplicates (A,B) vs (B,A)
                GraphNode n1 = nodeA.id < nodeB.id ? nodeA : nodeB;
                GraphNode n2 = nodeA.id < nodeB.id ? nodeB : nodeA;

                if (n1 != n2) // Should always be true here after Distinct logic in node creation
                {
                    validSegments.Add((n1, n2));
                    // Debug.Log($"Adding valid segment between Node {n1.id} ({n1.position}) and Node {n2.id} ({n2.position}) from spline {container.gameObject.name}");
                }
            }
        }

        // === Step 3.4: Build Neighbour Lists based on Valid Segments ===
        foreach (var segment in validSegments)
        {
            GraphNode nodeA = segment.Item1;
            GraphNode nodeB = segment.Item2;

            // Add bidirectional edges
            if (!nodeA.neighbours.Contains(nodeB))
                nodeA.neighbours.Add(nodeB);
            if (!nodeB.neighbours.Contains(nodeA))
                nodeB.neighbours.Add(nodeA);
        }
        // Optional: Log nodes and their final neighbors for debugging
        // foreach(var node in nodes.Values) {
        //     string neighbourIds = string.Join(", ", node.neighbours.Select(n => n.id));
        //     Debug.Log($"Node {node.id} ({node.position}) final neighbours: [{neighbourIds}]");
        // }
    }

    private GraphNode AddNodeIfNew(Vector3 point)
    {
        // Check if a node already exists at this approximate location
        foreach (var node in nodes.Values)
        {
            if (Vector3.Distance(node.position, point) < intersectionMergeThreshold)
            {
                return node; // Return existing node
            }
        }
        // Add new node
        var newNode = new GraphNode(point, nextNodeId++);
        nodes.Add(newNode.id, newNode);
        return newNode;
    }

    // --- Helper for Distinct() with tolerance ---
    public class Vector3EqualityComparer : IEqualityComparer<Vector3>
    {
        private float _toleranceSquared;
        public Vector3EqualityComparer(float tolerance)
        {
            _toleranceSquared = tolerance * tolerance;
        }
        public bool Equals(Vector3 v1, Vector3 v2)
        {
            return (v1 - v2).sqrMagnitude < _toleranceSquared;
        }
        public int GetHashCode(Vector3 obj)
        {
            // Basic hash code - might cause collisions but needed for Distinct
            // A better spatial hashing could be used for performance on huge datasets
            return obj.GetHashCode();
        }
    }


    // --- Step 4: Cycle Finding (DFS Based) ---
    private void FindCyclesInGraph()
    {
        HashSet<int> visitedGlobally = new HashSet<int>();
        foreach (var startNode in nodes.Values)
        {
            if (!visitedGlobally.Contains(startNode.id))
            {
                Stack<GraphNode> path = new Stack<GraphNode>();
                HashSet<int> visitedOnPath = new HashSet<int>();
                DFS_FindCycles(startNode, startNode, path, visitedOnPath, visitedGlobally);
            }
        }
        // Filter duplicates might be needed here depending on DFS implementation
    }

    private void DFS_FindCycles(GraphNode startNode, GraphNode currentNode, Stack<GraphNode> path, HashSet<int> visitedOnPath, HashSet<int> visitedGlobally)
    {
        path.Push(currentNode);
        visitedOnPath.Add(currentNode.id);
        visitedGlobally.Add(currentNode.id); // Mark as visited globally too

        foreach (var neighbour in currentNode.neighbours)
        {
            if (neighbour == path.ElementAtOrDefault(path.Count - 2)) // Don't immediately go back
            {
                continue;
            }

            if (visitedOnPath.Contains(neighbour.id)) // Cycle detected
            {
                // Cycle ends here, neighbour is already on path
                if (neighbour == startNode && path.Count >= 3) // Make sure cycle involves at least 3 nodes
                {
                    // Found cycle back to start node
                    List<Vector3> cyclePoints = new List<Vector3>();
                    // Extract cycle path from stack up to the neighbour
                    bool record = false;
                    foreach (var node in path.Reverse()) // Stack iterates LIFO, reverse needed
                    {
                        if (node == neighbour) record = true;
                        if (record) cyclePoints.Add(node.position);
                    }

                    // Only add if it's a valid polygon and maybe not added yet (needs duplicate check)
                    if (cyclePoints.Count >= 3)
                    {
                        // Basic duplicate check (order matters!) - needs improvement
                        if (!DoesPolygonExist(cyclePoints))
                        {
                            roadNetworkData.plotPolygons.Add(cyclePoints);
                        }
                    }
                }
            }
            else // Not visited on this path yet
            {
                // Only explore if not globally visited *from another starting path*? - depends on goal
                // If allowing paths to merge, remove global check here
                if (!visitedGlobally.Contains(neighbour.id))
                {
                    DFS_FindCycles(startNode, neighbour, path, visitedOnPath, visitedGlobally);
                }
            }
        }

        path.Pop();
        visitedOnPath.Remove(currentNode.id);
    }

    // Very basic polygon existence check - assumes vertex order matters.
    // Needs improvement for robust duplicate detection (e.g., sorting IDs, hashing).
    private bool DoesPolygonExist(List<Vector3> newPolygon)
    {
        foreach (var existing in roadNetworkData.plotPolygons)
        {
            if (existing.Count == newPolygon.Count)
            {
                bool match = true;
                for (int i = 0; i < newPolygon.Count; i++)
                {
                    // Simple comparison, prone to floating point issues & order
                    if (Vector3.Distance(existing[i], newPolygon[i]) > 0.01f)
                    {
                        // Allow for reversed order check too
                        if (Vector3.Distance(existing[i], newPolygon[newPolygon.Count - 1 - i]) > 0.01f)
                        {
                            match = false;
                            break;
                        }
                    }
                }
                if (match) return true;
            }
        }
        return false;
    }


    // --- Optional Gizmo Helper ---
    private void PopulateRoadSegmentsForGizmos()
    {
        roadNetworkData.roadSegments.Clear();
        HashSet<(int, int)> addedEdges = new HashSet<(int, int)>();
        foreach (var node in nodes.Values)
        {
            foreach (var neighbor in node.neighbours)
            {
                int id1 = Mathf.Min(node.id, neighbor.id);
                int id2 = Mathf.Max(node.id, neighbor.id);
                if (addedEdges.Add((id1, id2)))
                { // Only add edge once
                    roadNetworkData.roadSegments.Add((node.position, neighbor.position));
                }
            }
        }
    }


    // --- Line Segment Intersection Helper (Standard Algorithm) ---
    // Returns true if the lines intersect, intersectionPoint is the intersection point
    public static bool LineSegmentIntersection(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 intersectionPoint)
    {
        intersectionPoint = Vector2.zero;

        float dx1 = p2.x - p1.x;
        float dy1 = p2.y - p1.y;
        float dx2 = p4.x - p3.x;
        float dy2 = p4.y - p3.y;

        float denominator = (dy1 * dx2 - dx1 * dy2);

        // Parallel check
        if (Mathf.Abs(denominator) < 0.0001f) return false;

        float t1 = ((p1.x - p3.x) * dy2 + (p3.y - p1.y) * dx2) / denominator;
        float t2 = ((p3.x - p1.x) * dy1 + (p1.y - p3.y) * dx1) / -denominator;

        // Check if intersection lies within both segments
        if (t1 >= 0 && t1 <= 1 && t2 >= 0 && t2 <= 1)
        {
            intersectionPoint = new Vector2(p1.x + dx1 * t1, p1.y + dy1 * t1);
            return true;
        }

        return false;
    }
    // Overload for Vector3 (ignores Z)
    public static bool LineSegmentIntersection(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, out Vector3 intersectionPoint)
    {
        Vector2 p1_2d = new Vector2(p1.x, p1.z);
        Vector2 p2_2d = new Vector2(p2.x, p2.z);
        Vector2 p3_2d = new Vector2(p3.x, p3.z);
        Vector2 p4_2d = new Vector2(p4.x, p4.z);
        if (LineSegmentIntersection(p1_2d, p2_2d, p3_2d, p4_2d, out Vector2 intersection2D))
        {
            // Interpolate Y based on t1 (parameter along segment p1-p2)
            float dx1 = p2.x - p1.x;
            float dy1 = p2.z - p1.z; // Use Z for y in 2D context
            float dx2 = p4.x - p3.x;
            float dy2 = p4.z - p3.z;
            float denominator = (dy1 * dx2 - dx1 * dy2);
            if (Mathf.Abs(denominator) < 0.0001f)
            { // Avoid division by zero
                intersectionPoint = Vector3.zero; // Should not happen if 2D intersected
                return false;
            }
            float t1 = ((p1.x - p3.x) * dy2 + (p3.z - p1.z) * dx2) / denominator; // Use Z for y

            float interpolatedY = Mathf.Lerp(p1.y, p2.y, t1); // Find Y value on segment 1
                                                              // Optional: Average Y with segment 2? Might be more stable.
                                                              // float t2 = ((p3.x - p1.x) * dy1 + (p1.z - p3.z) * dx1) / -denominator;
                                                              // float interpolatedY2 = Mathf.Lerp(p3.y, p4.y, t2);
                                                              // interpolatedY = (interpolatedY + interpolatedY2) * 0.5f;


            intersectionPoint = new Vector3(intersection2D.x, interpolatedY, intersection2D.y);
            return true;
        }
        intersectionPoint = Vector3.zero;
        return false;
    }
}