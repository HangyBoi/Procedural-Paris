using UnityEngine;
using System.Collections.Generic;
using MIConvexHull;

public class CitySectorData
{
    public List<Vector2> SeedPoints { get; set; } = new List<Vector2>();
    public List<List<Vector2>> RawVoronoiCells { get; set; } = new List<List<Vector2>>();
    public List<List<Vector2>> ProcessedBuildingPlots { get; set; } = new List<List<Vector2>>();
    public ITriangulation<DefaultVertex, DefaultTriangulationCell<DefaultVertex>> DelaunayTriangulation { get; set; }

}