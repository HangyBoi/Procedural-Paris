// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
// This script defines the data associated with a single side of the building's polygon footprint.
//

using UnityEngine;
using System;

/// <summary>
/// A data container for settings specific to one side of the building's polygon.
/// This allows for per-side overrides of the default building style.
/// </summary>
[Serializable]
public class PolygonSideData
{
    [Tooltip("If true, this side will use the 'Side Style Preset' below instead of the generator's default style.")]
    public bool useCustomStyle = false;

    [Tooltip("Assign a Side Style Preset to define a unique set of prefabs for this side.")]
    public SideStyleSO sideStylePreset;
}