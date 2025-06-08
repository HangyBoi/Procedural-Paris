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
/// Generates a procedural building based on a user-defined polygon footprint and style settings.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class PolygonBuildingGenerator : MonoBehaviour
{
    [Header("Building Style")]
    [Tooltip("Assign a Building Style ScriptableObject to define default prefabs and overall style.")]
    public BuildingStyleSO buildingStyle;

    [Header("Polygon Definition")]
    public List<PolygonVertexData> vertexData = new List<PolygonVertexData>() {
        new() { position = new Vector3(0, 0, 0), addCornerElement = true },
        new() { position = new Vector3(0, 0, 5), addCornerElement = true },
        new() { position = new Vector3(5, 0, 5), addCornerElement = true },
        new() { position = new Vector3(5, 0, 0), addCornerElement = true }
    };

    [Header("Style Application")]
    public bool useConsistentStyleForAllSides = true;
    public List<PolygonSideData> sideData = new List<PolygonSideData>();

    [Header("Polygon Editing Settings")]
    public float vertexSnapSize = 10.0f;
    public int minSideLengthUnits = 1;

    [Header("Pavement Settings")]
    public float pavementOutset = 0.5f;

    [Header("Building Structure Settings")]
    public int middleFloors = 3;
    public float floorHeight = 10.0f;

    [Header("Facade Placement Settings")]
    public bool scaleFacadesToFitSide = true;
    public float nominalFacadeWidth = 10.0f;

    [Header("Corner Element Settings")]
    public bool useCornerCaps = true;
    public float cornerElementForwardOffset = 0.0f;

    [Header("Procedural Mansard Roof")]
    public bool useMansardFloor = true;
    public Material mansardMaterial;
    public float mansardSlopeHorizontalDistance = 1.5f;
    public float mansardRiseHeight = 2.0f;

    [Header("Procedural Attic Roof")]
    public bool useAtticFloor = true;
    public Material atticMaterial;
    public float atticSlopeHorizontalDistance = 1.0f;
    public float atticRiseHeight = 1.5f;

    [Header("Top Roof Settings")]
    public Material roofMaterial;
    public float flatRoofEdgeOffset = 0.0f;
    public float roofUvScale = 1.0f;

    [Header("Roof Window Settings")]
    public bool placeMansardWindows = true;
    public bool placeAtticWindows = true;
    public float mansardWindowInset = 0.05f;
    public float atticWindowInset = 0.05f;
    public bool scaleRoofWindowsToFitSegment = false;

    [Header("Roof Window Generation Minima/Maxima")]
    public float maxMansardHDistForWindows = 8.0f;
    public float minMansardRiseForWindows = 3.0f;
    public float maxAtticHDistForWindows = 30.0f;
    public float minAtticRiseForWindows = 10.0f;

    [HideInInspector] public List<Vector2> originalPavementPlotVertices2D;
    [HideInInspector] public Material pavementMaterial;

    private PavementGenerator _pavementGeneratorInstance;
    private GameObject _generatedBuildingRoot;
    private FacadeGenerator _facadeGenerator;
    private CornerGenerator _cornerGenerator;
    private RoofGenerator _roofGenerator;
    private RoofWindowGenerator _roofWindowManager;
    private GeneratedBuildingElements _currentBuildingElements;

    private const string PAVEMENT_GAMEOBJECT_NAME = "Pavement";
    private const string BUILDING_PARTS_ROOT_NAME = "Generated Building Parts";
    private const string FACADES_ROOT_NAME = "Facade Elements";
    private const string CORNERS_ROOT_NAME = "Corner Elements";
    private const string ROOF_ROOT_NAME = "Roof Elements";
    private const string ROOF_WINDOWS_ROOT_NAME = "Roof Windows";

    /// <summary>
    /// Clears any existing building parts and generates a new building based on the current settings.
    /// </summary>
    /// <returns>True if generation was successful, false otherwise.</returns>
    public bool GenerateBuilding()
    {
        ClearBuilding();
        _currentBuildingElements = new GeneratedBuildingElements();
        for (int i = 0; i < vertexData.Count; i++)
        {
            _currentBuildingElements.facadeElementsPerSide.Add(new SideElementGroup { sideIndex = i });
        }
        SynchronizeSideData();

        if (!ValidatePreGenerationState()) return false;

        _generatedBuildingRoot = new GameObject(BUILDING_PARTS_ROOT_NAME);
        _generatedBuildingRoot.transform.SetParent(this.transform, false);
        _currentBuildingElements.buildingRoot = _generatedBuildingRoot;

        HandlePavementGeneration();
        SetupHierarchy(out var facadesParent, out var cornersParent, out var roofParent, out var roofWindowsParent);

        _facadeGenerator = new FacadeGenerator(this, vertexData, sideData, buildingStyle, _currentBuildingElements);
        _cornerGenerator = new CornerGenerator(this, vertexData, buildingStyle, _currentBuildingElements);
        _roofGenerator = new RoofGenerator(this, vertexData, buildingStyle, _currentBuildingElements);
        _roofWindowManager = new RoofWindowGenerator(this, vertexData, sideData, buildingStyle, _currentBuildingElements);

        _facadeGenerator.GenerateAllFacades(facadesParent);
        _cornerGenerator.GenerateAllCorners(cornersParent);

        GeneratedRoofObjects generatedRoofs = _roofGenerator.GenerateMainRoof(roofParent, out bool roofSuccess);
        if (!roofSuccess)
        {
            Debug.LogWarning($"Building '{gameObject.name}': Main roof generation failed. Aborting.", this);
            ClearBuildingPartsOnly();
            return false;
        }

        _roofWindowManager.GenerateAllWindows(roofWindowsParent, generatedRoofs);
        AddDataManager();
        return true;
    }

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

    private void SetupHierarchy(out Transform facades, out Transform corners, out Transform roof, out Transform roofWindows)
    {
        facades = new GameObject(FACADES_ROOT_NAME).transform;
        facades.SetParent(_generatedBuildingRoot.transform, false);

        corners = new GameObject(CORNERS_ROOT_NAME).transform;
        corners.SetParent(_generatedBuildingRoot.transform, false);

        roof = new GameObject(ROOF_ROOT_NAME).transform;
        roof.SetParent(_generatedBuildingRoot.transform, false);

        roofWindows = new GameObject(ROOF_WINDOWS_ROOT_NAME).transform;
        roofWindows.SetParent(roof, false);
    }

    private void HandlePavementGeneration()
    {
        List<Vector2> pavementVertices = DeterminePavementVertices();
        if (pavementVertices == null || pavementVertices.Count < 3)
        {
            _pavementGeneratorInstance = null;
            return;
        }

        var pavementGO = new GameObject(PAVEMENT_GAMEOBJECT_NAME);
        pavementGO.transform.SetParent(this.transform, false);
        _pavementGeneratorInstance = pavementGO.AddComponent<PavementGenerator>();
        _pavementGeneratorInstance.GeneratePavement(pavementVertices, this.pavementMaterial);
    }

    private List<Vector2> DeterminePavementVertices()
    {
        if (this.vertexData != null && this.vertexData.Count >= 3)
        {
            List<Vector2> footprint = this.vertexData.Select(vd => new Vector2(vd.position.x, vd.position.z)).ToList();
            if (PolygonUtils.ValidatePlotGeometry(footprint, 0.01f, 1f, 0.01f))
            {
                if (Mathf.Abs(this.pavementOutset) > 1e-6f)
                {
                    List<Vector2> outsetVertices = PolygonUtils.OffsetPolygonBasic(footprint, -this.pavementOutset);
                    if (outsetVertices != null && outsetVertices.Count >= 3 && PolygonUtils.ValidatePlotGeometry(outsetVertices, 0.01f, 1f, 0.01f))
                    {
                        return outsetVertices;
                    }
                }
                return footprint;
            }
        }

        if (this.originalPavementPlotVertices2D != null && this.originalPavementPlotVertices2D.Count >= 3)
        {
            return new List<Vector2>(this.originalPavementPlotVertices2D);
        }

        return null;
    }

    private void AddDataManager()
    {
        BuildingInstanceDataManager dataManager = _generatedBuildingRoot.AddComponent<BuildingInstanceDataManager>();
        dataManager.Initialize(this, _generatedBuildingRoot);
        dataManager.elements = this._currentBuildingElements;
    }

    /// <summary>
    /// Destroys all generated building parts and pavement.
    /// </summary>
    public void ClearBuilding()
    {
        ClearBuildingPartsOnly();
        Transform pavementGOTransform = transform.Find(PAVEMENT_GAMEOBJECT_NAME);
        SafeDestroy(pavementGOTransform?.gameObject);
        _pavementGeneratorInstance = null;
    }

    private void ClearBuildingPartsOnly()
    {
        Transform existingBuildingPartsRoot = transform.Find(BUILDING_PARTS_ROOT_NAME);
        SafeDestroy(existingBuildingPartsRoot != null ? existingBuildingPartsRoot.gameObject : null);
        _generatedBuildingRoot = null;

        _currentBuildingElements?.ClearReferences();
    }

    /// <summary>
    /// Ensures the 'sideData' list matches the number of vertices, adding or removing entries as needed.
    /// </summary>
    public void SynchronizeSideData()
    {
        vertexData ??= new List<PolygonVertexData>();
        sideData ??= new List<PolygonSideData>();

        int requiredCount = vertexData.Count;
        while (sideData.Count < requiredCount)
            sideData.Add(new PolygonSideData());
        while (sideData.Count > requiredCount && sideData.Count > 0)
            sideData.RemoveAt(sideData.Count - 1);
    }

    /// <summary>
    /// Snaps a vertex position to a grid defined by 'vertexSnapSize'.
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
    /// Safely destroys a GameObject, handling both Editor and Play mode.
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
    /// Called in the editor when a script variable is changed. Enforces constraints on values.
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

        minMansardRiseForWindows = Mathf.Max(0f, minMansardRiseForWindows);
        maxMansardHDistForWindows = Mathf.Max(0f, maxMansardHDistForWindows);
        minAtticRiseForWindows = Mathf.Max(0f, minAtticRiseForWindows);
        maxAtticHDistForWindows = Mathf.Max(0f, maxAtticHDistForWindows);
    }
}