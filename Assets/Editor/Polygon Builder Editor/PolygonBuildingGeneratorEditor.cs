#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PolygonBuildingGenerator))]
public class PolygonBuildingGeneratorEditor : Editor
{
    private PolygonBuildingGenerator _targetScript;
    private Transform _targetTransform;
    private SerializedProperty _vertexDataProp;
    private SerializedProperty _sideDataProp;

    private const float HANDLE_SIZE_MULTIPLIER = 0.2f;

    private void OnEnable()
    {
        _targetScript = (PolygonBuildingGenerator)target;
        _targetTransform = _targetScript.transform;

        // Find the serialized properties
        _vertexDataProp = serializedObject.FindProperty("vertexData");
        _sideDataProp = serializedObject.FindProperty("sideData");

        // Ensure initial sync
        _targetScript.SynchronizeSideData();
        if (!Application.isPlaying) EditorUtility.SetDirty(_targetScript);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw default fields EXCEPT the ones we handle manually
        string[] propertiesToExclude = new string[] {
            "m_Script", "vertexData", "sideData"
        };
        DrawPropertiesExcluding(serializedObject, propertiesToExclude);

        // Polygon's Vertices Data
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Polygon Data", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_vertexDataProp, true);

        // --- Side Data List (Overrides) ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Per-Side Style Overrides", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        if (_vertexDataProp.arraySize != _sideDataProp.arraySize)
        {
            EditorGUILayout.HelpBox("Vertex and Side data count mismatch! Regenerating or changing vertex count might fix this.", MessageType.Warning);
        }
        else if (_sideDataProp != null)
        {
            EditorGUILayout.PropertyField(_sideDataProp, true);
        }
        EditorGUI.indentLevel--;

        // --- Polygon Editing Tools ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Polygon Editing Tools", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Vertex"))
        {
            AddVertex();
            _targetScript.SynchronizeSideData(); // Ensure sync after add/remove
            serializedObject.ApplyModifiedProperties(); // Apply before repaint
            SceneView.RepaintAll();
        }
        EditorGUI.BeginDisabledGroup(_vertexDataProp.arraySize <= 3);
        if (GUILayout.Button("Remove Last Vertex"))
        {
            RemoveLastVertex();
            _targetScript.SynchronizeSideData(); // Ensure sync after add/remove
            serializedObject.ApplyModifiedProperties(); // Apply before repaint
            SceneView.RepaintAll();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        // --- Generation Buttons ---
        EditorGUILayout.Space();
        if (GUILayout.Button("Generate Building"))
        {
            SnapAllVertices();
            _targetScript.GenerateBuilding();
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Clear Building"))
        {
            _targetScript.ClearBuilding();
            SceneView.RepaintAll();
        }
        // --- End Generation Buttons ---


        // Apply changes at the end
        if (serializedObject.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(_targetScript);
            SceneView.RepaintAll();
        }
    }


    private void OnSceneGUI()
    {
        if (_targetScript == null || _vertexDataProp == null) return;

        // Use local rotation if tool handle is set to local
        Quaternion handleRotation = Tools.pivotRotation == PivotRotation.Local ? _targetTransform.rotation : Quaternion.identity;

        // --- Draw Handles and Lines ---
        Handles.color = Color.yellow; // Use yellow for lines
        Vector3[] worldVertices = new Vector3[_vertexDataProp.arraySize];

        for (int i = 0; i < _vertexDataProp.arraySize; i++)
        {
            // Get the PolygonVertexData element property
            SerializedProperty vertexElementProp = _vertexDataProp.GetArrayElementAtIndex(i);
            // Get the position property within the element
            SerializedProperty positionProp = vertexElementProp.FindPropertyRelative("position");
            // Get the corner element flag property
            SerializedProperty cornerFlagProp = vertexElementProp.FindPropertyRelative("addCornerElement");

            Vector3 localPos = positionProp.vector3Value;
            worldVertices[i] = _targetTransform.TransformPoint(localPos);

            // --- Draw Handle ---
            Handles.color = cornerFlagProp.boolValue ? Color.magenta : Color.cyan; // Different color if corner element is enabled
            float handleSize = HandleUtility.GetHandleSize(worldVertices[i]) * HANDLE_SIZE_MULTIPLIER;

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPos = Handles.FreeMoveHandle(worldVertices[i], handleSize, Vector3.one * 0.5f, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_targetScript, "Move Polygon Vertex");

                Vector3 newLocalPos = _targetTransform.InverseTransformPoint(newWorldPos);
                Vector3 snappedLocalPos = _targetScript.SnapVertexPosition(newLocalPos); // Use the snapping method

                // Update the position via SerializedProperty for Undo handling
                positionProp.vector3Value = snappedLocalPos;

                // We don't enforce min side length here anymore,
                // the generator handles it via minSideLengthUnits.

                serializedObject.ApplyModifiedProperties(); // Apply the change
                EditorUtility.SetDirty(_targetScript);
                // Optional real-time update (can be slow)
                // if (Event.current.type == EventType.Used) { _targetScript.GenerateBuilding(); }
            }

            // --- Draw Side Info ---
            DrawSideInfo(i, worldVertices); // Pass index and current world vertices array
        }

        // Draw polygon lines
        Handles.color = Color.yellow;
        if (worldVertices.Length > 1)
        {
            Handles.DrawPolyLine(worldVertices);
            Handles.DrawLine(worldVertices[worldVertices.Length - 1], worldVertices[0]); // Close the loop
        }
    }

    // --- Helper Functions ---

    private void AddVertex()
    {
        // Use SerializedProperty access for Undo
        Undo.RecordObject(_targetScript, "Add Vertex");

        _vertexDataProp.arraySize++; // Increase the size of the list

        // Get the new element property (it's the last one)
        SerializedProperty newVertexProp = _vertexDataProp.GetArrayElementAtIndex(_vertexDataProp.arraySize - 1);
        SerializedProperty newPosProp = newVertexProp.FindPropertyRelative("position");
        SerializedProperty newCornerFlagProp = newVertexProp.FindPropertyRelative("addCornerElement");


        Vector3 newVertexPos = Vector3.zero;
        int count = _vertexDataProp.arraySize; // Use current size (after increment)

        if (count > 2) // If there were at least 2 vertices before adding
        {
            // Get positions of the vertices that now form the 'last' edge
            Vector3 lastPos = _vertexDataProp.GetArrayElementAtIndex(count - 2).FindPropertyRelative("position").vector3Value; // The previously last vertex
            Vector3 firstPos = _vertexDataProp.GetArrayElementAtIndex(0).FindPropertyRelative("position").vector3Value;      // The first vertex
            newVertexPos = (lastPos + firstPos) / 2f;
            newVertexPos += Vector3.right * _targetScript.vertexSnapSize; // Offset slightly outwards
        }
        else if (count == 2) // If there was only 1 vertex before adding
        {
            Vector3 existingPos = _vertexDataProp.GetArrayElementAtIndex(0).FindPropertyRelative("position").vector3Value;
            newVertexPos = existingPos + Vector3.forward * _targetScript.vertexSnapSize * 2;
        }
        // else: it's the first vertex, leave at (0,0,0)


        // Set default values for the new vertex
        newPosProp.vector3Value = _targetScript.SnapVertexPosition(newVertexPos);
        newCornerFlagProp.boolValue = false; // Default to no corner element

        // Editor list sync should handle sideData, triggered by ApplyModifiedProperties / OnValidate
        // _targetScript.SynchronizeSideData(); // Explicit sync just in case
    }

    private void RemoveLastVertex()
    {
        if (_vertexDataProp.arraySize > 0)
        {
            Undo.RecordObject(_targetScript, "Remove Vertex");
            _vertexDataProp.arraySize--; // Just decrease the size, element is removed
                                         // Editor list sync should handle sideData, triggered by ApplyModifiedProperties / OnValidate
                                         // _targetScript.SynchronizeSideData(); // Explicit sync just in case
        }
    }


    private void SnapAllVertices()
    {
        Undo.RecordObject(_targetScript, "Snap All Vertices");
        for (int i = 0; i < _vertexDataProp.arraySize; i++)
        {
            SerializedProperty posProp = _vertexDataProp.GetArrayElementAtIndex(i).FindPropertyRelative("position");
            posProp.vector3Value = _targetScript.SnapVertexPosition(posProp.vector3Value);
        }
        serializedObject.ApplyModifiedProperties(); // Apply the snapped values
        EditorUtility.SetDirty(_targetScript);
    }

    // Helper to draw side length and normal info in Scene View
    private void DrawSideInfo(int index, Vector3[] worldVertices)
    {
        if (worldVertices.Length < 2) return;

        Vector3 p1_world = worldVertices[index];
        Vector3 p2_world = worldVertices[(index + 1) % worldVertices.Length]; // Wrap around
        Vector3 midpoint_world = (p1_world + p2_world) / 2f;
        float distance = Vector3.Distance(p1_world, p2_world);

        // Calculate segment count based on potential scaling choice
        int numSegments = 0;
        if (distance > 0.01f)
        {
            numSegments = Mathf.Max(_targetScript.minSideLengthUnits, Mathf.RoundToInt(distance / _targetScript.nominalFacadeWidth));
        }


        // Display side length / segment count
        Handles.Label(midpoint_world + _targetTransform.up * 0.2f, $"L: {distance:F1} ({numSegments} Seg)"); // Use transform.up for offset

        // Draw Normal indicator (Local space calculation is more reliable here)
        Vector3 p1_local = _targetScript.vertexData[index].position;
        Vector3 p2_local = _targetScript.vertexData[(index + 1) % _targetScript.vertexData.Count].position;
        Vector3 sideDir_local = (p2_local - p1_local).normalized;

        if (sideDir_local == Vector3.zero) return; // Avoid issues with zero length sides

        Vector3 normal_local = Vector3.Cross(sideDir_local, Vector3.up).normalized;

        // Robust Normal Check (copied logic from generator)
        Vector3 polygonCenter = Vector3.zero;
        if (_targetScript.vertexData.Count > 0)
        {
            foreach (var vd in _targetScript.vertexData) polygonCenter += vd.position;
            polygonCenter /= _targetScript.vertexData.Count;
        }
        Vector3 sideMidpoint_local = p1_local + sideDir_local * (distance / 2f); // Distance is world, but direction is local - careful. Use local distance.
        float localDistance = Vector3.Distance(p1_local, p2_local);
        sideMidpoint_local = p1_local + sideDir_local * (localDistance / 2f);

        Vector3 centerToMidpoint_local = sideMidpoint_local - polygonCenter;
        centerToMidpoint_local.y = 0;
        Vector3 checkNormal = normal_local; checkNormal.y = 0;
        if (Vector3.Dot(checkNormal.normalized, centerToMidpoint_local.normalized) < -0.01f)
        {
            normal_local *= -1;
        }

        // Convert local normal to world for drawing
        Vector3 normal_world = _targetTransform.TransformDirection(normal_local);

        Handles.color = Color.green;
        Handles.DrawLine(midpoint_world, midpoint_world + normal_world * 0.5f); // Draw short normal line
    }
}
#endif // UNITY_EDITOR