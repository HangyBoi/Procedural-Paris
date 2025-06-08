// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
// This script defines a ScriptableObject for storing the default visual style of a procedural building.
//

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines a reusable building style preset containing default prefabs for various building parts.
/// This allows for easy swapping of a building's entire visual theme.
/// </summary>
[CreateAssetMenu(fileName = "NewBuildingStyle", menuName = "Building Generation/Building Style", order = 0)]
public class BuildingStyleSO : ScriptableObject
{
    [Header("Default Facade Prefabs")]
    [Tooltip("Prefabs used for the ground floor facade segments.")]
    public List<GameObject> defaultGroundFloorPrefabs;
    [Tooltip("Prefabs used for all middle floor facade segments.")]
    public List<GameObject> defaultMiddleFloorPrefabs;

    [Header("Default Corner Elements")]
    [Tooltip("Prefabs for the main body of a corner element (e.g., chimney stack).")]
    public List<GameObject> defaultChimneyBodyPrefabs;
    [Tooltip("Prefabs for the cap on top of a corner element.")]
    public List<GameObject> defaultChimneyCapPrefabs;

    [Header("Default Roof Window Prefabs")]
    [Tooltip("Default prefabs used for windows placed on the mansard roof layer.")]
    public List<GameObject> defaultMansardWindowPrefabs;
    [Tooltip("Default prefabs used for windows placed on the attic roof layer.")]
    public List<GameObject> defaultAtticWindowPrefabs;

    [Header("Default Roof Facade/Dormer Prefabs")]
    [Tooltip("These prefabs are checked by Side Styles to override roof windows, potentially for creating dormers with facade-like segments.")]
    public List<GameObject> defaultMansardFloorPrefabs;
    [Tooltip("These prefabs are checked by Side Styles to override roof windows, potentially for creating dormers with facade-like segments.")]
    public List<GameObject> defaultAtticFloorPrefabs;

    [Header("Dedicated Mansard Corner Sets (if applicable)")]
    public List<MansardCornerSet> mansardCornerSets = new List<MansardCornerSet>();
}