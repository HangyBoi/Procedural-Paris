using UnityEngine;
using System.Collections.Generic;

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

    public void ApplySideStyle(int sideIndex, SideStyleSO newStyle)
    {
        if (newStyle == null || sourceGenerator == null || sourceGenerator.buildingStyle == null) return;
        if (sideIndex < 0 || sideIndex >= elements.facadeElementsPerSide.Count) return;

        SideElementGroup sideGroup = elements.facadeElementsPerSide.Find(sg => sg.sideIndex == sideIndex);
        if (sideGroup == null) return;

        // Ground Floor Segments
        ReplaceFacadeSegments(sideGroup.groundFacadeSegments, newStyle.groundFloorPrefabs, sourceGenerator.buildingStyle.defaultGroundFloorPrefabs);
        // Middle Floor Segments
        ReplaceFacadeSegments(sideGroup.middleFacadeSegments, newStyle.middleFloorPrefabs, sourceGenerator.buildingStyle.defaultMiddleFloorPrefabs);

        Debug.Log($"Applied style '{newStyle.name}' to side {sideIndex}.");
    }

    private void ReplaceFacadeSegments(List<GameObject> existingSegments, List<GameObject> newStylePrefabs, List<GameObject> defaultStylePrefabs)
    {
        List<GameObject> prefabsToUse = (newStylePrefabs != null && newStylePrefabs.Count > 0) ? newStylePrefabs : defaultStylePrefabs;
        if (prefabsToUse == null || prefabsToUse.Count == 0)
        {
            // If no new prefabs, maybe clear existing or leave them? For now, leave them.
            // Or, one could destroy them:
            // foreach (var seg in existingSegments) { if (seg != null) DestroyImmediate(seg); }
            // existingSegments.Clear();
            return;
        }

        for (int i = 0; i < existingSegments.Count; i++)
        {
            GameObject oldSegment = existingSegments[i];
            if (oldSegment == null) continue;

            GameObject newPrefab = prefabsToUse[Random.Range(0, prefabsToUse.Count)];
            if (newPrefab == null) continue;

            Transform parent = oldSegment.transform.parent;
            Vector3 position = oldSegment.transform.position;
            Quaternion rotation = oldSegment.transform.rotation;
            Vector3 scale = oldSegment.transform.localScale;

            GameObject newInstance = Instantiate(newPrefab, position, rotation, parent);
            newInstance.transform.localScale = scale;
            newInstance.name = oldSegment.name;

            DestroyImmediate(oldSegment);
            existingSegments[i] = newInstance;
        }
    }

    public void SetAllCornerBodiesActive(bool active)
    {
        if (elements.allCornerBodies != null)
        {
            foreach (GameObject cornerBody in elements.allCornerBodies)
            {
                if (cornerBody != null) cornerBody.SetActive(active);
            }
        }
    }

    public void SetAllCornerCapsActive(bool active)
    {
        if (elements.allCornerCaps != null)
        {
            foreach (GameObject cornerCap in elements.allCornerCaps)
            {
                if (cornerCap != null) cornerCap.SetActive(active);
            }
        }
    }

    public void SetAllWindowsActive(string windowType, bool active)
    {
        List<GameObject> windows = null;
        if (windowType.Equals("Mansard", System.StringComparison.OrdinalIgnoreCase)) windows = elements.allMansardWindows;
        else if (windowType.Equals("Attic", System.StringComparison.OrdinalIgnoreCase)) windows = elements.allAtticWindows;

        if (windows != null)
        {
            foreach (GameObject window in windows)
            {
                if (window != null) window.SetActive(active);
            }
        }
    }

    // Method to change a specific material on all renderers of a specific facade segment type on a side
    public void ChangeFacadeMaterial(int sideIndex, string segmentType /*"Ground" or "Middle"*/, Material newMaterial, int materialSlotIndex = 0)
    {
        if (newMaterial == null) return;
        if (sideIndex < 0 || sideIndex >= elements.facadeElementsPerSide.Count) return;

        SideElementGroup sideGroup = elements.facadeElementsPerSide.Find(sg => sg.sideIndex == sideIndex);
        if (sideGroup == null) return;

        List<GameObject> segmentsToChange = null;
        if (segmentType.Equals("Ground", System.StringComparison.OrdinalIgnoreCase))
            segmentsToChange = sideGroup.groundFacadeSegments;
        else if (segmentType.Equals("Middle", System.StringComparison.OrdinalIgnoreCase))
            segmentsToChange = sideGroup.middleFacadeSegments;

        if (segmentsToChange != null)
        {
            foreach (GameObject segment in segmentsToChange)
            {
                if (segment == null) continue;
                Renderer[] renderers = segment.GetComponentsInChildren<Renderer>();
                foreach (Renderer rend in renderers)
                {
                    Material[] mats = rend.sharedMaterials; // Get copy
                    if (materialSlotIndex >= 0 && materialSlotIndex < mats.Length)
                    {
                        mats[materialSlotIndex] = newMaterial;
                        rend.sharedMaterials = mats; // Assign back
                    }
                }
            }
        }
    }

    public void SetRoofMaterial(string roofType, Material material)
    {
        if (material == null) return;
        GameObject roofObj = null;
        if (roofType.Equals("Mansard", System.StringComparison.OrdinalIgnoreCase)) roofObj = elements.mansardRoofMeshObject;
        else if (roofType.Equals("Attic", System.StringComparison.OrdinalIgnoreCase)) roofObj = elements.atticRoofMeshObject;
        else if (roofType.Equals("Flat", System.StringComparison.OrdinalIgnoreCase)) roofObj = elements.flatRoofMeshObject;

        if (roofObj != null)
        {
            Renderer rend = roofObj.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = material;
            }
        }
    }

}