using UnityEngine;
using System.Collections.Generic;


[CreateAssetMenu(fileName = "NewBuildingStyle", menuName = "Building Generation/Building Style", order = 0)]
public class BuildingStyleSO : ScriptableObject
{
    [Header("Default Facade Prefabs")]
    public List<GameObject> defaultGroundFloorPrefabs;
    public List<GameObject> defaultMiddleFloorPrefabs;
    public List<GameObject> defaultMansardFloorPrefabs;
    public List<GameObject> defaultAtticFloorPrefabs;

    [Header("Dedicated Mansard Corner Sets")]
    public List<MansardCornerSet> mansardCornerSets = new List<MansardCornerSet>();

    [Header("Default Corner Elements (Chimneys)")]
    public List<GameObject> defaultChimneyBodyPrefabs;
    public List<GameObject> defaultChimneyCapPrefabs;

    [Header("Default Roof Window Prefabs")]
    public List<GameObject> defaultMansardWindowPrefabs;
    public List<GameObject> defaultAtticWindowPrefabs;
}