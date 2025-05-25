using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Utility class for instantiating and configuring facade/corner prefabs.
/// </summary>
public static class PrefabInstantiator
{
    /// <summary>
    /// Instantiates a randomly chosen prefab from a list at the given position and rotation.
    /// Optionally scales the instance along its local X-axis.
    /// </summary>
    /// <param name="prefabList">List of prefabs to choose from.</param>
    /// <param name="worldPosition">World position for the new instance.</param>
    /// <param name="worldRotation">World rotation for the new instance.</param>
    /// <param name="parent">Parent transform for the new instance.</param>
    /// <param name="actualSegmentWidth">The actual width this segment should occupy.</param>
    /// <param name="isCorner">Flag indicating if this is a corner element (might affect scaling logic if further refined).</param>
    /// <param name="scaleToFit">Whether to scale the prefab to fit the actualSegmentWidth.</param>
    /// <param name="nominalPrefabWidth">The original design width of the prefab, used for scaling calculations.</param>
    /// <returns>The instantiated GameObject, or null if instantiation failed.</returns>
    public static GameObject InstantiateSegment(
        List<GameObject> prefabList,
        Vector3 worldPosition,
        Quaternion worldRotation,
        Transform parent,
        float actualSegmentWidth,
        bool isCorner,
        bool scaleToFit,
        float nominalPrefabWidth)
    {
        if (prefabList == null || prefabList.Count == 0) return null;

        int randomIndex = Random.Range(0, prefabList.Count);
        GameObject prefab = prefabList[randomIndex];
        if (prefab == null)
        {
            Debug.LogWarning($"Prefab at index {randomIndex} in list is null.");
            return null;
        }

        GameObject instance = Object.Instantiate(prefab, parent);
        instance.transform.position = worldPosition;
        instance.transform.rotation = worldRotation;

        // Scale the instantiated segment if needed (typically for non-corner facades)
        if (!isCorner && scaleToFit && nominalPrefabWidth > GeometryConstants.GeometricEpsilon &&
            Mathf.Abs(actualSegmentWidth - nominalPrefabWidth) > GeometryConstants.GeometricEpsilon)
        {
            Vector3 localScale = instance.transform.localScale;
            float scaleFactor = actualSegmentWidth / nominalPrefabWidth;
            instance.transform.localScale = new Vector3(localScale.x * scaleFactor, localScale.y, localScale.z);
        }
        return instance;
    }
}