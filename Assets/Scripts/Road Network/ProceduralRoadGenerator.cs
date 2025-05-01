using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Used for LINQ queries

// Place this script on a dedicated Manager GameObject
public class ProceduralRoadGenerator : MonoBehaviour
{
    [Header("Generation Parameters")]
    public Vector2 areaSize = new Vector2(50, 50);
    public int numberOfNodes = 30;
    public float minNodeDistance = 2.0f; // Minimum distance between generated nodes
    public int maxConnectionsPerNode = 4;
    public float connectionRadius = 15.0f; // Max distance to look for neighbors
    public float minConnectionAngleDeg = 30.0f; // Min angle between connected segments at a node

    [Header("References")]
    [Tooltip("Drag the GameObject with the RoadNetworkData script here.")]
    public RoadNetworkData roadNetworkData;

    // Internal data structures for graph building
    private class GraphNode
    {
        public Vector3 position;
        public List<GraphNode> neighbours = new List<GraphNode>();
        public int id; // Unique ID for cycle finding
        public List<float> connectionAngles = new List<float>(); // Store angles for constraints
        public GraphNode(Vector3 pos, int id) { this.position = pos; this.id = id; }
    }
    private Dictionary<int, GraphNode> nodes = new Dictionary<int, GraphNode>();
    private int nextNodeId = 0;
    private List<(GraphNode, GraphNode)> edges = new List<(GraphNode, GraphNode)>(); // For intersection checks

    [ContextMenu("Generate Procedural Roads")]
    public void GenerateNetwork()
    {
        if (roadNetworkData == null)
        {
            Debug.LogError("RoadNetworkData reference not set!");
            return;
        }

        // 0. Clear previous data
        roadNetworkData.ClearData();
        nodes.Clear();
        edges.Clear();
        nextNodeId = 0;

        // 1. Generate Nodes
        GenerateNodes();

        // 2. Generate Edges (Connect Nodes)
        GenerateEdges();

        // 3. Find Cycles (Plots) - Reuse same logic as Spline approach
        FindCyclesInGraph();

        // For debugging/visualization: Populate segments in RoadNetworkData
        PopulateRoadSegmentsForGizmos(); // Reuse same logic as Spline approach

        Debug.Log($"Generation complete. Generated {nodes.Count} nodes, {edges.Count} edges and {roadNetworkData.plotPolygons.Count} potential plots.");

    }

    // --- Step 1: Node Generation ---
    private void GenerateNodes()
    {
        int attempts = 0;
        int maxAttempts = numberOfNodes * 10; // Limit attempts to avoid infinite loop

        while (nodes.Count < numberOfNodes && attempts < maxAttempts)
        {
            attempts++;
            // Generate random position within bounds
            float x = Random.Range(-areaSize.x / 2f, areaSize.x / 2f);
            float z = Random.Range(-areaSize.y / 2f, areaSize.y / 2f);
            // Optional: Add some height variation?
            float y = 0; // Or Sample terrain height: Terrain.activeTerrain?.SampleHeight(new Vector3(x, 0, z)) ?? 0;
            Vector3 potentialPos = new Vector3(x, y, z);

            // Check minimum distance from existing nodes
            bool tooClose = false;
            foreach (var node in nodes.Values)
            {
                if (Vector3.Distance(node.position, potentialPos) < minNodeDistance)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                var newNode = new GraphNode(potentialPos, nextNodeId++);
                nodes.Add(newNode.id, newNode);
            }
        }
        if (nodes.Count < numberOfNodes)
        {
            Debug.LogWarning($"Could only generate {nodes.Count}/{numberOfNodes} nodes satisfying min distance.");
        }
    }

    // --- Step 2: Edge Generation ---
    private void GenerateEdges()
    {
        List<GraphNode> nodeList = nodes.Values.ToList(); // For easier iteration/lookup

        // Sort nodes perhaps by proximity for efficiency? Optional.

        foreach (var nodeA in nodes.Values)
        {
            if (nodeA.neighbours.Count >= maxConnectionsPerNode) continue; // Skip if node is full

            // Find nearby potential neighbours (within connectionRadius)
            var potentialNeighbours = nodeList
                .Where(nodeB => nodeA != nodeB && Vector3.Distance(nodeA.position, nodeB.position) < connectionRadius)
                .OrderBy(nodeB => Vector3.Distance(nodeA.position, nodeB.position)) // Connect closest first
                .ToList();

            foreach (var nodeB in potentialNeighbours)
            {
                if (nodeA.neighbours.Count >= maxConnectionsPerNode) break; // Node A is full
                if (nodeB.neighbours.Count >= maxConnectionsPerNode) continue; // Node B is full
                if (nodeA.neighbours.Contains(nodeB)) continue; // Already connected

                Vector3 dirToB = (nodeB.position - nodeA.position).normalized;

                // Angle Constraint Check
                bool angleOk = true;
                float angleToB = Mathf.Atan2(dirToB.z, dirToB.x) * Mathf.Rad2Deg;
                foreach (float existingAngle in nodeA.connectionAngles)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(angleToB, existingAngle)) < minConnectionAngleDeg)
                    {
                        angleOk = false;
                        break;
                    }
                }
                if (!angleOk) continue; // Check node A angles

                // Also check Node B angles
                Vector3 dirToA = (nodeA.position - nodeB.position).normalized;
                float angleToA = Mathf.Atan2(dirToA.z, dirToA.x) * Mathf.Rad2Deg;
                foreach (float existingAngle in nodeB.connectionAngles)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(angleToA, existingAngle)) < minConnectionAngleDeg)
                    {
                        angleOk = false;
                        break;
                    }
                }
                if (!angleOk) continue; // Check node B angles


                // Intersection Check (Check if new edge AB intersects existing edges)
                bool intersects = false;
                foreach (var edge in edges)
                {
                    // Skip edges connected to A or B
                    if (edge.Item1 == nodeA || edge.Item2 == nodeA || edge.Item1 == nodeB || edge.Item2 == nodeB) continue;

                    if (SplineRoadProcessor.LineSegmentIntersection(nodeA.position, nodeB.position, edge.Item1.position, edge.Item2.position, out _))
                    {
                        intersects = true;
                        break;
                    }
                }

                if (!intersects)
                {
                    // Add bidirectional edge
                    nodeA.neighbours.Add(nodeB);
                    nodeB.neighbours.Add(nodeA);
                    nodeA.connectionAngles.Add(angleToB); // Store angle
                    nodeB.connectionAngles.Add(angleToA); // Store angle
                    edges.Add((nodeA, nodeB)); // Add edge to list for intersection checks
                }
            }
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


    // --- Draw Area Bounds Gizmo ---
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 center = transform.position; // Assumes generator is at center 0,0,0
        Vector3 size = new Vector3(areaSize.x, 1f, areaSize.y); // Use Z for Y size
        Gizmos.DrawWireCube(center, size);
    }
}