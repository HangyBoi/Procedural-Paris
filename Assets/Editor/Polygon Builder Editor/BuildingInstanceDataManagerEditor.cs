// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
//  This script manages building instances, allowing customization of building elements at runtime or in the editor.
//

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom editor for BuildingInstanceDataManager. Allows post-generation customization of building instances.
/// </summary>
[CustomEditor(typeof(BuildingInstanceDataManager))]
public class BuildingInstanceDataManagerEditor : Editor
{
    private BuildingInstanceDataManager _script;

    // UI state variables
    private int _sideStyleIndex;
    private SideStyleSO _sideStyle;
    private int _matSideIndex;
    private int _matSegmentTypeIndex;
    private Material _facadeMaterial;
    private int _materialSlot;
    private Material _mansardMat, _atticMat, _flatMat;

    // State for collapsible foldout sections
    private static bool _showFacadeStyle = true;
    private static bool _showVisibility = true;
    private static bool _showMaterials = true;

    /// <summary>
    /// Draws the inspector GUI for post-generation building customization.
    /// </summary>
    public override void OnInspectorGUI()
    {
        _script = (BuildingInstanceDataManager)target;
        // Ensure the building data is fully initialized before drawing the custom UI
        if (_script.elements.buildingRoot == null || _script.sourceGenerator == null)
        {
            EditorGUILayout.HelpBox("Building data not initialized. Regenerate the building.", MessageType.Warning);
            return;
        }

        serializedObject.Update();

        EditorGUILayout.LabelField("Post-Generation Customization", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Modify this instance without full regeneration.", MessageType.Info);

        // Draw collapsible sections for organization
        _showFacadeStyle = EditorGUILayout.Foldout(_showFacadeStyle, "Facade Style Override", true, EditorStyles.foldoutHeader);
        if (_showFacadeStyle) DrawSideStyleSection();

        _showVisibility = EditorGUILayout.Foldout(_showVisibility, "Element Visibility", true, EditorStyles.foldoutHeader);
        if (_showVisibility) DrawVisibilitySection();

        _showMaterials = EditorGUILayout.Foldout(_showMaterials, "Material Overrides", true, EditorStyles.foldoutHeader);
        if (_showMaterials) DrawMaterialSection();

        EditorGUILayout.Space(20);
        // Button to trigger a full regeneration from the source generator
        if (GUILayout.Button("REGENERATE FULL BUILDING", GUILayout.Height(30)))
        {
            Undo.RecordObject(_script.sourceGenerator.gameObject, "Regenerate Full Building");
            _script.sourceGenerator.GenerateBuilding();
            // Exit GUI to prevent layout errors after a significant hierarchy change
            GUIUtility.ExitGUI();
        }

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Draws the section for applying a style override to a specific building side.
    /// </summary>
    private void DrawSideStyleSection()
    {
        EditorGUILayout.BeginVertical("box");
        int numSides = _script.sourceGenerator.vertexData.Count;
        if (numSides > 0)
        {
            _sideStyleIndex = EditorGUILayout.IntSlider("Side Index", _sideStyleIndex, 0, numSides - 1);
            _sideStyle = (SideStyleSO)EditorGUILayout.ObjectField("New Side Style", _sideStyle, typeof(SideStyleSO), false);

            if (GUILayout.Button($"Apply Style to Side {_sideStyleIndex}") && _sideStyle != null)
            {
                // Applying a style can instantiate/destroy many objects, so a full hierarchy undo is required.
                Undo.RegisterFullObjectHierarchyUndo(_script.gameObject, "Apply Side Style");
                _script.ApplySideStyle(_sideStyleIndex, _sideStyle);
            }
        }
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Draws the section for toggling the visibility of major building element groups.
    /// </summary>
    private void DrawVisibilitySection()
    {
        EditorGUILayout.BeginVertical("box");
        DrawVisibilityToggle("Corner Bodies", _script.elements.allCornerBodies, active => _script.SetAllCornerBodiesActive(active));
        DrawVisibilityToggle("Corner Caps", _script.elements.allCornerCaps, active => _script.SetAllCornerCapsActive(active));
        EditorGUILayout.Space();
        DrawVisibilityToggle("Mansard Windows", _script.elements.allMansardWindows, active => _script.SetAllWindowsActive("Mansard", active));
        DrawVisibilityToggle("Attic Windows", _script.elements.allAtticWindows, active => _script.SetAllWindowsActive("Attic", active));
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Generic helper to draw a visibility toggle for a list of GameObjects.
    /// </summary>
    private void DrawVisibilityToggle(string label, List<GameObject> targets, System.Action<bool> setter)
    {
        // Disable the toggle if the target list is invalid or empty.
        if (targets == null || targets.Count == 0 || targets[0] == null)
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Toggle(label, false);
            EditorGUI.EndDisabledGroup();
            return;
        }

        // Use the active state of the first element as representative for the whole group.
        bool initialValue = targets[0].activeSelf;
        bool newValue = EditorGUILayout.Toggle(label, initialValue);

        if (newValue != initialValue)
        {
            // Record each object in the group for a proper Undo operation.
            foreach (var go in targets)
                if (go != null) Undo.RecordObject(go, $"Toggle {label}");
            setter(newValue);
        }
    }

    /// <summary>
    /// Draws the section for overriding facade and roof materials.
    /// </summary>
    private void DrawMaterialSection()
    {
        EditorGUILayout.BeginVertical("box");

        // --- Facade Materials ---
        EditorGUILayout.LabelField("Facade Material per Side", EditorStyles.boldLabel);
        int numSides = _script.sourceGenerator.vertexData.Count;
        string[] segmentTypes = { "Ground", "Middle" };

        if (numSides > 0)
        {
            _matSideIndex = EditorGUILayout.IntSlider("Side Index", _matSideIndex, 0, numSides - 1);
            _matSegmentTypeIndex = EditorGUILayout.Popup("Segment Type", _matSegmentTypeIndex, segmentTypes);
            _facadeMaterial = (Material)EditorGUILayout.ObjectField("New Material", _facadeMaterial, typeof(Material), false);
            _materialSlot = Mathf.Max(0, EditorGUILayout.IntField("Material Slot Index", _materialSlot));

            if (GUILayout.Button("Apply Facade Material") && _facadeMaterial != null)
            {
                RecordFacadeRenderersForUndo(_matSideIndex, segmentTypes[_matSegmentTypeIndex]);
                _script.ChangeFacadeMaterial(_matSideIndex, segmentTypes[_matSegmentTypeIndex], _facadeMaterial, _materialSlot);
            }
        }

        EditorGUILayout.Space();

        // --- Roof Materials ---
        EditorGUILayout.LabelField("Roof Materials", EditorStyles.boldLabel);
        DrawMaterialChanger("Mansard", ref _mansardMat, _script.elements.mansardRoofMeshObject);
        DrawMaterialChanger("Attic", ref _atticMat, _script.elements.atticRoofMeshObject);
        DrawMaterialChanger("Flat", ref _flatMat, _script.elements.flatRoofMeshObject);

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Generic helper to draw a material field and apply button for a given element.
    /// </summary>
    private void DrawMaterialChanger(string name, ref Material material, GameObject targetObject)
    {
        material = (Material)EditorGUILayout.ObjectField($"{name} Roof Material", material, typeof(Material), false);

        // Disable the button if no material is assigned or the target object doesn't exist.
        EditorGUI.BeginDisabledGroup(material == null || targetObject == null);
        if (GUILayout.Button($"Apply {name} Material"))
        {
            Undo.RecordObject(targetObject.GetComponent<Renderer>(), $"Change {name} Material");
            _script.SetRoofMaterial(name, material);
        }
        EditorGUI.EndDisabledGroup();
    }

    /// <summary>
    /// Records all renderers on a facade side for undo before a material change is applied.
    /// </summary>
    private void RecordFacadeRenderersForUndo(int sideIndex, string segmentType)
    {
        SideElementGroup sideGroup = _script.elements.facadeElementsPerSide[sideIndex];
        List<GameObject> segments = segmentType == "Ground" ? sideGroup.groundFacadeSegments : sideGroup.middleFacadeSegments;

        if (segments != null)
        {
            foreach (var seg in segments)
            {
                // Must record all renderers on the segment and its children for Undo to work correctly.
                if (seg != null) Undo.RecordObjects(seg.GetComponentsInChildren<Renderer>(), "Change Facade Material");
            }
        }
    }
}
#endif