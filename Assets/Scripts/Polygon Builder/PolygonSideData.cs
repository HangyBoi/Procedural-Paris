using UnityEngine;
using System;

[Serializable]
public class PolygonSideData
{
    [Tooltip("Check this to use the assigned 'Side Style Preset' below for this side, instead of the generator's default prefabs.")]
    public bool useCustomStyle = false;

    [Tooltip("Assign a Side Style Preset (Scriptable Object) to define prefabs for this side.")]
    public SideStyleSO sideStylePreset;

}