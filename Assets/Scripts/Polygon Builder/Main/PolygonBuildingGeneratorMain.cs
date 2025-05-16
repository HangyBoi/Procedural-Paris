using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
public class PolygonBuildingGeneratorMain : MonoBehaviour
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
    public float vertexSnapSize = 1.0f; // Adjusted back from 10 for typical use
    public int minSideLengthUnits = 1;

    [Header("Building Structure Settings")]
    public int middleFloors = 3;
    public float floorHeight = 3.0f; // Adjusted back from 10

    [Header("Facade Placement Settings")]
    public bool scaleFacadesToFitSide = true;
    public float nominalFacadeWidth = 1.0f; // Adjusted back from 10

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
    public float flatRoofEdgeOffset = 0.0f;
    public Material roofMaterial;
    public float roofUvScale = 1.0f;

    [Header("Roof Window Settings")]
    public bool placeMansardWindows = true;
    public bool placeAtticWindows = true;
    [Tooltip("How much to inset windows into the roof surface, relative to the roof surface normal. Positive pushes in, negative out.")]
    public float roofWindowInset = 0.05f; // Small value
    public bool scaleRoofWindowsToFitSegment = false;

    private GameObject _generatedBuildingRoot;
    private const string ROOT_NAME = "Generated Building";
    private const string FACADES_ROOT_NAME = "Facade Elements";
    private const string CORNERS_ROOT_NAME = "Corner Elements";
    private const string ROOF_ROOT_NAME = "Roof Elements";
    private const string ROOF_WINDOWS_ROOT_NAME = "Roof Windows"; // New root for roof windows

    // Removed: _debug* roof mesh/transform variables

    private FacadeGenerator _facadeGenerator;
    private CornerGenerator _cornerGenerator;
    private RoofGenerator _roofGenerator;
    private RoofWindowGenerator _roofWindowManager; // New manager

    public void GenerateBuilding()
    {
        ClearBuilding();
        SynchronizeSideData();

        if (buildingStyle == null)
        {
            Debug.LogError("Cannot generate building: Building Style SO is not assigned.", this);
            return;
        }
        if (vertexData.Count < 3)
        {
            Debug.LogWarning("Cannot generate building: Polygon requires at least 3 vertices.", this);
            return;
        }

        _generatedBuildingRoot = new GameObject(ROOT_NAME);
        _generatedBuildingRoot.transform.SetParent(this.transform, false);

        _facadeGenerator = new FacadeGenerator(this, vertexData, sideData, buildingStyle);
        _cornerGenerator = new CornerGenerator(this, vertexData, buildingStyle);
        _roofGenerator = new RoofGenerator(this, vertexData, buildingStyle);
        _roofWindowManager = new RoofWindowGenerator(this, vertexData, sideData, buildingStyle); // Initialize

        Transform facadesParent = new GameObject(FACADES_ROOT_NAME).transform;
        facadesParent.SetParent(_generatedBuildingRoot.transform, false);
        Transform cornersParent = new GameObject(CORNERS_ROOT_NAME).transform;
        cornersParent.SetParent(_generatedBuildingRoot.transform, false);
        Transform roofParent = new GameObject(ROOF_ROOT_NAME).transform;
        roofParent.SetParent(_generatedBuildingRoot.transform, false);
        Transform roofWindowsParent = new GameObject(ROOF_WINDOWS_ROOT_NAME).transform; // Create parent for roof windows
        roofWindowsParent.SetParent(roofParent, false); // Child of main roof container


        _facadeGenerator.GenerateAllFacades(facadesParent);
        _cornerGenerator.GenerateAllCorners(cornersParent);

        // Generate roof meshes first
        GeneratedRoofObjects generatedRoofs = _roofGenerator.GenerateMainRoof(roofParent);

        // Then generate windows onto these roofs
        _roofWindowManager.GenerateAllWindows(roofWindowsParent, generatedRoofs);
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
        if (vertexSnapSize <= GeometryUtils.Epsilon)
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
    }
}