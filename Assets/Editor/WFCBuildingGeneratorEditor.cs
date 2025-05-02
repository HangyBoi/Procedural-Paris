using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(WFCBuildingGenerator))]
public class WFCBuildingGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        WFCBuildingGenerator generator = (WFCBuildingGenerator)target;

        // --- Generation Button ---
        if (GUILayout.Button("Generate Building Footprint & Instantiate"))
        {
            bool modulesOk = generator.allModules != null && generator.allModules.Count > 0 && !generator.allModules.Any(m => m == null);
            if (!modulesOk)
            {
                EditorUtility.DisplayDialog("Error", "Please assign valid WFC Module ScriptableObjects to the 'All Modules' list.", "OK");
                return; // Stop here
            }

            // Check for missing critical prefabs within modules
            bool prefabsOk = true;
            foreach (var module in generator.allModules.Where(m => m != null)) // Iterate only non-null modules
            {
                // Check if essential lists/prefabs are missing *if they are expected to be used*
                bool groundMissing = module.groundFloorPrefabs == null || module.groundFloorPrefabs.Count == 0 || module.groundFloorPrefabs.Any(p => p == null);
                // Only check middle if height > 0 might be generated
                bool middleMissing = (generator.maxHeight > 0) && (module.middleFloorPrefabs == null || module.middleFloorPrefabs.Count == 0 || module.middleFloorPrefabs.Any(p => p == null));
                bool mansardMissing = module.mansardFloorPrefabs == null || module.mansardFloorPrefabs.Count == 0 || module.mansardFloorPrefabs.Any(p => p == null);
                bool atticMissing = module.atticRoofPrefab == null; // Attic is still single

                if (groundMissing || middleMissing || mansardMissing || atticMissing)
                {
                    Debug.LogWarning($"Module '{module.moduleName}' has missing prefab assignments:" +
                                     $"{(groundMissing ? " [Ground]" : "")}" +
                                     $"{(middleMissing ? " [Middle]" : "")}" +
                                     $"{(mansardMissing ? " [Mansard]" : "")}" +
                                     $"{(atticMissing ? " [Attic]" : "")}", module);
                    prefabsOk = false; // Mark that at least one module has issues
                }
            }

            if (!prefabsOk)
            {
                if (!EditorUtility.DisplayDialog("Prefab Warning", "One or more modules have missing prefab assignments (check Console warnings). Generation might look incomplete. Continue anyway?", "Yes, Generate", "Cancel"))
                {
                    return; // Stop if user cancels
                }
            }

            // If all checks passed or user confirmed, proceed
            // Use the generator's parent or the generator itself for Undo root
            Transform undoRoot = generator.generationParent != null ? generator.generationParent : generator.transform;
            Undo.RegisterCompleteObjectUndo(undoRoot.gameObject, "Generate Building"); // Record state *before* clearing
            Undo.RegisterCompleteObjectUndo(generator, "Generate Building Params");

            generator.GenerateBuilding();

            // Mark the scene as dirty so changes prompt a save
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            }
        }

        // --- Clear Button ---
        if (GUILayout.Button("Clear Generated Building"))
        {
            Transform undoRoot = generator.generationParent != null ? generator.generationParent : generator.transform;
            // Record the state *before* clearing for Undo
            Undo.RegisterCompleteObjectUndo(undoRoot.gameObject, "Clear Generated Building");
            Undo.RegisterCompleteObjectUndo(generator, "Clear Building State");

            generator.ClearPreviousGeneration();

            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            }
        }
    }
}