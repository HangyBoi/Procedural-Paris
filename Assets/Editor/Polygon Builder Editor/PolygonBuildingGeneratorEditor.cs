#if UNITY_EDITOR // Encapsulate editor code
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(PolygonBuildingGenerator))]
public class PolygonBuildingGeneratorEditor : Editor
{
    private PolygonBuildingGenerator _targetScript;
    private Transform _targetTransform;
    private Quaternion _handleRotation;

    private const float HANDLE_SIZE_MULTIPLIER = 0.1f; // Adjust handle size

    private void OnEnable()
    {
        _targetScript = (PolygonBuildingGenerator)target;
        _targetTransform = _targetScript.transform;
        // Ensure vertices list is not null
        if (_targetScript.vertices == null)
        {
            _targetScript.vertices = new List<Vector3>();
        }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); // Draw the default fields

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Building"))
        {
            // Ensure vertices are snapped before generating
            SnapAllVertices();
            _targetScript.GenerateBuilding();
            SceneView.RepaintAll(); // Update scene view
        }

        if (GUILayout.Button("Clear Building"))
        {
            _targetScript.ClearBuilding();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Polygon Editing", EditorStyles.boldLabel);

        if (GUILayout.Button("Add Vertex"))
        {
            Undo.RecordObject(_targetScript, "Add Vertex");
            // Add new vertex intelligently (e.g., halfway along the last edge)
            Vector3 newVertexPos = Vector3.zero;
            if (_targetScript.vertices.Count >= 2)
            {
                newVertexPos = (_targetScript.vertices[_targetScript.vertices.Count - 1] + _targetScript.vertices[0]) / 2f;
                newVertexPos += Vector3.right * _targetScript.vertexSnapSize; // Offset slightly
            }
            else if (_targetScript.vertices.Count == 1)
            {
                newVertexPos = _targetScript.vertices[0] + Vector3.forward * _targetScript.vertexSnapSize * 2;
            }
            _targetScript.vertices.Add(_targetScript.SnapVertex(newVertexPos));
            EditorUtility.SetDirty(_targetScript);
            SceneView.RepaintAll();
        }

        if (_targetScript.vertices.Count > 3) // Only allow removal if polygon remains valid
        {
            if (GUILayout.Button("Remove Last Vertex"))
            {
                Undo.RecordObject(_targetScript, "Remove Vertex");
                _targetScript.vertices.RemoveAt(_targetScript.vertices.Count - 1);
                EditorUtility.SetDirty(_targetScript);
                SceneView.RepaintAll();
            }
        }
        else
        {
            EditorGUI.BeginDisabledGroup(true);
            GUILayout.Button("Remove Last Vertex (Needs >3)");
            EditorGUI.EndDisabledGroup();
        }


        if (GUI.changed) // If any value changed in inspector
        {
            EditorUtility.SetDirty(_targetScript);
            SnapAllVertices(); // Re-snap if settings like snap size change
            SceneView.RepaintAll();
        }
    }

    private void OnSceneGUI()
    {
        if (_targetScript == null || _targetScript.vertices == null) return;

        _handleRotation = Tools.pivotRotation == PivotRotation.Local ? _targetTransform.rotation : Quaternion.identity;

        // --- Draw Handles and Lines ---
        Handles.color = Color.cyan;
        Vector3[] worldVertices = new Vector3[_targetScript.vertices.Count];

        for (int i = 0; i < _targetScript.vertices.Count; i++)
        {
            // Convert local vertex pos to world pos for handles
            worldVertices[i] = _targetTransform.TransformPoint(_targetScript.vertices[i]);

            EditorGUI.BeginChangeCheck(); // Check if handle is moved

            // Make handle size relative to distance from camera
            float handleSize = HandleUtility.GetHandleSize(worldVertices[i]) * HANDLE_SIZE_MULTIPLIER;

            // Draw a position handle (can use FreeMoveHandle, PositionHandle etc.)
            // Using FreeMoveHandle for snapping flexibility within the handle logic
            Vector3 newWorldPos = Handles.FreeMoveHandle(worldVertices[i], handleSize, Vector3.one * 0.5f, Handles.SphereHandleCap);


            if (EditorGUI.EndChangeCheck()) // If handle was moved
            {
                Undo.RecordObject(_targetScript, "Move Polygon Vertex"); // Register Undo

                // Convert back to local space
                Vector3 newLocalPos = _targetTransform.InverseTransformPoint(newWorldPos);

                // Apply Snapping
                Vector3 snappedLocalPos = _targetScript.SnapVertex(newLocalPos);

                // --- Side Length Constraint ---
                // Get adjacent vertices (handling wrap around)
                Vector3 prevVertex = _targetScript.vertices[(i + _targetScript.vertices.Count - 1) % _targetScript.vertices.Count];
                Vector3 nextVertex = _targetScript.vertices[(i + 1) % _targetScript.vertices.Count];

                // Calculate lengths based on the *proposed* snapped position
                float distToPrev = Vector3.Distance(snappedLocalPos, prevVertex);
                float distToNext = Vector3.Distance(snappedLocalPos, nextVertex);

                // Check if new position would violate minimum length constraint
                bool prevSideOk = distToPrev >= (_targetScript.minSideLengthUnits * _targetScript.vertexSnapSize - 0.01f); // Tolerance
                bool nextSideOk = distToNext >= (_targetScript.minSideLengthUnits * _targetScript.vertexSnapSize - 0.01f);

                // Only update if BOTH adjacent sides meet minimum length requirement
                // (Unless it's the only way to satisfy it from a previously invalid state)
                // This logic can get complex. A simpler approach is to just enforce the snap
                // and let the GenerateBuilding function handle the segment count based on snapped vertices.
                // Let's stick to the simpler approach for now: Update the vertex position based on snapping.
                // The GenerateBuilding method already ensures minSideLengthUnits prefabs are placed.

                // Update the vertex position if it actually changed after snapping
                if (Vector3.Distance(_targetScript.vertices[i], snappedLocalPos) > 0.001f)
                {
                    _targetScript.vertices[i] = snappedLocalPos;
                    EditorUtility.SetDirty(_targetScript); // Mark script as changed
                                                           // Optionally, regenerate building preview in real-time (can be slow)
                                                           // _targetScript.GenerateBuilding();
                }
            }

            // Draw side information (Optional: For Debugging)
            DrawSideInfo(i, worldVertices);
        }

        // Draw polygon lines connecting the world vertices
        Handles.color = Color.yellow;
        if (worldVertices.Length > 1)
        {
            // Draw lines between vertices, closing the loop
            Handles.DrawPolyLine(worldVertices);
            Handles.DrawLine(worldVertices[worldVertices.Length - 1], worldVertices[0]);
        }
    }


    // Helper to draw side length and normal info in Scene View
    private void DrawSideInfo(int index, Vector3[] worldVertices)
    {
        Vector3 p1_world = worldVertices[index];
        Vector3 p2_world = worldVertices[(index + 1) % worldVertices.Length];
        Vector3 midpoint_world = (p1_world + p2_world) / 2f;
        float distance = Vector3.Distance(p1_world, p2_world);
        int numSegments = Mathf.Max(_targetScript.minSideLengthUnits, Mathf.RoundToInt(distance / _targetScript.vertexSnapSize));

        // Display side length / segment count
        Handles.Label(midpoint_world + Vector3.up * 0.2f, $"L: {distance:F1} ({numSegments} Seg)");

        // Draw Normal indicator
        Vector3 sideDir_world = (p2_world - p1_world).normalized;
        Vector3 normal_world = Vector3.Cross(sideDir_world, _targetTransform.up).normalized; // Use transform.up

        // Recalculate center in world space for accurate normal check
        Vector3 center_world = Vector3.zero;
        foreach (Vector3 v in worldVertices) center_world += v;
        center_world /= worldVertices.Length;
        Vector3 centerToMidpoint_world = midpoint_world - center_world;
        if (Vector3.Dot(normal_world, centerToMidpoint_world) < 0)
        {
            normal_world *= -1;
        }

        Handles.color = Color.green;
        Handles.DrawLine(midpoint_world, midpoint_world + normal_world * 0.5f); // Draw short normal line
    }

    // Helper to snap all vertices at once
    private void SnapAllVertices()
    {
        if (_targetScript == null || _targetScript.vertices == null) return;
        Undo.RecordObject(_targetScript, "Snap All Vertices");
        for (int i = 0; i < _targetScript.vertices.Count; i++)
        {
            _targetScript.vertices[i] = _targetScript.SnapVertex(_targetScript.vertices[i]);
        }
        EditorUtility.SetDirty(_targetScript);
    }
}
#endif // UNITY_EDITOR