using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(MeshFilter))]
public class PolygonBuildingGenerator : MonoBehaviour
{
    [Header("Polygon Definition")]
    public List<PolygonVertexData> vertexData = new List<PolygonVertexData>() {
        new() { position = new Vector3(0, 0, 0) },
        new() { position = new Vector3(0, 0, 5) },
        new() { position = new Vector3(5, 0, 5) },
        new() { position = new Vector3(5, 0, 0) }
    };
    public List<PolygonSideData> sideData = new List<PolygonSideData>();
    public float vertexSnapSize = 1.0f;
    public int minSideLengthUnits = 1;

    [Header("Building Settings")]
    public int middleFloors = 3;
    public float floorHeight = 3.0f;
    public bool allowHeightVariation = false;
    [Range(0, 5)] public int maxHeightVariation = 1;

    [Header("Facade Placement")]
    public bool scaleFacadesToFitSide = true;
    public float nominalFacadeWidth = 1.0f;

    [Header("Default Prefabs")]
    public List<GameObject> defaultGroundFloorPrefabs;
    public List<GameObject> defaultMiddleFloorPrefabs;
    public List<GameObject> defaultMansardFloorPrefabs;
    public List<GameObject> defaultAtticFloorPrefabs;

    [Header("Corner Elements")]
    public List<GameObject> cornerElementPrefabs;
    public List<GameObject> cornerCapPrefabs;
    public float cornerElementForwardOffset = 0.0f;

    [Header("Mansard/Attic Settings")]
    [Tooltip("Enable generating a procedural mesh for the Mansard level.")]
    public bool useMansardFloor = true; // Keep this if you want to toggle it
    public Material mansardMaterial;
    public float mansardSlopeHorizontalDistance = 1.5f;
    public float mansardRiseHeight = 2.0f;

    [Tooltip("Enable generating a procedural mesh for the Attic level (builds on Mansard or wall).")]
    public bool useAtticFloor = true; // Keep this too
    public Material atticMaterial;
    public float atticSlopeHorizontalDistance = 1.0f;
    public float atticRiseHeight = 1.5f;

    [Header("Roof Settings")]
    public bool generateSlopedRoof = true;
    public bool generateRoofTopCap = true;
    public float roofSlopeHorizontalDistance = 2.0f;
    public float roofRiseHeight = 1.5f;
    public float flatRoofEdgeOffset = 0.0f;
    public Material roofMaterial;
    public Material roofTopCapMaterial;
    public float roofUvScale = 1.0f;


    // --- Private Fields ---
    private GameObject generatedBuildingRoot;
    private const string ROOT_NAME = "Generated Building";
    private const string CORNERS_NAME = "Corner Elements";
    private const string ROOF_SLOPED_NAME = "Procedural Sloped Roof";
    private const string ROOF_FLAT_NAME = "Procedural Flat Roof";
    private const string ROOF_CAP_NAME = "Procedural Roof Cap";

#if UNITY_EDITOR
    // --- Make Debug Data Public for Editor Access ---
    [HideInInspector] public List<Vector3> _debugOuterRoofVertices;
    [HideInInspector] public List<Vector3> _debugInnerRoofVertices;
    [HideInInspector] public Mesh _debugSlopedRoofMesh;
    [HideInInspector] public Mesh _debugFlatRoofMesh;
    [HideInInspector] public Mesh _debugRoofCapMesh;
    [HideInInspector] public Transform _debugSlopedRoofTransform;
    [HideInInspector] public Transform _debugFlatRoofTransform;
    [HideInInspector] public Transform _debugRoofCapTransform;
#endif

    // --- Core Generation Logic ---
    public void GenerateBuilding()
    {
        ClearBuilding();
        SynchronizeSideData();
        if (vertexData.Count < 3) return;

        generatedBuildingRoot = new GameObject(ROOT_NAME);
        generatedBuildingRoot.transform.SetParent(this.transform, false);

        Vector3 polygonCenter = CalculatePolygonCenter();
        int[] sideMiddleFloors = CalculateSideHeights(); // Calculate heights once

        GenerateFacades(sideMiddleFloors, polygonCenter);
        GenerateCornerElements(sideMiddleFloors, polygonCenter); // Pass center if needed
        GenerateRoof(sideMiddleFloors, polygonCenter);         // Pass center if needed
    }

    public void ClearBuilding()
    {
        while (transform.Find(ROOT_NAME) != null)
        {
            Transform existingRoot = transform.Find(ROOT_NAME);
            if (Application.isEditor && !Application.isPlaying) DestroyImmediate(existingRoot.gameObject);
            else Destroy(existingRoot.gameObject);
        }
        generatedBuildingRoot = null;

#if UNITY_EDITOR
        // Clear debug data when clearing the building
        _debugOuterRoofVertices = null;
        _debugInnerRoofVertices = null;
        _debugSlopedRoofMesh = null;
        _debugFlatRoofMesh = null;
        _debugRoofCapMesh = null;
        _debugSlopedRoofTransform = null;
        _debugFlatRoofTransform = null;
        _debugRoofCapTransform = null;
#endif
    }


    // --- Sub-Generation Functions ---

    void GenerateFacades(int[] sideMiddleFloors, Vector3 polygonCenter)
    {
        for (int i = 0; i < vertexData.Count; i++) // Loop through sides
        {
            GameObject sideParent = new GameObject($"Side_{i}");
            sideParent.transform.SetParent(generatedBuildingRoot.transform, false);

            Vector3 p1 = vertexData[i].position;
            Vector3 p2 = vertexData[(i + 1) % vertexData.Count].position;
            Vector3 sideVector = p2 - p1;
            float sideDistance = sideVector.magnitude;
            if (sideDistance < 0.01f) continue;
            Vector3 sideDirection = sideVector.normalized;

            Vector3 sideNormal = CalculateSideNormal(p1, p2);
            int numSegments = CalculateNumSegments(sideDistance);
            float actualSegmentWidth = CalculateSegmentWidth(sideDistance, numSegments);
            int currentMiddleFloors = sideMiddleFloors[i];

            // Get prefab lists for this side
            GetSidePrefabLists(i, out var currentGround, out var currentMiddle, out var currentMansard, out var currentAttic);

            // Loop through horizontal segments on this side
            for (int j = 0; j < numSegments; j++)
            {
                // Calculate base position and rotation for this segment
                Vector3 segmentBasePos = p1 + sideDirection * (actualSegmentWidth * (j + 0.5f));
                Quaternion segmentRotation = Quaternion.LookRotation(sideNormal);

                // Build vertical stack for this segment
                float currentY = 0; // Reset Y for each NEW vertical stack (at segment j)

                // Ground Floor for this segment
                InstantiateFacadeSegment(currentGround, segmentBasePos, segmentRotation, sideParent.transform, actualSegmentWidth);
                currentY += floorHeight;

                // Middle Floors for this segment
                for (int floor = 0; floor < currentMiddleFloors; floor++)
                {
                    InstantiateFacadeSegment(currentMiddle, segmentBasePos + Vector3.up * currentY, segmentRotation, sideParent.transform, actualSegmentWidth);
                    currentY += floorHeight;
                }

/*                // Mansard Floor for this segment (if used)
                if (useMansardFloor)
                {
                    InstantiateFacadeSegment(currentMansard, segmentBasePos + Vector3.up * currentY, segmentRotation, sideParent.transform, actualSegmentWidth);
                    currentY += floorHeight;
                }

                // Attic Floor for this segment (if used)
                if (useAtticFloor)
                {
                    InstantiateFacadeSegment(currentAttic, segmentBasePos + Vector3.up * currentY, segmentRotation, sideParent.transform, actualSegmentWidth);
                    // No Y increment after the top floor
                }*/
            }
        }
    }

    void GenerateCornerElements(int[] sideMiddleFloors, Vector3 polygonCenter)
    {
        bool hasCornerPrefabs = cornerElementPrefabs != null && cornerElementPrefabs.Count > 0;
        bool hasCapPrefabs = cornerCapPrefabs != null && cornerCapPrefabs.Count > 0;
        if (!hasCornerPrefabs && !hasCapPrefabs) return;

        GameObject cornersParent = new GameObject(CORNERS_NAME);
        cornersParent.transform.SetParent(generatedBuildingRoot.transform, false);

        for (int i = 0; i < vertexData.Count; i++)
        {
            if (!vertexData[i].addCornerElement) continue;

            int prevI = (i + vertexData.Count - 1) % vertexData.Count;
            int nextI = (i + 1) % vertexData.Count;
            Vector3 currentPosRaw = vertexData[i].position;
            Vector3 prevPos = vertexData[prevI].position;
            Vector3 nextPos = vertexData[nextI].position;

            // Calculate corner rotation and offset position using helpers
            CalculateCornerTransform(prevPos, currentPosRaw, nextPos, polygonCenter, out Vector3 cornerBasePos, out Quaternion cornerRotation);

            // Determine Height
            int cornerMiddleFloors = Mathf.Max(sideMiddleFloors[prevI], sideMiddleFloors[i]);

            // Instantiate Stack
            float currentY = 0;
            float cornerWidth = nominalFacadeWidth;
            int regularSegmentsBeforeCap = CalculateRegularCornerSegments(cornerMiddleFloors, hasCapPrefabs);

            // Instantiate Ground, Middle, Mansard, Attic segments (if needed)
            if (hasCornerPrefabs && 0 < regularSegmentsBeforeCap) { InstantiateFacadeSegment(cornerElementPrefabs, cornerBasePos + Vector3.up * currentY, cornerRotation, cornersParent.transform, cornerWidth); }
            currentY += floorHeight;
            for (int floor = 0; floor < cornerMiddleFloors; floor++) { if (hasCornerPrefabs && 1 + floor < regularSegmentsBeforeCap) { InstantiateFacadeSegment(cornerElementPrefabs, cornerBasePos + Vector3.up * currentY, cornerRotation, cornersParent.transform, cornerWidth); } currentY += floorHeight; }
            if (useMansardFloor) { if (hasCornerPrefabs && 1 + cornerMiddleFloors < regularSegmentsBeforeCap) { InstantiateFacadeSegment(cornerElementPrefabs, cornerBasePos + Vector3.up * currentY, cornerRotation, cornersParent.transform, cornerWidth); } currentY += floorHeight; }
            if (useAtticFloor) { if (hasCornerPrefabs && 1 + cornerMiddleFloors + (useMansardFloor ? 1 : 0) < regularSegmentsBeforeCap) { InstantiateFacadeSegment(cornerElementPrefabs, cornerBasePos + Vector3.up * currentY, cornerRotation, cornersParent.transform, cornerWidth); } /* No Y increment */ }

            // Instantiate Cap
            if (hasCapPrefabs)
            {
                float capY = cornerBasePos.y + floorHeight * regularSegmentsBeforeCap;
                InstantiateFacadeSegment(cornerCapPrefabs, cornerBasePos + Vector3.up * capY, cornerRotation, cornersParent.transform, cornerWidth);
            }
        }
    }

    void GenerateRoof(int[] sideMiddleFloors, Vector3 polygonCenter) // Use the original sideMiddleFloors reflecting variation
    {
        if (vertexData.Count < 3) return;

        // --- Calculate the Initial Outer Perimeter at ACTUAL Wall Top Heights ---
        // Pass the *original* sideMiddleFloors array which accounts for height variation.
        // CalculateRoofPerimeterVertices uses CalculateCornerHeight internally for each vertex,
        // using the Max height of adjacent sides, resulting in a loop matching the wall tops.
        List<Vector3> currentOuterEdgeLoop = CalculateRoofPerimeterVertices(sideMiddleFloors, polygonCenter, 0.0f); // Offset 0

        if (currentOuterEdgeLoop == null || currentOuterEdgeLoop.Count < 3)
        {
            Debug.LogError("Failed to calculate initial roof base perimeter at actual heights.");
            return;
        }

        // The loop 'currentOuterEdgeLoop' now has vertices potentially at different Y levels,
        // matching the top of the walls below.

        // --- The rest of the function proceeds using this loop as the base ---

        List<Vector3> previousOuterEdgeLoop = new List<Vector3>(currentOuterEdgeLoop); // Keep track for debug info (optional)

        // --- Generate Mansard Floor Mesh (if enabled) ---
        List<Vector3> innerMansardEdgeLoop = null;
        if (useMansardFloor && mansardSlopeHorizontalDistance > GeometryUtils.Epsilon)
        {
            // CalculateInnerRoofEdge will correctly handle varying Y in currentOuterEdgeLoop
            innerMansardEdgeLoop = CalculateInnerRoofEdge(currentOuterEdgeLoop, mansardSlopeHorizontalDistance, mansardRiseHeight);

            if (innerMansardEdgeLoop != null && innerMansardEdgeLoop.Count >= 3)
            {
                // GenerateStripMeshData connects the varying height loops correctly
                GenerateStripMeshData(currentOuterEdgeLoop, innerMansardEdgeLoop, out var meshVertices, out var meshTriangles, out var meshUVs);
                Material mat = mansardMaterial ?? roofMaterial; // Fallback material
                CreateMeshObject(meshVertices, meshTriangles, meshUVs, mat, "MansardFloorMesh", "Procedural Mansard Floor", generatedBuildingRoot.transform);

                currentOuterEdgeLoop = innerMansardEdgeLoop; // Update the loop for the next stage
            }
            else
            {
                Debug.LogWarning("Failed to calculate inner mansard edge loop or not enough vertices, skipping mansard mesh generation.");
                useMansardFloor = false; // Prevent trying to use it if calc failed
            }
        }

        // --- Generate Attic Floor Mesh (if enabled) ---
        List<Vector3> innerAtticEdgeLoop = null;
        if (useAtticFloor && atticSlopeHorizontalDistance > GeometryUtils.Epsilon)
        {
            innerAtticEdgeLoop = CalculateInnerRoofEdge(currentOuterEdgeLoop, atticSlopeHorizontalDistance, atticRiseHeight);

            if (innerAtticEdgeLoop != null && innerAtticEdgeLoop.Count >= 3)
            {
                GenerateStripMeshData(currentOuterEdgeLoop, innerAtticEdgeLoop, out var meshVertices, out var meshTriangles, out var meshUVs);
                Material mat = atticMaterial ?? roofMaterial; // Fallback material
                CreateMeshObject(meshVertices, meshTriangles, meshUVs, mat, "AtticFloorMesh", "Procedural Attic Floor", generatedBuildingRoot.transform);

                currentOuterEdgeLoop = innerAtticEdgeLoop; // Update the loop for the final roof stage
            }
            else
            {
                Debug.LogWarning("Failed to calculate inner attic edge loop or not enough vertices, skipping attic mesh generation.");
                useAtticFloor = false; // Prevent trying to use it if calc failed
            }
        }

        // --- Generate Final Top Roof (Sloped or Flat) ---
        // Builds on the latest 'currentOuterEdgeLoop' which now sits correctly on the (potentially varied height) walls

        if (generateSlopedRoof)
        {
            List<Vector3> finalInnerLoop = CalculateInnerRoofEdge(currentOuterEdgeLoop, roofSlopeHorizontalDistance, roofRiseHeight);

            if (finalInnerLoop != null && finalInnerLoop.Count >= 3)
            {
                GenerateStripMeshData(currentOuterEdgeLoop, finalInnerLoop, out var meshVertices, out var meshTriangles, out var meshUVs);
                GameObject slopedRoofObj = CreateMeshObject(meshVertices, meshTriangles, meshUVs, roofMaterial, "SlopedRoofMesh", ROOF_SLOPED_NAME, generatedBuildingRoot.transform);
#if UNITY_EDITOR
                _debugOuterRoofVertices = new List<Vector3>(currentOuterEdgeLoop);
                _debugInnerRoofVertices = new List<Vector3>(finalInnerLoop);
                _debugSlopedRoofMesh = slopedRoofObj?.GetComponent<MeshFilter>()?.sharedMesh;
                _debugSlopedRoofTransform = slopedRoofObj?.transform;
                ClearFlatRoofDebug(); // Clear other types
                ClearRoofCapDebug();
#endif

                if (generateRoofTopCap && finalInnerLoop.Count >= 3)
                {
                    if (GeometryUtils.TriangulatePolygonEarClipping(finalInnerLoop, out var capTriangles))
                    {
                        List<Vector2> capUVs = CalculatePlanarUVs(finalInnerLoop);
                        GameObject capObj = CreateMeshObject(finalInnerLoop, capTriangles, capUVs, roofTopCapMaterial ?? roofMaterial, "RoofCapMesh", ROOF_CAP_NAME, generatedBuildingRoot.transform);
#if UNITY_EDITOR
                        _debugRoofCapMesh = capObj?.GetComponent<MeshFilter>()?.sharedMesh;
                        _debugRoofCapTransform = capObj?.transform;
#endif
                    }
                    else { Debug.LogError("Roof Cap triangulation failed."); ClearRoofCapDebug(); }
                }
                else { ClearRoofCapDebug(); }
            }
            else { Debug.LogWarning("Failed to calculate final inner roof loop for sloped roof."); ClearSlopedRoofDebug(); ClearRoofCapDebug(); }
        }
        else // Generate Flat Roof
        {
            List<Vector3> flatRoofVertices = currentOuterEdgeLoop; // Use the final edge loop which sits on walls

            // Apply flatRoofEdgeOffset *relative* to this potentially non-planar loop if desired.
            // For simplicity, we'll triangulate the loop as is for now.
            // If offset is needed:
            // flatRoofVertices = CalculateInnerRoofEdge(currentOuterEdgeLoop, -flatRoofEdgeOffset, 0) ?? currentOuterEdgeLoop;


            if (GeometryUtils.TriangulatePolygonEarClipping(flatRoofVertices, out var meshTriangles))
            {
                List<Vector2> meshUVs = CalculatePlanarUVs(flatRoofVertices);
                GameObject flatRoofObj = CreateMeshObject(flatRoofVertices, meshTriangles, meshUVs, roofMaterial, "FlatRoofMesh", ROOF_FLAT_NAME, generatedBuildingRoot.transform);
#if UNITY_EDITOR
                _debugFlatRoofMesh = flatRoofObj?.GetComponent<MeshFilter>()?.sharedMesh;
                _debugFlatRoofTransform = flatRoofObj?.transform;
                ClearSlopedRoofDebug(); // Clear other roof type debug info
                ClearRoofCapDebug();
#endif
            }
            else { Debug.LogError("Flat Roof triangulation failed."); ClearFlatRoofDebug(); }
        }
    }

#if UNITY_EDITOR
    // Helper methods to clear debug info
    void ClearSlopedRoofDebug() { _debugOuterRoofVertices = null; _debugInnerRoofVertices = null; _debugSlopedRoofMesh = null; _debugSlopedRoofTransform = null; }
    void ClearFlatRoofDebug() { _debugFlatRoofMesh = null; _debugFlatRoofTransform = null; }
    void ClearRoofCapDebug() { _debugRoofCapMesh = null; _debugRoofCapTransform = null; }
#endif

    // --- Roof Generation Helpers ---

    void GenerateRoofMesh_Flat(int[] sideMiddleFloors, Vector3 polygonCenter)
    {
        List<Vector3> roofVertices = CalculateRoofPerimeterVertices(sideMiddleFloors, polygonCenter, flatRoofEdgeOffset);
        if (roofVertices == null || roofVertices.Count < 3)
        {
            Debug.LogWarning("Cannot generate flat roof: Less than 3 perimeter vertices calculated.");
            return;
        }

        //GenerateFanMeshData(roofVertices, out List<Vector3> meshVertices, out List<int> meshTriangles, out List<Vector2> meshUVs);
        //GameObject roofObject = CreateMeshObject(meshVertices, meshTriangles, meshUVs, roofMaterial, "FlatRoofMesh", ROOF_FLAT_NAME, generatedBuildingRoot.transform);

        // --- Triangulate using Ear Clipping ---
        List<int> meshTriangles;
        // Call the triangulation function (assuming it's in GeometryUtils)
        if (!GeometryUtils.TriangulatePolygonEarClipping(roofVertices, out meshTriangles))
        {
            Debug.LogError("Flat Roof triangulation failed. Aborting roof generation.");
            // Clear debug data related to flat roof if needed
#if UNITY_EDITOR
            _debugFlatRoofMesh = null;
            _debugFlatRoofTransform = null;
#endif
            return; // Stop generation if triangulation failed
        }

        // Vertices are just the perimeter vertices themselves
        List<Vector3> meshVertices = roofVertices;

        // Calculate UVs (Simple Planar Projection - Same as before)
        List<Vector2> meshUVs = new List<Vector2>(meshVertices.Count);
        foreach (Vector3 v in meshVertices)
        {
            meshUVs.Add(new Vector2(v.x * roofUvScale, v.z * roofUvScale));
        }

        // Create GameObject and store mesh/transform for Gizmos
        GameObject roofObject = CreateMeshObject(meshVertices, meshTriangles, meshUVs, roofMaterial, "FlatRoofMesh", ROOF_FLAT_NAME, generatedBuildingRoot.transform);


#if UNITY_EDITOR
        if (roofObject != null) // Check if object creation succeeded
        {
            _debugFlatRoofMesh = roofObject.GetComponent<MeshFilter>()?.sharedMesh; // Use sharedMesh in editor
            _debugFlatRoofTransform = roofObject.transform;
        }
        else
        {
            _debugFlatRoofMesh = null;
            _debugFlatRoofTransform = null;
        }
        _debugOuterRoofVertices = null;
        _debugInnerRoofVertices = null;
        _debugSlopedRoofMesh = null;
        _debugRoofCapMesh = null;
        _debugSlopedRoofTransform = null;
        _debugRoofCapTransform = null;
#endif
    }

    void GenerateRoofMesh_Sloped(int[] sideMiddleFloors, Vector3 polygonCenter)
    {
        if (vertexData.Count < 3 || roofSlopeHorizontalDistance <= 0.01f) return;
        CalculateSlopedRoofEdges(sideMiddleFloors, polygonCenter, out List<Vector3> outerVertices, out List<Vector3> innerVertices);
        if (outerVertices == null || innerVertices == null || outerVertices.Count < 3) return;

#if UNITY_EDITOR
        // Store vertices for Gizmos *before* generating mesh
        _debugOuterRoofVertices = new List<Vector3>(outerVertices);
        _debugInnerRoofVertices = new List<Vector3>(innerVertices);
        // Clear flat roof debug info
        _debugFlatRoofMesh = null;
        _debugFlatRoofTransform = null;
        _debugRoofCapMesh = null;
        _debugRoofCapTransform = null;
#endif

        GenerateStripMeshData(outerVertices, innerVertices, out List<Vector3> meshVertices, out List<int> meshTriangles, out List<Vector2> meshUVs);
        GameObject slopedRoofObject = CreateMeshObject(meshVertices, meshTriangles, meshUVs, roofMaterial, "SlopedRoofMesh", ROOF_SLOPED_NAME, generatedBuildingRoot.transform);

#if UNITY_EDITOR
        if (slopedRoofObject != null)
        {
            _debugSlopedRoofMesh = slopedRoofObject.GetComponent<MeshFilter>()?.sharedMesh;
            _debugSlopedRoofTransform = slopedRoofObject.transform;
        }
        else
        {
            _debugSlopedRoofMesh = null;
            _debugSlopedRoofTransform = null;
        }
#endif

        // Generate the top cap using Ear Clipping
        if (generateRoofTopCap && innerVertices.Count >= 3)
        {
            // --- Triangulate the inner loop using Ear Clipping ---
            List<int> capTriangles;
            // Use the *inner* vertices for the cap
            if (!GeometryUtils.TriangulatePolygonEarClipping(innerVertices, out capTriangles))
            {
                Debug.LogError("Roof Cap triangulation failed.");
#if UNITY_EDITOR
                _debugRoofCapMesh = null;
                _debugRoofCapTransform = null;
#endif
                // Optionally decide whether to stop or just skip the cap
            }
            else
            {
                // Vertices are the inner vertices
                List<Vector3> capVertices = innerVertices;

                // Calculate UVs for the cap (Planar projection)
                List<Vector2> capUVs = new List<Vector2>(capVertices.Count);
                // Optional: Project UVs relative to the cap's center for better distribution
                Vector3 capCenter = Vector3.zero;
                foreach (var v in capVertices) capCenter += v;
                capCenter /= capVertices.Count;

                foreach (Vector3 v in capVertices)
                {
                    // UVs relative to center can look better than absolute world coords
                    capUVs.Add(new Vector2((v.x - capCenter.x) * roofUvScale, (v.z - capCenter.z) * roofUvScale) + new Vector2(0.5f, 0.5f));
                    // Or stick to world projection:
                    // capUVs.Add(new Vector2(v.x * roofUvScale, v.z * roofUvScale));
                }

                // Create the cap object
                GameObject capObject = CreateMeshObject(capVertices, capTriangles, capUVs, roofTopCapMaterial ?? roofMaterial, "RoofCapMesh", ROOF_CAP_NAME, generatedBuildingRoot.transform);

#if UNITY_EDITOR
                if (capObject != null)
                {
                    _debugRoofCapMesh = capObject.GetComponent<MeshFilter>()?.sharedMesh;
                    _debugRoofCapTransform = capObject.transform;
                }
                else
                {
                    _debugRoofCapMesh = null;
                    _debugRoofCapTransform = null;
                }
#endif
            }
        }
#if UNITY_EDITOR
        else if (!generateRoofTopCap) // Ensure cap debug info is cleared if cap isn't generated
        {
            _debugRoofCapMesh = null;
            _debugRoofCapTransform = null;
        }
#endif
    }

    // Add this function inside the PolygonBuildingGenerator class

    /// <summary>
    /// Calculates the vertices of an inner loop offset horizontally and vertically from an outer loop.
    /// Uses the base vertexData for stable normal/direction calculation.
    /// </summary>
    /// <param name="outerLoop">The list of vertices defining the outer edge loop.</param>
    /// <param name="horizontalDistance">How far to offset inwards horizontally.</param>
    /// <param name="riseHeight">How far to offset upwards vertically.</param>
    /// <returns>A new list of vertices for the inner loop, or null if calculation fails.</returns>
    List<Vector3> CalculateInnerRoofEdge(List<Vector3> outerLoop, float horizontalDistance, float riseHeight)
    {
        // Basic validation
        if (outerLoop == null || outerLoop.Count < 3)
        {
            Debug.LogError("CalculateInnerRoofEdge: Outer loop is null or has less than 3 vertices.");
            return null;
        }
        if (horizontalDistance <= GeometryUtils.Epsilon && riseHeight <= GeometryUtils.Epsilon)
        {
            return new List<Vector3>(outerLoop); // No offset, return a copy of the outer loop
        }
        if (horizontalDistance < -GeometryUtils.Epsilon)
        {
            // Handle outward offset if needed, or treat as error/clamp?
            // For inward offset calculation, negative distance is problematic.
            Debug.LogWarning("CalculateInnerRoofEdge: Negative horizontalDistance detected, clamping to 0 for inward offset calculation.");
            horizontalDistance = 0;
        }


        // Ensure vertexData matches the outer loop count for reliable indexing
        if (vertexData.Count != outerLoop.Count)
        {
            Debug.LogError($"CalculateInnerRoofEdge: VertexData count ({vertexData.Count}) mismatch with outerLoop count ({outerLoop.Count}). Cannot reliably calculate normals/intersections.");
            return null;
        }

        List<Vector3> innerVertices = new List<Vector3>(outerLoop.Count);
        int n = outerLoop.Count;

        for (int i = 0; i < n; i++)
        {
            // Indices for base polygon vertices around the current corner 'i'
            int prevBaseI = (i + n - 1) % n;
            int currBaseI = i;
            int nextBaseI = (i + 1) % n;

            Vector3 p1_base = vertexData[prevBaseI].position;
            Vector3 p2_base = vertexData[currBaseI].position;
            Vector3 p3_base = vertexData[nextBaseI].position;

            // Get the corresponding vertex from the provided outerLoop (has the correct current Y)
            Vector3 p2_outer = outerLoop[currBaseI];

            // Calculate side directions and OUTWARD normals based on the base polygon
            Vector3 sideDirPrev = (p2_base - p1_base).normalized;
            Vector3 sideDirNext = (p3_base - p2_base).normalized;
            Vector3 normalPrev = CalculateSideNormal(p1_base, p2_base); // Your reliable normal func
            Vector3 normalNext = CalculateSideNormal(p2_base, p3_base); // Your reliable normal func

            Vector3 innerVertexPosXZ;

            // Define the offset lines *inwards* from the base polygon outline
            // We subtract because the normal points outwards
            Vector3 innerLineOriginPrev = p1_base - normalPrev * horizontalDistance;
            Vector3 innerLineOriginNext = p2_base - normalNext * horizontalDistance;

            // Find the intersection of these two INWARD offset lines
            if (GeometryUtils.LineLineIntersection(innerLineOriginPrev, sideDirPrev, innerLineOriginNext, sideDirNext, out innerVertexPosXZ))
            {
                // Intersection found - this is the XZ position of the inner vertex
            }
            else // Fallback for parallel or failed intersection
            {
                Debug.LogWarning($"Parallel lines or intersection failed calculating inner loop at index {i} (Base vertex: {p2_base}). Using fallback offset.");
                // Fallback: Offset the *outer* point inwards along the average normal
                Vector3 avgNormal = (normalPrev + normalNext).normalized;
                // Handle 180 degree straight line case where normals cancel out
                if (avgNormal.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon)
                {
                    avgNormal = Quaternion.Euler(0, 90, 0) * sideDirPrev; // Rotate one direction 90 deg
                }
                // Use the XZ of the outer point as the base for the fallback offset
                innerVertexPosXZ = new Vector3(p2_outer.x, 0, p2_outer.z) - avgNormal * horizontalDistance;
            }

            // Calculate final Y position: Start from the outer loop's Y and add the rise
            float innerCornerY = p2_outer.y + riseHeight;
            innerVertices.Add(new Vector3(innerVertexPosXZ.x, innerCornerY, innerVertexPosXZ.z));
        }

        return innerVertices;
    }

    // Add this function inside the PolygonBuildingGenerator class

    /// <summary>
    /// Calculates simple planar UVs (XZ projection) for a list of vertices.
    /// </summary>
    List<Vector2> CalculatePlanarUVs(List<Vector3> vertices)
    {
        List<Vector2> uvs = new List<Vector2>();
        if (vertices == null || vertices.Count == 0) return uvs;

        // Option 1: Absolute World Projection
        foreach (Vector3 v in vertices)
        {
            uvs.Add(new Vector2(v.x * roofUvScale, v.z * roofUvScale));
        }

        // Option 2: Relative to Center (Uncomment to use)
        /*
        Vector3 center = Vector3.zero;
        foreach(var v in vertices) center += v;
        if (vertices.Count > 0) center /= vertices.Count;
        foreach(Vector3 v in vertices)
        {
            uvs.Add(new Vector2((v.x - center.x) * roofUvScale, (v.z - center.z) * roofUvScale) + new Vector2(0.5f, 0.5f));
        }
        */
        return uvs;
    }

    // --- Mesh Data Generation ---

    // Calculates vertices for a flat roof perimeter or sloped roof outer edge
    List<Vector3> CalculateRoofPerimeterVertices(int[] sideMiddleFloors, Vector3 polygonCenter, float edgeOffset)
    {
        List<Vector3> vertices = new List<Vector3>();
        if (vertexData.Count < 3) return vertices; // Need at least 3 vertices

        for (int i = 0; i < vertexData.Count; i++)
        {
            int prevI = (i + vertexData.Count - 1) % vertexData.Count;
            Vector3 p1 = vertexData[prevI].position;
            Vector3 p2 = vertexData[i].position; // The current corner vertex
            Vector3 p3 = vertexData[(i + 1) % vertexData.Count].position;

            // Calculate side directions and normals
            Vector3 sideDirPrev = (p2 - p1).normalized;
            Vector3 sideDirNext = (p3 - p2).normalized;
            // Ensure normals point outwards consistently using your existing method
            Vector3 normalPrev = CalculateSideNormal(p1, p2);
            Vector3 normalNext = CalculateSideNormal(p2, p3);

            Vector3 vertexPosXZ;

            // --- Consistent Offset Logic using Line Intersection ---
            if (Mathf.Abs(edgeOffset) > 0.01f) // If there IS an offset (positive or negative)
            {
                // Define the two lines, offset by edgeOffset along their normals
                Vector3 lineOriginPrev = p1 + normalPrev * edgeOffset;
                Vector3 lineOriginNext = p2 + normalNext * edgeOffset;

                // Find the intersection point of these two offset lines
                if (GeometryUtils.LineLineIntersection(lineOriginPrev, sideDirPrev, lineOriginNext, sideDirNext, out vertexPosXZ))
                {
                    // Intersection found, use it.
                }
                else // Fallback for parallel or nearly parallel lines
                {
                    Vector3 avgNormal = (normalPrev + normalNext).normalized;
                    if (avgNormal.sqrMagnitude < GeometryUtils.Epsilon * GeometryUtils.Epsilon) avgNormal = Quaternion.Euler(0, 90, 0) * sideDirPrev; // Handle 180 degree turn
                    vertexPosXZ = p2 + avgNormal * edgeOffset;
                }
            }
            else // No offset (edgeOffset is effectively zero)
            {
                vertexPosXZ = p2; // Use the original vertex XZ position
            }

            // Calculate height (remains the same)
            int cornerMiddleFloors = Mathf.Max(sideMiddleFloors[prevI], sideMiddleFloors[i]);
            float cornerY = CalculateCornerHeight(cornerMiddleFloors); // Make sure this calculates the top of the wall correctly

            vertices.Add(new Vector3(vertexPosXZ.x, cornerY, vertexPosXZ.z));
        }
        return vertices;
    }

    // Calculates both outer and inner edge vertices for sloped roof
    // In CalculateSlopedRoofEdges function

    void CalculateSlopedRoofEdges(int[] sideMiddleFloors, Vector3 polygonCenter,
                                   out List<Vector3> outerVertices, out List<Vector3> innerVertices)
    {
        outerVertices = new List<Vector3>();
        innerVertices = new List<Vector3>();

        for (int i = 0; i < vertexData.Count; i++)
        {
            int prevI = (i + vertexData.Count - 1) % vertexData.Count;
            Vector3 p1_base = vertexData[prevI].position;
            Vector3 p2_base = vertexData[i].position;
            Vector3 p3_base = vertexData[(i + 1) % vertexData.Count].position;

            // Outer Vertex (No horizontal offset, calculated height)
            int outerCornerMiddleFloors = Mathf.Max(sideMiddleFloors[prevI], sideMiddleFloors[i]);
            float outerCornerY = CalculateCornerHeight(outerCornerMiddleFloors);
            outerVertices.Add(new Vector3(p2_base.x, outerCornerY, p2_base.z));

            // Inner Vertex (Horizontal offset, calculated height + rise)
            Vector3 sideDirPrev = (p2_base - p1_base).normalized;
            Vector3 sideDirNext = (p3_base - p2_base).normalized;
            Vector3 normalPrev = CalculateSideNormal(p1_base, p2_base);
            Vector3 normalNext = CalculateSideNormal(p2_base, p3_base);

            Vector3 innerVertexPosXZ;
            // *** FIX: Subtract offset distance to move inwards ***
            Vector3 innerLineOriginPrev = p1_base - normalPrev * roofSlopeHorizontalDistance; // Subtracted
            Vector3 innerLineOriginNext = p2_base - normalNext * roofSlopeHorizontalDistance; // Subtracted
            // Find intersection of INWARD offset lines
            if (!GeometryUtils.LineLineIntersection(innerLineOriginPrev, sideDirPrev, innerLineOriginNext, sideDirNext, out innerVertexPosXZ))
            {
                // Fallback for parallel lines: Offset inwards
                Vector3 avgNormal = (normalPrev + normalNext).normalized;
                if (avgNormal == Vector3.zero) avgNormal = normalPrev;
                innerVertexPosXZ = p2_base - avgNormal * roofSlopeHorizontalDistance; // Offset inwards
            }

            float innerCornerY = outerCornerY + roofRiseHeight;
            innerVertices.Add(new Vector3(innerVertexPosXZ.x, innerCornerY, innerVertexPosXZ.z));
        }
    }

    // Generates mesh data using center fan triangulation
    void GenerateFanMeshData(List<Vector3> perimeterVertices,
                              out List<Vector3> meshVertices, out List<int> meshTriangles, out List<Vector2> meshUVs,
                              bool forceFlatY = false)
    {
        meshVertices = new List<Vector3>();
        meshTriangles = new List<int>();
        meshUVs = new List<Vector2>();

        float finalY = perimeterVertices.Min(v => v.y); // Use Min Y if forcing flat, otherwise highest Y
        Vector3 center = Vector3.zero;
        foreach (var p in perimeterVertices) center += p;
        center /= perimeterVertices.Count;
        center.y = forceFlatY ? finalY : perimeterVertices.Max(v => v.y); // Choose Y based on flag

        meshVertices.Add(center);
        meshUVs.Add(new Vector2(center.x / roofUvScale, center.z / roofUvScale));

        foreach (Vector3 p in perimeterVertices)
        {
            Vector3 vertexToAdd = forceFlatY ? new Vector3(p.x, finalY, p.z) : p;
            meshVertices.Add(vertexToAdd);
            meshUVs.Add(new Vector2(p.x / roofUvScale, p.z / roofUvScale));
        }

        int centerIndex = 0;
        for (int i = 0; i < perimeterVertices.Count; i++)
        {
            int indexA = i + 1;
            int indexB = (i + 1) % perimeterVertices.Count + 1;
            // Correct Winding (Counter-Clockwise):
            meshTriangles.Add(centerIndex);
            meshTriangles.Add(indexA);
            meshTriangles.Add(indexB);
        }
    }

    // Generates mesh data for the strips connecting outer and inner roof loops
    void GenerateStripMeshData(List<Vector3> outerVertices, List<Vector3> innerVertices,
                                out List<Vector3> meshVertices, out List<int> meshTriangles, out List<Vector2> meshUVs)
    {
        meshVertices = new List<Vector3>();
        meshTriangles = new List<int>();
        meshUVs = new List<Vector2>();

        meshVertices.AddRange(outerVertices);
        meshVertices.AddRange(innerVertices);

        // Simple planar UVs for now
        foreach (Vector3 v in meshVertices)
        {
            meshUVs.Add(new Vector2(v.x * roofUvScale, v.z * roofUvScale)); // Project onto XZ plane
        }

        int N = outerVertices.Count;
        for (int i = 0; i < N; i++)
        {
            int currentOuter = i;
            int nextOuter = (i + 1) % N;
            int currentInner = i + N;
            int nextInner = (i + 1) % N + N;

            // Correct Winding for Triangle 1: currentOuter, nextInner, nextOuter

            meshTriangles.Add(currentOuter); meshTriangles.Add(nextOuter); meshTriangles.Add(nextInner);

            // Correct Winding for Triangle 2: currentOuter, currentInner, nextInner

            meshTriangles.Add(currentOuter); meshTriangles.Add(nextInner); meshTriangles.Add(currentInner);
        }
    }

    // Creates the actual mesh and GameObject
    GameObject CreateMeshObject(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, Material material, string meshName, string objectName, Transform parent)
    {
        Mesh mesh = new Mesh { name = meshName };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject meshObject = new GameObject(objectName);
        meshObject.transform.SetParent(parent, false);
        MeshFilter mf = meshObject.AddComponent<MeshFilter>();
        mf.mesh = mesh;
        MeshRenderer mr = meshObject.AddComponent<MeshRenderer>();
        mr.material = material;

        // Add this line
        return meshObject;
    }


    // --- Corner Calculation Helpers ---

    void CalculateCornerTransform(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 polygonCenter, out Vector3 cornerPos, out Quaternion cornerRot)
    {
        Vector3 sideNormalPrev = CalculateSideNormal(p1, p2);
        Vector3 sideNormalNext = CalculateSideNormal(p2, p3);

        Vector3 avgNormal = (sideNormalPrev + sideNormalNext).normalized;
        if (avgNormal == Vector3.zero) avgNormal = sideNormalNext; // Fallback

        cornerRot = Quaternion.LookRotation(avgNormal);

        Vector3 localOffset = Vector3.forward * cornerElementForwardOffset;
        Vector3 worldOffset = cornerRot * localOffset;
        cornerPos = p2 + worldOffset;
    }

    int CalculateRegularCornerSegments(int cornerMiddleFloors, bool placeCap)
    {
        int segments = 1 + cornerMiddleFloors;
        if (useMansardFloor) segments++;
        if (useAtticFloor) segments++;
        if (placeCap) segments = Mathf.Max(0, segments - 1);
        return segments;
    }


    // --- Height and Prefab Helpers ---

    int[] CalculateSideHeights()
    {
        int[] sideMiddleFloors = new int[vertexData.Count];
        for (int i = 0; i < vertexData.Count; i++)
        {
            int height = middleFloors; // Start with default
            if (allowHeightVariation)
            {
                height = Mathf.Max(0, middleFloors + Random.Range(-maxHeightVariation, maxHeightVariation + 1));
            }
            sideMiddleFloors[i] = height;
        }
        return sideMiddleFloors;
    }

    float CalculateCornerHeight(int middleFloorsCount) // Helper moved here
    {
        float cornerY = 0; // Start at base
        cornerY += floorHeight; // Add Ground floor height
        cornerY += Mathf.Max(0, middleFloorsCount) * floorHeight;
        //if (useMansardFloor) cornerY += floorHeight;
        //if (useAtticFloor) cornerY += floorHeight;
        return cornerY;
    }

    void GetSidePrefabLists(int sideIndex, out List<GameObject> ground, out List<GameObject> middle, out List<GameObject> mansard, out List<GameObject> attic)
    {
        PolygonSideData currentSideSettings = sideData[sideIndex];
        if (currentSideSettings.useCustomStyle && currentSideSettings.sideStylePreset != null)
        {
            SideStyleSO style = currentSideSettings.sideStylePreset;
            ground = style.groundFloorPrefabs ?? defaultGroundFloorPrefabs;
            middle = style.middleFloorPrefabs ?? defaultMiddleFloorPrefabs;
            mansard = style.mansardFloorPrefabs ?? defaultMansardFloorPrefabs;
            attic = style.atticFloorPrefabs ?? defaultAtticFloorPrefabs;
        }
        else
        {
            ground = defaultGroundFloorPrefabs;
            middle = defaultMiddleFloorPrefabs;
            mansard = defaultMansardFloorPrefabs;
            attic = defaultAtticFloorPrefabs;
        }
    }


    // --- Instantiation and Utility Helpers ---

    void InstantiateFacadeSegment(List<GameObject> prefabList, Vector3 worldPosition, Quaternion worldRotation, Transform parent, float segmentWidth) // Renamed parameters for clarity
    {
        if (prefabList == null || prefabList.Count == 0) return;
        int randomIndex = Random.Range(0, prefabList.Count);
        GameObject prefab = prefabList[randomIndex];
        if (prefab == null) return;

        // Instantiate at world pos/rot initially for correct prefab orientation relative to world
        GameObject instance = Instantiate(prefab, worldPosition, worldRotation, parent);

        // Apply scaling if needed
        if (scaleFacadesToFitSide && Mathf.Abs(segmentWidth - nominalFacadeWidth) > 0.01f)
        {
            Vector3 localScale = instance.transform.localScale; // Get current local scale
            float scaleFactor = segmentWidth / nominalFacadeWidth;
            // Scale relative to the nominal width, applying to instance's current X scale
            instance.transform.localScale = new Vector3(localScale.x * scaleFactor, localScale.y, localScale.z);
        }
        // Optionally, you might want to reset local position/rotation after parenting if the prefab pivot isn't centered
        // instance.transform.localPosition = parent.InverseTransformPoint(worldPosition);
        // instance.transform.localRotation = Quaternion.Inverse(parent.rotation) * worldRotation;
    }

    // Overload for corner elements where position/rotation is already calculated correctly relative to parent
    void InstantiateFacadeSegment(List<GameObject> prefabList, Vector3 localPosition, Quaternion localRotation, Transform parent, float segmentWidth, bool isCorner = true)
    {
        if (!isCorner)
        { // Fallback to the other method if not explicitly a corner
            InstantiateFacadeSegment(prefabList, parent.TransformPoint(localPosition), parent.rotation * localRotation, parent, segmentWidth);
            return;
        }

        if (prefabList == null || prefabList.Count == 0) return;
        int randomIndex = Random.Range(0, prefabList.Count);
        GameObject prefab = prefabList[randomIndex];
        if (prefab == null) return;

        GameObject instance = Instantiate(prefab, parent);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = localRotation;
        // No scaling typically applied to corners
    }

    Vector3 CalculatePolygonCenter()
    {
        Vector3 center = Vector3.zero;
        if (vertexData.Count > 0)
        {
            foreach (var vd in vertexData) center += vd.position;
            center /= vertexData.Count;
        }
        return center;
    }

    float CalculateSignedArea()
    {
        if (vertexData == null || vertexData.Count < 3) return 0f;

        float area = 0f;
        for (int i = 0; i < vertexData.Count; i++)
        {
            Vector3 p1 = vertexData[i].position;
            Vector3 p2 = vertexData[(i + 1) % vertexData.Count].position; // Wrap around
            // Shoelace formula component for XZ plane
            area += (p1.x * p2.z) - (p2.x * p1.z);
        }
        return area / 2.0f;
    }

    int CalculateNumSegments(float sideDistance)
    {
        int num = Mathf.Max(minSideLengthUnits, Mathf.RoundToInt(sideDistance / nominalFacadeWidth));
        if (!scaleFacadesToFitSide)
        {
            num = Mathf.Max(minSideLengthUnits, Mathf.FloorToInt(sideDistance / nominalFacadeWidth));
            if (num == 0 && minSideLengthUnits > 0) num = minSideLengthUnits;
        }
        return num;
    }

    float CalculateSegmentWidth(float sideDistance, int numSegments)
    {
        return scaleFacadesToFitSide ? (sideDistance / numSegments) : nominalFacadeWidth;
    }

    /*    Vector3 CalculateSideNormal(Vector3 p1, Vector3 p2, Vector3 polygonCenter)
        {
            Vector3 sideDirection = (p2 - p1).normalized;
            Vector3 sideNormal = Vector3.Cross(sideDirection, Vector3.up).normalized;
            Vector3 sideMidpoint = p1 + sideDirection * Vector3.Distance(p1, p2) / 2f;
            Vector3 centerToMidpoint = sideMidpoint - polygonCenter;
            centerToMidpoint.y = 0;
            Vector3 checkNormal = sideNormal; checkNormal.y = 0;
            if (Vector3.Dot(checkNormal.normalized, centerToMidpoint.normalized) < -0.01f)
            {
                sideNormal *= -1;
            }
            return sideNormal;
        }*/

    Vector3 CalculateSideNormal(Vector3 p1, Vector3 p2) // Removed polygonCenter parameter
    {
        Vector3 sideDirection = (p2 - p1).normalized;
        if (sideDirection == Vector3.zero) return Vector3.forward;

        // Normal pointing "right" of direction on XZ plane
        Vector3 initialNormal = Vector3.Cross(sideDirection, Vector3.up).normalized;
        float signedArea = CalculateSignedArea();

        // If CCW (positive area), outward is "left", so flip the initial "right" normal.
        if (signedArea > Mathf.Epsilon)
        {
            return -initialNormal;
        }
        // If CW (negative area), outward is "right", use initial normal.
        // Also handles zero area case by falling back to initial normal.
        else
        {
            if (Mathf.Abs(signedArea) < Mathf.Epsilon)
            {
                Debug.LogWarning("Polygon area is close to zero, normal calculation might be unreliable.");
            }
            return initialNormal;
        }
    }

    // Ensure sideData list count matches vertexData list count
    // Call this from OnValidate or the editor script to keep things synced
    public void SynchronizeSideData()
    {
        int requiredCount = vertexData.Count;
        // Add missing entries
        while (sideData.Count < requiredCount)
        {
            sideData.Add(new PolygonSideData());
        }
        // Remove excess entries
        while (sideData.Count > requiredCount && sideData.Count > 0)
        {
            sideData.RemoveAt(sideData.Count - 1);
        }
    }

    // Called when script values are changed in the inspector (Editor only)
    void OnValidate()
    {
        // Ensure sideData count matches vertexData count when edited in inspector
        if (vertexData == null) vertexData = new List<PolygonVertexData>();
        if (sideData == null) sideData = new List<PolygonSideData>();
        SynchronizeSideData();
    }

    // Modify SnapVertex to work with PolygonVertexData
    public Vector3 SnapVertexPosition(Vector3 vertexPos)
    {
        return new Vector3(
            Mathf.Round(vertexPos.x / vertexSnapSize) * vertexSnapSize,
            0f,
            Mathf.Round(vertexPos.z / vertexSnapSize) * vertexSnapSize
        );
    }

}