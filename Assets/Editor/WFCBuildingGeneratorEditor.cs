using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(WFCBuildingGenerator))]
public class WFCBuildingGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default inspector elements
        DrawDefaultInspector();

        // Get the target script instance
        WFCBuildingGenerator generator = (WFCBuildingGenerator)target;

        // Add a button to trigger generation
        if (GUILayout.Button("Generate Building Footprint & Instantiate"))
        {
            // Ensure the generator has the modules needed
            if (generator.allModules == null || generator.allModules.Count == 0 || generator.allModules.Any(m => m == null))
            {
                EditorUtility.DisplayDialog("Error", "Please assign WFC Module ScriptableObjects to the 'All Modules' list in the generator.", "OK");
            }
            else if (generator.allModules.Any(m => m.groundFloorPrefab == null || (m.middleFloorPrefabs.Count == 0 && generator.maxHeight > 0) || m.mansardFloorPrefab == null || m.atticRoofPrefab == null))
            {
                if (EditorUtility.DisplayDialog("Warning", "One or more modules are missing prefab assignments for some floor types. Generation might look incomplete. Continue anyway?", "Yes", "No"))
                {
                    generator.GenerateBuilding();
                }
            }
            else
            {
                // Record undo state for the generator itself and potentially created objects
                Undo.RegisterFullObjectHierarchyUndo(generator.generationParent != null ? generator.generationParent.gameObject : generator.gameObject, "Generate Building");
                generator.GenerateBuilding();
            }
        }

        if (GUILayout.Button("Clear Generated Building"))
        {
            Undo.RegisterFullObjectHierarchyUndo(generator.generationParent != null ? generator.generationParent.gameObject : generator.gameObject, "Clear Generated Building");
            generator.ClearPreviousGeneration();
        }
    }
}