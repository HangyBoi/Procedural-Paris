using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

    [HideInInspector]
    public List<Vector2> originalPavementPlotVertices2D; // Set by CitySectorGenerator
    public Material pavementMaterial;                    // Set by CitySectorGenerator
    private PavementGenerator _pavementGeneratorInstance;
    private const string PAVEMENT_GAMEOBJECT_NAME = "Pavement";

    private GameObject _generatedBuildingRoot;
    private const string BUILDING_PARTS_ROOT_NAME = "Generated Building Parts";
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

        Transform existingBuildingPartsRoot = transform.Find(BUILDING_PARTS_ROOT_NAME);
        if (existingBuildingPartsRoot != null) DestroyImmediate(existingBuildingPartsRoot.gameObject);

        _generatedBuildingRoot = new GameObject(BUILDING_PARTS_ROOT_NAME);
        _generatedBuildingRoot.transform.SetParent(this.transform, false);
        _currentBuildingElements.buildingRoot = _generatedBuildingRoot;

        // --- SETUP AND GENERATE PAVEMENT ---
        List<Vector2> pavementVerticesForGeneration = null;

        // Try to derive pavement from the current building footprint (vertexData)
        if (this.vertexData != null && this.vertexData.Count >= 3)
        {
            List<Vector2> currentBuildingFootprint2D = this.vertexData
                .Select(vd => new Vector2(vd.position.x, vd.position.z))
                .ToList();

            // Ensure the footprint is valid before trying to outset/expand
            // You might want to use a more lenient validation for intermediate steps
            if (PolygonUtils.ValidatePlotGeometry(currentBuildingFootprint2D, 0.01f, 1f, 0.01f)) // Basic validation
            {
                // pavementOutset is how much larger the pavement should be.
                // OffsetPolygonBasic: positive shrinks, negative expands.
                // So, we pass -this.pavementOutset to expand.
                float offsetValueForPavement = -this.pavementOutset;

                if (Mathf.Abs(this.pavementOutset) > GeometryConstants.GeometricEpsilon) // Only offset if outset is significant
                {
                    pavementVerticesForGeneration = PolygonUtils.OffsetPolygonBasic(currentBuildingFootprint2D, offsetValueForPavement);

                    if (pavementVerticesForGeneration == null || pavementVerticesForGeneration.Count < 3 ||
                        !PolygonUtils.ValidatePlotGeometry(pavementVerticesForGeneration, 0.01f, 1f, 0.01f))
                    {
                        Debug.LogWarning($"Pavement offsetting by {offsetValueForPavement} for '{gameObject.name}' failed or resulted in an invalid polygon. Using building footprint directly for pavement as a fallback.", this);
                        pavementVerticesForGeneration = new List<Vector2>(currentBuildingFootprint2D); // Fallback
                    }
                }
                else
                {
                    // No outset defined, pavement matches the building footprint
                    pavementVerticesForGeneration = new List<Vector2>(currentBuildingFootprint2D);
                }
            }
            else
            {
                Debug.LogWarning($"Current building footprint (vertexData) for '{gameObject.name}' is invalid. Cannot derive pavement from it at this stage.", this);
            }
        }

        // If deriving from vertexData failed, fall back to originalPavementPlotVertices2D if available
        if (pavementVerticesForGeneration == null || pavementVerticesForGeneration.Count < 3)
        {
            if (this.originalPavementPlotVertices2D != null && this.originalPavementPlotVertices2D.Count >= 3)
            {
                Debug.LogWarning($"Using 'originalPavementPlotVertices2D' for pavement for '{gameObject.name}' as derivation from current vertexData failed or was not applicable.", this);
                pavementVerticesForGeneration = new List<Vector2>(this.originalPavementPlotVertices2D);
            }
            else
            {
                Debug.LogWarning($"No valid vertices could be determined for pavement generation for '{gameObject.name}'. No pavement will be generated.", this);
            }
        }

        // Manage Pavement GameObject and PavementGenerator component
        GameObject pavementGO = null;
        // _pavementGeneratorInstance might be null if ClearBuilding destroyed it or it was never created

        if (pavementVerticesForGeneration != null && pavementVerticesForGeneration.Count >= 3)
        {
            // Find or create the Pavement GameObject
            Transform existingPavementGOTransform = transform.Find(PAVEMENT_GAMEOBJECT_NAME);
            if (existingPavementGOTransform != null)
            {
                pavementGO = existingPavementGOTransform.gameObject;
                _pavementGeneratorInstance = pavementGO.GetComponent<PavementGenerator>();
                if (_pavementGeneratorInstance == null) // Should not happen if setup correctly
                {
                    Debug.LogWarning($"Pavement GameObject '{PAVEMENT_GAMEOBJECT_NAME}' existed but was missing PavementGenerator component. Recreating component on existing GO for '{gameObject.name}'.");
                    _pavementGeneratorInstance = pavementGO.AddComponent<PavementGenerator>(); // Add if missing
                }
            }
            else
            {
                pavementGO = new GameObject(PAVEMENT_GAMEOBJECT_NAME);
                pavementGO.transform.SetParent(this.transform, false);
                _pavementGeneratorInstance = pavementGO.AddComponent<PavementGenerator>();
            }
            _pavementGeneratorInstance.GeneratePavement(pavementVerticesForGeneration, this.pavementMaterial);
        }
        else
        {
            // No valid vertices for pavement, ensure any existing pavement object is cleared/hidden
            Transform oldPavementTransform = transform.Find(PAVEMENT_GAMEOBJECT_NAME);
            if (oldPavementTransform != null)
            {
                PavementGenerator oldPavementGen = oldPavementTransform.GetComponent<PavementGenerator>();
                if (oldPavementGen != null) oldPavementGen.ClearPavement(); // Clears mesh and deactivates
                else oldPavementTransform.gameObject.SetActive(false); // Deactivate if component missing
            }
            _pavementGeneratorInstance = null; // Ensure instance is null if no pavement
        }
        // --- END PAVEMENT ---

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
            ClearBuildingPartsOnly();
            return false;
        }

        _roofWindowManager.GenerateAllWindows(roofWindowsParent, generatedRoofs);

        // Add and configure the BuildingInstanceDataManager to the BUILDING PARTS ROOT
        BuildingInstanceDataManager dataManager = _generatedBuildingRoot.AddComponent<BuildingInstanceDataManager>();
        dataManager.Initialize(this, _generatedBuildingRoot);
        dataManager.elements = this._currentBuildingElements;


        return true;
    }

    private void ClearBuildingPartsOnly()
    {
        // Clear actual building parts
        Transform existingBuildingPartsRoot = transform.Find(BUILDING_PARTS_ROOT_NAME);
        if (existingBuildingPartsRoot != null)
        {
            if (Application.isEditor && !Application.isPlaying)
                DestroyImmediate(existingBuildingPartsRoot.gameObject);
            else
                Destroy(existingBuildingPartsRoot.gameObject);
        }
        _generatedBuildingRoot = null;

        if (_currentBuildingElements != null)
        {
            _currentBuildingElements.ClearReferences();
        }
    }

    public void ClearBuilding() // This clears everything: building parts and pavement
    {
        ClearBuildingPartsOnly();

        // Clear Pavement
        // Find Pavement GO by name as _pavementGeneratorInstance might be stale after domain reloads
        Transform pavementGOTransform = transform.Find(PAVEMENT_GAMEOBJECT_NAME);
        if (pavementGOTransform != null)
        {
            if (Application.isEditor && !Application.isPlaying)
                DestroyImmediate(pavementGOTransform.gameObject);
            else
                Destroy(pavementGOTransform.gameObject);
        }
        _pavementGeneratorInstance = null; // Nullify the reference
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