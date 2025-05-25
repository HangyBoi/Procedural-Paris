using UnityEngine;
using System;

[Serializable]
public class PolygonVertexData
{
    public Vector3 position;
    [Tooltip("Should this vertex spawn a corner feature?")]
    public bool addCornerElement = true;
}