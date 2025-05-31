using UnityEngine;
using System.Collections.Generic;

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
    public List<PolygonSideData> sideData = new List<PolygonSideData>();

    [Header("Polygon Editing Settings")]
    public float vertexSnapSize = 10.0f;
    public int minSideLengthUnits = 1;

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

    private GameObject _generatedBuildingRoot;
    private const string ROOT_NAME = "Generated Building";
    private const string FACADES_ROOT_NAME = "Facade Elements";
    private const string CORNERS_ROOT_NAME = "Corner Elements";
    private const string ROOF_ROOT_NAME = "Roof Elements";
    private const string ROOF_WINDOWS_ROOT_NAME = "Roof Windows";

    private FacadeGenerator _facadeGenerator;
    private CornerGenerator _cornerGenerator;
    private RoofGenerator _roofGenerator;
    private RoofWindowGenerator _roofWindowManager;

    private GeneratedBuildingElements _currentBuildingElements;

    public bool GenerateBuilding()
    {
/*        if (vertexData != null && vertexSnapSize > GeometryConstants.GeometricEpsilon)
        {
            for (int i = 0; i < vertexData.Count; i++)
            {
                // Create a new PolygonVertexData to avoid modifying struct in list directly if it causes issues
                PolygonVertexData currentVD = vertexData[i];
                currentVD.position = SnapVertexPosition(currentVD.position);
                vertexData[i] = currentVD;
            }
        }*/

        ClearBuilding();
        _currentBuildingElements = new GeneratedBuildingElements();
        SynchronizeSideData();

        if (buildingStyle == null)
        {
            Debug.LogError("Cannot generate building: Building Style SO is not assigned.", this);
            return false;
        }
        if (vertexData.Count < 3)
        {
            Debug.LogWarning("Cannot generate building: Polygon requires at least 3 vertices.", this);
            return false;
        }

        _generatedBuildingRoot = new GameObject(ROOT_NAME);
        _generatedBuildingRoot.transform.SetParent(this.transform, false);
        _currentBuildingElements.buildingRoot = _generatedBuildingRoot;

        for (int i = 0; i < vertexData.Count; i++)
        {
            _currentBuildingElements.facadeElementsPerSide.Add(new SideElementGroup { sideIndex = i });
        }

        _facadeGenerator = new FacadeGenerator(this, vertexData, sideData, buildingStyle, _currentBuildingElements);
        _cornerGenerator = new CornerGenerator(this, vertexData, buildingStyle, _currentBuildingElements);
        _roofGenerator = new RoofGenerator(this, vertexData, buildingStyle, _currentBuildingElements);
        _roofWindowManager = new RoofWindowGenerator(this, vertexData, sideData, buildingStyle, _currentBuildingElements);

        Transform facadesParent = new GameObject(FACADES_ROOT_NAME).transform;
        facadesParent.SetParent(_generatedBuildingRoot.transform, false);
        Transform cornersParent = new GameObject(CORNERS_ROOT_NAME).transform;
        cornersParent.SetParent(_generatedBuildingRoot.transform, false);
        Transform roofParent = new GameObject(ROOF_ROOT_NAME).transform;
        roofParent.SetParent(_generatedBuildingRoot.transform, false);
        Transform roofWindowsParent = new GameObject(ROOF_WINDOWS_ROOT_NAME).transform;
        roofWindowsParent.SetParent(roofParent, false);


        _facadeGenerator.GenerateAllFacades(facadesParent);
        _cornerGenerator.GenerateAllCorners(cornersParent);

        GeneratedRoofObjects generatedRoofs = _roofGenerator.GenerateMainRoof(roofParent, out bool roofSuccess);
        if (!roofSuccess)
        {
            Debug.LogWarning($"Building '{gameObject.name}': Main roof generation failed (likely flat cap). Aborting and clearing this building.", this);
            ClearBuilding(); // Clean up partially generated building
            return false;
        }

        _roofWindowManager.GenerateAllWindows(roofWindowsParent, generatedRoofs);

        // Add and configure the BuildingInstanceDataManager
        BuildingInstanceDataManager dataManager = _generatedBuildingRoot.AddComponent<BuildingInstanceDataManager>();
        dataManager.Initialize(this, _generatedBuildingRoot);
        // Copy populated elements to the data manager
        dataManager.elements = this._currentBuildingElements;


        return true;
    }

    public void ClearBuilding()
    {
        Transform existingRoot = transform.Find(ROOT_NAME);
        while (existingRoot != null)
        {
            if (Application.isEditor && !Application.isPlaying)
                DestroyImmediate(existingRoot.gameObject);
            else
                Destroy(existingRoot.gameObject);
            existingRoot = transform.Find(ROOT_NAME);
        }
        _generatedBuildingRoot = null;
        if (_currentBuildingElements != null)
        {
            _currentBuildingElements.ClearReferences();
        }
    }

    public void SynchronizeSideData()
    {
        vertexData ??= new List<PolygonVertexData>();
        sideData ??= new List<PolygonSideData>();
        int requiredCount = vertexData.Count;
        while (sideData.Count < requiredCount) sideData.Add(new PolygonSideData());
        while (sideData.Count > requiredCount && sideData.Count > 0) sideData.RemoveAt(sideData.Count - 1);
    }

    public Vector3 SnapVertexPosition(Vector3 vertexPos)
    {
        if (vertexSnapSize <= GeometryConstants.GeometricEpsilon)
            return new Vector3(vertexPos.x, 0f, vertexPos.z);
        return new Vector3(
            Mathf.Round(vertexPos.x / vertexSnapSize) * vertexSnapSize,
            0f,
            Mathf.Round(vertexPos.z / vertexSnapSize) * vertexSnapSize
        );
    }

    void OnValidate()
    {
        SynchronizeSideData();
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