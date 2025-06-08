// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
//  This script generates a city sector by creating Voronoi diagrams to define building plots,
//  then populates those plots with procedurally generated buildings.
//

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using MIConvexHull;

/// <summary>
/// Generates a city sector by creating Voronoi diagrams to define building plots,
/// then populates those plots with procedurally generated buildings.
/// </summary>
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

    [Header("Plot Validation")]
    public float minPlotSideLength = 5.0f;
    public float minPlotAngleDegrees = 15.0f;
    public float minPlotArea = 25.0f;

    [Header("Plot Styling")]
    public Material pavementMaterial;
    public float buildingInsetFromPavementEdge = 0.5f;

    [Header("Building Style Variation")]
    [Range(0f, 1f)] public float chanceForVariedSides = 0.1f;
    public List<SideStyleSO> availableSideStylesForVariation;
    [Range(0f, 1f)] public float chancePerVertexForCorner = 0.7f;

    [Header("Building Structure Randomization")]
    public int minFloors = 2;
    public int maxFloors = 7;
    [Range(0f, 1f)] public float chanceForAtticFloor = 0.5f;

    [Header("Building Roof Randomization")]
    public Vector2 randomMansardHDistRange = new(1.0f, 2.5f);
    public Vector2 randomMansardRiseRange = new(1.5f, 3.0f);
    public Vector2 randomAtticHDistRange = new(1.0f, 2.0f);
    public Vector2 randomAtticRiseRange = new(1.0f, 2.5f);

    [Header("Debug")]
    [Tooltip("Show gizmos in the Scene view for visualizing the generation process.")]
    public bool showDebugGizmos = true;

    [Header("Generator Output")]
    [SerializeField] private CitySectorData _generatedData = new CitySectorData();
    public CitySectorData GeneratedData => _generatedData;

    private const string GENERATED_SECTOR_ROOT_NAME = "GeneratedSectorContent";

    /// <summary>
    /// Clears any existing sector content and generates a new city sector based on current parameters.
    /// </summary>
    public void GenerateFullSector()
    {
        ClearGeneratedSector();
        if (!ValidatePrerequisites()) return;

        _generatedData = new CitySectorData();
        Transform sectorRoot = GetGeneratedSectorRoot();

        // Generate initial data for Voronoi diagram
        _generatedData.SeedPoints = GenerateSeedPoints(numberOfSeedPoints, sectorSize, voronoiBoundsPadding);
        var miconvexInputPoints = _generatedData.SeedPoints
            .Select(p => new DefaultVertex { Position = new double[] { p.x, p.y } })
            .ToList();

        // Create the Delaunay triangulation, which is the dual of the Voronoi diagram
        try
        {
            _generatedData.DelaunayTriangulation = Triangulation.CreateDelaunay(miconvexInputPoints);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Delaunay triangulation failed: {ex.Message}", this);
            return;
        }

        if (_generatedData.DelaunayTriangulation == null || !_generatedData.DelaunayTriangulation.Cells.Any())
        {
            Debug.LogError("Delaunay triangulation produced no cells.", this);
            return;
        }

        int buildingsPlacedCount = 0;
        // Process each Voronoi site to create a building plot
        for (int i = 0; i < miconvexInputPoints.Count; i++)
        {
            if (TryProcessSite(miconvexInputPoints[i], out var buildingFootprint, out var pavementPlot))
            {
                _generatedData.ProcessedBuildingPlots.Add(new List<Vector2>(pavementPlot));
                bool success = InstantiateAndGenerateBuildingOnPlot(buildingFootprint, pavementPlot, sectorRoot, buildingsPlacedCount);

                if (success)
                {
                    buildingsPlacedCount++;
                }
                else
                {
                    // If building generation failed, remove its plot from the data
                    _generatedData.ProcessedBuildingPlots.RemoveAt(_generatedData.ProcessedBuildingPlots.Count - 1);
                }
            }
        }
        Debug.Log($"City sector generation complete. Placed {buildingsPlacedCount} buildings.", this);
    }

    /// <summary>
    /// Processes a single Voronoi site to generate valid building and pavement plots.
    /// </summary>
    /// <returns>True if a valid plot was created, false otherwise.</returns>
    private bool TryProcessSite(DefaultVertex site, out List<Vector2> buildingFootprint, out List<Vector2> pavementPlot)
    {
        buildingFootprint = null;
        pavementPlot = null;

        // 1. Create the raw Voronoi cell from the Delaunay triangulation
        List<Vector2> rawCell = CreateVoronoiCell(site, _generatedData.DelaunayTriangulation);
        if (rawCell == null) return false;
        _generatedData.RawVoronoiCells.Add(new List<Vector2>(rawCell));

        // 2. Clip the cell to the sector's boundaries
        Rect sectorBoundsRect = new Rect(-sectorSize.x / 2f, -sectorSize.y / 2f, sectorSize.x, sectorSize.y);
        List<Vector2> clippedCell = ClipPolygonSutherlandHodgman.GetIntersectedPolygon(rawCell, sectorBoundsRect);
        if (clippedCell == null || clippedCell.Count < 3) return false;

        // 3. (Optional) Snap vertices to a grid
        List<Vector2> snappedCell = (plotVertexSnapSize > 1e-6f) ? PolygonUtils.SnapPolygonVertices2D(clippedCell, plotVertexSnapSize) : clippedCell;
        if (snappedCell == null || snappedCell.Count < 3) return false;

        // 4. Shrink the cell to create the pavement plot (leaving space for streets)
        pavementPlot = PolygonUtils.OffsetPolygonBasic(snappedCell, streetWidth / 2.0f);
        if (pavementPlot == null || !PolygonUtils.ValidatePlotGeometry(pavementPlot, minPlotSideLength, minPlotAngleDegrees, minPlotArea)) return false;

        // 5. Shrink the pavement plot to create the final building footprint
        buildingFootprint = (buildingInsetFromPavementEdge > 1e-6f)
            ? PolygonUtils.OffsetPolygonBasic(pavementPlot, buildingInsetFromPavementEdge)
            : new List<Vector2>(pavementPlot);

        // 6. Final validation of the building footprint
        return buildingFootprint != null && PolygonUtils.ValidatePlotGeometry(buildingFootprint, minPlotSideLength, minPlotAngleDegrees, minPlotArea);
    }

    /// <summary>
    /// Creates a Voronoi cell polygon for a given site using its incident triangles from the Delaunay triangulation.
    /// </summary>
    private List<Vector2> CreateVoronoiCell(DefaultVertex site, ITriangulation<DefaultVertex, DefaultTriangulationCell<DefaultVertex>> delaunay)
    {
        // The vertices of a Voronoi cell are the circumcenters of the Delaunay triangles connected to the site
        var incidentTriangles = delaunay.Cells.Where(c => c.Vertices.Contains(site)).ToList();
        if (incidentTriangles.Count < 1) return null;

        var circumcenters = new List<Vector2>();
        foreach (var triangle in incidentTriangles)
        {
            double[] center = PolygonUtils.CalculateCircumcenter(triangle.Vertices[0], triangle.Vertices[1], triangle.Vertices[2]);
            if (center != null) circumcenters.Add(new Vector2((float)center[0], (float)center[1]));
        }

        if (circumcenters.Count < 3) return null;

        var siteCenter = new Vector2((float)site.Position[0], (float)site.Position[1]);
        return PolygonUtils.OrderVerticesOfPolygon(circumcenters, siteCenter);
    }

    /// <summary>
    /// Instantiates and configures a building generator for a given plot, then triggers generation.
    /// </summary>
    /// <returns>True if the building was generated successfully.</returns>
    private bool InstantiateAndGenerateBuildingOnPlot(List<Vector2> buildingFootprint2D, List<Vector2> pavementPlot2D, Transform sectorRoot, int plotIndex)
    {
        GameObject buildingComplexGO = Instantiate(buildingGeneratorPrefab, sectorRoot);
        buildingComplexGO.name = $"BuildingComplex_{plotIndex}";

        if (!buildingComplexGO.TryGetComponent<PolygonBuildingGenerator>(out var generator))
        {
            Debug.LogError($"Instantiated prefab for plot {plotIndex} is missing a PolygonBuildingGenerator component.", buildingComplexGO);
            DestroySafely(buildingComplexGO);
            return false;
        }

        // Configure the generator with plot data and randomized parameters
        ConfigureBuildingGenerator(generator, buildingFootprint2D, pavementPlot2D);

        // Trigger the final building and pavement generation
        bool success = generator.GenerateBuilding();
        if (!success)
        {
            Debug.LogWarning($"Generation failed for building {plotIndex}. Destroying instance.", buildingComplexGO);
            DestroySafely(buildingComplexGO);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Sets all randomized parameters on a new building generator instance.
    /// </summary>
    private void ConfigureBuildingGenerator(PolygonBuildingGenerator generator, List<Vector2> buildingFootprint2D, List<Vector2> pavementPlot2D)
    {
        // 1. Footprint & Pavement
        List<PolygonVertexData> footprint3D = buildingFootprint2D
            .Select(v => new PolygonVertexData { position = new Vector3(v.x, 0, v.y), addCornerElement = Random.value < chancePerVertexForCorner })
            .ToList();
        // Ensure counter-clockwise winding order for correct normal calculations
        if (BuildingFootprintUtils.CalculateSignedAreaXZ(footprint3D) > 0) footprint3D.Reverse();

        generator.vertexData = footprint3D;
        generator.originalPavementPlotVertices2D = pavementPlot2D;
        generator.pavementMaterial = this.pavementMaterial;
        generator.pavementOutset = this.buildingInsetFromPavementEdge;
        generator.SynchronizeSideData();

        // 2. Style
        generator.buildingStyle = GetRandomStyle();
        if (Random.value < chanceForVariedSides && availableSideStylesForVariation?.Count > 0)
        {
            generator.useConsistentStyleForAllSides = false;
            for (int i = 0; i < generator.sideData.Count; i++)
            {
                if (Random.value < 0.5f)
                { // 50% chance for a side to get a custom style
                    generator.sideData[i].useCustomStyle = true;
                    generator.sideData[i].sideStylePreset = availableSideStylesForVariation[Random.Range(0, availableSideStylesForVariation.Count)];
                }
            }
        }

        // 3. Structure
        generator.middleFloors = Random.Range(minFloors, maxFloors + 1);
        generator.useAtticFloor = Random.value < chanceForAtticFloor;
        generator.mansardSlopeHorizontalDistance = Random.Range(randomMansardHDistRange.x, randomMansardHDistRange.y);
        generator.mansardRiseHeight = Random.Range(randomMansardRiseRange.x, randomMansardRiseRange.y);
        generator.atticSlopeHorizontalDistance = Random.Range(randomAtticHDistRange.x, randomAtticHDistRange.y);
        generator.atticRiseHeight = Random.Range(randomAtticRiseRange.x, randomAtticRiseRange.y);
    }

    /// <summary>
    /// Destroys all GameObjects generated by this component.
    /// </summary>
    public void ClearGeneratedSector()
    {
        Transform root = transform.Find(GENERATED_SECTOR_ROOT_NAME);
        if (root != null) DestroySafely(root.gameObject);
        _generatedData = new CitySectorData(); // Reset data
    }

    // --- Helpers and Validation ---

    private bool ValidatePrerequisites()
    {
        if (buildingGeneratorPrefab == null || buildingGeneratorPrefab.GetComponent<PolygonBuildingGenerator>() == null)
        {
            Debug.LogError("Building Generator Prefab (with PolygonBuildingGenerator component) is not assigned.", this);
            return false;
        }
        if (defaultBuildingStyle == null && (availableBuildingStyles == null || !availableBuildingStyles.Any(s => s != null)))
        {
            Debug.LogWarning("No valid building styles are assigned. Buildings may fail to generate.", this);
        }
        return true;
    }

    private void DestroySafely(GameObject obj)
    {
        if (obj == null) return;
        if (Application.isEditor && !Application.isPlaying) DestroyImmediate(obj);
        else Destroy(obj);
    }

    private Transform GetGeneratedSectorRoot()
    {
        Transform root = transform.Find(GENERATED_SECTOR_ROOT_NAME);
        if (root == null)
        {
            root = new GameObject(GENERATED_SECTOR_ROOT_NAME).transform;
            root.SetParent(this.transform, false);
        }
        return root;
    }

    private List<Vector2> GenerateSeedPoints(int count, Vector2 dimensions, float padding)
    {
        var points = new HashSet<Vector2>(); // Use HashSet to automatically handle duplicates
        float minX = -dimensions.x / 2f + padding;
        float maxX = dimensions.x / 2f - padding;
        float minY = -dimensions.y / 2f + padding;
        float maxY = dimensions.y / 2f - padding;

        for (int i = 0; i < count; i++)
        {
            points.Add(new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY)));
        }
        return points.ToList();
    }

    private BuildingStyleSO GetRandomStyle()
    {
        var validStyles = availableBuildingStyles?.Where(s => s != null).ToList();
        if (validStyles != null && validStyles.Count > 0)
        {
            return validStyles[Random.Range(0, validStyles.Count)];
        }
        return defaultBuildingStyle; // Fallback to default
    }

    private void OnValidate()
    {
        // Clamp values to ensure sane generation parameters
        numberOfSeedPoints = Mathf.Max(4, numberOfSeedPoints);
        streetWidth = Mathf.Max(0.1f, streetWidth);
        minFloors = Mathf.Max(1, minFloors);
        maxFloors = Mathf.Max(minFloors, maxFloors);
        voronoiBoundsPadding = Mathf.Max(0, voronoiBoundsPadding);
        minPlotSideLength = Mathf.Max(0.1f, minPlotSideLength);
        minPlotAngleDegrees = Mathf.Clamp(minPlotAngleDegrees, 1f, 179f);
        minPlotArea = Mathf.Max(0.1f, minPlotArea);
        buildingInsetFromPavementEdge = Mathf.Max(0f, buildingInsetFromPavementEdge);
    }
}