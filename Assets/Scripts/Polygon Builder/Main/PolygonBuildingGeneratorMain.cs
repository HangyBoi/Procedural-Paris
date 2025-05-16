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
    public List<PolygonSideData> sideData = new List<PolygonSideData>(); // Per-side style overrides

    [Header("Polygon Editing Settings")]
    public float vertexSnapSize = 10.0f;
    public int minSideLengthUnits = 1; // Min facade segments per side

    [Header("Building Structure Settings")]
    public int middleFloors = 3;
    public float floorHeight = 10.0f;

    [Header("Facade Placement Settings")]
    public bool scaleFacadesToFitSide = true;
    public float nominalFacadeWidth = 10.0f; // Design width of facade prefabs

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
    public float flatRoofEdgeOffset = 0.0f; // Inward (positive) or outward (negative) offset for the flat roof cap
    public Material roofMaterial;
    public float roofUvScale = 1.0f;

    private GameObject _generatedBuildingRoot;
    private const string ROOT_NAME = "Generated Building";
    private const string FACADES_ROOT_NAME = "Facade Elements";
    private const string CORNERS_ROOT_NAME = "Corner Elements";
    private const string ROOF_ROOT_NAME = "Roof Elements";


#if UNITY_EDITOR
    // These are populated by RoofGenerator for editor script visualization
    [HideInInspector] public Mesh _debugFlatRoofMesh;
    [HideInInspector] public Transform _debugFlatRoofTransform;
    [HideInInspector] public Mesh _debugMansardMesh;
    [HideInInspector] public Transform _debugMansardTransform;
    [HideInInspector] public Mesh _debugAtticMesh;
    [HideInInspector] public Transform _debugAtticTransform;
#endif

    private FacadeGenerator _facadeGenerator;
    private CornerGenerator _cornerGenerator;
    private RoofGenerator _roofGenerator;

    public void GenerateBuilding()
    {
        ClearBuilding();
        SynchronizeSideData(); // Ensure sideData matches vertexData count

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

        // Create the main root for all generated parts
        _generatedBuildingRoot = new GameObject(ROOT_NAME);
        _generatedBuildingRoot.transform.SetParent(this.transform, false);

        // Initialize generators
        _facadeGenerator = new FacadeGenerator(this, vertexData, sideData, buildingStyle);
        _cornerGenerator = new CornerGenerator(this, vertexData, buildingStyle);
        _roofGenerator = new RoofGenerator(this, vertexData, buildingStyle);

        // Create sub-roots for organization
        Transform facadesParent = new GameObject(FACADES_ROOT_NAME).transform;
        facadesParent.SetParent(_generatedBuildingRoot.transform, false);

        Transform cornersParent = new GameObject(CORNERS_ROOT_NAME).transform;
        cornersParent.SetParent(_generatedBuildingRoot.transform, false);

        Transform roofParent = new GameObject(ROOF_ROOT_NAME).transform;
        roofParent.SetParent(_generatedBuildingRoot.transform, false);

        // --- Generate Components ---
        _facadeGenerator.GenerateAllFacades(facadesParent);
        _cornerGenerator.GenerateAllCorners(cornersParent);

        RoofDebugData roofDebug = _roofGenerator.GenerateMainRoof(roofParent);
#if UNITY_EDITOR
        _debugFlatRoofMesh = roofDebug.FlatRoofMesh;
        _debugFlatRoofTransform = roofDebug.FlatRoofTransform;
        _debugMansardMesh = roofDebug.MansardMesh;
        _debugMansardTransform = roofDebug.MansardTransform;
        _debugAtticMesh = roofDebug.AtticMesh;
        _debugAtticTransform = roofDebug.AtticTransform;
#endif
    }

    public void ClearBuilding()
    {
        // Find and destroy the existing generated building root by name
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

#if UNITY_EDITOR
        // Clear debug mesh references
        _debugFlatRoofMesh = null;
        _debugFlatRoofTransform = null;
        _debugMansardMesh = null;
        _debugMansardTransform = null;
        _debugAtticMesh = null;
        _debugAtticTransform = null;
#endif
    }

    /// <summary>
    /// Ensures the sideData list has the same number of elements as the vertexData list.
    /// Adds or removes PolygonSideData entries as needed.
    /// </summary>
    public void SynchronizeSideData()
    {
        vertexData ??= new List<PolygonVertexData>();
        sideData ??= new List<PolygonSideData>();

        int requiredCount = vertexData.Count;

        while (sideData.Count < requiredCount)
        {
            sideData.Add(new PolygonSideData());
        }
        while (sideData.Count > requiredCount && sideData.Count > 0)
        {
            sideData.RemoveAt(sideData.Count - 1);
        }
    }

    /// <summary>
    /// Snaps a vertex position to the defined grid size. Y is always set to 0.
    /// </summary>
    public Vector3 SnapVertexPosition(Vector3 vertexPos)
    {
        if (vertexSnapSize <= GeometryUtils.Epsilon) // No snapping if size is too small
            return new Vector3(vertexPos.x, 0f, vertexPos.z);

        return new Vector3(
            Mathf.Round(vertexPos.x / vertexSnapSize) * vertexSnapSize,
            0f, // Vertices are defined on the XZ plane
            Mathf.Round(vertexPos.z / vertexSnapSize) * vertexSnapSize
        );
    }

    // Called in editor when script values are changed
    void OnValidate()
    {
        SynchronizeSideData(); // Keep side data consistent with vertex count

        // Clamp some values to sensible ranges
        middleFloors = Mathf.Max(0, middleFloors);
        floorHeight = Mathf.Max(0.1f, floorHeight);
        nominalFacadeWidth = Mathf.Max(0.1f, nominalFacadeWidth);
        minSideLengthUnits = Mathf.Max(0, minSideLengthUnits); // 0 means use calculation, 1+ means enforce minimum

        mansardSlopeHorizontalDistance = Mathf.Max(0, mansardSlopeHorizontalDistance);
        mansardRiseHeight = Mathf.Max(0, mansardRiseHeight);
        atticSlopeHorizontalDistance = Mathf.Max(0, atticSlopeHorizontalDistance);
        atticRiseHeight = Mathf.Max(0, atticRiseHeight);
    }
}