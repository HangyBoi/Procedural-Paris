using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using MIConvexHull;

public class CitySectorGenerator : MonoBehaviour
{
    [Header("Prefabs & Styles")]
    public GameObject buildingGeneratorPrefab;
    public BuildingStyleSO defaultBuildingStyle;
    public List<BuildingStyleSO> availableBuildingStyles;

    [Header("Generation Parameters")]
    public int numberOfSeedPoints = 50;
    public Vector2 sectorSize = new Vector2(500f, 500f);
    public float streetWidth = 8f;
    public int minFloors = 2;
    public int maxFloors = 7;
    public float voronoiBoundsPadding = 50f; // Padding around seed points for voronoi generation

    [Header("Plot Validation Parameters")]
    public float minPlotSideLength = 5.0f;
    public float minPlotAngleDegrees = 15.0f;
    public float minPlotArea = 25.0f;

    [Header("Generator Output")]
    [SerializeField]
    private CitySectorData _generatedData = new CitySectorData();
    public CitySectorData GeneratedData => _generatedData;

    private const string GENERATED_SECTOR_ROOT_NAME = "GeneratedSectorContent";

    void Start()
    {
        GenerateFullSector();
    }

    public void GenerateFullSector()
    {
        ClearGeneratedSector();

        if (buildingGeneratorPrefab == null)
        {
            Debug.LogError("Building Generator Prefab is not assigned!", this);
            return;
        }
        if (buildingGeneratorPrefab.GetComponent<PolygonBuildingGenerator>() == null)
        {
            Debug.LogError("Building Generator Prefab must have a PolygonBuildingGenerator component!", this);
            return;
        }

        _generatedData = new CitySectorData();

        _generatedData.SeedPoints = GenerateSeedPoints(numberOfSeedPoints, sectorSize, voronoiBoundsPadding);

        if (_generatedData.SeedPoints.Count < 3) // MIConvexHull Delaunay might need at least 3 for 2D
        {
            Debug.LogWarning("Not enough unique seed points (need at least 3 for Delaunay) to generate a Voronoi diagram.", this);
            return;
        }

        var miconvexInputPoints = _generatedData.SeedPoints
            .Select(p => new DefaultVertex { Position = new double[] { p.x, p.y } })
            .ToList();

        try
        {
            // Use the specific types for clarity and direct access
            _generatedData.DelaunayTriangulation = Triangulation.CreateDelaunay<DefaultVertex, DefaultTriangulationCell<DefaultVertex>>(
                                                    miconvexInputPoints, Constants.DefaultPlaneDistanceTolerance);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error during Delaunay triangulation: {ex.Message}\n{ex.StackTrace}", this);
            return;
        }

        if (_generatedData.DelaunayTriangulation == null || !_generatedData.DelaunayTriangulation.Cells.Any())
        {
            Debug.LogError("Delaunay triangulation failed to produce any cells.", this);
            return;
        }
        Debug.Log($"Delaunay triangulation successful. Number of Delaunay cells (triangles): {_generatedData.DelaunayTriangulation.Cells.Count()}", this);

        for (int i = 0; i < miconvexInputPoints.Count; i++)
        {
            DefaultVertex currentSiteMIVertex = miconvexInputPoints[i]; // The MIConvexHull vertex object
            Vector2 currentSiteVec2 = _generatedData.SeedPoints[i];     // The original Vector2 site
            Debug.Log($"--- Processing Site {i}: {currentSiteVec2} ---");

            // Find all Delaunay triangles (cells) that have the currentSiteMIVertex as one of their vertices.
            // Using ReferenceEquals is more robust than comparing float positions.
            var incidentDelaunayTriangles = _generatedData.DelaunayTriangulation.Cells
                .Where(triangle =>
                {
                    if (triangle.Vertices == null) return false;
                    // Check if any of the triangle's vertices is the *exact same object instance*
                    return triangle.Vertices.Any(tv => System.Object.ReferenceEquals(tv, currentSiteMIVertex));
                })
                .ToList();

            Debug.Log($"Site {i}: Found {incidentDelaunayTriangles.Count} incident Delaunay triangles.");

            if (incidentDelaunayTriangles.Count < 1)
            {
                Debug.LogWarning($"Site {i} ({currentSiteVec2}) has no incident Delaunay triangles. This can happen for sites on the convex hull of all seed points if Voronoi cells extend to infinity. Skipping Voronoi cell generation for this site based purely on its own triangles for now.");
                continue;
            }

            List<Vector2> voronoiCellVertices = new List<Vector2>();
            int triangleCounter = 0;
            foreach (var triangle in incidentDelaunayTriangles) // triangle is DefaultTriangulationCell<DefaultVertex>
            {
                triangleCounter++;
                // THIS IS THE CRITICAL CHANGE: Calculate circumcenter directly from 2D vertices
                if (triangle.Vertices != null && triangle.Vertices.Length == 3)
                {
                    // triangle.Vertices are DefaultVertex[], so triangle.Vertices[0] is a DefaultVertex
                    double[] circumcenterCoords = PolygonUtils.CalculateCircumcenter(
                                                        triangle.Vertices[0],
                                                        triangle.Vertices[1],
                                                        triangle.Vertices[2]);

                    if (circumcenterCoords != null)
                    {
                        Vector2 circumcenter = new Vector2((float)circumcenterCoords[0], (float)circumcenterCoords[1]);
                        voronoiCellVertices.Add(circumcenter);
                        Debug.Log($"Site {i}, Triangle {triangleCounter}: Added circumcenter {circumcenter} calculated from 2D vertices.");
                    }
                    else
                    {
                        Debug.LogWarning($"Site {i}, Triangle {triangleCounter}: CalculateCircumcenter returned null (likely degenerate/collinear triangle). Vertices: " +
                                         $"P0({triangle.Vertices[0].Position[0]:F2},{triangle.Vertices[0].Position[1]:F2}), " +
                                         $"P1({triangle.Vertices[1].Position[0]:F2},{triangle.Vertices[1].Position[1]:F2}), " +
                                         $"P2({triangle.Vertices[2].Position[0]:F2},{triangle.Vertices[2].Position[1]:F2}). Skipping.");
                    }
                }
                else
                {
                    Debug.LogWarning($"Site {i}, Triangle {triangleCounter}: Does not have 3 vertices or Vertices array is null. Actual count: {(triangle.Vertices?.Length ?? -1)}. Skipping circumcenter.");
                }
            }

            Debug.Log($"Site {i}: Collected {voronoiCellVertices.Count} potential Voronoi vertices (circumcenters).");

            if (voronoiCellVertices.Count >= 3)
            {
                Debug.Log($"Site {i}: Ordering {voronoiCellVertices.Count} Voronoi vertices.");
                voronoiCellVertices = PolygonUtils.OrderVerticesOfPolygon(voronoiCellVertices, currentSiteVec2);

                Debug.Log($"Site {i}: Vertices ordered. First ordered vertex: {voronoiCellVertices[0]}");
                _generatedData.RawVoronoiCells.Add(new List<Vector2>(voronoiCellVertices));
                Debug.Log($"Site {i}: Added raw Voronoi cell with {voronoiCellVertices.Count} vertices. Total raw cells: {_generatedData.RawVoronoiCells.Count}");

                Rect sectorBoundsRect = new Rect(-sectorSize.x / 2f, -sectorSize.y / 2f, sectorSize.x, sectorSize.y);
                List<Vector2> clippedCell = ClipPolygonSutherlandHodgman.GetIntersectedPolygon(voronoiCellVertices, sectorBoundsRect);

                if (clippedCell != null && clippedCell.Count >= 3)
                {
                    Debug.Log($"Site {i}: Cell clipped successfully with {clippedCell.Count} vertices.");
                    List<Vector2> processedCellVertices2D = PolygonUtils.ShrinkPolygonBasic(clippedCell, streetWidth / 2.0f);

                    if (processedCellVertices2D != null && processedCellVertices2D.Count >= 3)
                    {
                        if (PolygonUtils.ValidatePlotGeometry(processedCellVertices2D, minPlotSideLength, minPlotAngleDegrees, minPlotArea))
                        {
                            Debug.Log($"Site {i}: Cell shrunk successfully with {processedCellVertices2D.Count} vertices.");
                            _generatedData.ProcessedBuildingPlots.Add(processedCellVertices2D);

                            List<PolygonVertexData> buildingFootprint = processedCellVertices2D
                                   .Select(v2 => new PolygonVertexData { position = new Vector3(v2.x, 0, v2.y), addCornerElement = true })
                                   .ToList();
                            GameObject buildingPlotGO = new GameObject($"BuildingPlot_{_generatedData.ProcessedBuildingPlots.Count - 1}");
                            buildingPlotGO.transform.SetParent(GetGeneratedSectorRoot(), false);
                            CreatePlaceholderMesh(buildingPlotGO, buildingFootprint);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Site {i}: Cell shrinking failed or resulted in < 3 vertices. Shrunk vertices: {(processedCellVertices2D == null ? "NULL" : processedCellVertices2D.Count.ToString())}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Site {i}: Cell clipping failed or resulted in < 3 vertices. Clipped vertices: {(clippedCell == null ? "NULL" : clippedCell.Count.ToString())}");
                }
            }
            else
            {
                Debug.LogWarning($"Site {i}: Not enough valid circumcenters ({voronoiCellVertices.Count}) to form a Voronoi polygon (need >= 3).");
            }
        }
        Debug.Log($"City sector generation complete. Placed {_generatedData.ProcessedBuildingPlots.Count} buildings out of {_generatedData.RawVoronoiCells.Count} raw Voronoi cells generated from {_generatedData.SeedPoints.Count} seeds.", this);
    }

    // Placeholder Mesh for Debugging
    void CreatePlaceholderMesh(GameObject parentGO, List<PolygonVertexData> footprint)
    {
        if (footprint.Count < 3) return;

        MeshFilter mf = parentGO.GetComponent<MeshFilter>();
        if (mf == null) mf = parentGO.AddComponent<MeshFilter>();
        MeshRenderer mr = parentGO.GetComponent<MeshRenderer>();
        if (mr == null) mr = parentGO.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh();
        mesh.name = "PlotPlaceholder";

        Vector3[] vertices = footprint.Select(vd => vd.position).ToArray();

        // Triangulate the polygon (simple ear clipping for convex/simple polygons)
        // For robust triangulation of complex polygons, a more advanced algorithm is needed.
        // This simple triangulator assumes convex or at least non-self-intersecting.
        List<int> triangles = new List<int>();
        if (vertices.Length >= 3)
        {
            for (int i = 1; i < vertices.Length - 1; i++)
            {
                triangles.Add(i + 1);
                triangles.Add(i);
                triangles.Add(0);
            }
        }


        mesh.vertices = vertices;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.mesh = mesh;
        // Assign a default material if you have one, or a simple colored one
        if (mr.sharedMaterial == null)
            mr.sharedMaterial = new Material(Shader.Find("Standard")); // Basic material
    }

    public void ClearGeneratedSector()
    {
        Transform root = transform.Find(GENERATED_SECTOR_ROOT_NAME);
        if (root != null)
        {
            if (Application.isEditor && !Application.isPlaying)
                DestroyImmediate(root.gameObject);
            else
                Destroy(root.gameObject);
        }
        _generatedData = new CitySectorData(); // Clear data
    }

    Transform GetGeneratedSectorRoot()
    {
        Transform root = transform.Find(GENERATED_SECTOR_ROOT_NAME);
        if (root == null)
        {
            GameObject rootGO = new GameObject(GENERATED_SECTOR_ROOT_NAME);
            rootGO.transform.SetParent(this.transform, false);
            root = rootGO.transform;
        }
        return root;
    }

    List<Vector2> GenerateSeedPoints(int count, Vector2 dimensions, float padding)
    {
        List<Vector2> points = new List<Vector2>();
        Random.InitState((int)System.DateTime.Now.Ticks + GetInstanceID());

        float minX = -dimensions.x / 2f + padding;
        float maxX = dimensions.x / 2f - padding;
        float minY = -dimensions.y / 2f + padding;
        float maxY = dimensions.y / 2f - padding;

        if (minX >= maxX || minY >= maxY)
        {
            Debug.LogWarning("Voronoi bounds padding is too large for the sector size. Using full sector for seed points.", this);
            minX = -dimensions.x / 2f; maxX = dimensions.x / 2f;
            minY = -dimensions.y / 2f; maxY = dimensions.y / 2f;
        }

        for (int i = 0; i < count; i++)
        {
            points.Add(new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY)));
        }
        return points.Distinct().ToList(); // Ensure unique points
    }

    BuildingStyleSO GetRandomStyle()
    {
        if (availableBuildingStyles != null && availableBuildingStyles.Count > 0)
        {
            return availableBuildingStyles[Random.Range(0, availableBuildingStyles.Count)];
        }
        return defaultBuildingStyle;
    }

    void OnValidate()
    {
        numberOfSeedPoints = Mathf.Max(4, numberOfSeedPoints);
        streetWidth = Mathf.Max(0.1f, streetWidth);
        minFloors = Mathf.Max(1, minFloors);
        maxFloors = Mathf.Max(minFloors, maxFloors);
        voronoiBoundsPadding = Mathf.Max(0, voronoiBoundsPadding);

        minPlotSideLength = Mathf.Max(0.1f, minPlotSideLength);
        minPlotAngleDegrees = Mathf.Clamp(minPlotAngleDegrees, 1f, 359f);
        minPlotArea = Mathf.Max(0.1f, minPlotArea);
    }
}