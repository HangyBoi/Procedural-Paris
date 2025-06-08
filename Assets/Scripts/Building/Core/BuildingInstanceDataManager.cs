// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, and Code Cleanup were done by AI*
//
// This code is part of a Unity project that manages building instances, allowing customization of building elements at runtime or in the editor.
//

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages and provides methods to customize a generated building instance at runtime or in the editor.
/// </summary>
public class BuildingInstanceDataManager : MonoBehaviour
{
    public PolygonBuildingGenerator sourceGenerator;
    public GeneratedBuildingElements elements = new GeneratedBuildingElements();

    public void Initialize(PolygonBuildingGenerator generator, GameObject root)
    {
        this.sourceGenerator = generator;
        this.elements.buildingRoot = root;
    }

    // --- Customization Methods ---

    /// <summary>
    /// Applies a specific visual style to a single side of the building.
    /// </summary>
    /// <param name="sideIndex">The index of the building side to modify.</param>
    /// <param name="newStyle">The style to apply.</param>
    public void ApplySideStyle(int sideIndex, SideStyleSO newStyle)
    {
        if (newStyle == null || sourceGenerator?.buildingStyle == null) return;
        if (sideIndex < 0 || sideIndex >= elements.facadeElementsPerSide.Count) return;

        SideElementGroup sideGroup = elements.facadeElementsPerSide[sideIndex];

        // Replace ground and middle floor facade segments with ones from the new style.
        ReplaceFacadeSegments(sideGroup.groundFacadeSegments, newStyle.groundFloorPrefabs, sourceGenerator.buildingStyle.defaultGroundFloorPrefabs);
        ReplaceFacadeSegments(sideGroup.middleFacadeSegments, newStyle.middleFloorPrefabs, sourceGenerator.buildingStyle.defaultMiddleFloorPrefabs);
    }

    private void ReplaceFacadeSegments(List<GameObject> existingSegments, List<GameObject> newPrefabs, List<GameObject> defaultPrefabs)
    {
        List<GameObject> prefabsToUse = (newPrefabs != null && newPrefabs.Count > 0) ? newPrefabs : defaultPrefabs;
        if (prefabsToUse == null || prefabsToUse.Count == 0) return;

        for (int i = 0; i < existingSegments.Count; i++)
        {
            GameObject oldSegment = existingSegments[i];
            if (oldSegment == null) continue;

            GameObject newPrefab = prefabsToUse[Random.Range(0, prefabsToUse.Count)];
            if (newPrefab == null) continue;

            Transform oldTransform = oldSegment.transform;
            GameObject newInstance = Instantiate(newPrefab, oldTransform.position, oldTransform.rotation, oldTransform.parent);
            newInstance.transform.localScale = oldTransform.localScale;
            newInstance.name = oldSegment.name;

            SafeDestroy(oldSegment);
            existingSegments[i] = newInstance;
        }
    }

    /// <summary>
    /// Sets the active state of all corner body elements.
    /// </summary>
    public void SetAllCornerBodiesActive(bool active)
    {
        if (elements.allCornerBodies == null) return;
        foreach (GameObject cornerBody in elements.allCornerBodies)
        {
            if (cornerBody != null) cornerBody.SetActive(active);
        }
    }

    /// <summary>
    /// Sets the active state of all corner cap elements.
    /// </summary>
    public void SetAllCornerCapsActive(bool active)
    {
        if (elements.allCornerCaps == null) return;
        foreach (GameObject cornerCap in elements.allCornerCaps)
        {
            if (cornerCap != null) cornerCap.SetActive(active);
        }
    }

    /// <summary>
    /// Sets the active state for a specific type of roof window.
    /// </summary>
    /// <param name="windowType">"Mansard" or "Attic". Case-insensitive.</param>
    public void SetAllWindowsActive(string windowType, bool active)
    {
        List<GameObject> windows = windowType.ToLowerInvariant() switch
        {
            "mansard" => elements.allMansardWindows,
            "attic" => elements.allAtticWindows,
            _ => null
        };

        if (windows == null) return;
        foreach (GameObject window in windows)
        {
            if (window != null) window.SetActive(active);
        }
    }

    /// <summary>
    /// Changes a material on all renderers for a specific type of facade segment on a given side.
    /// </summary>
    /// <param name="sideIndex">The index of the building side.</param>
    /// <param name="segmentType">"Ground" or "Middle". Case-insensitive.</param>
    /// <param name="newMaterial">The new material to apply.</param>
    /// <param name="materialSlotIndex">The material index on the renderer to replace.</param>
    public void ChangeFacadeMaterial(int sideIndex, string segmentType, Material newMaterial, int materialSlotIndex = 0)
    {
        if (newMaterial == null) return;
        if (sideIndex < 0 || sideIndex >= elements.facadeElementsPerSide.Count) return;

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
    /// Sets the material for a specific type of roof mesh.
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

