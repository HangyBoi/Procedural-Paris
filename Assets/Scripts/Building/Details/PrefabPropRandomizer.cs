using UnityEngine;

public class PrefabPropRandomizer : MonoBehaviour
{
    public GameObject[] optionalProps;
    [Range(0f, 1f)] public float chancePerProp = 0.5f;

    public void RandomizeProps()
    {
        if (optionalProps == null || optionalProps.Length == 0)
        {
            return;
        }

        foreach (GameObject prop in optionalProps)
        {
            if (prop != null)
            {
                bool shouldBeActive = Random.value < chancePerProp;
                prop.SetActive(shouldBeActive);
            }
        }
    }
}