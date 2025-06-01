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
    public Vector2 sectorSize = new(500f, 500f);
    public float streetWidth = 8f;
    public float voronoiBoundsPadding = 50f;
    public float plotVertexSnapSize = 0.0f;

    [Header("Plot Validation Parameters")]
    public float minPlotSideLength = 5.0f;
    public float minPlotAngleDegrees = 15.0f;
    public float minPlotArea = 25.0f;

    [Header("Plot Styling Parameters")]
    public Material pavementMaterial;
    public float buildingInsetFromPavementEdge = 0.5f;

    [Header("Building Style Variation")]
    [Range(0f, 1f)]
    public float chanceForVariedSides = 0.1f;
    public List<SideStyleSO> availableSideStylesForVariation;

    [Header("Building Randomization")]
    public int minFloors = 2;
    public int maxFloors = 7;

    [Header("Generator Output")]
    [SerializeField]
    private CitySectorData _generatedData = new CitySectorData();
    public CitySectorData GeneratedData => _generatedData;

    private const string GENERATED_SECTOR_ROOT_NAME = "GeneratedSectorContent";

    void Start()
    {
        // GenerateFullSector();
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

        if (defaultBuildingStyle == null && (availableBuildingStyles == null || availableBuildingStyles.Count == 0 || availableBuildingStyles.All(s => s == null)))
        {
            Debug.LogWarning("DefaultBuildingStyle is not assigned, and no valid AvailableBuildingStyles are provided. Buildings might not generate correctly if their style is not set.", this);
        }

        _generatedData = new CitySectorData();

        Transform sectorRoot = GetGeneratedSectorRoot();

        _generatedData.SeedPoints = GenerateSeedPoints(numberOfSeedPoints, sectorSize, voronoiBoundsPadding);

        if (_generatedData.SeedPoints.Count < 3)
        {
            Debug.LogWarning("Not enough unique seed points (need at least 3 for Delaunay) to generate a Voronoi diagram.", this);
            return;
        }

        var miconvexInputPoints = _generatedData.SeedPoints
            .Select(p => new DefaultVertex { Position = new double[] { p.x, p.y } })
            .ToList();

        try
        {
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
        // Debug.Log($"Delaunay triangulation successful. Number of Delaunay cells (triangles): {_generatedData.DelaunayTriangulation.Cells.Count()}", this);

        int buildingsPlacedCount = 0;
        for (int i = 0; i < miconvexInputPoints.Count; i++)
        {
            DefaultVertex currentSiteMIVertex = miconvexInputPoints[i];
            Vector2 currentSiteVec2 = _generatedData.SeedPoints[i];

            // Find all Delaunay triangles (cells) that have the currentSiteMIVertex as one of their vertices.
            // Using ReferenceEquals is more robust than comparing float positions.
            var incidentDelaunayTriangles = _generatedData.DelaunayTriangulation.Cells
                .Where(triangle => triangle.Vertices != null && triangle.Vertices.Any(tv => System.Object.ReferenceEquals(tv, currentSiteMIVertex)))
                .ToList();

            if (incidentDelaunayTriangles.Count < 1)
            {
                // Debug.LogWarning($"Site {i} ({currentSiteVec2}) has no incident Delaunay triangles. This can happen for sites on the convex hull of all seed points if Voronoi cells extend to infinity. Skipping Voronoi cell generation for this site based purely on its own triangles for now.");
                continue;
            }

            List<Vector2> voronoiCellVertices = new List<Vector2>();
            // int triangleCounter = 0;
            foreach (var triangle in incidentDelaunayTriangles) // triangle is DefaultTriangulationCell<DefaultVertex>
            {
                // triangleCounter++;
                if (triangle.Vertices != null && triangle.Vertices.Length == 3)
                {
                    // triangle.Vertices are DefaultVertex[], so triangle.Vertices[0] is a DefaultVertex
                    double[] circumcenterCoords = PolygonUtils.CalculateCircumcenter(
                                                        triangle.Vertices[0],
                                                        triangle.Vertices[1],
                                                        triangle.Vertices[2]);

                    if (circumcenterCoords != null)
                    {
                        voronoiCellVertices.Add(new Vector2((float)circumcenterCoords[0], (float)circumcenterCoords[1]));
                        // Debug.Log($"Site {i}, Triangle {triangleCounter}: Added circumcenter {circumcenter} calculated from 2D vertices.");
                    }

                }
            }

            if (voronoiCellVertices.Count >= 3)
            {
                voronoiCellVertices = PolygonUtils.OrderVerticesOfPolygon(voronoiCellVertices, currentSiteVec2);
                _generatedData.RawVoronoiCells.Add(new List<Vector2>(voronoiCellVertices));

                Rect sectorBoundsRect = new Rect(-sectorSize.x / 2f, -sectorSize.y / 2f, sectorSize.x, sectorSize.y);
                List<Vector2> clippedCell = ClipPolygonSutherlandHodgman.GetIntersectedPolygon(voronoiCellVertices, sectorBoundsRect);

                if (clippedCell != null && clippedCell.Count >= 3)
                {
                    // ---- SNAP CLIPPED CELL VERTICES ----
                    List<Vector2> snappedClippedCell = clippedCell; // Start with the original clipped cell
                    if (plotVertexSnapSize > GeometryConstants.GeometricEpsilon)
                    {
                        snappedClippedCell = PolygonUtils.SnapPolygonVertices2D(clippedCell, plotVertexSnapSize);
                        if (snappedClippedCell == null || snappedClippedCell.Count < 3)
                        {
                            // Debug.LogWarning($"Site {i}: Snapping of clipped cell resulted in a degenerate polygon. Skipping plot.");
                            continue; // Skip this plot if snapping made it invalid
                        }
                    }
                    // ---- END SNAPPING ----

                    List<Vector2> pavementPlotVertices = PolygonUtils.OffsetPolygonBasic(snappedClippedCell, streetWidth / 2.0f);

                    if (pavementPlotVertices != null && pavementPlotVertices.Count >= 3)
                    {
                        if (PolygonUtils.ValidatePlotGeometry(pavementPlotVertices, minPlotSideLength, minPlotAngleDegrees, minPlotArea))
                        {
                            // Now, shrink the pavement plot to get the building footprint
                            List<Vector2> buildingFootprintVertices = null;
                            if (buildingInsetFromPavementEdge > GeometryConstants.GeometricEpsilon)
                            {
                                buildingFootprintVertices = PolygonUtils.OffsetPolygonBasic(pavementPlotVertices, buildingInsetFromPavementEdge);
                            }
                            else
                            {
                                buildingFootprintVertices = new List<Vector2>(pavementPlotVertices); // No further inset
                            }

                            if (buildingFootprintVertices != null && buildingFootprintVertices.Count >= 3)
                            {
                                if (PolygonUtils.ValidatePlotGeometry(buildingFootprintVertices,
                                                                     minPlotSideLength, // Slightly smaller side allowed for building
                                                                     minPlotAngleDegrees,      // Same angle
                                                                     minPlotArea))     // Smaller area allowed for building
                                {


                                    _generatedData.ProcessedBuildingPlots.Add(new List<Vector2>(pavementPlotVertices));

                                    // Try to instantiate and generate the building
                                    bool buildingSuccess = InstantiateAndGenerateBuildingOnPlot(
                                        buildingFootprintVertices,      // INSET footprint for the building structure
                                        pavementPlotVertices,           // ORIGINAL (non-inset) plot for the pavement
                                        sectorRoot,
                                        buildingsPlacedCount
                                    );

                                    if (buildingSuccess)
                                    {
                                        buildingsPlacedCount++;
                                    }
                                    else
                                    {
                                        if (_generatedData.ProcessedBuildingPlots.Count > 0 &&
                                            _generatedData.ProcessedBuildingPlots.Last() == pavementPlotVertices)
                                        {
                                            _generatedData.ProcessedBuildingPlots.RemoveAt(_generatedData.ProcessedBuildingPlots.Count - 1);
                                        }
                                        Debug.LogWarning($"Plot for site {i} (potential building {buildingsPlacedCount}) failed to generate a building. Pavement and building attempt removed.");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        Debug.Log($"City sector generation complete. Placed {buildingsPlacedCount} buildings out of {_generatedData.RawVoronoiCells.Count} raw Voronoi cells (from {_generatedData.SeedPoints.Count} seeds).", this);
    }

    private bool InstantiateAndGenerateBuildingOnPlot(
        List<Vector2> buildingFootprintVertices2D,    // INSET footprint for the building structure
        List<Vector2> originalPavementPlotVertices2D, // NON-INSET plot for the pavement
        Transform sectorRoot,
        int plotIndex)
    {
        if (buildingFootprintVertices2D == null || buildingFootprintVertices2D.Count < 3)
        {
            Debug.LogWarning($"Plot {plotIndex}: Cannot init building, building footprint has < 3 vertices.");
            return false;
        }

        // --- INSTANTIATE THE POLYGONBUILDINGGENERATOR PREFAB ---
        // This GameObject will be the root for this "Building Complex" (structure + pavement)
        GameObject buildingComplexGO = Instantiate(buildingGeneratorPrefab);
        buildingComplexGO.name = $"BuildingComplex_{plotIndex}";
        buildingComplexGO.transform.SetParent(sectorRoot, false);

        if (!buildingComplexGO.TryGetComponent<PolygonBuildingGenerator>(out var buildingGenerator))
        {
            Debug.LogError($"Plot {plotIndex}: Instantiated building prefab '{buildingGeneratorPrefab.name}' must have a PolygonBuildingGenerator. Destroying instance.", buildingComplexGO);
            DestroySafely(buildingComplexGO);
            return false;
        }

        // --- CONFIGURE THE POLYGONBUILDINGGENERATOR ---
        // 1. Set Building Footprint (vertexData)
        List<PolygonVertexData> footprintForBuildingStructure = buildingFootprintVertices2D
            .Select(v2 => new PolygonVertexData { position = new Vector3(v2.x, 0, v2.y), addCornerElement = true })
            .ToList();

        float signedArea = BuildingFootprintUtils.CalculateSignedAreaXZ(footprintForBuildingStructure);
        if (signedArea > GeometryConstants.GeometricEpsilon) // If Clockwise (negative area)
        {
            footprintForBuildingStructure.Reverse();
        }

        buildingGenerator.vertexData = footprintForBuildingStructure;

        // 2. Set Pavement Data
        if (originalPavementPlotVertices2D != null && originalPavementPlotVertices2D.Count >= 3)
        {
            buildingGenerator.originalPavementPlotVertices2D = new List<Vector2>(originalPavementPlotVertices2D);
        }
        else
        {
            buildingGenerator.originalPavementPlotVertices2D = null;
        }
        buildingGenerator.pavementMaterial = this.pavementMaterial;
        buildingGenerator.pavementOutset = this.buildingInsetFromPavementEdge;

        // 3. Synchronize side data (for facades, etc.)
        buildingGenerator.SynchronizeSideData();

        // 4. Assign Building Style
        buildingGenerator.buildingStyle = GetRandomStyle();
        if (buildingGenerator.buildingStyle == null)
        {
            Debug.LogError($"Plot {plotIndex}: Critical - No BuildingStyleSO assigned for {buildingComplexGO.name}. Destroying instance.");
            DestroySafely(buildingComplexGO);
            return false;
        }

        // 5. Randomize Floors
        buildingGenerator.middleFloors = Random.Range(minFloors, maxFloors + 1);

        // 6. Apply Style Variation Logic
        if (Random.value < chanceForVariedSides && availableSideStylesForVariation != null && availableSideStylesForVariation.Count > 0)
        {
            buildingGenerator.useConsistentStyleForAllSides = false;
            for (int i = 0; i < buildingGenerator.sideData.Count; i++) // sideData was synced above
            {
                if (Random.value < 0.5f)
                {
                    buildingGenerator.sideData[i].useCustomStyle = true;
                    buildingGenerator.sideData[i].sideStylePreset = availableSideStylesForVariation[Random.Range(0, availableSideStylesForVariation.Count)];
                }
                else
                {
                    buildingGenerator.sideData[i].useCustomStyle = false;
                }
            }
        }
        else
        {
            buildingGenerator.useConsistentStyleForAllSides = true;
        }

        // You can set other PolygonBuildingGenerator parameters here if desired:
        // buildingGenerator.useMansardFloor = Random.value > 0.3f; 
        // etc. Otherwise, they will use the values from the prefab.

        bool success = buildingGenerator.GenerateBuilding(); // Generates building parts AND pavement

        if (!success)
        {
            Debug.LogWarning($"Plot {plotIndex}: Full building complex generation failed for '{buildingComplexGO.name}'. Destroying the main complex GO.");
            DestroySafely(buildingComplexGO); // Destroy the PolygonBuildingGenerator's GameObject
            return false;
        }

        return true;
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
        mesh.name = "PlotPavementMesh";

        Vector3[] vertices = footprint.Select(vd => vd.position).ToArray();

        List<int> triangles;
        if (GeometryUtils.TriangulatePolygonEarClipping(vertices.ToList(), out triangles)) // Convert to List for Triangulate
        {
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals(); // Normals should point up
            mesh.RecalculateBounds();

            mf.mesh = mesh;

            if (pavementMaterial != null)
            {
                mr.sharedMaterial = pavementMaterial;
            }
            else
            {
                // Fallback if no material is assigned in inspector
                Debug.LogWarning($"Pavement material not set on CitySectorGenerator. Using default pink for {parentGO.name}.");
                if (mr.sharedMaterial == null)
                    mr.sharedMaterial = new Material(Shader.Find("HDRP/Lit"));
            }
        }
        else
        {
            Debug.LogError($"Failed to triangulate pavement for {parentGO.name}");
        }
    }

    private void DestroySafely(GameObject obj)
    {
        if (obj == null) return;
        if (Application.isEditor && !Application.isPlaying)
            DestroyImmediate(obj);
        else
            Destroy(obj);
    }

    public void ClearGeneratedSector()
    {
        Transform root = transform.Find(GENERATED_SECTOR_ROOT_NAME);
        if (root != null)
        {
            // DestroyImmediate is for editor context. If running builds, use Destroy.
            if (Application.isEditor && !Application.isPlaying)
                DestroyImmediate(root.gameObject);
            else
                Destroy(root.gameObject);
        }
        _generatedData = new CitySectorData();
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
        List<BuildingStyleSO> validStyles = null;
        if (availableBuildingStyles != null && availableBuildingStyles.Count > 0)
        {
            validStyles = availableBuildingStyles.Where(s => s != null).ToList();
        }

        if (validStyles != null && validStyles.Count > 0)
        {
            return validStyles[Random.Range(0, validStyles.Count)];
        }
        return defaultBuildingStyle; // This can also be null if not set
    }

    void OnValidate()
    {
        numberOfSeedPoints = Mathf.Max(4, numberOfSeedPoints);
        streetWidth = Mathf.Max(0.1f, streetWidth);
        minFloors = Mathf.Max(1, minFloors);
        maxFloors = Mathf.Max(minFloors, maxFloors);
        voronoiBoundsPadding = Mathf.Max(0, voronoiBoundsPadding);

        minPlotSideLength = Mathf.Max(0.1f, minPlotSideLength);
        minPlotAngleDegrees = Mathf.Clamp(minPlotAngleDegrees, 1f, 179f);
        minPlotArea = Mathf.Max(0.1f, minPlotArea);
    }
}