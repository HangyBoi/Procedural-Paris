#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic; // Keep this if PolygonVertexData or PolygonSideData are defined in a namespace that needs it.

[CustomEditor(typeof(PolygonBuildingGeneratorMain))] // Correctly targets PolygonBuildingGeneratorMain
public class PolygonBuildingGeneratorMainEditor : Editor
{
    // Change the type of _targetScript to PolygonBuildingGeneratorMain
    private PolygonBuildingGeneratorMain _targetScript;
    private Transform _targetTransform;
    private SerializedProperty _vertexDataProp;
    private SerializedProperty _sideDataProp;

    private const float HANDLE_SIZE_MULTIPLIER = 0.2f;

    private void OnEnable()
    {
        // Cast to the correct type: PolygonBuildingGeneratorMain
        _targetScript = (PolygonBuildingGeneratorMain)target;
        if (_targetScript != null) // Add a null check
        {
            _targetTransform = _targetScript.transform;
            _vertexDataProp = serializedObject.FindProperty("vertexData");
            _sideDataProp = serializedObject.FindProperty("sideData");
            _targetScript.SynchronizeSideData(); // Ensure this is called
            if (!Application.isPlaying) EditorUtility.SetDirty(_targetScript);
        }
    }

    public override void OnInspectorGUI()
    {
        if (_targetScript == null)
        {
            EditorGUILayout.HelpBox("Target script is null. This can happen if the script was removed or during recompilation.", MessageType.Error);
            DrawDefaultInspector(); // Draw default inspector as a fallback
            return;
        }

        serializedObject.Update();

        string[] propertiesToExclude = new string[] {
            "m_Script", "vertexData", "sideData", // Add any fields that are handled by custom debug drawing below
            "_debugFlatRoofMesh", "_debugFlatRoofTransform" // These are for debug, not direct editing
        };
        DrawPropertiesExcluding(serializedObject, propertiesToExclude);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Polygon Data", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_vertexDataProp, true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Per-Side Style Overrides", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        if (_vertexDataProp != null && _sideDataProp != null && _vertexDataProp.arraySize != _sideDataProp.arraySize)
        {
            EditorGUILayout.HelpBox("Vertex and Side data count mismatch! Please click 'Generate Building' or 'Clear Building', or manually adjust vertex count to re-sync.", MessageType.Warning);
        }

        if (_sideDataProp != null) // Check if sideDataProp is found
        {
            EditorGUILayout.PropertyField(_sideDataProp, true);
        }
        else
        {
            EditorGUILayout.HelpBox("SideData property not found. This might indicate an issue with the script's serialization or variable names.", MessageType.Error);
        }
        EditorGUI.indentLevel--;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Polygon Editing Tools", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Vertex"))
        {
            Undo.RecordObject(_targetScript, "Add Vertex");
            AddVertex();
            _targetScript.SynchronizeSideData(); // Ensure sync after adding
            serializedObject.ApplyModifiedProperties(); // Apply changes before next step
            EditorUtility.SetDirty(_targetScript);
            SceneView.RepaintAll();
        }
        EditorGUI.BeginDisabledGroup(_vertexDataProp == null || _vertexDataProp.arraySize <= 3);
        if (GUILayout.Button("Remove Last Vertex"))
        {
            Undo.RecordObject(_targetScript, "Remove Last Vertex");
            RemoveLastVertex();
            _targetScript.SynchronizeSideData(); // Ensure sync after removing
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(_targetScript);
            SceneView.RepaintAll();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate Building"))
        {
            Undo.RecordObject(_targetScript, "Snap All Vertices for Generate");
            SnapAllVertices();
            // No need to record _targetScript.gameObject again if ClearBuilding/GenerateBuilding handles its children.
            // GenerateBuilding creates a new root, so recording the old one for destroy is fine.
            _targetScript.GenerateBuilding(); // This will also call ClearBuilding first
            SceneView.RepaintAll();
            if (!Application.isPlaying && _targetScript.gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(_targetScript.gameObject.scene);
        }
        if (GUILayout.Button("Clear Building"))
        {
            // ClearBuilding destroys children, which is an operation on the GameObject itself and its hierarchy.
            // Recording _targetScript.gameObject is appropriate here.
            Undo.RecordObject(_targetScript.gameObject, "Clear Building Action"); // More specific undo name
            _targetScript.ClearBuilding();
            SceneView.RepaintAll();
            if (!Application.isPlaying && _targetScript.gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(_targetScript.gameObject.scene);
        }

        if (serializedObject.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(_targetScript);
            // Optionally call SynchronizeSideData again if property changes might affect it,
            // though vertexData changes are handled by Add/Remove buttons.
            // _targetScript.SynchronizeSideData();
            SceneView.RepaintAll();
        }
    }


    private void OnSceneGUI()
    {
        if (_targetScript == null || _vertexDataProp == null) return;

        // Ensure _targetTransform is valid, especially after recompiles or if the target is destroyed
        if (_targetScript.transform == null)
        {
            _targetTransform = _targetScript.GetComponent<Transform>(); // Attempt to re-acquire
            if (_targetTransform == null) return; // Still null, can't proceed
        }
        else if (_targetTransform != _targetScript.transform) // Check if it's still the correct one
        {
            _targetTransform = _targetScript.transform;
        }


        Quaternion handleRotation = Tools.pivotRotation == PivotRotation.Local ? _targetTransform.rotation : Quaternion.identity;
        Handles.color = Color.yellow;
        Vector3[] worldVertices = new Vector3[_vertexDataProp.arraySize];

        bool changedByHandle = false;

        for (int i = 0; i < _vertexDataProp.arraySize; i++)
        {
            SerializedProperty vertexElementProp = _vertexDataProp.GetArrayElementAtIndex(i);
            SerializedProperty positionProp = vertexElementProp.FindPropertyRelative("position");
            SerializedProperty cornerFlagProp = vertexElementProp.FindPropertyRelative("addCornerElement");

            Vector3 localPos = positionProp.vector3Value;
            worldVertices[i] = _targetTransform.TransformPoint(localPos);

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
                // Apply properties immediately so the script's internal state is updated
                // before a potential GenerateBuilding call.
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(_targetScript);
                changedByHandle = true; // Mark that a handle moved
            }
            DrawSideInfo(i, worldVertices);
        }

        Handles.color = Color.yellow;
        if (worldVertices.Length > 1)
        {
            Handles.DrawPolyLine(worldVertices);
            if (worldVertices.Length > 0) // Added check
            {
                Handles.DrawLine(worldVertices[worldVertices.Length - 1], worldVertices[0]);
            }
        }

        // If a handle was moved, and it's a 'used' event (like mouse release after drag), regenerate.
        if (changedByHandle && Event.current.type == EventType.Used)
        {
            if (_targetScript != null) // Final null check before calling generate
            {
                // Consider if real-time generation on every drag is too slow.
                // Current logic triggers on mouse up after drag completes.
                _targetScript.GenerateBuilding();
                SceneView.RepaintAll(); // Repaint after generation
            }
        }


        // Roof Debug Gizmos - ONLY for Flat Roof
        DrawMeshNormalsGizmos(_targetScript._debugFlatRoofMesh, _targetScript._debugFlatRoofTransform, Color.yellow);
    }

    // DrawRoofEdgeGizmos is no longer needed as flat roof perimeter is drawn by the vertex loop
    // and its mesh normals by DrawMeshNormalsGizmos.

    private void DrawMeshNormalsGizmos(Mesh mesh, Transform meshTransform, Color color)
    {
        // Add extra null checks for safety, especially for meshTransform which might become null
        if (mesh == null || meshTransform == null || mesh.vertexCount == 0 || mesh.normals == null || mesh.normals.Length == 0) return;

        Handles.color = color;
        Vector3[] verts = mesh.vertices;
        Vector3[] normals = mesh.normals;
        float gizmoLength = 0.3f;

        for (int i = 0; i < verts.Length; i++)
        {
            if (i >= normals.Length) continue; // Should not happen with valid mesh data
            Vector3 worldPos = meshTransform.TransformPoint(verts[i]);
            Vector3 worldNormal = meshTransform.TransformDirection(normals[i]);
            Handles.DrawLine(worldPos, worldPos + worldNormal * gizmoLength);
        }
    }

    private void AddVertex()
    {
        if (_vertexDataProp == null) return;

        int currentSize = _vertexDataProp.arraySize;
        _vertexDataProp.arraySize++;
        SerializedProperty newVertexProp = _vertexDataProp.GetArrayElementAtIndex(currentSize); // Index is old size
        SerializedProperty newPosProp = newVertexProp.FindPropertyRelative("position");
        SerializedProperty newCornerFlagProp = newVertexProp.FindPropertyRelative("addCornerElement");

        Vector3 newVertexPos = Vector3.zero;

        if (currentSize >= 2) // Need at least 2 existing vertices to interpolate
        {
            // Place new vertex between the last and first of the *original* loop before adding
            Vector3 lastPos = _vertexDataProp.GetArrayElementAtIndex(currentSize - 1).FindPropertyRelative("position").vector3Value;
            Vector3 firstPos = _vertexDataProp.GetArrayElementAtIndex(0).FindPropertyRelative("position").vector3Value;
            newVertexPos = (lastPos + firstPos) / 2f;
            // Optional: Offset slightly to avoid perfect co-linearity initially
            Vector3 offsetDir = (Quaternion.Euler(0, 90, 0) * (firstPos - lastPos).normalized) * (_targetScript != null ? _targetScript.vertexSnapSize * 0.5f : 0.5f);
            newVertexPos += offsetDir;

        }
        else if (currentSize == 1)
        {
            Vector3 existingPos = _vertexDataProp.GetArrayElementAtIndex(0).FindPropertyRelative("position").vector3Value;
            newVertexPos = existingPos + Vector3.forward * (_targetScript != null ? _targetScript.vertexSnapSize * 2 : 2.0f);
        }
        // If currentSize is 0, newVertexPos remains Vector3.zero, which is fine.

        newPosProp.vector3Value = (_targetScript != null ? _targetScript.SnapVertexPosition(newVertexPos) : newVertexPos);
        newCornerFlagProp.boolValue = true; // Default new vertices to have corners enabled

        // SynchronizeSideData is called by the GUI button action after this
    }

    private void RemoveLastVertex()
    {
        if (_vertexDataProp == null) return;
        if (_vertexDataProp.arraySize > 0) // Keep minimum 0, though generator needs 3
        {
            _vertexDataProp.arraySize--;
        }
        // SynchronizeSideData is called by the GUI button action after this
    }

    private void SnapAllVertices()
    {
        if (_vertexDataProp == null || _targetScript == null) return;
        for (int i = 0; i < _vertexDataProp.arraySize; i++)
        {
            SerializedProperty posProp = _vertexDataProp.GetArrayElementAtIndex(i).FindPropertyRelative("position");
            posProp.vector3Value = _targetScript.SnapVertexPosition(posProp.vector3Value);
        }
        // ApplyModifiedProperties is called in OnInspectorGUI after this
    }

    private void DrawSideInfo(int index, Vector3[] worldVertices)
    {
        if (_targetScript == null || _targetScript.vertexData == null || worldVertices == null ||
            index >= _targetScript.vertexData.Count || index >= worldVertices.Length) return;

        int nextIndex = (index + 1) % worldVertices.Length;
        if (nextIndex >= worldVertices.Length) return; // Should not happen if worldVertices matches vertexData count

        Vector3 p1_world = worldVertices[index];
        Vector3 p2_world = worldVertices[nextIndex];
        Vector3 midpoint_world = (p1_world + p2_world) / 2f;
        float distance = Vector3.Distance(p1_world, p2_world);

        int numSegments = 0;
        // Check nominalFacadeWidth directly as _targetScript should be valid here
        if (distance > GeometryUtils.Epsilon && _targetScript.nominalFacadeWidth > GeometryUtils.Epsilon)
        {
            // Use the same logic as the generator for consistency
            numSegments = _targetScript.CalculateNumSegments(distance);
        }
        else if (distance > GeometryUtils.Epsilon) // If nominalFacadeWidth is zero, but minSideLengthUnits might be set
        {
            numSegments = Mathf.Max(1, _targetScript.minSideLengthUnits > 0 ? _targetScript.minSideLengthUnits : 1);
        }


        // Offset label slightly upwards relative to the scene view camera
        Vector3 labelOffset = Vector3.up * 0.1f;
        if (SceneView.currentDrawingSceneView != null && SceneView.currentDrawingSceneView.camera != null)
        {
            labelOffset = SceneView.currentDrawingSceneView.camera.transform.up * HandleUtility.GetHandleSize(midpoint_world) * 0.3f;
        }
        Handles.Label(midpoint_world + labelOffset, $"L: {distance:F1} ({numSegments} Seg)");

        // Get the actual side normal calculated by the generator
        // This requires p1_local and p2_local to be correct.
        if (nextIndex >= _targetScript.vertexData.Count) return; // Ensure next index is valid for vertexData

        Vector3 p1_local = _targetScript.vertexData[index].position;
        Vector3 p2_local = _targetScript.vertexData[nextIndex].position;

        // Only draw normal if side has length
        if ((p2_local - p1_local).sqrMagnitude > GeometryUtils.Epsilon * GeometryUtils.Epsilon)
        {
            Vector3 sideNormal_local = _targetScript.CalculateSideNormal(p1_local, p2_local);
            Vector3 sideNormal_world = _targetTransform.TransformDirection(sideNormal_local);

            Handles.color = Color.green;
            Handles.DrawLine(midpoint_world, midpoint_world + sideNormal_world * 0.5f);
        }
    }
}
#endif // UNITY_EDITOR