// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
// This script defines a ScriptableObject for storing a custom visual style for a single building side.
//

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines a reusable style preset for a single side of a building.
/// This can be assigned to a 'PolygonSideData' entry to override the default building style.
/// </summary>
[CreateAssetMenu(fileName = "NewSideStyle", menuName = "Building Generation/Side Style Preset", order = 1)]
public class SideStyleSO : ScriptableObject
{
    [Header("Facade Prefabs for this Style")]
    [Tooltip("A list of ground floor prefabs specific to this style.")]
    public List<GameObject> groundFloorPrefabs;

    [Tooltip("A list of middle floor prefabs specific to this style.")]
    public List<GameObject> middleFloorPrefabs;

    [Header("Roof/Dormer Prefabs for this Style")]
    [Tooltip("A list of prefabs to use for mansard-level windows or dormers for this style. Overrides default window prefabs.")]
    public List<GameObject> mansardFloorPrefabs;

    [Tooltip("A list of prefabs to use for attic-level windows or dormers for this style. Overrides default window prefabs.")]
    public List<GameObject> atticFloorPrefabs;
}