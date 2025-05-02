using UnityEngine;
using System.Collections.Generic;

// Define connection socket types - customize as needed
public enum SocketType
{
    Straight,
    CornerOut45, // Turning right relative to forward direction
    CornerIn45,  // Turning left relative to forward direction
    CornerOut90, // For potential sharper turns
    CornerIn90,
    // Add more if needed (e.g., 135 degrees)
}

[CreateAssetMenu(fileName = "WFCModule", menuName = "Procedural Paris/WFC Module", order = 1)]
public class WFCModule : ScriptableObject
{
    [Header("Module Identification")]
    public string moduleName = "New Module"; // For easier debugging

    [Header("Prefabs per Floor")]
    public GameObject groundFloorPrefab;
    public List<GameObject> middleFloorPrefabs = new List<GameObject>(); // Allow variations
    public GameObject mansardFloorPrefab;
    public GameObject atticRoofPrefab; // Renamed for clarity

    [Header("Connectivity")]
    // What kind of socket this module REQUIRES on its 'input' side (from the previous module)
    public SocketType inputSocket;
    // What kind of socket this module PROVIDES on its 'output' side (for the next module)
    public SocketType outputSocket;

    [Header("Geometry")]
    [Tooltip("Length of this wall segment along its forward direction.")]
    public float segmentLength = 5.0f; // Example length, adjust based on your prefab dimensions

    [Tooltip("Rotation around Y-axis applied AFTER placing this segment, before placing the next.")]
    public float placementRotationY = 0.0f; // 0 for straight, 45 for CornerOut45, -45 for CornerIn45, etc.

    [Header("Generation Settings")]
    [Range(0.1f, 10.0f)]
    public float probabilityWeight = 1.0f; // Higher weight = more likely to be chosen

    // Add any other relevant properties, e.g., min/max height contribution?
}