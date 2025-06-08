// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
//  This script is reponsible for generating procedural roof layers (mansard, attic, flat top) based on the building's polygon shape and style preferences.
//

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Responsible for generating procedural roof layers (mansard, attic, flat top).
/// </summary>
public class RoofGenerator
{
    // Constants for naming generated objects consistently.
    private const string ROOF_FLAT_NAME = "Procedural Flat Roof Cap";
    private const string MANSARD_FLOOR_NAME = "Procedural Mansard Floor";
    private const string ATTIC_FLOOR_NAME = "Procedural Attic Floor";

    private readonly PolygonBuildingGenerator _settings;
    private readonly List<PolygonVertexData> _vertexData;
        private readonly BuildingStyleSO _buildingStyle;
    private readonly GeneratedBuildingElements _elementsStore;

    public RoofGenerator(PolygonBuildingGenerator settings, List<PolygonVertexData> vertexData, BuildingStyleSO buildingStyle, GeneratedBuildingElements elementsStore)
    {
        _settings = settings;
        _vertexData = vertexData;
        _buildingStyle = buildingStyle;
        _elementsStore = elementsStore;
    }

    /// <summary>
    /// Generates all roof layers based on the building settings.
    /// The process is sequential: Mansard -> Attic -> Flat Cap.
    /// </summary>
    /// <param name="roofRoot">The parent transform for all generated roof objects.</param>
    /// <param name="success">Outputs true if all stages completed successfully, false otherwise.</param>
    /// <returns>A data structure containing references to the generated roof GameObjects.</returns>
    public GeneratedRoofObjects GenerateMainRoof(Transform roofRoot, out bool success)
    {
        success = true;
        var generatedRoofs = new GeneratedRoofObjects();

        if (_vertexData.Count < 3)
        {
            Debug.LogWarning("RoofGenerator: Base polygon has fewer than 3 vertices. Cannot generate roof.");
            success = false;
            return generatedRoofs;
        }

        // --- Stage 1: Initial Setup ---
        float wallTopHeight = _settings.floorHeight * (1 + _settings.middleFloors);
        List<Vector3> currentEdgeLoop = _vertexData.Select(vd => new Vector3(vd.position.x, wallTopHeight, vd.position.z)).ToList();

        // --- Stage 2: Generate Mansard Layer ---
        if (_settings.useMansardFloor)
        {
            generatedRoofs.MansardRoofObject = GenerateSlopedRoofLayer(ref currentEdgeLoop, roofRoot,
                _settings.mansardSlopeHorizontalDistance, _settings.mansardRiseHeight,
                _settings.mansardMaterial ?? _settings.roofMaterial, MANSARD_FLOOR_NAME, ref success);
        }
        if (!success) return generatedRoofs;

        // --- Stage 3: Generate Attic Layer ---
        if (_settings.useAtticFloor)
        {
            generatedRoofs.AtticRoofObject = GenerateSlopedRoofLayer(ref currentEdgeLoop, roofRoot,
                _settings.atticSlopeHorizontalDistance, _settings.atticRiseHeight,
                _settings.atticMaterial ?? _settings.roofMaterial, ATTIC_FLOOR_NAME, ref success);
        }
        if (!success) return generatedRoofs;

        // --- Stage 4: Generate Flat Roof Cap ---
        generatedRoofs.FlatRoofObject = GenerateFlatCap(currentEdgeLoop, roofRoot, ref success);

        // Store final created objects. Note: Side-effect inside CreateMeshObject handles this.
        generatedRoofs.AtticRoofObject = _elementsStore.atticRoofMeshObject;
        generatedRoofs.MansardRoofObject = _elementsStore.mansardRoofMeshObject;
        generatedRoofs.FlatRoofObject = _elementsStore.flatRoofMeshObject;

        return generatedRoofs;
    }

    /// <summary>
    /// Generates a single sloped roof layer (like a mansard or attic section).
    /// </summary>
    /// <returns>The generated GameObject for the sloped layer, or null on failure.</returns>
    private GameObject GenerateSlopedRoofLayer(ref List<Vector3> outerEdgeLoop, Transform parent, float horizontalInset, float verticalRise, Material material, string objectName, ref bool success)
    {
        if (horizontalInset <= GeometryConstants.GeometricEpsilon) return null;

        List<Vector3> innerEdgeLoop = CalculateInnerRoofEdge(outerEdgeLoop, horizontalInset, verticalRise);
        if (innerEdgeLoop == null || innerEdgeLoop.Count < 3)
        {
            Debug.LogWarning($"RoofGenerator: Failed to calculate inner edge loop for '{objectName}'.");
            success = false;
            return null;
        }

        GenerateStripMeshData(outerEdgeLoop, innerEdgeLoop, out var vertices, out var triangles, out var uvs);
        outerEdgeLoop = innerEdgeLoop; // The inner loop becomes the outer loop for the next stage.

        return CreateMeshObject(vertices, triangles, uvs, material, $"{objectName}Mesh", objectName, parent);
    }

    /// <summary>
    /// Generates the final flat roof cap on top of the structure.
    /// </summary>
    /// <returns>The generated GameObject for the flat cap, or null on failure.</returns>
    private GameObject GenerateFlatCap(List<Vector3> perimeter, Transform parent, ref bool success)
    {
        // Apply an optional overhang or inset to the final perimeter.
        if (Mathf.Abs(_settings.flatRoofEdgeOffset) > GeometryConstants.GeometricEpsilon)
        {
            List<Vector3> offsetPerimeter = CalculateInnerRoofEdge(perimeter, -_settings.flatRoofEdgeOffset, 0f);
            if (offsetPerimeter != null && offsetPerimeter.Count >= 3)
            {
                perimeter = offsetPerimeter;
            }
            else
            {
                Debug.LogWarning("RoofGenerator: Flat roof offset calculation failed. Using non-offset perimeter.");
            }
        }

        if (perimeter == null || perimeter.Count < 3)
        {
            Debug.LogWarning($"RoofGenerator: Final roof perimeter is degenerate. Skipping cap. Vertices: {perimeter?.Count ?? 0}");
            success = false;
            return null;
        }

        // Triangulate the flat 2D shape.
        if (GeometryUtils.TriangulatePolygonEarClipping(perimeter, out List<int> capTris))
        {
            List<Vector2> capUVs = perimeter.Select(v => new Vector2(v.x * _settings.roofUvScale, v.z * _settings.roofUvScale)).ToList();
            return CreateMeshObject(perimeter, capTris, capUVs, _settings.roofMaterial, "FlatRoofCapMesh", ROOF_FLAT_NAME, parent);
        }

        Debug.LogError($"RoofGenerator: Flat Roof cap triangulation failed for '{ROOF_FLAT_NAME}'.");
        success = false;
        return null;
    }

    /// <summary>
    /// Calculates an inner perimeter by offsetting each edge of an outer loop inwards.
    /// </summary>
    /// <returns>A new list of vertices for the inner loop, or null if calculation fails.</returns>
    private List<Vector3> CalculateInnerRoofEdge(List<Vector3> outerLoop, float horizontalDistance, float riseHeight)
    {
        if (outerLoop == null || outerLoop.Count < 3) return null;

        var innerVertices = new List<Vector3>(outerLoop.Count);
        int n = outerLoop.Count;

        for (int i = 0; i < n; i++)
        {
            // Use original building footprint for stable normal/direction calculations.
            Vector3 p1_base = _vertexData[(i + n - 1) % n].position;
            Vector3 p2_base = _vertexData[i].position;
            Vector3 p3_base = _vertexData[(i + 1) % n].position;

            // Get normals and directions of the adjacent sides.
            Vector3 sideDirPrev_base = (p2_base - p1_base).normalized;
            Vector3 normalPrev_base = BuildingFootprintUtils.CalculateSideNormal(p1_base, p2_base, _vertexData);
            Vector3 normalNext_base = BuildingFootprintUtils.CalculateSideNormal(p2_base, p3_base, _vertexData);

            // Create two lines representing the offset edges.
            Vector3 lineOriginPrev_XZ = new Vector3(p1_base.x, 0, p1_base.z) - normalPrev_base * horizontalDistance;
            Vector3 lineOriginNext_XZ = new Vector3(p2_base.x, 0, p2_base.z) - normalNext_base * horizontalDistance;

            // The intersection of these offset lines gives the new inner corner position.
            if (GeometryUtils.LineLineIntersection(lineOriginPrev_XZ, sideDirPrev_base, lineOriginNext_XZ, (p3_base - p2_base).normalized, out Vector3 innerVertexPosXZ))
            {
                float innerCornerY = outerLoop[i].y + riseHeight;
                innerVertices.Add(new Vector3(innerVertexPosXZ.x, innerCornerY, innerVertexPosXZ.z));
            }
            else // Fallback for parallel lines (e.g., a 180-degree corner).
            {
                Debug.LogWarning($"RoofGenerator: Line intersection failed at index {i}. Using fallback method.");
                Vector3 avgNormal = (normalPrev_base + normalNext_base).normalized;
                if (avgNormal.sqrMagnitude < GeometryConstants.GeometricEpsilonSqr)
                    avgNormal = new Vector3(sideDirPrev_base.z, 0, -sideDirPrev_base.x); // Perpendicular fallback.

                Vector3 fallbackPosXZ = new Vector3(outerLoop[i].x, 0, outerLoop[i].z) - avgNormal * horizontalDistance;
                float innerCornerY = outerLoop[i].y + riseHeight;
                innerVertices.Add(new Vector3(fallbackPosXZ.x, innerCornerY, fallbackPosXZ.z));
            }
        }
        return innerVertices;
    }

    /// <summary>
    /// Creates the vertices, triangles, and UVs for a strip of quads between two edge loops.
    /// </summary>
    private void GenerateStripMeshData(List<Vector3> outerVertices, List<Vector3> innerVertices,
                                       out List<Vector3> meshVertices, out List<int> meshTriangles, out List<Vector2> meshUVs)
    {
        meshVertices = new List<Vector3>(outerVertices.Count + innerVertices.Count);
        meshTriangles = new List<int>();

        meshVertices.AddRange(outerVertices);
        meshVertices.AddRange(innerVertices);

        meshUVs = meshVertices.Select(v => new Vector2(v.x * _settings.roofUvScale, v.z * _settings.roofUvScale)).ToList();

        int n = outerVertices.Count;
        for (int i = 0; i < n; i++)
        {
            int currentOuter = i;
            int nextOuter = (i + 1) % n;
            int currentInner = i + n;
            int nextInner = ((i + 1) % n) + n;

            // First triangle of the quad
            meshTriangles.Add(currentOuter);
            meshTriangles.Add(nextOuter);
            meshTriangles.Add(nextInner);
            // Second triangle of the quad
            meshTriangles.Add(currentOuter);
            meshTriangles.Add(nextInner);
            meshTriangles.Add(currentInner);
        }
    }

    /// <summary>
    /// Factory method to create a GameObject with a procedural mesh and necessary components.
    /// </summary>
    /// <returns>The created GameObject, or null if mesh data is invalid.</returns>
    private GameObject CreateMeshObject(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, Material material, string meshName, string gameObjectName, Transform parent)
    {
        if (vertices.Count == 0 || triangles.Count == 0)
        {
            Debug.LogWarning($"CreateMeshObject: Attempted to create empty mesh for '{gameObjectName}'.");
            return null;
        }

        var meshObject = new GameObject(gameObjectName);
        meshObject.transform.SetParent(parent, false);

        var mesh = new Mesh { name = meshName };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        if (uvs != null && uvs.Count == vertices.Count) mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshObject.AddComponent<MeshFilter>().mesh = mesh;
        meshObject.AddComponent<MeshRenderer>().material = material;
        meshObject.AddComponent<MeshCollider>().sharedMesh = mesh;

        // This method has a side-effect of populating the central element store.
        if (gameObjectName == MANSARD_FLOOR_NAME) _elementsStore.mansardRoofMeshObject = meshObject;
        else if (gameObjectName == ATTIC_FLOOR_NAME) _elementsStore.atticRoofMeshObject = meshObject;
        else if (gameObjectName == ROOF_FLAT_NAME) _elementsStore.flatRoofMeshObject = meshObject;

        return meshObject;
    }
}