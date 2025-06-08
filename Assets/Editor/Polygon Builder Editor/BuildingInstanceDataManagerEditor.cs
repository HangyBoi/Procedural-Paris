// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
// This script provides a custom editor for post-generation customization of building instances.
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
        // Ensure the building data is fully initialized before drawing the custom UI.
        if (_script.elements?.buildingRoot == null || _script.sourceGenerator == null)
        {
            EditorGUILayout.HelpBox("Building data not initialized. Regenerate the building.", MessageType.Warning);
            DrawDefaultInspector(); // Show default fields to help debug.
            return;
        }

        serializedObject.Update();

        EditorGUILayout.LabelField("Post-Generation Customization", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Modify this instance without a full regeneration.", MessageType.Info);

        // Draw collapsible sections for organization.
        _showFacadeStyle = EditorGUILayout.Foldout(_showFacadeStyle, "Facade Style Override", true, EditorStyles.foldoutHeader);
        if (_showFacadeStyle) DrawSideStyleSection();

        _showVisibility = EditorGUILayout.Foldout(_showVisibility, "Element Visibility", true, EditorStyles.foldoutHeader);
        if (_showVisibility) DrawVisibilitySection();

        _showMaterials = EditorGUILayout.Foldout(_showMaterials, "Material Overrides", true, EditorStyles.foldoutHeader);
        if (_showMaterials) DrawMaterialSection();

        DrawRegenerateButton();

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

            EditorGUI.BeginDisabledGroup(_sideStyle == null);
            if (GUILayout.Button($"Apply Style to Side {_sideStyleIndex}"))
            {
                // Applying a style can instantiate/destroy objects, so a full hierarchy undo is required.
                Undo.RegisterFullObjectHierarchyUndo(_script.gameObject, "Apply Side Style");
                _script.ApplySideStyle(_sideStyleIndex, _sideStyle);
            }
            EditorGUI.EndDisabledGroup();
        }

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Draws the section for toggling the visibility of major building element groups.
    /// </summary>
    private void DrawVisibilitySection()
    {
        EditorGUILayout.BeginVertical("box");

        // Use the new, unified method by passing the correct string key for each element type.
        DrawVisibilityToggle("Corner Bodies", _script.elements.allCornerBodies, "CornerBodies");
        DrawVisibilityToggle("Corner Caps", _script.elements.allCornerCaps, "CornerCaps");
        EditorGUILayout.Space();
        DrawVisibilityToggle("Mansard Windows", _script.elements.allMansardWindows, "MansardWindows");
        DrawVisibilityToggle("Attic Windows", _script.elements.allAtticWindows, "AtticWindows");

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Generic helper to draw a visibility toggle for a group of GameObjects.
    /// </summary>
    /// <param name="label">The user-facing label for the toggle.</param>
    /// <param name="targets">The list of GameObjects to affect.</param>
    /// <param name="elementTypeKey">The key used by SetElementsActive to identify the group.</param>
    private void DrawVisibilityToggle(string label, IReadOnlyList<GameObject> targets, string elementTypeKey)
    {
        bool hasTargets = targets != null && targets.Count > 0 && targets[0] != null;
        EditorGUI.BeginDisabledGroup(!hasTargets);

        // Use the active state of the first element as representative for the whole group.
        bool initialValue = hasTargets ? targets[0].activeSelf : false;
        bool newValue = EditorGUILayout.Toggle(label, initialValue);

        if (newValue != initialValue)
        {
            // Record each object in the group for a proper Undo operation.
            foreach (var go in targets)
            {
                if (go != null) Undo.RecordObject(go, $"Toggle {label}");
            }
            // Call the single, refactored method with the appropriate key.
            _script.SetElementsActive(elementTypeKey, newValue);
        }

        EditorGUI.EndDisabledGroup();
    }

    /// <summary>
    /// Draws the section for overriding facade and roof materials.
    /// </summary>
    private void DrawMaterialSection()
    {
        EditorGUILayout.BeginVertical("box");

        DrawFacadeMaterialChanger();
        EditorGUILayout.Space();
        DrawRoofMaterialChangers();

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Draws the UI for changing facade materials.
    /// </summary>
    private void DrawFacadeMaterialChanger()
    {
        EditorGUILayout.LabelField("Facade Material per Side", EditorStyles.boldLabel);

        int numSides = _script.sourceGenerator.vertexData.Count;
        if (numSides <= 0) return;

        string[] segmentTypes = { "Ground", "Middle" };

        _matSideIndex = EditorGUILayout.IntSlider("Side Index", _matSideIndex, 0, numSides - 1);
        _matSegmentTypeIndex = EditorGUILayout.Popup("Segment Type", _matSegmentTypeIndex, segmentTypes);
        _facadeMaterial = (Material)EditorGUILayout.ObjectField("New Material", _facadeMaterial, typeof(Material), false);
        _materialSlot = Mathf.Max(0, EditorGUILayout.IntField("Material Slot Index", _materialSlot));

        EditorGUI.BeginDisabledGroup(_facadeMaterial == null);
        if (GUILayout.Button("Apply Facade Material"))
        {
            RecordFacadeRenderersForUndo(_matSideIndex, segmentTypes[_matSegmentTypeIndex]);
            _script.ChangeFacadeMaterial(_matSideIndex, segmentTypes[_matSegmentTypeIndex], _facadeMaterial, _materialSlot);
        }
        EditorGUI.EndDisabledGroup();
    }

    /// <summary>
    /// Draws the UI for changing all roof materials.
    /// </summary>
    private void DrawRoofMaterialChangers()
    {
        EditorGUILayout.LabelField("Roof Materials", EditorStyles.boldLabel);
        DrawMaterialChanger("Mansard", ref _mansardMat, _script.elements.mansardRoofMeshObject);
        DrawMaterialChanger("Attic", ref _atticMat, _script.elements.atticRoofMeshObject);
        DrawMaterialChanger("Flat", ref _flatMat, _script.elements.flatRoofMeshObject);
    }

    /// <summary>
    /// Generic helper to draw a material field and apply button for a given roof element.
    /// </summary>
    private void DrawMaterialChanger(string name, ref Material material, GameObject targetObject)
    {
        material = (Material)EditorGUILayout.ObjectField($"{name} Roof Material", material, typeof(Material), false);

        EditorGUI.BeginDisabledGroup(material == null || targetObject == null);
        if (GUILayout.Button($"Apply {name} Material"))
        {
            Undo.RecordObject(targetObject.GetComponent<Renderer>(), $"Change {name} Material");
            _script.SetRoofMaterial(name, material);
        }
        EditorGUI.EndDisabledGroup();
    }

    /// <summary>
    /// Draws the button to trigger a full regeneration from the source generator.
    /// </summary>
    private void DrawRegenerateButton()
    {
        EditorGUILayout.Space(20);
        if (GUILayout.Button("REGENERATE FULL BUILDING", GUILayout.Height(30)))
        {
            if (_script.sourceGenerator != null)
            {
                Undo.RecordObject(_script.sourceGenerator.gameObject, "Regenerate Full Building");
                _script.sourceGenerator.GenerateBuilding();
                GUIUtility.ExitGUI(); // Exit GUI to prevent layout errors after a hierarchy change.
            }
        }
    }

    /// <summary>
    /// Records all renderers on a facade side for undo before a material change is applied.
    /// </summary>
    private void RecordFacadeRenderersForUndo(int sideIndex, string segmentType)
    {
        SideElementGroup sideGroup = _script.elements.facadeElementsPerSide[sideIndex];
        List<GameObject> segments = segmentType.ToLowerInvariant() == "ground"
            ? sideGroup.groundFacadeSegments
            : sideGroup.middleFacadeSegments;

        if (segments == null) return;

        foreach (var seg in segments)
        {
            if (seg != null)
            {
                // Must record all renderers on the segment and its children for Undo to work correctly.
                Undo.RecordObjects(seg.GetComponentsInChildren<Renderer>(), "Change Facade Material");
            }
        }
    }
}
#endif