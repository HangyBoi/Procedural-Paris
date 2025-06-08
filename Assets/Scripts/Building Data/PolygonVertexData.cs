// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
// This script defines the data associated with a single vertex of the building's polygon footprint.
//

using UnityEngine;
using System;

/// <summary>
/// A data container for a single vertex in the building's polygon footprint.
/// It stores the position and settings related to that specific corner.
/// </summary>
[Serializable]
public class PolygonVertexData
{
    [Tooltip("The local X,Z position of the vertex on the ground plane.")]
    public Vector3 position;

    [Tooltip("If true, a corner element (e.g., a chimney) will be generated at this vertex.")]
    public bool addCornerElement = true;
}