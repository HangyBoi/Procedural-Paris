// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
//  This script is responsible for generating pavement areas in a procedural generation context, based on a 2D polygon footprint defined by vertices.
//

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates and manages a procedural mesh for a pavement area.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PavementGenerator : MonoBehaviour
{
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _mesh;

    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// Ensures that all required components and the mesh object are ready for use.
    /// </summary>
    private void EnsureInitialized()
    {
        if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
        if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();

        // Create a mesh if it doesn't exist or if the filter is pointing to a different mesh.
        if (_mesh == null || _meshFilter.sharedMesh != _mesh)
        {
            _mesh = new Mesh { name = "PavementInstanceMesh" };
            _meshFilter.mesh = _mesh;
        }
    }

    /// <summary>
    /// Generates the pavement mesh from a 2D polygon footprint.
    /// </summary>
    /// <param name="pavementPlotVertices2D">The list of 2D vertices defining the pavement shape.</param>
    /// <param name="materialToApply">The material to apply to the generated pavement.</param>
    public void GeneratePavement(List<Vector2> pavementPlotVertices2D, Material materialToApply)
    {
        EnsureInitialized();
        _mesh.Clear();

        // A valid polygon requires at least 3 vertices.
        if (pavementPlotVertices2D == null || pavementPlotVertices2D.Count < 3)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        // Convert 2D vertices to 3D, assuming a flat plane at Y=0.
        List<Vector3> vertices3D = new List<Vector3>(pavementPlotVertices2D.Count);
        foreach (var v2 in pavementPlotVertices2D)
        {
            vertices3D.Add(new Vector3(v2.x, 0, v2.y));
        }

        // Ensure vertex winding order is counter-clockwise (CCW) for correct triangulation and normals.
        if (BuildingFootprintUtils.CalculateSignedAreaXZ(vertices3D) > GeometryConstants.GeometricEpsilon)
        {
            vertices3D.Reverse(); // Reverse if clockwise.
        }

        // Triangulate the polygon shape to create the mesh triangles.
        if (GeometryUtils.TriangulatePolygonEarClipping(vertices3D, out List<int> triangles))
        {
            _mesh.SetVertices(vertices3D);
            _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateNormals(); // Normals should point up (towards +Y).
            _mesh.RecalculateBounds();

            ApplyMaterial(materialToApply);
        }
        else
        {
            Debug.LogError($"PavementGenerator: Failed to triangulate pavement for {gameObject.name}. Disabling object.", this);
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Applies the specified material, with a fallback to a default material if none is provided.
    /// </summary>
    private void ApplyMaterial(Material materialToApply)
    {
        if (materialToApply != null)
        {
            _meshRenderer.sharedMaterial = materialToApply;
        }
        else
        {
            Debug.LogWarning($"PavementGenerator: Material not supplied for {gameObject.name}. Using default.", this);
            // Fallback to a standard material if none is assigned.
            if (_meshRenderer.sharedMaterial == null)
            {
                var defaultShader = Shader.Find("Standard");
                if (defaultShader != null)
                {
                    _meshRenderer.sharedMaterial = new Material(defaultShader);
                }
                else
                {
                    Debug.LogError("PavementGenerator: Default 'Standard' shader not found for fallback material.");
                }
            }
        }
    }

    /// <summary>
    /// Clears the generated mesh and deactivates the GameObject.
    /// </summary>
    public void ClearPavement()
    {
        if (_mesh != null)
        {
            _mesh.Clear();
        }
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        // Clean up the created mesh asset to prevent memory leaks in the editor.
        if (_mesh != null)
        {
            if (Application.isEditor && !Application.isPlaying)
                DestroyImmediate(_mesh);
            else
                Destroy(_mesh);
        }
    }
}