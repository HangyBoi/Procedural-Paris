using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewSideStyle", menuName = "Building Generation/Side Style Preset", order = 1)]
public class SideStyleSO : ScriptableObject
{
    [Header("Prefab Lists for this Style")]
    public List<GameObject> groundFloorPrefabs;
    public List<GameObject> middleFloorPrefabs;
    public List<GameObject> mansardFloorPrefabs;
    public List<GameObject> atticFloorPrefabs;
}