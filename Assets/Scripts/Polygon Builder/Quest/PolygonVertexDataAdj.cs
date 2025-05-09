// PolygonVertexDataAdj.cs
using UnityEngine;
using System;

[Serializable]
public class PolygonVertexDataAdj
{
    public Vector3 position;
    public bool addCornerElement = true; // Should this vertex spawn a corner feature?
    // Add other potential vertex-specific data here later (e.g., specific corner prefab based on angle if you expand)
}