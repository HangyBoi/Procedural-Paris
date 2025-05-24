using UnityEngine;
using System.Collections.Generic;

// Removed: RoofDebugData struct

/// <summary>
/// Responsible for generating procedural roof layers (mansard, attic, flat top).
/// </summary>
public class RoofGenerator
{
    private readonly PolygonBuildingGenerator _settings;
    private readonly List<PolygonVertexData> _vertexData;
    private readonly BuildingStyleSO _buildingStyle;

    private const string ROOF_FLAT_NAME = "Procedural Flat Roof Cap";
    private const string MANSARD_FLOOR_NAME = "Procedural Mansard Floor";
    private const string ATTIC_FLOOR_NAME = "Procedural Attic Floor";

    public RoofGenerator(PolygonBuildingGenerator settings, List<PolygonVertexData> vertexData, BuildingStyleSO buildingStyle)
    {
        _settings = settings;
        _vertexData = vertexData;
        _buildingStyle = buildingStyle;
    }

    /// <summary>
    /// Generates all roof layers and returns references to the created roof GameObjects.
    /// </summary>
    public GeneratedRoofObjects GenerateMainRoof(Transform roofRoot) // Changed return type
    {
        GeneratedRoofObjects generatedRoofs = new GeneratedRoofObjects();
        if (_vertexData.Count < 3)
        {
            Debug.LogWarning("RoofGenerator: Cannot generate roof, base polygon has less than 3 vertices.");
            return generatedRoofs;
        }

        float wallTopHeight = CalculateTotalWallTopHeight();
        List<Vector3> currentOuterEdgeLoop = new List<Vector3>();
        foreach (var vd in _vertexData)
        {
            currentOuterEdgeLoop.Add(new Vector3(vd.position.x, wallTopHeight, vd.position.z));
        }

        if (_settings.useMansardFloor && _settings.mansardSlopeHorizontalDistance > GeometryUtils.Epsilon)
        {
            List<Vector3> innerMansardEdgeLoop = CalculateInnerRoofEdge(currentOuterEdgeLoop, _settings.mansardSlopeHorizontalDistance, _settings.mansardRiseHeight);
            if (innerMansardEdgeLoop != null && innerMansardEdgeLoop.Count >= 3)
            {
                GenerateStripMeshData(currentOuterEdgeLoop, innerMansardEdgeLoop, out var v, out var t, out var uv);
                Material mat = _settings.mansardMaterial ?? _settings.roofMaterial;
                generatedRoofs.MansardRoofObject = CreateMeshObject(v, t, uv, mat, "MansardFloorMesh", MANSARD_FLOOR_NAME, roofRoot);
                currentOuterEdgeLoop = innerMansardEdgeLoop;
            }
            else Debug.LogWarning("RoofGenerator: Failed to calculate inner mansard edge loop.");
        }

        if (_settings.useAtticFloor && _settings.atticSlopeHorizontalDistance > GeometryUtils.Epsilon)
        {
            List<Vector3> innerAtticEdgeLoop = CalculateInnerRoofEdge(currentOuterEdgeLoop, _settings.atticSlopeHorizontalDistance, _settings.atticRiseHeight);
            if (innerAtticEdgeLoop != null && innerAtticEdgeLoop.Count >= 3)
            {
                GenerateStripMeshData(currentOuterEdgeLoop, innerAtticEdgeLoop, out var v, out var t, out var uv);
                Material mat = _settings.atticMaterial ?? _settings.roofMaterial;
                generatedRoofs.AtticRoofObject = CreateMeshObject(v, t, uv, mat, "AtticFloorMesh", ATTIC_FLOOR_NAME, roofRoot);
                currentOuterEdgeLoop = innerAtticEdgeLoop;
            }
            else Debug.LogWarning("RoofGenerator: Failed to calculate inner attic edge loop.");
        }

        List<Vector3> flatRoofPerimeter;
        if (Mathf.Abs(_settings.flatRoofEdgeOffset) > GeometryUtils.Epsilon)
        {
            flatRoofPerimeter = CalculateInnerRoofEdge(currentOuterEdgeLoop, -_settings.flatRoofEdgeOffset, 0f);
            if (flatRoofPerimeter == null || flatRoofPerimeter.Count < 3)
            {
                Debug.LogWarning("RoofGenerator: Flat roof offset calculation failed.");
                flatRoofPerimeter = new List<Vector3>(currentOuterEdgeLoop);
            }
        }
        else flatRoofPerimeter = new List<Vector3>(currentOuterEdgeLoop);

        if (flatRoofPerimeter.Count >= 3)
        {
            if (GeometryUtils.TriangulatePolygonEarClipping(flatRoofPerimeter, out List<int> capTris))
            {
                List<Vector2> capUVs = CalculatePlanarUVs(flatRoofPerimeter, _settings.roofUvScale);
                // We could assign to generatedRoofs.FlatRoofObject here if needed
                CreateMeshObject(flatRoofPerimeter, capTris, capUVs, _settings.roofMaterial, "FlatRoofCapMesh", ROOF_FLAT_NAME, roofRoot);
            }
            else Debug.LogError("RoofGenerator: Flat Roof cap triangulation failed.");
        }
        else Debug.LogWarning("RoofGenerator: Cannot generate flat roof cap < 3 vertices.");

        return generatedRoofs;
    }

    private List<Vector3> CalculateInnerRoofEdge(List<Vector3> outerLoop, float horizontalDistance, float riseHeight)
    {
        if (outerLoop == null || outerLoop.Count < 3)
        {
            Debug.LogError("CalculateInnerRoofEdge: Outer loop is null or has less than 3 vertices.");
            return null;
        }

        if (_vertexData.Count != outerLoop.Count)
        {
            Debug.LogError($"CalculateInnerRoofEdge: VertexData count ({_vertexData.Count}) mismatch with outerLoop count ({outerLoop.Count}). This implies an issue with how loops are passed or generated. The offsetting logic may be compromised.");
        }

        List<Vector3> innerVertices = new List<Vector3>(outerLoop.Count);
        int n = outerLoop.Count;

        for (int i = 0; i < n; i++)
        {
            Vector3 p1_base = _vertexData[(i + n - 1) % n].position;
            Vector3 p2_base = _vertexData[i % n].position;
            Vector3 p3_base = _vertexData[(i + 1) % n].position;

            Vector3 p2_outer = outerLoop[i];

            Vector3 sideDirPrev_base = (p2_base - p1_base).normalized;
            Vector3 sideDirNext_base = (p3_base - p2_base).normalized;
            Vector3 normalPrev_base = PolygonGeometry.CalculateSideNormal(p1_base, p2_base, _vertexData);
            Vector3 normalNext_base = PolygonGeometry.CalculateSideNormal(p2_base, p3_base, _vertexData);

            Vector3 innerVertexPosXZ;

            if (Mathf.Abs(horizontalDistance) < GeometryUtils.Epsilon)
            {
                innerVertexPosXZ = new Vector3(p2_outer.x, 0, p2_outer.z);
            }
            else
            {
                Vector3 lineOriginPrev_XZ = new Vector3(p1_base.x, 0, p1_base.z) - normalPrev_base * horizontalDistance;
                Vector3 lineDirPrev_XZ = sideDirPrev_base;

                Vector3 lineOriginNext_XZ = new Vector3(p2_base.x, 0, p2_base.z) - normalNext_base * horizontalDistance;
                Vector3 lineDirNext_XZ = sideDirNext_base;

                if (GeometryUtils.LineLineIntersection(lineOriginPrev_XZ, lineDirPrev_XZ, lineOriginNext_XZ, lineDirNext_XZ, out innerVertexPosXZ))
                {
                    // Intersection found
                }
                else
                {
                    Vector3 avgNormal = (normalPrev_base + normalNext_base).normalized;
                    if (avgNormal.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon)
                    {
                        avgNormal = new Vector3(sideDirPrev_base.z, 0, -sideDirPrev_base.x);
                    }
                    innerVertexPosXZ = new Vector3(p2_outer.x, 0, p2_outer.z) - avgNormal * horizontalDistance;
                }
            }
            float innerCornerY = p2_outer.y + riseHeight;
            innerVertices.Add(new Vector3(innerVertexPosXZ.x, innerCornerY, innerVertexPosXZ.z));
        }
        return innerVertices;
    }

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

        meshVertices.AddRange(outerVertices);
        meshVertices.AddRange(innerVertices);

        foreach (Vector3 v in meshVertices)
        {
            meshUVs.Add(new Vector2(v.x * _settings.roofUvScale, v.z * _settings.roofUvScale));
        }

        int N = outerVertices.Count;
        for (int i = 0; i < N; i++)
        {
            int currentOuter = i;
            int nextOuter = (i + 1) % N;
            int currentInner = i + N;
            int nextInner = ((i + 1) % N) + N;

            meshTriangles.Add(currentOuter);
            meshTriangles.Add(nextOuter);
            meshTriangles.Add(nextInner);

            meshTriangles.Add(currentOuter);
            meshTriangles.Add(nextInner);
            meshTriangles.Add(currentInner);
        }
    }

    private List<Vector2> CalculatePlanarUVs(List<Vector3> vertices, float uvScale)
    {
        List<Vector2> uvs = new List<Vector2>(vertices.Count);
        foreach (Vector3 v in vertices)
        {
            uvs.Add(new Vector2(v.x * uvScale, v.z * uvScale));
        }
        return uvs;
    }

    private GameObject CreateMeshObject(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, Material material, string meshName, string gameObjectName, Transform parent)
    {
        if (vertices.Count == 0 || triangles.Count == 0)
        {
            Debug.LogWarning($"CreateMeshObject: Attempted to create empty mesh for '{gameObjectName}'.");
            return null;
        }

        Mesh mesh = new Mesh { name = meshName };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        if (uvs != null && uvs.Count == vertices.Count)
        {
            mesh.SetUVs(0, uvs);
        }
        else if (uvs != null)
        {
            Debug.LogWarning($"CreateMeshObject: UV count mismatch for '{meshName}'. UVs not applied.");
        }

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject meshObject = new GameObject(gameObjectName);
        meshObject.transform.SetParent(parent, false);

        MeshFilter mf = meshObject.AddComponent<MeshFilter>();
        mf.mesh = mesh;

        MeshRenderer mr = meshObject.AddComponent<MeshRenderer>();
        mr.material = material;
        if (material == null)
        {
            Debug.LogWarning($"CreateMeshObject: Material for '{gameObjectName}' is null. Object will likely be invisible or use default pink.");
        }

        // Add MeshCollider for raycasting
        MeshCollider mc = meshObject.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;

        return meshObject;
    }

    private float CalculateTotalWallTopHeight()
    {
        float height = 0;
        height += _settings.floorHeight;
        height += _settings.middleFloors * _settings.floorHeight;
        return height;
    }
}