using UnityEngine;
using System.Collections.Generic;
using System; // Required for [Serializable]

[Serializable]
public class PolygonSideData
{
    public bool overridePrefabs = false;
    [Tooltip("If overriding, assign specific prefabs for this side.")]
    public List<GameObject> groundFloorPrefabs;
    public List<GameObject> middleFloorPrefabs;
    public List<GameObject> mansardFloorPrefabs;
    public List<GameObject> atticFloorPrefabs;

    // You could add more side-specific settings here later
    // public bool specificHeightVariation = false;
    // public int sideMiddleFloors = 3;
}