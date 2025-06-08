// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
// This script manages and provides methods to customize a generated building instance.
//

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages a generated building instance, holding references to its parts and providing
/// methods to customize its appearance at runtime or in the editor.
/// </summary>
public class BuildingInstanceDataManager : MonoBehaviour
{
    [Tooltip("A reference to the generator that created this building.")]
    public PolygonBuildingGenerator sourceGenerator;

    [Tooltip("A data container with references to all generated building elements.")]
    public GeneratedBuildingElements elements = new GeneratedBuildingElements();

    /// <summary>
    /// Initializes the data manager with references from the generation process.
    /// </summary>
    /// <param name="generator">The PolygonBuildingGenerator that created this instance.</param>
    /// <param name="root">The root GameObject of the generated building model.</param>
    /// <param name="generatedElements">The data object containing all element references.</param>
    public void Initialize(PolygonBuildingGenerator generator, GameObject root, GeneratedBuildingElements generatedElements)
    {
        this.sourceGenerator = generator;
        this.elements = generatedElements; // Assign the complete data structure passed from the generator.
        this.elements.buildingRoot = root; // Ensure the root reference is set within the assigned data.
    }

    // --- Customization Methods ---

    /// <summary>
    /// Applies a specific visual style to a single side of the building by replacing its facade segments.
    /// </summary>
    /// <param name="sideIndex">The index of the building side to modify.</param>
    /// <param name="newStyle">The style to apply.</param>
    public void ApplySideStyle(int sideIndex, SideStyleSO newStyle)
    {
        if (newStyle == null || sourceGenerator?.buildingStyle == null) return;
        if (sideIndex < 0 || sideIndex >= elements.facadeElementsPerSide.Count) return;

        SideElementGroup sideGroup = elements.facadeElementsPerSide[sideIndex];

        // Determine which prefabs to use, falling back to the building's default style if the new style is incomplete.
        var groundPrefabs = GetPrefabsForStyle(newStyle.groundFloorPrefabs, sourceGenerator.buildingStyle.defaultGroundFloorPrefabs);
        var middlePrefabs = GetPrefabsForStyle(newStyle.middleFloorPrefabs, sourceGenerator.buildingStyle.defaultMiddleFloorPrefabs);

        // Replace the existing segments with new ones from the selected prefabs.
        ReplaceFacadeSegments(sideGroup.groundFacadeSegments, groundPrefabs);
        ReplaceFacadeSegments(sideGroup.middleFacadeSegments, middlePrefabs);
    }

    /// <summary>
    /// Helper to select the primary prefab list, falling back to a default if the primary is empty.
    /// </summary>
    private List<GameObject> GetPrefabsForStyle(List<GameObject> primaryPrefabs, List<GameObject> fallbackPrefabs)
    {
        return (primaryPrefabs != null && primaryPrefabs.Count > 0) ? primaryPrefabs : fallbackPrefabs;
    }

    /// <summary>
    /// Replaces a list of existing GameObjects with new instances from a list of prefabs.
    /// </summary>
    private void ReplaceFacadeSegments(List<GameObject> existingSegments, List<GameObject> prefabsToUse)
    {
        if (prefabsToUse == null || prefabsToUse.Count == 0) return;

        for (int i = 0; i < existingSegments.Count; i++)
        {
            GameObject oldSegment = existingSegments[i];
            if (oldSegment == null) continue;

            GameObject newPrefab = prefabsToUse[Random.Range(0, prefabsToUse.Count)];
            if (newPrefab == null) continue;

            // Instantiate the new segment, preserving the transform properties of the old one.
            Transform oldTransform = oldSegment.transform;
            GameObject newInstance = Instantiate(newPrefab, oldTransform.position, oldTransform.rotation, oldTransform.parent);
            newInstance.transform.localScale = oldTransform.localScale;
            newInstance.name = oldSegment.name;

            existingSegments[i] = newInstance; // Update the reference in our data list.
            SafeDestroy(oldSegment);
        }
    }

    /// <summary>
    /// Sets the active state for a specific type of element (e.g., corner bodies, windows).
    /// </summary>
    /// <param name="elementType">"CornerBodies", "CornerCaps", "MansardWindows", or "AtticWindows". Case-insensitive.</param>
    /// <param name="active">The desired active state (true or false).</param>
    public void SetElementsActive(string elementType, bool active)
    {
        List<GameObject> elementList = elementType.ToLowerInvariant() switch
        {
            "cornerbodies" => elements.allCornerBodies,
            "cornercaps" => elements.allCornerCaps,
            "mansardwindows" => elements.allMansardWindows,
            "atticwindows" => elements.allAtticWindows,
            _ => null
        };

        SetListActive(elementList, active);
    }

    /// <summary>
    /// Helper to set the active state for all GameObjects in a list.
    /// </summary>
    private void SetListActive(List<GameObject> gameObjects, bool active)
    {
        if (gameObjects == null) return;
        foreach (var go in gameObjects)
        {
            if (go != null) go.SetActive(active);
        }
    }

    /// <summary>
    /// Changes a material on all renderers for a specific group of facade segments.
    /// </summary>
    /// <param name="sideIndex">The index of the building side.</param>
    /// <param name="segmentType">"Ground" or "Middle". Case-insensitive.</param>
    /// <param name="newMaterial">The new material to apply.</param>
    /// <param name="materialSlotIndex">The material index on the renderer to replace.</param>
    public void ChangeFacadeMaterial(int sideIndex, string segmentType, Material newMaterial, int materialSlotIndex = 0)
    {
        if (newMaterial == null || sideIndex < 0 || sideIndex >= elements.facadeElementsPerSide.Count) return;

        SideElementGroup sideGroup = elements.facadeElementsPerSide[sideIndex];
        List<GameObject> segmentsToChange = segmentType.ToLowerInvariant() switch
        {
            "ground" => sideGroup.groundFacadeSegments,
            "middle" => sideGroup.middleFacadeSegments,
            _ => null
        };

        if (segmentsToChange == null) return;

        foreach (GameObject segment in segmentsToChange)
        {
            if (segment == null) continue;
            // Apply the material change to all renderers in the segment's hierarchy.
            foreach (Renderer rend in segment.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = rend.sharedMaterials;
                if (materialSlotIndex >= 0 && materialSlotIndex < mats.Length)
                {
                    mats[materialSlotIndex] = newMaterial;
                    rend.sharedMaterials = mats;
                }
            }
        }
    }

    /// <summary>
    /// Sets the material for a specific type of procedurally generated roof mesh.
    /// </summary>
    /// <param name="roofType">"Mansard", "Attic", or "Flat". Case-insensitive.</param>
    public void SetRoofMaterial(string roofType, Material material)
    {
        if (material == null) return;

        GameObject roofObj = roofType.ToLowerInvariant() switch
        {
            "mansard" => elements.mansardRoofMeshObject,
            "attic" => elements.atticRoofMeshObject,
            "flat" => elements.flatRoofMeshObject,
            _ => null
        };

        if (roofObj != null && roofObj.TryGetComponent<Renderer>(out var rend))
        {
            rend.sharedMaterial = material;
        }
    }

    private static void SafeDestroy(GameObject obj)
    {
        if (obj == null) return;
        if (Application.isEditor && !Application.isPlaying)
            DestroyImmediate(obj);
        else
            Destroy(obj);
    }
}