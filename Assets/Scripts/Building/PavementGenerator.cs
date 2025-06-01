// PavementGenerator.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PavementGenerator : MonoBehaviour
{
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _mesh;

    void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
        _mesh = new Mesh { name = "PavementInstanceMesh" };
        _meshFilter.mesh = _mesh;
    }

    public void GeneratePavement(List<Vector2> pavementPlotVertices2D, Material materialToApply)
    {
        if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>(); // Ensure components are fetched if Awake hasn't run
        if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();

        if (_mesh == null) // If Awake hasn't run or mesh wasn't set
        {
            _mesh = new Mesh { name = "PavementInstanceMesh_DirectInit" }; // Give a slightly different name for debugging
            if (_meshFilter != null)
            {
                _meshFilter.mesh = _mesh;
            }
            else
            {
                Debug.LogError("PavementGenerator: MeshFilter is null, cannot assign mesh.", this);
                return;
            }
        }
        else if (_meshFilter.mesh != _mesh)
        {
            _meshFilter.mesh = _mesh; // Ensure the mesh filter is using the correct mesh
        }
        _mesh.Clear(); // Clear any previous data

        if (pavementPlotVertices2D == null || pavementPlotVertices2D.Count < 3)
        {
            // Debug.LogWarning("PavementGenerator: Invalid footprint (null or < 3 vertices). Clearing mesh.", this);
            gameObject.SetActive(false); // Hide if invalid
            return;
        }

        gameObject.SetActive(true); // Ensure it's active if we have valid data

        List<Vector3> vertices3D = pavementPlotVertices2D.Select(v2 => new Vector3(v2.x, 0, v2.y)).ToList();
        List<int> triangles;

        // Ensure vertices are counter-clockwise for correct normal calculation by TriangulatePolygonEarClipping
        // For a flat pavement on Y=0, we want the normal to be (0,1,0).
        // CalculateSignedAreaXZ gives negative for CCW and positive for CW when viewed from +Y.
        // We want CCW.
        float signedArea = BuildingFootprintUtils.CalculateSignedAreaXZ(vertices3D);
        if (signedArea > GeometryConstants.GeometricEpsilon) // If Clockwise (positive area for XZ plane)
        {
            vertices3D.Reverse(); // Make it Counter-Clockwise
        }


        if (GeometryUtils.TriangulatePolygonEarClipping(vertices3D, out triangles))
        {
            _mesh.SetVertices(vertices3D);
            _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateNormals(); // Normals should point up
            _mesh.RecalculateBounds();

            if (materialToApply != null)
            {
                _meshRenderer.sharedMaterial = materialToApply;
            }
            else
            {
                Debug.LogWarning($"PavementGenerator: Material not supplied for {gameObject.name}. Using default.", this);
                // Fallback if no material is assigned
                if (_meshRenderer.sharedMaterial == null) // Only if it doesn't have one already
                {
                    // Ensure you have a fallback material or shader. For HDRP/URP, use appropriate default.
                    var defaultShader = Shader.Find("Standard"); // Or "HDRP/Lit", "URP/Lit"
                    if (defaultShader != null)
                        _meshRenderer.sharedMaterial = new Material(defaultShader);
                    else
                        Debug.LogError("PavementGenerator: Default shader not found for fallback material.");
                }
            }
        }
        else
        {
            Debug.LogError($"PavementGenerator: Failed to triangulate pavement for {gameObject.name}. Clearing mesh.", this);
            _mesh.Clear();
            gameObject.SetActive(false); // Hide if triangulation failed
        }
    }

    public void ClearPavement()
    {
        if (_mesh != null)
        {
            _mesh.Clear();
        }
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (_mesh != null)
        {
            // If this component is destroyed, clean up the mesh it created
            // especially if it was instantiated and not part of a prefab asset.
            if (Application.isEditor && !Application.isPlaying)
                DestroyImmediate(_mesh);
            else
                Destroy(_mesh);
        }
    }
}