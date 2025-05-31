#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BuildingInstanceDataManager))]
public class BuildingInstanceDataManagerEditor : Editor
{
    private BuildingInstanceDataManager _script;

    private int _selectedSideIndexForStyle = 0;
    private SideStyleSO _newSideStyle;

    private bool _cornerBodiesActive = true;
    private bool _cornerCapsActive = true;

    private bool _mansardWindowsActive = true;
    private bool _atticWindowsActive = true;

    private int _selectedSideIndexForMaterial = 0;
    private string[] _segmentTypes = { "Ground", "Middle" };
    private int _selectedSegmentTypeIndex = 0;
    private Material _newFacadeMaterial;
    private int _materialSlot = 0;

    private Material _newMansardMaterial;
    private Material _newAtticMaterial;
    private Material _newFlatMaterial;


    private void OnEnable()
    {
        _script = (BuildingInstanceDataManager)target;

        if (_script.elements.allMansardWindows != null && _script.elements.allMansardWindows.Count > 0 && _script.elements.allMansardWindows[0] != null)
            _mansardWindowsActive = _script.elements.allMansardWindows[0].activeSelf;
        if (_script.elements.allAtticWindows != null && _script.elements.allAtticWindows.Count > 0 && _script.elements.allAtticWindows[0] != null)
            _atticWindowsActive = _script.elements.allAtticWindows[0].activeSelf;

        if (_script.elements.allCornerBodies != null && _script.elements.allCornerBodies.Count > 0 && _script.elements.allCornerBodies[0] != null)
            _cornerBodiesActive = _script.elements.allCornerBodies[0].activeSelf;
        if (_script.elements.allCornerCaps != null && _script.elements.allCornerCaps.Count > 0 && _script.elements.allCornerCaps[0] != null)
            _cornerCapsActive = _script.elements.allCornerCaps[0].activeSelf;
    }

    public override void OnInspectorGUI()
    {
        _script = (BuildingInstanceDataManager)target;
        if (_script.elements.buildingRoot == null)
        {
            EditorGUILayout.HelpBox("Building data not fully initialized. Regenerate the building.", MessageType.Warning);
            return;
        }

        serializedObject.Update();
        Undo.RecordObject(_script, "Building Instance Data Change");


        EditorGUILayout.LabelField("Post-Generation Customization", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("These changes modify the current instance without full regeneration. For structural changes, use the PolygonBuildingGenerator.", MessageType.Info);

        EditorGUILayout.Space();

        // --- 1. Facade Style Per Side ---
        EditorGUILayout.LabelField("Facade Style Per Side", EditorStyles.boldLabel);
        int numSides = _script.sourceGenerator.vertexData.Count; // Use sourceGenerator for vertex count
        if (numSides > 0)
        {
            _selectedSideIndexForStyle = EditorGUILayout.IntSlider("Side Index", _selectedSideIndexForStyle, 0, numSides - 1);
            _newSideStyle = (SideStyleSO)EditorGUILayout.ObjectField("New Side Style", _newSideStyle, typeof(SideStyleSO), false);
            if (GUILayout.Button("Apply Style to Side " + _selectedSideIndexForStyle) && _newSideStyle != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(_script.gameObject, "Apply Side Style");
                _script.ApplySideStyle(_selectedSideIndexForStyle, _newSideStyle);
                EditorUtility.SetDirty(_script);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No sides defined in source generator.", MessageType.Info);
        }

        EditorGUILayout.Space();

        // --- 2. Corner Elements Visibility ---
        EditorGUILayout.LabelField("Corner Elements Visibility", EditorStyles.boldLabel);
        bool prevCornerBodiesActive = _cornerBodiesActive;
        _cornerBodiesActive = EditorGUILayout.Toggle("Corner Bodies Active", _cornerBodiesActive);
        if (prevCornerBodiesActive != _cornerBodiesActive)
        {
            // Record Undo for all affected corner bodies
            if (_script.elements.allCornerBodies != null)
            {
                foreach (var body in _script.elements.allCornerBodies) if (body != null) Undo.RecordObject(body, "Toggle Corner Bodies");
            }
            _script.SetAllCornerBodiesActive(_cornerBodiesActive);
            EditorUtility.SetDirty(_script); // Mark for save
        }

        bool prevCornerCapsActive = _cornerCapsActive;
        _cornerCapsActive = EditorGUILayout.Toggle("Corner Caps Active", _cornerCapsActive);
        if (prevCornerCapsActive != _cornerCapsActive)
        {
            // Record Undo for all affected corner caps
            if (_script.elements.allCornerCaps != null)
            {
                foreach (var cap in _script.elements.allCornerCaps) if (cap != null) Undo.RecordObject(cap, "Toggle Corner Caps");
            }
            _script.SetAllCornerCapsActive(_cornerCapsActive);
            EditorUtility.SetDirty(_script); // Mark for save
        }

        EditorGUILayout.Space();

        // --- 3. Roof Windows Visibility ---
        EditorGUILayout.LabelField("Roof Windows Visibility", EditorStyles.boldLabel);
        bool prevMansardActive = _mansardWindowsActive;
        _mansardWindowsActive = EditorGUILayout.Toggle("Mansard Windows Active", _mansardWindowsActive);
        if (prevMansardActive != _mansardWindowsActive)
        {
            if (_script.elements.allMansardWindows != null)
            {
                foreach (var window in _script.elements.allMansardWindows) if (window != null) Undo.RecordObject(window, "Toggle Mansard Windows");
            }
            _script.SetAllWindowsActive("Mansard", _mansardWindowsActive);
            EditorUtility.SetDirty(_script);
        }

        bool prevAtticActive = _atticWindowsActive;
        _atticWindowsActive = EditorGUILayout.Toggle("Attic Windows Active", _atticWindowsActive);
        if (prevAtticActive != _atticWindowsActive)
        {
            if (_script.elements.allAtticWindows != null)
            {
                foreach (var window in _script.elements.allAtticWindows) if (window != null) Undo.RecordObject(window, "Toggle Attic Windows");
            }
            _script.SetAllWindowsActive("Attic", _atticWindowsActive);
            EditorUtility.SetDirty(_script);
        }

        EditorGUILayout.Space();

        // --- 4. Facade Materials per side segment ---
        EditorGUILayout.LabelField("Facade Material Per Side Segment Type", EditorStyles.boldLabel);
        if (numSides > 0)
        {
            _selectedSideIndexForMaterial = EditorGUILayout.IntSlider("Side Index", _selectedSideIndexForMaterial, 0, numSides - 1);
            _selectedSegmentTypeIndex = EditorGUILayout.Popup("Segment Type", _selectedSegmentTypeIndex, _segmentTypes);
            _newFacadeMaterial = (Material)EditorGUILayout.ObjectField("New Material", _newFacadeMaterial, typeof(Material), false);
            _materialSlot = EditorGUILayout.IntField("Material Slot Index", _materialSlot);
            _materialSlot = Mathf.Max(0, _materialSlot); // Ensure non-negative

            if (GUILayout.Button($"Apply Material to Side {_selectedSideIndexForMaterial} {_segmentTypes[_selectedSegmentTypeIndex]} Segments") && _newFacadeMaterial != null)
            {
                // Record changes for all affected renderers
                SideElementGroup sideGroup = _script.elements.facadeElementsPerSide.Find(sg => sg.sideIndex == _selectedSideIndexForMaterial);
                if (sideGroup != null)
                {
                    List<GameObject> segmentsToChange = null;
                    if (_segmentTypes[_selectedSegmentTypeIndex].Equals("Ground", System.StringComparison.OrdinalIgnoreCase))
                        segmentsToChange = sideGroup.groundFacadeSegments;
                    else if (_segmentTypes[_selectedSegmentTypeIndex].Equals("Middle", System.StringComparison.OrdinalIgnoreCase))
                        segmentsToChange = sideGroup.middleFacadeSegments;

                    if (segmentsToChange != null)
                    {
                        foreach (var seg in segmentsToChange)
                        {
                            if (seg != null)
                            {
                                Renderer[] renderers = seg.GetComponentsInChildren<Renderer>();
                                Undo.RecordObjects(renderers, "Change Facade Material");
                            }
                        }
                    }
                }
                _script.ChangeFacadeMaterial(_selectedSideIndexForMaterial, _segmentTypes[_selectedSegmentTypeIndex], _newFacadeMaterial, _materialSlot);
                EditorUtility.SetDirty(_script);
            }
        }

        EditorGUILayout.Space();

        // --- 5. Roof Materials override ---
        EditorGUILayout.LabelField("Roof Materials Override", EditorStyles.boldLabel);
        _newMansardMaterial = (Material)EditorGUILayout.ObjectField("Mansard Roof Material", _newMansardMaterial, typeof(Material), false);
        if (GUILayout.Button("Apply Mansard Material") && _newMansardMaterial != null && _script.elements.mansardRoofMeshObject != null)
        {
            Undo.RecordObject(_script.elements.mansardRoofMeshObject.GetComponent<Renderer>(), "Change Mansard Material");
            _script.SetRoofMaterial("Mansard", _newMansardMaterial);
            EditorUtility.SetDirty(_script);
        }

        _newAtticMaterial = (Material)EditorGUILayout.ObjectField("Attic Roof Material", _newAtticMaterial, typeof(Material), false);
        if (GUILayout.Button("Apply Attic Material") && _newAtticMaterial != null && _script.elements.atticRoofMeshObject != null)
        {
            Undo.RecordObject(_script.elements.atticRoofMeshObject.GetComponent<Renderer>(), "Change Attic Material");
            _script.SetRoofMaterial("Attic", _newAtticMaterial);
            EditorUtility.SetDirty(_script);
        }

        _newFlatMaterial = (Material)EditorGUILayout.ObjectField("Flat Roof Material", _newFlatMaterial, typeof(Material), false);
        if (GUILayout.Button("Apply Flat Material") && _newFlatMaterial != null && _script.elements.flatRoofMeshObject != null)
        {
            Undo.RecordObject(_script.elements.flatRoofMeshObject.GetComponent<Renderer>(), "Change Flat Roof Material");
            _script.SetRoofMaterial("Flat", _newFlatMaterial);
            EditorUtility.SetDirty(_script);
        }

        EditorGUILayout.Space(20);
        if (GUILayout.Button("REGENERATE FULL BUILDING"))
        {
            Undo.RecordObject(_script.sourceGenerator.gameObject, "Regenerate Full Building"); // Record the generator's GO for Undo
            _script.sourceGenerator.GenerateBuilding();

            GUIUtility.ExitGUI();
        }

        if (GUI.changed)
        {
            EditorUtility.SetDirty(_script);
        }
    }
}
#endif