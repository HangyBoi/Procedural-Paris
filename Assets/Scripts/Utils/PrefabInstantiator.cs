// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
//  This scrpt provides a static utility class for instantiating and configuring prefabs in Unity.
//

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A static utility class for instantiating and configuring prefabs.
/// </summary>
public static class PrefabInstantiator
{
    /// <summary>
    /// Instantiates a random prefab from a list and optionally scales it to fit a target width.
    /// </summary>
    /// <param name="prefabList">List of prefabs to choose from.</param>
    /// <param name="worldPosition">World position for the new instance.</param>
    /// <param name="worldRotation">World rotation for the new instance.</param>
    /// <param name="parent">Parent transform for the new instance.</param>
    /// <param name="actualSegmentWidth">The actual width this segment should occupy.</param>
    /// <param name="isCorner">Flag to prevent scaling on corner elements.</param>
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

        // Select a random prefab from the provided list.
        GameObject prefab = prefabList[Random.Range(0, prefabList.Count)];
        if (prefab == null)
        {
            Debug.LogWarning("A prefab in the provided list is null and was selected. Skipping instantiation.");
            return null;
        }

        GameObject instance = Object.Instantiate(prefab, parent);
        instance.transform.position = worldPosition;
        instance.transform.rotation = worldRotation;

        // Determine if scaling is required based on settings.
        bool shouldScale = !isCorner && scaleToFit && nominalPrefabWidth > GeometryConstants.GeometricEpsilon;

        if (shouldScale)
        {
            float scaleFactor = actualSegmentWidth / nominalPrefabWidth;
            Vector3 localScale = instance.transform.localScale;
            instance.transform.localScale = new Vector3(localScale.x * scaleFactor, localScale.y, localScale.z);
        }

        return instance;
    }
}