// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
// This script defines a simple data structure for passing roof object references between generators.
//

using UnityEngine;

/// <summary>
/// A simple data structure (struct) to hold references to the GameObjects of generated roof segments.
/// This is used as a return type for the main roof generation method.
/// </summary>
public struct GeneratedRoofObjects
{
    /// <summary>
    /// The GameObject containing the generated mansard roof mesh.
    /// </summary>
    public GameObject MansardRoofObject { get; set; }

    /// <summary>
    /// The GameObject containing the generated attic roof mesh.
    /// </summary>
    public GameObject AtticRoofObject { get; set; }

    /// <summary>
    /// The GameObject containing the generated flat top roof mesh.
    /// </summary>
    public GameObject FlatRoofObject { get; set; }
}