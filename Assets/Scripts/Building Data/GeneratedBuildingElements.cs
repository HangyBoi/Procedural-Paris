// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
// This script defines data structures used to store references to all procedurally generated building components.
//

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A data container for grouping GameObjects by the building side they belong to.
/// </summary>
[System.Serializable]
public class SideElementGroup
{
    [Tooltip("The index of the side this group belongs to.")]
    public int sideIndex;

    [Tooltip("List of instantiated ground floor facade segments for this side.")]
    public List<GameObject> groundFacadeSegments = new List<GameObject>();

    [Tooltip("List of instantiated middle floor facade segments for this side.")]
    public List<GameObject> middleFacadeSegments = new List<GameObject>();
}

/// <summary>
/// A central data storage class that holds references to all GameObjects created during the building generation process.
/// This object is typically attached to the generated building's root for runtime access.
/// </summary>
[System.Serializable]
public class GeneratedBuildingElements
{
    [Tooltip("A reference to the root GameObject of the entire generated building model.")]
    public GameObject buildingRoot;

    [Tooltip("A list containing element groups for each side of the building.")]
    public List<SideElementGroup> facadeElementsPerSide = new List<SideElementGroup>();

    [Tooltip("A list of all instantiated corner element bodies (e.g., chimney stacks).")]
    public List<GameObject> allCornerBodies = new List<GameObject>();
    [Tooltip("A list of all instantiated corner element caps.")]
    public List<GameObject> allCornerCaps = new List<GameObject>();

    [Tooltip("Reference to the generated mansard roof mesh GameObject.")]
    public GameObject mansardRoofMeshObject;
    [Tooltip("Reference to the generated attic roof mesh GameObject.")]
    public GameObject atticRoofMeshObject;
    [Tooltip("Reference to the generated flat top roof mesh GameObject.")]
    public GameObject flatRoofMeshObject;

    [Tooltip("A list of all instantiated mansard roof windows.")]
    public List<GameObject> allMansardWindows = new List<GameObject>();
    [Tooltip("A list of all instantiated attic roof windows.")]
    public List<GameObject> allAtticWindows = new List<GameObject>();

    /// <summary>
    /// Initializes a new instance and pre-populates the side element groups.
    /// </summary>
    /// <param name="sideCount">The number of sides the building has.</param>
    public GeneratedBuildingElements(int sideCount = 0)
    {
        facadeElementsPerSide = new List<SideElementGroup>(sideCount);
        for (int i = 0; i < sideCount; i++)
        {
            facadeElementsPerSide.Add(new SideElementGroup { sideIndex = i });
        }
    }

    /// <summary>
    /// Clears all lists and nullifies all references to generated GameObjects.
    /// Does not destroy the GameObjects themselves.
    /// </summary>
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