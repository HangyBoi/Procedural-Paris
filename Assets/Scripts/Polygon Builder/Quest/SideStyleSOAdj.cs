// SideStyleSOAdj.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewSideStyleAdj", menuName = "Building Generation/Side Style Preset Adj", order = 1)]
public class SideStyleSOAdj : ScriptableObject
{
    [Header("Prefab Lists for this Style")]
    public List<GameObject> groundFloorPrefabs;
    public List<GameObject> middleFloorPrefabs;
    public List<GameObject> mansardFloorPrefabs;
    public List<GameObject> atticFloorPrefabs;
}