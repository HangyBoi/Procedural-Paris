// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
// This script provides a custom editor for the PolygonBuildingGenerator component in Unity, allowing users to edit polygon vertices, generate buildings, and visualize debug information in the scene view.
//

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for PolygonBuildingGenerator. Provides polygon editing, building generation, and debug visualization in the Unity Editor.
/// </summary>
[CustomEditor(typeof(PolygonBuildingGenerator))]
public class PolygonBuildingGeneratorEditor : Editor
{
    private PolygonBuildingGenerator _targetScript;
    private Transform _targetTransform;

    // Serialized properties for inspector fields
    private SerializedProperty _buildingStyleProp;
    private SerializedProperty _vertexDataProp;
    private SerializedProperty _sideDataProp;
    private SerializedProperty _middleFloorsProp;

    // Persisted state for debug gizmo visibility
    private static bool _showDebugInfo;
    private const string ShowDebugInfoKey = "PolygonBuildingGeneratorEditor_ShowDebugInfo";

    // Constants for gizmo and handle styling
    private const float HANDLE_SIZE_MULTIPLIER = 0.2f;
    private const float DEBUG_GIZMO_NORMAL_LENGTH = 1.5f;
    private const float HEIGHT_HANDLE_SIZE_MULTIPLIER = 0.8f;

    /// <summary>
    /// Initializes references and synchronizes data when the editor is enabled.
    /// </summary>
    private void OnEnable()
    {
        _targetScript = (PolygonBuildingGenerator)target;
        if (_targetScript == null) return;

        _targetTransform = _targetScript.transform;

        // Cache serialized properties for performance and Undo/Redo functionality
        _buildingStyleProp = serializedObject.FindProperty("buildingStyle");
        _vertexDataProp = serializedObject.FindProperty("vertexData");
        _sideDataProp = serializedObject.FindProperty("sideData");
        _middleFloorsProp = serializedObject.FindProperty("middleFloors");

        // Load user preference for gizmo visibility
        _showDebugInfo = EditorPrefs.GetBool(ShowDebugInfoKey, true);

        // Ensure data consistency on selection
        _targetScript.SynchronizeSideData();
        if (!Application.isPlaying) EditorUtility.SetDirty(_targetScript);
    }

    /// <summary>
    /// Draws the custom inspector GUI for the building generator.
    /// </summary>
    public override void OnInspectorGUI()
    {
        if (_targetScript == null) return;

        // Update serializedObject to reflect the latest state of the target
        serializedObject.Update();

        DrawMainSettings();
        DrawPolygonTools();
        DrawGenerationControls();

        // Apply any modified properties and repaint scene views if needed
        if (serializedObject.ApplyModifiedProperties())
        {
            SceneView.RepaintAll();
        }
    }

    /// <summary>
    /// Draws the primary settings section in the inspector.
    /// </summary>
    private void DrawMainSettings()
    {
        EditorGUILayout.PropertyField(_buildingStyleProp);
        if (_buildingStyleProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("A Building Style is required for generation.", MessageType.Warning);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Polygon & Side Data", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_vertexDataProp, true);
        EditorGUILayout.PropertyField(_sideDataProp, true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Building Settings", EditorStyles.boldLabel);
        // Draw all other properties automatically, excluding those handled manually
        string[] propertiesToExclude = { "m_Script", "buildingStyle", "vertexData", "sideData" };
        DrawPropertiesExcluding(serializedObject, propertiesToExclude);
    }

    /// <summary>
    /// Draws polygon editing tools, including vertex management and gizmo toggles.
    /// </summary>
    private void DrawPolygonTools()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Editing Tools", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        _showDebugInfo = EditorGUILayout.Toggle("Show Debug Gizmos", _showDebugInfo);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetBool(ShowDebugInfoKey, _showDebugInfo); // Save preference
            SceneView.RepaintAll();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Vertex"))
        {
            Undo.RecordObject(_targetScript, "Add Vertex");
            AddVertex();
            RequestRegenerateAndRepaint();
        }

        EditorGUI.BeginDisabledGroup(_vertexDataProp.arraySize <= 3);
        if (GUILayout.Button("Remove Last Vertex"))
        {
            Undo.RecordObject(_targetScript, "Remove Last Vertex");
            RemoveLastVertex();
            RequestRegenerateAndRepaint();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Draws building generation and clearing controls.
    /// </summary>
    private void DrawGenerationControls()
    {
        EditorGUILayout.Space(10);
        if (GUILayout.Button("Generate Building", GUILayout.Height(30)))
        {
            Undo.RecordObject(_targetScript, "Snap All Vertices");
            SnapAllVertices();
            Undo.RecordObject(_targetScript.gameObject, "Generate Building");
            _targetScript.GenerateBuilding();
            MarkSceneDirty();
        }
        if (GUILayout.Button("Clear Building"))
        {
            Undo.RecordObject(_targetScript.gameObject, "Clear Building");
            _targetScript.ClearBuilding();
            MarkSceneDirty();
        }
    }

    /// <summary>
    /// Handles scene view drawing and interaction for polygon editing.
    /// </summary>
    private void OnSceneGUI()
    {
        if (_targetScript == null || _vertexDataProp == null) return;

        bool changedByHandle = false;
        int vertexCount = _vertexDataProp.arraySize;
        if (vertexCount == 0) return;

        // Pre-calculate all vertex positions in world space for efficiency
        Vector3[] worldVertices = new Vector3[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            worldVertices[i] = _targetTransform.TransformPoint(_targetScript.vertexData[i].position);
        }

        // 1. Draw interactive handles for each vertex
        for (int i = 0; i < vertexCount; i++)
        {
            changedByHandle |= DrawVertexHandle(i, ref worldVertices[i]);
        }

        // 2. Draw the visual outline of the polygon
        Handles.color = Color.yellow;
        if (vertexCount > 1)
        {
            Handles.DrawPolyLine(worldVertices);
            Handles.DrawLine(worldVertices[vertexCount - 1], worldVertices[0]);
        }

        // 3. Draw optional debug information if enabled
        if (_showDebugInfo)
        {
            for (int i = 0; i < vertexCount; i++)
            {
                DrawSideInfo(i, worldVertices);
                if (vertexCount >= 3) DrawVertexAngleInfo(i, worldVertices);
            }
        }

        // 4. Draw the handle for adjusting building height
        if (vertexCount >= 3)
        {
            changedByHandle |= DrawHeightAdjustmentHandle(worldVertices);
        }

        // Apply changes and request a repaint if a handle was moved
        if (changedByHandle)
        {
            serializedObject.ApplyModifiedProperties();
            // Wait for mouse release to trigger full regeneration to avoid lag during dragging
            if (Event.current.type == EventType.MouseUp)
            {
                RequestRegenerateAndRepaint();
            }
        }
    }

    /// <summary>
    /// Draws a handle for a polygon vertex and updates its position if moved.
    /// </summary>
    private bool DrawVertexHandle(int index, ref Vector3 worldPos)
    {
        SerializedProperty vertexElementProp = _vertexDataProp.GetArrayElementAtIndex(index);
        SerializedProperty positionProp = vertexElementProp.FindPropertyRelative("position");
        SerializedProperty cornerFlagProp = vertexElementProp.FindPropertyRelative("addCornerElement");

        float handleSize = HandleUtility.GetHandleSize(worldPos) * HANDLE_SIZE_MULTIPLIER;
        // Color the handle based on whether it will generate a corner element
        Handles.color = cornerFlagProp.boolValue ? Color.magenta : Color.cyan;

        if (_showDebugInfo)
        {
            Handles.Label(worldPos + Vector3.up * handleSize * 1.5f, $"V{index}");
        }

        EditorGUI.BeginChangeCheck();
        Vector3 newWorldPos = Handles.FreeMoveHandle(worldPos, handleSize, Vector3.one * 0.5f, Handles.SphereHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_targetScript, "Move Polygon Vertex");
            // Convert back to local space and snap before storing
            positionProp.vector3Value = _targetScript.SnapVertexPosition(_targetTransform.InverseTransformPoint(newWorldPos));
            return true;
        }
        return false;
    }

    /// <summary>
    /// Adds a new vertex to the polygon, positioning it intelligently.
    /// </summary>
    private void AddVertex()
    {
        int currentSize = _vertexDataProp.arraySize;
        _vertexDataProp.InsertArrayElementAtIndex(currentSize);
        SerializedProperty newVertexProp = _vertexDataProp.GetArrayElementAtIndex(currentSize);
        newVertexProp.FindPropertyRelative("position").vector3Value = GetNewVertexPosition(currentSize);
        newVertexProp.FindPropertyRelative("addCornerElement").boolValue = true;
        _targetScript.SynchronizeSideData();
    }

    /// <summary>
    /// Calculates an intelligent position for a new vertex.
    /// </summary>
    private Vector3 GetNewVertexPosition(int currentSize)
    {
        Vector3 newVertexPos = Vector3.zero;
        if (currentSize >= 2) // Place between the last and first vertex
        {
            Vector3 lastPos = _vertexDataProp.GetArrayElementAtIndex(currentSize - 1).FindPropertyRelative("position").vector3Value;
            Vector3 firstPos = _vertexDataProp.GetArrayElementAtIndex(0).FindPropertyRelative("position").vector3Value;
            newVertexPos = (lastPos + firstPos) / 2f;
            Vector3 offsetDir = (Quaternion.Euler(0, 90, 0) * (firstPos - lastPos).normalized) * _targetScript.vertexSnapSize;
            newVertexPos += offsetDir;
        }
        else if (currentSize == 1) // Place relative to the first vertex
        {
            Vector3 existingPos = _vertexDataProp.GetArrayElementAtIndex(0).FindPropertyRelative("position").vector3Value;
            newVertexPos = existingPos + Vector3.forward * (_targetScript.vertexSnapSize * 2f);
        }
        return _targetScript.SnapVertexPosition(newVertexPos);
    }

    /// <summary>
    /// Removes the last vertex from the polygon.
    /// </summary>
    private void RemoveLastVertex()
    {
        if (_vertexDataProp.arraySize > 0)
        {
            _vertexDataProp.DeleteArrayElementAtIndex(_vertexDataProp.arraySize - 1);
            _targetScript.SynchronizeSideData();
        }
    }

    /// <summary>
    /// Regenerates the building if a style is present and repaints the scene view.
    /// </summary>
    private void RequestRegenerateAndRepaint()
    {
        if (_targetScript.buildingStyle != null)
        {
            _targetScript.GenerateBuilding();
        }
        SceneView.RepaintAll();
    }

    /// <summary>
    /// Snaps all polygon vertices to the defined grid size.
    /// </summary>
    private void SnapAllVertices()
    {
        for (int i = 0; i < _vertexDataProp.arraySize; i++)
        {
            SerializedProperty posProp = _vertexDataProp.GetArrayElementAtIndex(i).FindPropertyRelative("position");
            posProp.vector3Value = _targetScript.SnapVertexPosition(posProp.vector3Value);
        }
        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Draws a normal gizmo for a polygon side in the scene view.
    /// </summary>
    private void DrawSideInfo(int index, Vector3[] worldVertices)
    {
        int nextIndex = (index + 1) % worldVertices.Length;
        Vector3 p1_world = worldVertices[index];
        Vector3 p2_world = worldVertices[nextIndex];
        Vector3 midpoint_world = (p1_world + p2_world) / 2f;

        Vector3 p1_local = _targetScript.vertexData[index].position;
        Vector3 p2_local = _targetScript.vertexData[nextIndex].position;

        if ((p2_local - p1_local).sqrMagnitude > 1e-6f)
        {
            Vector3 sideNormal_local = BuildingFootprintUtils.CalculateSideNormal(p1_local, p2_local, _targetScript.vertexData);
            Vector3 sideNormal_world = _targetTransform.TransformDirection(sideNormal_local);

            Handles.color = Color.green;
            Handles.DrawLine(midpoint_world, midpoint_world + sideNormal_world * DEBUG_GIZMO_NORMAL_LENGTH);
        }
    }

    /// <summary>
    /// Draws the angle at a polygon vertex in the scene view.
    /// </summary>
    private void DrawVertexAngleInfo(int vertexIndex, Vector3[] worldVertices)
    {
        int count = worldVertices.Length;
        Vector3 p_curr_world = worldVertices[vertexIndex];
        Vector3 p_prev_world = worldVertices[(vertexIndex + count - 1) % count];
        Vector3 p_next_world = worldVertices[(vertexIndex + 1) % count];

        // Flatten vectors to 2D plane for angle calculation
        Vector3 edge1 = p_prev_world - p_curr_world;
        Vector3 edge2 = p_next_world - p_curr_world;
        edge1.y = 0;
        edge2.y = 0;

        if (edge1.sqrMagnitude < 1e-6f || edge2.sqrMagnitude < 1e-6f) return;

        float angle = Vector3.Angle(edge1, edge2);
        Vector3 angleLabelOffset = (edge1.normalized + edge2.normalized).normalized * 0.5f * HandleUtility.GetHandleSize(p_curr_world);

        Handles.color = Color.white;
        Handles.Label(p_curr_world + angleLabelOffset, $"{angle:F1}°");
    }

    /// <summary>
    /// Draws a handle for adjusting building height (number of floors) in the scene view.
    /// </summary>
    private bool DrawHeightAdjustmentHandle(Vector3[] worldPolygonBaseVertices)
    {
        // Find the polygon's 2D center
        Vector3 baseCenter_world = Vector3.zero;
        foreach (Vector3 v_world in worldPolygonBaseVertices) baseCenter_world += v_world;
        baseCenter_world /= worldPolygonBaseVertices.Length;

        // Position the handle at the center plus the current total building height
        float currentHeight_local = (_middleFloorsProp.intValue + 1) * _targetScript.floorHeight;
        Vector3 handlePosition_world = _targetTransform.TransformPoint(new Vector3(0, currentHeight_local, 0)) + baseCenter_world;

        float handleDrawSize = HandleUtility.GetHandleSize(handlePosition_world) * HEIGHT_HANDLE_SIZE_MULTIPLIER;
        Handles.color = Color.cyan;

        EditorGUI.BeginChangeCheck();
        Vector3 newHandlePosition_world = Handles.Slider(handlePosition_world, _targetTransform.up, handleDrawSize, Handles.ConeHandleCap, 0.05f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_targetScript, "Adjust Building Height");

            // Convert the handle's new world height back into a number of floors
            float localTargetHeight = _targetTransform.InverseTransformPoint(newHandlePosition_world).y;
            int newMiddleFloors = Mathf.Max(0, Mathf.RoundToInt(localTargetHeight / _targetScript.floorHeight) - 1);

            if (newMiddleFloors != _middleFloorsProp.intValue)
            {
                _middleFloorsProp.intValue = newMiddleFloors;
                return true;
            }
        }

        Handles.Label(handlePosition_world + _targetTransform.right * 0.2f * handleDrawSize, $"Floors: {_middleFloorsProp.intValue + 1}");
        return false;
    }

    /// <summary>
    /// Marks the active scene as dirty to ensure changes are saved.
    /// </summary>
    private void MarkSceneDirty()
    {
        if (!Application.isPlaying && _targetScript.gameObject.scene.IsValid())
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(_targetScript.gameObject.scene);
        }
        SceneView.RepaintAll();
    }
}
#endif