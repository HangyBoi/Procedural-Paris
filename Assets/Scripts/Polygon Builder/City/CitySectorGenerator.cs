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
    public int minFloors = 2; // Total floors for building
    public int maxFloors = 7; // Total floors for building
    public float voronoiBoundsPadding = 50f; // Padding around seed points for voronoi generation

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
                    double[] circumcenterCoords = CalculateCircumcenter(
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
                voronoiCellVertices = OrderVerticesOfPolygon(voronoiCellVertices, currentSiteVec2);

                Debug.Log($"Site {i}: Vertices ordered. First ordered vertex: {voronoiCellVertices[0]}");
                _generatedData.RawVoronoiCells.Add(new List<Vector2>(voronoiCellVertices));
                Debug.Log($"Site {i}: Added raw Voronoi cell with {voronoiCellVertices.Count} vertices. Total raw cells: {_generatedData.RawVoronoiCells.Count}");

                Rect sectorBoundsRect = new Rect(-sectorSize.x / 2f, -sectorSize.y / 2f, sectorSize.x, sectorSize.y);
                List<Vector2> clippedCell = ClipPolygonSutherlandHodgman.GetIntersectedPolygon(voronoiCellVertices, sectorBoundsRect);

                if (clippedCell != null && clippedCell.Count >= 3)
                {
                    Debug.Log($"Site {i}: Cell clipped successfully with {clippedCell.Count} vertices.");
                    List<Vector2> processedCellVertices2D = ShrinkPolygonBasic(clippedCell, streetWidth / 2.0f);
                    if (processedCellVertices2D != null && processedCellVertices2D.Count >= 3)
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


    public static double[] CalculateCircumcenter(IVertex p1_IVertex, IVertex p2_IVertex, IVertex p3_IVertex)
    {
        // Assumes p1, p2, p3 are IVertex and their Position arrays have at least 2 elements for x and y
        double ax = p1_IVertex.Position[0];
        double ay = p1_IVertex.Position[1];
        double bx = p2_IVertex.Position[0];
        double by = p2_IVertex.Position[1];
        double cx = p3_IVertex.Position[0];
        double cy = p3_IVertex.Position[1];

        double aSq = ax * ax + ay * ay;
        double bSq = bx * bx + by * by;
        double cSq = cx * cx + cy * cy;

        double D = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));

        if (System.Math.Abs(D) < 1e-9) // Points are collinear or very close
        {
            return null; // Cannot form a unique circumcircle
        }

        double ux = (aSq * (by - cy) + bSq * (cy - ay) + cSq * (ay - by)) / D;
        double uy = (aSq * (cx - bx) + bSq * (ax - cx) + cSq * (bx - ax)) / D;

        return new double[] { ux, uy };
    }

    // Add this helper method too for ordering (if not already present)
    public static List<Vector2> OrderVerticesOfPolygon(List<Vector2> vertices, Vector2 centerPoint) // Changed centerPoint to Vector2
    {
        if (vertices == null || vertices.Count < 3)
        {
            return vertices;
        }

        // Use the site's original Vector2 position as the reference for angle calculation
        float refX = centerPoint.x; // Now float
        float refY = centerPoint.y; // Now float

        // Sort vertices by angle around the reference point
        vertices.Sort((v1, v2) => // v1 and v2 are Vector2, so .x and .y are float
        {
            // Arguments to Mathf.Atan2 are now float
            double angle1 = Mathf.Atan2(v1.y - refY, v1.x - refX);
            double angle2 = Mathf.Atan2(v2.y - refY, v2.x - refX);
            return angle1.CompareTo(angle2);
        });

        return vertices;
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
                    triangles.Add(0);
                    triangles.Add(i);
                    triangles.Add(i + 1);
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

    List<Vector2> ShrinkPolygonBasic(List<Vector2> polygon, float distance)
    {
        if (polygon == null || polygon.Count < 3 || distance <= Mathf.Epsilon) return polygon;

        Vector2 centroid = Vector2.zero;
        foreach (var v in polygon) centroid += v;
        centroid /= polygon.Count;

        List<Vector2> shrunkPolygon = new List<Vector2>();
        for (int i = 0; i < polygon.Count; ++i)
        {
            Vector2 dirToCentroid = (centroid - polygon[i]);
            if (dirToCentroid.sqrMagnitude < Mathf.Epsilon * Mathf.Epsilon) return null; // Centroid is on vertex, likely degenerate

            float distToCentroid = dirToCentroid.magnitude;
            if (distToCentroid < distance + 0.01f) return null; // Not enough space to shrink

            shrunkPolygon.Add(polygon[i] + dirToCentroid.normalized * distance);
        }
        // Check if shrunk polygon is still valid (e.g. not self-intersecting, still has area)
        // For basic shrink, just check vertex count. A robust solution needs more.
        if (shrunkPolygon.Count < 3) return null;
        return shrunkPolygon;
    }

    // Placeholder for a proper polygon clipping algorithm.
    // You should replace this with a real implementation.
    public static class ClipPolygonSutherlandHodgman
    {
        public static List<Vector2> GetIntersectedPolygon(List<Vector2> subjectPolygon, Rect clipRect)
        {
            if (subjectPolygon == null || subjectPolygon.Count < 3)
                return null;

            List<Vector2> clippedPolygon = new List<Vector2>(subjectPolygon);

            // Clip against left edge
            clippedPolygon = ClipAgainstEdge(clippedPolygon, new Vector2(clipRect.xMin, clipRect.yMin), new Vector2(clipRect.xMin, clipRect.yMax));
            if (clippedPolygon.Count < 3) return null;
            // Clip against top edge
            clippedPolygon = ClipAgainstEdge(clippedPolygon, new Vector2(clipRect.xMin, clipRect.yMax), new Vector2(clipRect.xMax, clipRect.yMax));
            if (clippedPolygon.Count < 3) return null;
            // Clip against right edge
            clippedPolygon = ClipAgainstEdge(clippedPolygon, new Vector2(clipRect.xMax, clipRect.yMax), new Vector2(clipRect.xMax, clipRect.yMin));
            if (clippedPolygon.Count < 3) return null;
            // Clip against bottom edge
            clippedPolygon = ClipAgainstEdge(clippedPolygon, new Vector2(clipRect.xMax, clipRect.yMin), new Vector2(clipRect.xMin, clipRect.yMin));

            return clippedPolygon.Count >= 3 ? clippedPolygon : null;
        }

        private static List<Vector2> ClipAgainstEdge(List<Vector2> subjectPolygon, Vector2 edgeP1, Vector2 edgeP2)
        {
            List<Vector2> outputList = new List<Vector2>();
            if (subjectPolygon.Count == 0) return outputList;

            Vector2 s = subjectPolygon[subjectPolygon.Count - 1]; // Start with the last vertex

            for (int i = 0; i < subjectPolygon.Count; i++)
            {
                Vector2 e = subjectPolygon[i]; // Current end vertex of subject polygon edge

                // Check if s is inside (to the left of or on the directed edge p1->p2)
                bool s_inside = IsLeftOf(edgeP1, edgeP2, s);
                // Check if e is inside
                bool e_inside = IsLeftOf(edgeP1, edgeP2, e);

                if (s_inside && e_inside) // Case 1: Both points are inside
                {
                    outputList.Add(e);
                }
                else if (s_inside && !e_inside) // Case 2: s is inside, e is outside (edge crosses out)
                {
                    outputList.Add(IntersectionPoint(edgeP1, edgeP2, s, e));
                }
                else if (!s_inside && e_inside) // Case 3: s is outside, e is inside (edge crosses in)
                {
                    outputList.Add(IntersectionPoint(edgeP1, edgeP2, s, e));
                    outputList.Add(e);
                }
                // Case 4: Both points are outside (do nothing)

                s = e; // Move to the next edge
            }
            return outputList;
        }

        // Checks if point p is to the left of, or on, the directed line segment from l1 to l2.
        // Uses cross product: (l2-l1) x (p-l1). For 2D, (x1*y2 - y1*x2).
        // (l2.x-l1.x)*(p.y-l1.y) - (l2.y-l1.y)*(p.x-l1.x)
        // > 0 for p left of l1-l2
        // = 0 for p on l1-l2
        // < 0 for p right of l1-l2
        private static bool IsLeftOf(Vector2 l1, Vector2 l2, Vector2 p)
        {
            return ((l2.x - l1.x) * (p.y - l1.y) - (l2.y - l1.y) * (p.x - l1.x)) >= -Mathf.Epsilon; // Include points on the line with a small epsilon
        }

        // Calculates the intersection point of two line segments: (p1-p2) and (p3-p4)
        private static Vector2 IntersectionPoint(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            // Line p1-p2 represented as p1 + ua (p2 - p1)
            // Line p3-p4 represented as p3 + ub (p4 - p3)
            float den = (p4.y - p3.y) * (p2.x - p1.x) - (p4.x - p3.x) * (p2.y - p1.y);
            if (Mathf.Abs(den) < Mathf.Epsilon) // Lines are parallel or collinear
            {
                return p3; // Should not happen if one point is inside and one is outside clip edge
            }

            float ua_num = (p4.x - p3.x) * (p1.y - p3.y) - (p4.y - p3.y) * (p1.x - p3.x);
            float ua = ua_num / den;

            return new Vector2(p1.x + ua * (p2.x - p1.x), p1.y + ua * (p2.y - p1.y));
        }
    }

    void OnValidate()
    {
        numberOfSeedPoints = Mathf.Max(4, numberOfSeedPoints);
        streetWidth = Mathf.Max(0.1f, streetWidth);
        minFloors = Mathf.Max(1, minFloors);
        maxFloors = Mathf.Max(minFloors, maxFloors);
        voronoiBoundsPadding = Mathf.Max(0, voronoiBoundsPadding);
    }
}