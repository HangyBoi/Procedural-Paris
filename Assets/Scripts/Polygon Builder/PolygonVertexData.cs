using UnityEngine;
using System; // Required for [Serializable]

[Serializable] // Makes it visible and editable in the Inspector when used in a List
public class PolygonVertexData
{
    public Vector3 position;
    public bool addCornerElement = false; // Should this vertex spawn a corner feature?
    // Add other potential vertex-specific data here later (e.g., specific corner prefab)
}