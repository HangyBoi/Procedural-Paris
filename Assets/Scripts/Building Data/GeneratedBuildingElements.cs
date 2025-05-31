using UnityEngine;
using System.Collections.Generic;

// Helper class to group GameObjects by side for easier access
[System.Serializable]
public class SideElementGroup
{
    public int sideIndex;
    public List<GameObject> groundFacadeSegments = new List<GameObject>();
    public List<GameObject> middleFacadeSegments = new List<GameObject>();
    // Potentially add roof windows per side here
}

[System.Serializable]
public class GeneratedBuildingElements
{
    public GameObject buildingRoot;

    public List<SideElementGroup> facadeElementsPerSide = new List<SideElementGroup>();

    public List<GameObject> allCornerBodies = new List<GameObject>();
    public List<GameObject> allCornerCaps = new List<GameObject>();

    public GameObject mansardRoofMeshObject;
    public GameObject atticRoofMeshObject;
    public GameObject flatRoofMeshObject;

    public List<GameObject> allMansardWindows = new List<GameObject>();
    public List<GameObject> allAtticWindows = new List<GameObject>();

    public void ClearReferences()
    {
        facadeElementsPerSide.Clear();
        allCornerBodies.Clear();
        allCornerCaps.Clear();
        allMansardWindows.Clear();
        allAtticWindows.Clear();
        mansardRoofMeshObject = null;
        atticRoofMeshObject = null;
        flatRoofMeshObject = null;
        buildingRoot = null;
    }
}