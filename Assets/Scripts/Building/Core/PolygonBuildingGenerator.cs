// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
// This script generates procedural buildings based on user-defined polygon footprints and style settings.
//

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The main controller for generating a procedural building. It coordinates various sub-generators
/// based on a user-defined polygon footprint and style settings.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class PolygonBuildingGenerator : MonoBehaviour
{
    // --- Public Fields for Inspector ---

    [Header("Building Style")]
    [Tooltip("Assign a Building Style ScriptableObject to define default prefabs and overall style.")]
    public BuildingStyleSO buildingStyle;

    [Header("Polygon Definition")]
    [Tooltip("The list of vertices that define the 2D footprint of the building on the XZ plane.")]
    public List<PolygonVertexData> vertexData = new List<PolygonVertexData>() {
        new() { position = new Vector3(0, 0, 0), addCornerElement = true },
        new() { position = new Vector3(0, 0, 5), addCornerElement = true },
        new() { position = new Vector3(5, 0, 5), addCornerElement = true },
        new() { position = new Vector3(5, 0, 0), addCornerElement = true }
    };

    [Header("Style Application")]
    [Tooltip("If true, all sides use the default style. If false, per-side styles can be used.")]
    public bool useConsistentStyleForAllSides = true;
    [Tooltip("Custom style settings for each side of the polygon. Only used if 'Use Consistent Style' is false.")]
    public List<PolygonSideData> sideData = new List<PolygonSideData>();

    [Header("Polygon Editing Settings")]
    [Tooltip("The grid size to which vertices will snap when moved in the editor.")]
    public float vertexSnapSize = 1.0f;
    [Tooltip("The minimum number of facade segments a side should have, regardless of its length.")]
    public int minSideLengthUnits = 1;

    [Header("Pavement Settings")]
    [Tooltip("The distance the pavement will extend outwards from the building footprint.")]
    public float pavementOutset = 0.5f;

    [Header("Building Structure Settings")]
    [Tooltip("The number of floors between the ground floor and the roof section.")]
    public int middleFloors = 3;
    [Tooltip("The vertical height of each floor.")]
    public float floorHeight = 10.0f;

    [Header("Facade Placement Settings")]
    [Tooltip("If true, facade prefabs will be scaled to perfectly fit the calculated segment width. If false, they will remain at their nominal width, which may leave gaps.")]
    public bool scaleFacadesToFitSide = true;
    [Tooltip("The ideal or design width of a standard facade prefab. Used for calculating segment counts and scaling.")]
    public float nominalFacadeWidth = 10.0f;

    [Header("Corner Element Settings")]
    [Tooltip("If true, corner elements (like chimneys) will have caps placed on top.")]
    public bool useCornerCaps = true;
    [Tooltip("How far to push corner elements outwards from their corner vertex.")]
    public float cornerElementForwardOffset = 0.0f;

    [Header("Procedural Mansard Roof")]
    [Tooltip("If true, a sloped mansard roof layer will be generated.")]
    public bool useMansardFloor = true;
    public Material mansardMaterial;
    [Tooltip("The horizontal inset distance of the mansard slope.")]
    public float mansardSlopeHorizontalDistance = 1.5f;
    [Tooltip("The vertical rise of the mansard slope.")]
    public float mansardRiseHeight = 2.0f;

    [Header("Procedural Attic Roof")]
    [Tooltip("If true, a sloped attic roof layer will be generated on top of the mansard (or walls).")]
    public bool useAtticFloor = true;
    public Material atticMaterial;
    [Tooltip("The horizontal inset distance of the attic slope.")]
    public float atticSlopeHorizontalDistance = 1.0f;
    [Tooltip("The vertical rise of the attic slope.")]
    public float atticRiseHeight = 1.5f;

    [Header("Top Roof Settings")]
    public Material roofMaterial;
    [Tooltip("An additional offset for the final flat roof cap. Can be positive (inset) or negative (overhang).")]
    public float flatRoofEdgeOffset = 0.0f;
    [Tooltip("The tiling scale for the roof material's UV coordinates.")]
    public float roofUvScale = 1.0f;

    [Header("Roof Window Settings")]
    public bool placeMansardWindows = true;
    public bool placeAtticWindows = true;
    [Tooltip("How far to push roof windows into the roof surface from their initial placement.")]
    public float mansardWindowInset = 0.05f;
    public float atticWindowInset = 0.05f;
    public bool scaleRoofWindowsToFitSegment = false;

    [Header("Roof Window Generation Minima/Maxima")]
    public float maxMansardHDistForWindows = 8.0f;
    public float minMansardRiseForWindows = 3.0f;
    public float maxAtticHDistForWindows = 30.0f;
    public float minAtticRiseForWindows = 10.0f;

    // --- Hidden & Internal State ---

    [HideInInspector] public List<Vector2> originalPavementPlotVertices2D;
    [HideInInspector] public Material pavementMaterial;

    private GeneratedBuildingElements _currentBuildingElements;
    private const string PAVEMENT_GAMEOBJECT_NAME = "Pavement";
    private const string BUILDING_PARTS_ROOT_NAME = "Generated Building Parts";
    private const string FACADES_ROOT_NAME = "Facade Elements";
    private const string CORNERS_ROOT_NAME = "Corner Elements";
    private const string ROOF_ROOT_NAME = "Roof Elements";
    private const string ROOF_WINDOWS_ROOT_NAME = "Roof Windows";

    /// <summary>
    /// The main entry point for generating the building. This method orchestrates the entire process,
    /// from cleaning up old data to calling each specialized generator module in sequence.
    /// </summary>
    /// <returns>True if generation completed successfully, false otherwise.</returns>
    public bool GenerateBuilding()
    {
        ClearAllGeneratedObjects();

        // Step 1: Initialize data stores and validate settings.
        _currentBuildingElements = new GeneratedBuildingElements(vertexData.Count);
        SynchronizeSideData();
        if (!ValidatePreGenerationState()) return false;

        // Step 2: Set up the root hierarchy for the new building parts.
        var (root, facades, corners, roof, windows) = CreateHierarchy();
        _currentBuildingElements.buildingRoot = root.gameObject;

        // Step 3: Initialize all specialized generator modules.
        var facadeGenerator = new FacadeGenerator(this, vertexData, sideData, buildingStyle, _currentBuildingElements);
        var cornerGenerator = new CornerGenerator(this, vertexData, buildingStyle, _currentBuildingElements);
        var roofGenerator = new RoofGenerator(this, vertexData, buildingStyle, _currentBuildingElements);
        var roofWindowManager = new RoofWindowGenerator(this, vertexData, sideData, buildingStyle, _currentBuildingElements);

        // Step 4: Execute the generation sequence.
        GeneratePavement();
        facadeGenerator.GenerateAllFacades(facades);
        cornerGenerator.GenerateAllCorners(corners);

        var generatedRoofs = roofGenerator.GenerateMainRoof(roof, out bool roofSuccess);
        if (!roofSuccess)
        {
            Debug.LogWarning($"Building '{gameObject.name}': Main roof generation failed. Aborting.", this);
            ClearBuildingModel(); // Clean up partial building but leave pavement.
            return false;
        }

        roofWindowManager.GenerateAllWindows(windows, generatedRoofs);

        // Step 5: Finalize by adding a data manager component for runtime access and customization.
        AddDataManager(root.gameObject);
        return true;
    }

    /// <summary>
    /// Checks for critical missing references or invalid data before generation starts.
    /// </summary>
    private bool ValidatePreGenerationState()
    {
        if (buildingStyle == null)
        {
            Debug.LogError("Cannot generate building: Building Style is not assigned.", this);
            return false;
        }
        if (vertexData.Count < 3)
        {
            Debug.LogWarning("Cannot generate building: Polygon requires at least 3 vertices.", this);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Creates the main root GameObject and all necessary child containers for organizing generated parts.
    /// </summary>
    private (Transform root, Transform facades, Transform corners, Transform roof, Transform windows) CreateHierarchy()
    {
        var root = new GameObject(BUILDING_PARTS_ROOT_NAME).transform;
        root.SetParent(this.transform, false);

        var facades = new GameObject(FACADES_ROOT_NAME).transform;
        facades.SetParent(root, false);

        var corners = new GameObject(CORNERS_ROOT_NAME).transform;
        corners.SetParent(root, false);

        var roof = new GameObject(ROOF_ROOT_NAME).transform;
        roof.SetParent(root, false);

        var windows = new GameObject(ROOF_WINDOWS_ROOT_NAME).transform;
        windows.SetParent(roof, false);

        return (root, facades, corners, roof, windows);
    }

    /// <summary>
    /// Determines the correct vertices for the pavement and instructs the PavementGenerator to create it.
    /// </summary>
    private void GeneratePavement()
    {
        List<Vector2> pavementVertices = DeterminePavementVertices();
        if (pavementVertices == null) return;

        var pavementGO = new GameObject(PAVEMENT_GAMEOBJECT_NAME);
        pavementGO.transform.SetParent(this.transform, false);
        var pavementGenerator = pavementGO.AddComponent<PavementGenerator>();
        pavementGenerator.GeneratePavement(pavementVertices, this.pavementMaterial);
    }

    /// <summary>
    /// Decides the final pavement outline, including applying an outset and falling back to original data if needed.
    /// </summary>
    private List<Vector2> DeterminePavementVertices()
    {
        // First, attempt to use the current live vertex data.
        if (vertexData != null && vertexData.Count >= 3)
        {
            var footprint = vertexData.Select(vd => new Vector2(vd.position.x, vd.position.z)).ToList();
            if (PolygonUtils.ValidatePlotGeometry(footprint, 0.01f, 1f, 0.01f))
            {
                if (Mathf.Abs(pavementOutset) > GeometryConstants.GeometricEpsilon)
                {
                    // Attempt to create an outset polygon.
                    var outsetVertices = PolygonUtils.OffsetPolygonBasic(footprint, -pavementOutset);
                    // If the outset is valid, use it. Otherwise, fall back to the base footprint.
                    if (outsetVertices != null && PolygonUtils.ValidatePlotGeometry(outsetVertices, 0.01f, 1f, 0.01f))
                    {
                        return outsetVertices;
                    }
                }
                return footprint; // Use base footprint if no outset or if outset failed.
            }
        }

        // As a final fallback, use stored original plot data if the live data is invalid.
        return (originalPavementPlotVertices2D != null && originalPavementPlotVertices2D.Count >= 3)
            ? new List<Vector2>(originalPavementPlotVertices2D)
            : null;
    }

    /// <summary>
    /// Adds a data manager component to the root object, which stores references to generated elements for later access.
    /// </summary>
    private void AddDataManager(GameObject root)
    {
        var dataManager = root.AddComponent<BuildingInstanceDataManager>();
        dataManager.Initialize(this, root, _currentBuildingElements);
    }

    /// <summary>
    /// Public method to completely clears all generated objects, including the building model and pavement.
    /// </summary>
    public void ClearAllGeneratedObjects()
    {
        ClearBuildingModel();
        ClearPavement();
    }

    /// <summary>
    /// Destroys only the generated building model (facades, corners, roof).
    /// </summary>
    private void ClearBuildingModel()
    {
        Transform existingRoot = transform.Find(BUILDING_PARTS_ROOT_NAME);
        SafeDestroy(existingRoot?.gameObject);
        _currentBuildingElements?.ClearReferences();
    }

    /// <summary>
    /// Destroys only the generated pavement object.
    /// </summary>
    private void ClearPavement()
    {
        Transform pavement = transform.Find(PAVEMENT_GAMEOBJECT_NAME);
        SafeDestroy(pavement?.gameObject);
    }

    /// <summary>
    /// Ensures the 'sideData' list has the same number of elements as the 'vertexData' list.
    /// This keeps the inspector data synchronized when adding or removing vertices.
    /// </summary>
    public void SynchronizeSideData()
    {
        vertexData ??= new List<PolygonVertexData>();
        sideData ??= new List<PolygonSideData>();

        while (sideData.Count < vertexData.Count) sideData.Add(new PolygonSideData());
        while (sideData.Count > vertexData.Count) sideData.RemoveAt(sideData.Count - 1);
    }

    /// <summary>
    /// Snaps a given vertex position to a grid defined by 'vertexSnapSize'.
    /// </summary>
    public Vector3 SnapVertexPosition(Vector3 vertexPos)
    {
        if (vertexSnapSize <= GeometryConstants.GeometricEpsilon) return new Vector3(vertexPos.x, 0f, vertexPos.z);

        return new Vector3(
            Mathf.Round(vertexPos.x / vertexSnapSize) * vertexSnapSize,
            0f, // Ensure Y is always 0 for the 2D footprint.
            Mathf.Round(vertexPos.z / vertexSnapSize) * vertexSnapSize
        );
    }

    /// <summary>
    /// Safely destroys a GameObject, handling both Editor and Play mode correctly.
    /// </summary>
    private static void SafeDestroy(GameObject obj)
    {
        if (obj == null) return;

        if (Application.isEditor && !Application.isPlaying)
            DestroyImmediate(obj);
        else
            Destroy(obj);
    }

    /// <summary>
    /// This method is called in the editor when a script variable is changed.
    /// It enforces constraints on values to prevent invalid generation parameters.
    /// </summary>
    private void OnValidate()
    {
        SynchronizeSideData();

        // Clamp numerical settings to sensible minimums.
        middleFloors = Mathf.Max(0, middleFloors);
        floorHeight = Mathf.Max(0.1f, floorHeight);
        nominalFacadeWidth = Mathf.Max(0.1f, nominalFacadeWidth);
        minSideLengthUnits = Mathf.Max(0, minSideLengthUnits);

        mansardSlopeHorizontalDistance = Mathf.Max(0, mansardSlopeHorizontalDistance);
        mansardRiseHeight = Mathf.Max(0, mansardRiseHeight);
        atticSlopeHorizontalDistance = Mathf.Max(0, atticSlopeHorizontalDistance);
        atticRiseHeight = Mathf.Max(0, atticRiseHeight);
    }
}