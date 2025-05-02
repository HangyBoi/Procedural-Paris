using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Define simplified connection socket types
public enum SocketType
{
    Straight,
    Corner // Represents the specific 90-degree corner prefab
}

[CreateAssetMenu(fileName = "WFCModule", menuName = "Procedural Paris/WFC Module", order = 1)]
public class WFCModule : ScriptableObject
{
    [Header("Module Identification")]
    public string moduleName = "New Module";

    [Header("Prefabs per Floor Type")]
    [Tooltip("List of possible Ground Floor prefabs for this module type.")]
    public List<GameObject> groundFloorPrefabs = new(); // Changed to List
    [Tooltip("List of possible Middle Floor prefabs for this module type.")]
    public List<GameObject> middleFloorPrefabs = new();
    [Tooltip("List of possible Mansard Floor prefabs for this module type.")]
    public List<GameObject> mansardFloorPrefabs = new(); // Changed to List
    [Tooltip("The single Attic/Roof prefab for this module type.")]
    public GameObject atticRoofPrefab; // Keeping as single for now, change if needed

    [Header("Connectivity")]
    [Tooltip("The socket type this module REQUIRES from the previous module's output.")]
    public SocketType inputSocket;
    [Tooltip("The socket type this module PROVIDES for the next module's input.")]
    public SocketType outputSocket;

    [Header("Geometry (Read Tooltips!)")]
    [Tooltip("Effective forward distance covered by this segment *before* any rotation is applied for the next segment. " +
             "For a Straight wall (0.8m), this is 0.8. " +
             "For the Corner prefab, this should represent the equivalent forward space it occupies along the current direction. " +
             "MEASURE YOUR PREFAB: If the corner starts, effectively moves forward 0.8m, then turns 90 degrees, set this to 0.8.")]
    public float segmentLength = 0.8f; // Defaulting to standard wall length

    [Tooltip("Rotation around Y-axis applied *after* placing this segment to orient for the NEXT segment. " +
             "0 for Straight Wall. " +
             "90 for the Corner prefab (if it represents a 90-degree clockwise turn). Use -90 for counter-clockwise.")]
    public float placementRotationY = 0.0f; // 0 for straight, likely 90 or -90 for your corner

    [Header("Generation Settings")]
    [Range(0.1f, 10.0f)]
    public float probabilityWeight = 1.0f;

    // --- Helper Methods ---

    // Selects a random prefab from a list, returns null if list is empty/null
    public GameObject GetRandomPrefab(List<GameObject> prefabList)
    {
        if (prefabList == null || prefabList.Count == 0)
        {
            return null;
        }
        // Filter out any null entries potentially added in the inspector
        var validPrefabs = prefabList.Where(p => p != null).ToList();
        if (validPrefabs.Count == 0)
        {
            return null;
        }
        return validPrefabs[Random.Range(0, validPrefabs.Count)];
    }
}