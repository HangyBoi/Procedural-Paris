using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Data structure to hold references to generated roof meshes and transforms for debugging.
/// </summary>
public struct RoofDebugData
{
    public Mesh FlatRoofMesh;
    public Transform FlatRoofTransform;
    public Mesh MansardMesh;
    public Transform MansardTransform;
    public Mesh AtticMesh;
    public Transform AtticTransform;
}

/// <summary>
/// Responsible for generating procedural roof layers (mansard, attic, flat top).
/// </summary>
public class RoofGenerator
{
    private readonly PolygonBuildingGeneratorMain _settings;
    private readonly List<PolygonVertexData> _vertexData; // Base polygon for initial roof edge
    private readonly BuildingStyleSO _buildingStyle; // For materials or future roof prefabs

    private const string ROOF_FLAT_NAME = "Procedural Flat Roof Cap";
    private const string MANSARD_FLOOR_NAME = "Procedural Mansard Floor";
    private const string ATTIC_FLOOR_NAME = "Procedural Attic Floor";


    public RoofGenerator(PolygonBuildingGeneratorMain settings, List<PolygonVertexData> vertexData, BuildingStyleSO buildingStyle)
    {
        _settings = settings;
        _vertexData = vertexData;
        _buildingStyle = buildingStyle;
    }

    /// <summary>
    /// Generates all roof layers and returns debug information.
    /// </summary>
    public RoofDebugData GenerateMainRoof(Transform roofRoot)
    {
        RoofDebugData debugData = new RoofDebugData();
        if (_vertexData.Count < 3)
        {
            Debug.LogWarning("RoofGenerator: Cannot generate roof, base polygon has less than 3 vertices.");
            return debugData;
        }

        float wallTopHeight = CalculateTotalWallTopHeight();

        // Initial outer edge loop for the roof, at the top of the main walls
        List<Vector3> currentOuterEdgeLoop = new List<Vector3>();
        foreach (var vd in _vertexData)
        {
            currentOuterEdgeLoop.Add(new Vector3(vd.position.x, wallTopHeight, vd.position.z));
        }

        // --- Generate Mansard Floor Mesh (if enabled) ---
        if (_settings.useMansardFloor && _settings.mansardSlopeHorizontalDistance > GeometryUtils.Epsilon)
        {
            List<Vector3> innerMansardEdgeLoop = CalculateInnerRoofEdge(currentOuterEdgeLoop, _settings.mansardSlopeHorizontalDistance, _settings.mansardRiseHeight);

            if (innerMansardEdgeLoop != null && innerMansardEdgeLoop.Count >= 3)
            {
                GenerateStripMeshData(currentOuterEdgeLoop, innerMansardEdgeLoop,
                                      out List<Vector3> mansardMeshVertices,
                                      out List<int> mansardMeshTriangles,
                                      out List<Vector2> mansardMeshUVs);

                Material mat = _settings.mansardMaterial != null ? _settings.mansardMaterial : _settings.roofMaterial; // Fallback to general roof material
                GameObject mansardObj = CreateMeshObject(mansardMeshVertices, mansardMeshTriangles, mansardMeshUVs, mat, "MansardFloorMesh", MANSARD_FLOOR_NAME, roofRoot);

                if (mansardObj != null)
                {
                    debugData.MansardMesh = mansardObj.GetComponent<MeshFilter>()?.sharedMesh;
                    debugData.MansardTransform = mansardObj.transform;
                }
                currentOuterEdgeLoop = innerMansardEdgeLoop; // Next roof layer builds upon this new inner edge
            }
            else
            {
                Debug.LogWarning("RoofGenerator: Failed to calculate inner mansard edge loop or not enough vertices, skipping mansard mesh generation.");
            }
        }

        // --- Generate Attic Floor Mesh (if enabled) ---
        if (_settings.useAtticFloor && _settings.atticSlopeHorizontalDistance > GeometryUtils.Epsilon)
        {
            List<Vector3> innerAtticEdgeLoop = CalculateInnerRoofEdge(currentOuterEdgeLoop, _settings.atticSlopeHorizontalDistance, _settings.atticRiseHeight);

            if (innerAtticEdgeLoop != null && innerAtticEdgeLoop.Count >= 3)
            {
                GenerateStripMeshData(currentOuterEdgeLoop, innerAtticEdgeLoop,
                                      out List<Vector3> atticMeshVertices,
                                      out List<int> atticMeshTriangles,
                                      out List<Vector2> atticMeshUVs);
                Material mat = _settings.atticMaterial != null ? _settings.atticMaterial : _settings.roofMaterial;
                GameObject atticObj = CreateMeshObject(atticMeshVertices, atticMeshTriangles, atticMeshUVs, mat, "AtticFloorMesh", ATTIC_FLOOR_NAME, roofRoot);

                if (atticObj != null)
                {
                    debugData.AtticMesh = atticObj.GetComponent<MeshFilter>()?.sharedMesh;
                    debugData.AtticTransform = atticObj.transform;
                }
                currentOuterEdgeLoop = innerAtticEdgeLoop;
            }
            else
            {
                Debug.LogWarning("RoofGenerator: Failed to calculate inner attic edge loop or not enough vertices, skipping attic mesh generation.");
            }
        }

        // --- Generate Final Top Roof Cap (Flat) ---
        List<Vector3> flatRoofPerimeter;
        if (Mathf.Abs(_settings.flatRoofEdgeOffset) > GeometryUtils.Epsilon)
        {
            // A negative offset here means the flat roof overhangs the 'currentOuterEdgeLoop'
            flatRoofPerimeter = CalculateInnerRoofEdge(currentOuterEdgeLoop, -_settings.flatRoofEdgeOffset, 0f);
            if (flatRoofPerimeter == null || flatRoofPerimeter.Count < 3)
            {
                Debug.LogWarning("RoofGenerator: Flat roof offset calculation failed, using un-offseted perimeter.");
                flatRoofPerimeter = new List<Vector3>(currentOuterEdgeLoop); // Fallback
            }
        }
        else
        {
            flatRoofPerimeter = new List<Vector3>(currentOuterEdgeLoop);
        }


        if (flatRoofPerimeter.Count < 3)
        {
            Debug.LogWarning("RoofGenerator: Cannot generate flat roof cap: Less than 3 perimeter vertices.");
            return debugData;
        }

        if (!GeometryUtils.TriangulatePolygonEarClipping(flatRoofPerimeter, out List<int> capMeshTriangles))
        {
            Debug.LogError("RoofGenerator: Flat Roof cap triangulation failed.");
            return debugData;
        }

        List<Vector3> capMeshVertices = flatRoofPerimeter; // Vertices are the perimeter points
        List<Vector2> capMeshUVs = CalculatePlanarUVs(capMeshVertices, _settings.roofUvScale);

        GameObject roofObject = CreateMeshObject(capMeshVertices, capMeshTriangles, capMeshUVs, _settings.roofMaterial, "FlatRoofCapMesh", ROOF_FLAT_NAME, roofRoot);

        if (roofObject != null)
        {
            debugData.FlatRoofMesh = roofObject.GetComponent<MeshFilter>()?.sharedMesh;
            debugData.FlatRoofTransform = roofObject.transform;
        }
        return debugData;
    }


    /// <summary>
    /// Calculates an inner (or outer if horizontalDistance is negative) edge loop for a roof layer.
    /// This is the core of the procedural roof slope generation.
    /// </summary>
    /// <param name="outerLoop">The existing outer edge of the roof layer.</param>
    /// <param name="horizontalDistance">How far inward (positive) or outward (negative) to move horizontally.</param>
    /// <param name="riseHeight">How much to raise the new inner edge vertically.</param>
    /// <returns>A new list of vertices forming the inner edge, or null on failure.</returns>
    private List<Vector3> CalculateInnerRoofEdge(List<Vector3> outerLoop, float horizontalDistance, float riseHeight)
    {
        if (outerLoop == null || outerLoop.Count < 3)
        {
            Debug.LogError("CalculateInnerRoofEdge: Outer loop is null or has less than 3 vertices.");
            return null;
        }

        // This method relies on the _vertexData (base polygon) for consistent normal calculations,
        // even as outerLoop changes height and position.
        if (_vertexData.Count != outerLoop.Count)
        {
            Debug.LogError($"CalculateInnerRoofEdge: VertexData count ({_vertexData.Count}) mismatch with outerLoop count ({outerLoop.Count}). This implies an issue with how loops are passed or generated. The offsetting logic may be compromised.");
        }

        List<Vector3> innerVertices = new List<Vector3>(outerLoop.Count);
        int n = outerLoop.Count; // Use outerLoop.Count as 'n' for safety if counts mismatch

        for (int i = 0; i < n; i++)
        {
            // Base polygon vertices corresponding to the current outerLoop vertex and its neighbors.
            // These define the fundamental geometry for normal/direction calculation.
            Vector3 p1_base = _vertexData[(i + n - 1) % n].position; // Previous vertex in base polygon
            Vector3 p2_base = _vertexData[i % n].position;           // Current vertex in base polygon
            Vector3 p3_base = _vertexData[(i + 1) % n].position; // Next vertex in base polygon

            Vector3 p2_outer = outerLoop[i]; // The current vertex on the *current* outer edge being processed

            // Directions and normals based on the *base polygon* for consistent offsetting
            Vector3 sideDirPrev_base = (p2_base - p1_base).normalized; // Normalized direction of incoming side
            Vector3 sideDirNext_base = (p3_base - p2_base).normalized; // Normalized direction of outgoing side
            Vector3 normalPrev_base = PolygonGeometry.CalculateSideNormal(p1_base, p2_base, _vertexData);
            Vector3 normalNext_base = PolygonGeometry.CalculateSideNormal(p2_base, p3_base, _vertexData);

            Vector3 innerVertexPosXZ; // This will be the XZ plane position of the new inner vertex

            if (Mathf.Abs(horizontalDistance) < GeometryUtils.Epsilon)
            {
                // No horizontal offset, just use the XZ of the outer vertex
                innerVertexPosXZ = new Vector3(p2_outer.x, 0, p2_outer.z);
            }
            else
            {
                // Define two lines on the XZ plane, offset from the base polygon sides.
                // The intersection of these lines gives the new inner corner's XZ position.
                Vector3 lineOriginPrev_XZ = new Vector3(p1_base.x, 0, p1_base.z) - normalPrev_base * horizontalDistance;
                Vector3 lineDirPrev_XZ = sideDirPrev_base; // Line runs parallel to the original side

                Vector3 lineOriginNext_XZ = new Vector3(p2_base.x, 0, p2_base.z) - normalNext_base * horizontalDistance;
                Vector3 lineDirNext_XZ = sideDirNext_base;

                if (GeometryUtils.LineLineIntersection(lineOriginPrev_XZ, lineDirPrev_XZ, lineOriginNext_XZ, lineDirNext_XZ, out innerVertexPosXZ))
                {
                    // Intersection found, this is our XZ position.
                }
                else // Lines are parallel (e.g., a straight segment in a polygon that's been offset)
                {
                    // Fallback: Offset along the average normal or a bisector.
                    // Average of the two side normals (should be robust for straight sections where normals are same)
                    Vector3 avgNormal = (normalPrev_base + normalNext_base).normalized;
                    if (avgNormal.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon) // If normals cancelled (e.g. 180 deg turn)
                    {
                        // Fallback to perpendicular of one of the sides (e.g. first side)
                        avgNormal = new Vector3(sideDirPrev_base.z, 0, -sideDirPrev_base.x); // Normal to sideDirPrev
                    }
                    innerVertexPosXZ = new Vector3(p2_outer.x, 0, p2_outer.z) - avgNormal * horizontalDistance;
                }
            }

            // The Y position of the inner vertex is the outer vertex's Y plus the rise height
            float innerCornerY = p2_outer.y + riseHeight;
            innerVertices.Add(new Vector3(innerVertexPosXZ.x, innerCornerY, innerVertexPosXZ.z));
        }
        return innerVertices;
    }


    /// <summary>
    /// Generates mesh data for a strip connecting an outer and inner loop of vertices (e.g., for mansard walls).
    /// </summary>
    private void GenerateStripMeshData(List<Vector3> outerVertices, List<Vector3> innerVertices,
                                       out List<Vector3> meshVertices, out List<int> meshTriangles, out List<Vector2> meshUVs)
    {
        meshVertices = new List<Vector3>();
        meshTriangles = new List<int>();
        meshUVs = new List<Vector2>();

        if (outerVertices.Count != innerVertices.Count || outerVertices.Count < 3)
        {
            Debug.LogError("GenerateStripMeshData: Vertex count mismatch or insufficient vertices.");
            return;
        }

        // Combine all vertices: outer loop first, then inner loop
        meshVertices.AddRange(outerVertices);
        meshVertices.AddRange(innerVertices);

        // Simple planar UV projection based on world XZ coordinates
        // This can lead to stretching on steep slopes or varied orientations.
        foreach (Vector3 v in meshVertices)
        {
            meshUVs.Add(new Vector2(v.x * _settings.roofUvScale, v.z * _settings.roofUvScale));
        }

        int N = outerVertices.Count; // Number of vertices in one loop
        for (int i = 0; i < N; i++)
        {
            // Indices for the current quad
            int currentOuter = i;
            int nextOuter = (i + 1) % N;
            int currentInner = i + N;          // Offset by N to get to inner loop vertices
            int nextInner = ((i + 1) % N) + N;

            // Triangle 1 of the quad (Outer1, NextOuter, NextInner)
            meshTriangles.Add(currentOuter);
            meshTriangles.Add(nextOuter);
            meshTriangles.Add(nextInner);

            // Triangle 2 of the quad (Outer1, NextInner, Inner1)
            meshTriangles.Add(currentOuter);
            meshTriangles.Add(nextInner);
            meshTriangles.Add(currentInner);
        }
    }

    /// <summary>
    /// Calculates planar UV coordinates based on XZ positions of vertices.
    /// </summary>
    private List<Vector2> CalculatePlanarUVs(List<Vector3> vertices, float uvScale)
    {
        List<Vector2> uvs = new List<Vector2>(vertices.Count);
        foreach (Vector3 v in vertices)
        {
            uvs.Add(new Vector2(v.x * uvScale, v.z * uvScale));
        }
        return uvs;
    }

    /// <summary>
    /// Creates a GameObject with a MeshFilter and MeshRenderer for the given mesh data.
    /// </summary>
    private GameObject CreateMeshObject(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, Material material, string meshName, string gameObjectName, Transform parent)
    {
        if (vertices.Count == 0 || triangles.Count == 0)
        {
            Debug.LogWarning($"CreateMeshObject: Attempted to create empty mesh for '{gameObjectName}'.");
            return null;
        }

        Mesh mesh = new Mesh { name = meshName };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0); // Submesh index 0
        if (uvs != null && uvs.Count == vertices.Count)
        {
            mesh.SetUVs(0, uvs); // UV channel 0
        }
        else if (uvs != null)
        {
            Debug.LogWarning($"CreateMeshObject: UV count mismatch for '{meshName}'. UVs not applied.");
        }

        mesh.RecalculateNormals(); // Essential for lighting
        mesh.RecalculateBounds();  // For culling and other bounds-based operations

        GameObject meshObject = new GameObject(gameObjectName);
        meshObject.transform.SetParent(parent, false); // Set parent, don't change world position/scale initially

        MeshFilter mf = meshObject.AddComponent<MeshFilter>();
        mf.mesh = mesh;

        MeshRenderer mr = meshObject.AddComponent<MeshRenderer>();
        mr.material = material; // Assign the specified material
        if (material == null)
        {
            Debug.LogWarning($"CreateMeshObject: Material for '{gameObjectName}' is null. Object will likely be invisible or use default pink.");
        }

        return meshObject;
    }

    /// <summary>
    /// Calculates the total height of the main walls (ground + middle floors).
    /// </summary>
    private float CalculateTotalWallTopHeight()
    {
        float height = 0;
        height += _settings.floorHeight; // Ground floor
        height += _settings.middleFloors * _settings.floorHeight; // Middle floors
        return height;
    }
}