#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PolygonBuildingGenerator))]
public class PolygonBuildingGeneratorEditor : Editor
{
    private PolygonBuildingGenerator _targetScript;
    private Transform _targetTransform;

    // SerializedProperties
    private SerializedProperty _buildingStyleProp;
    private SerializedProperty _vertexDataProp;
    private SerializedProperty _sideDataProp;
    private SerializedProperty _middleFloorsProp;

    private const float HANDLE_SIZE_MULTIPLIER = 0.2f;
    private const float DEBUG_GIZMO_NORMAL_LENGTH = 1.5f;
    private const float HEIGHT_HANDLE_SIZE_MULTIPLIER = 0.8f;

    private void OnEnable()
    {
        _targetScript = (PolygonBuildingGenerator)target;
        if (_targetScript != null)
        {
            _targetTransform = _targetScript.transform;
            _buildingStyleProp = serializedObject.FindProperty("buildingStyle");
            _vertexDataProp = serializedObject.FindProperty("vertexData");
            _sideDataProp = serializedObject.FindProperty("sideData");
            _middleFloorsProp = serializedObject.FindProperty("middleFloors");

            _targetScript.SynchronizeSideData();
            if (!Application.isPlaying) EditorUtility.SetDirty(_targetScript);
        }
    }

    public override void OnInspectorGUI()
    {
        if (_targetScript == null)
        {
            EditorGUILayout.HelpBox("Target script is null.", MessageType.Error);
            DrawDefaultInspector();
            return;
        }

        serializedObject.Update();

        EditorGUILayout.PropertyField(_buildingStyleProp);
        if (_buildingStyleProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Assign a Building Style ScriptableObject.", MessageType.Warning);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Polygon Definition", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_vertexDataProp, true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Per-Side Style Overrides", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        if (_vertexDataProp != null && _sideDataProp != null && _vertexDataProp.arraySize != _sideDataProp.arraySize)
        {
            EditorGUILayout.HelpBox("Vertex/Side data count mismatch! Re-sync via tools or Generate.", MessageType.Warning);
        }
        if (_sideDataProp != null) EditorGUILayout.PropertyField(_sideDataProp, true);
        else EditorGUILayout.HelpBox("SideData property not found.", MessageType.Error);
        EditorGUI.indentLevel--;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Other Building Settings", EditorStyles.boldLabel);
        // Remove _debug* properties as they no longer exist on the target script
        string[] propertiesToExclude = {
            "m_Script", "buildingStyle", "vertexData", "sideData"
            // "_debugFlatRoofMesh", "_debugFlatRoofTransform", "_debugMansardMesh", (Removed)
            // "_debugMansardTransform", "_debugAtticMesh", "_debugAtticTransform" (Removed)
        };
        DrawPropertiesExcluding(serializedObject, propertiesToExclude);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Polygon Editing Tools", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Vertex"))
        {
            Undo.RecordObject(_targetScript, "Add Vertex");
            AddVertex();
            _targetScript.SynchronizeSideData();
            EditorUtility.SetDirty(_targetScript);
            RequestRegenerateAndRepaint();
        }
        EditorGUI.BeginDisabledGroup(_vertexDataProp == null || _vertexDataProp.arraySize <= 3);
        if (GUILayout.Button("Remove Last Vertex"))
        {
            Undo.RecordObject(_targetScript, "Remove Last Vertex");
            RemoveLastVertex();
            _targetScript.SynchronizeSideData();
            EditorUtility.SetDirty(_targetScript);
            RequestRegenerateAndRepaint();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate Building"))
        {
            Undo.RecordObject(_targetScript, "Snap All Vertices for Generate");
            SnapAllVertices();
            Undo.RecordObject(_targetScript.gameObject, "Generate Building Action (includes Clear)");
            _targetScript.GenerateBuilding();
            MarkSceneDirty();
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Clear Building"))
        {
            Undo.RecordObject(_targetScript.gameObject, "Clear Building Action");
            _targetScript.ClearBuilding();
            MarkSceneDirty();
            SceneView.RepaintAll();
        }

        if (serializedObject.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(_targetScript);
            SceneView.RepaintAll();
        }
    }

    private void RequestRegenerateAndRepaint()
    {
        if (_targetScript.buildingStyle != null)
        {
            _targetScript.GenerateBuilding();
        }
        SceneView.RepaintAll();
    }

    private void OnSceneGUI()
    {
        if (_targetScript == null || _vertexDataProp == null || _targetTransform == null || _middleFloorsProp == null)
        {
            OnEnable();
            if (_targetScript == null || _vertexDataProp == null || _targetTransform == null || _middleFloorsProp == null) return;
        }

        Event e = Event.current;
        bool changedByHandle = false;

        int vertexCount = _vertexDataProp.arraySize;
        Vector3[] worldVertices = new Vector3[vertexCount];

        if (vertexCount > 0)
        {
            for (int i = 0; i < vertexCount; i++)
            {
                SerializedProperty vertexElementProp = _vertexDataProp.GetArrayElementAtIndex(i);
                SerializedProperty positionProp = vertexElementProp.FindPropertyRelative("position");
                SerializedProperty cornerFlagProp = vertexElementProp.FindPropertyRelative("addCornerElement");

                Vector3 localPos = positionProp.vector3Value;
                worldVertices[i] = _targetTransform.TransformPoint(localPos);

                Handles.color = Color.white;
                Vector3 indexLabelOffset = (SceneView.currentDrawingSceneView.camera.transform.right * 0.1f) + (SceneView.currentDrawingSceneView.camera.transform.up * 0.1f);
                Handles.Label(worldVertices[i] + indexLabelOffset * HandleUtility.GetHandleSize(worldVertices[i]), $"V{i}");

                Handles.color = cornerFlagProp.boolValue ? Color.magenta : Color.cyan;
                float handleSize = HandleUtility.GetHandleSize(worldVertices[i]) * HANDLE_SIZE_MULTIPLIER;

                EditorGUI.BeginChangeCheck();
                Vector3 newWorldPos = Handles.FreeMoveHandle(worldVertices[i], handleSize, Vector3.one * 0.5f, Handles.SphereHandleCap);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_targetScript, "Move Polygon Vertex");
                    Vector3 newLocalPos = _targetTransform.InverseTransformPoint(newWorldPos);
                    Vector3 snappedLocalPos = _targetScript.SnapVertexPosition(newLocalPos);
                    positionProp.vector3Value = snappedLocalPos;
                    changedByHandle = true;
                }
            }

            Handles.color = Color.yellow;
            if (vertexCount > 1)
            {
                Handles.DrawPolyLine(worldVertices);
                Handles.DrawLine(worldVertices[vertexCount - 1], worldVertices[0]);
            }
        }

        if (vertexCount > 0)
        {
            for (int i = 0; i < vertexCount; i++)
            {
                DrawSideInfo(i, worldVertices);
                if (vertexCount >= 3)
                {
                    DrawVertexAngleInfo(i, worldVertices);
                }
            }
        }

        if (vertexCount >= 3 && _targetScript.floorHeight > GeometryUtils.Epsilon)
        {
            if (DrawHeightAdjustmentHandle(worldVertices)) changedByHandle = true;
        }

        if (changedByHandle)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(_targetScript);
            if (e.type == EventType.Used || e.type == EventType.MouseUp)
            {
                RequestRegenerateAndRepaint();
            }
        }

        // Removed: DrawRoofOutlines();
    }

    private void AddVertex()
    {
        if (_vertexDataProp == null) return;
        int currentSize = _vertexDataProp.arraySize;
        _vertexDataProp.InsertArrayElementAtIndex(currentSize);
        SerializedProperty newVertexProp = _vertexDataProp.GetArrayElementAtIndex(currentSize);
        SerializedProperty newPosProp = newVertexProp.FindPropertyRelative("position");
        SerializedProperty newCornerFlagProp = newVertexProp.FindPropertyRelative("addCornerElement");

        Vector3 newVertexPos = Vector3.zero;
        if (currentSize >= 2)
        {
            Vector3 lastPos = _vertexDataProp.GetArrayElementAtIndex(currentSize - 1).FindPropertyRelative("position").vector3Value;
            Vector3 firstPos = _vertexDataProp.GetArrayElementAtIndex(0).FindPropertyRelative("position").vector3Value;
            newVertexPos = (lastPos + firstPos) / 2f;
            Vector3 offsetDir = (Quaternion.Euler(0, 90, 0) * (firstPos - lastPos).normalized) * (_targetScript.vertexSnapSize * 0.5f);
            newVertexPos += offsetDir;
        }
        else if (currentSize == 1)
        {
            Vector3 existingPos = _vertexDataProp.GetArrayElementAtIndex(0).FindPropertyRelative("position").vector3Value;
            newVertexPos = existingPos + Vector3.forward * (_targetScript.vertexSnapSize * 2f);
        }
        newPosProp.vector3Value = _targetScript.SnapVertexPosition(newVertexPos);
        newCornerFlagProp.boolValue = true;
    }

    private void RemoveLastVertex()
    {
        if (_vertexDataProp == null || _vertexDataProp.arraySize <= 0) return;
        _vertexDataProp.DeleteArrayElementAtIndex(_vertexDataProp.arraySize - 1);
    }

    private void SnapAllVertices()
    {
        if (_vertexDataProp == null) return;
        for (int i = 0; i < _vertexDataProp.arraySize; i++)
        {
            SerializedProperty posProp = _vertexDataProp.GetArrayElementAtIndex(i).FindPropertyRelative("position");
            posProp.vector3Value = _targetScript.SnapVertexPosition(posProp.vector3Value);
        }
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSideInfo(int index, Vector3[] worldVertices)
    {
        if (_targetScript == null || _targetScript.vertexData == null || worldVertices == null ||
            index >= _targetScript.vertexData.Count || index >= worldVertices.Length || _targetScript.vertexData.Count < 2) return;

        int nextIndex = (index + 1) % worldVertices.Length;
        if (nextIndex >= worldVertices.Length) return;

        Vector3 p1_world = worldVertices[index];
        Vector3 p2_world = worldVertices[nextIndex];
        Vector3 midpoint_world = (p1_world + p2_world) / 2f;

        Vector3 p1_local = _targetScript.vertexData[index].position;
        int p2_local_idx = (index + 1) % _targetScript.vertexData.Count;
        Vector3 p2_local = _targetScript.vertexData[p2_local_idx].position;

        if ((p2_local - p1_local).sqrMagnitude > GeometryUtils.Epsilon * GeometryUtils.Epsilon)
        {
            Vector3 sideNormal_local = PolygonGeometry.CalculateSideNormal(p1_local, p2_local, _targetScript.vertexData);
            Vector3 sideNormal_world = _targetTransform.TransformDirection(sideNormal_local);

            Handles.color = Color.green;
            Handles.DrawLine(midpoint_world, midpoint_world + sideNormal_world * DEBUG_GIZMO_NORMAL_LENGTH);
        }
    }

    private void DrawVertexAngleInfo(int vertexIndex, Vector3[] worldVertices)
    {
        int count = worldVertices.Length;
        if (count < 3) return;

        Vector3 p_curr_world = worldVertices[vertexIndex];
        Vector3 p_prev_world = worldVertices[(vertexIndex + count - 1) % count];
        Vector3 p_next_world = worldVertices[(vertexIndex + 1) % count];

        Vector3 edge1_world = (p_prev_world - p_curr_world);
        Vector3 edge2_world = (p_next_world - p_curr_world);

        edge1_world.y = 0;
        edge2_world.y = 0;

        if (edge1_world.sqrMagnitude < GeometryUtils.Epsilon || edge2_world.sqrMagnitude < GeometryUtils.Epsilon) return;

        float angle = Vector3.Angle(edge1_world.normalized, edge2_world.normalized);

        Vector3 angleLabelOffsetDir_world = (edge1_world.normalized + edge2_world.normalized);
        if (angleLabelOffsetDir_world.sqrMagnitude < GeometryUtils.Epsilon)
        {
            angleLabelOffsetDir_world = Quaternion.Euler(0, 90, 0) * edge1_world.normalized;
        }

        Vector3 angleLabelOffset = angleLabelOffsetDir_world.normalized * 0.4f * HandleUtility.GetHandleSize(p_curr_world);

        Handles.color = Color.white;
        Handles.Label(p_curr_world + angleLabelOffset, $"{angle:F1}°");
    }

    private bool DrawHeightAdjustmentHandle(Vector3[] worldPolygonBaseVertices)
    {
        if (worldPolygonBaseVertices.Length < 3 || _targetScript.floorHeight <= GeometryUtils.Epsilon) return false;
        bool changed = false;

        Vector3 baseCenter_world = Vector3.zero;
        foreach (Vector3 v_world in worldPolygonBaseVertices) baseCenter_world += v_world;
        baseCenter_world /= worldPolygonBaseVertices.Length;

        float currentMainWallHeight_local = (_middleFloorsProp.intValue + 1) * _targetScript.floorHeight;
        Vector3 baseCenter_local = _targetTransform.InverseTransformPoint(baseCenter_world);
        Vector3 handlePosition_world = _targetTransform.TransformPoint(new Vector3(baseCenter_local.x, currentMainWallHeight_local, baseCenter_local.z));

        float handleDrawSize = HandleUtility.GetHandleSize(handlePosition_world) * HEIGHT_HANDLE_SIZE_MULTIPLIER;

        Handles.color = Color.cyan;

        EditorGUI.BeginChangeCheck();
        Vector3 newHandlePosition_world = Handles.Slider(handlePosition_world, _targetTransform.up, handleDrawSize, Handles.ConeHandleCap, 0.05f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_targetScript, "Adjust Building Height");
            float localTargetHeight = _targetTransform.InverseTransformPoint(newHandlePosition_world).y;

            int newTotalFloors = Mathf.Max(1, Mathf.RoundToInt(localTargetHeight / _targetScript.floorHeight));
            int newMiddleFloors = Mathf.Max(0, newTotalFloors - 1);

            if (newMiddleFloors != _middleFloorsProp.intValue)
            {
                _middleFloorsProp.intValue = newMiddleFloors;
                changed = true;
            }
        }

        int totalFloors = _middleFloorsProp.intValue + 1;
        Handles.Label(handlePosition_world + _targetTransform.right * 0.2f * handleDrawSize, $"Floors: {totalFloors}");
        return changed;
    }

    // Removed: DrawRoofOutlines, DrawMeshPerimeter, DrawStripMeshOutlines methods

    private void MarkSceneDirty()
    {
        if (!Application.isPlaying && _targetScript.gameObject.scene.IsValid())
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(_targetScript.gameObject.scene);
        }
    }
}
#endif // UNITY_EDITOR