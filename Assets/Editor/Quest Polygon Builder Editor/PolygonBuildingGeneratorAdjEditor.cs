// PolygonBuildingGeneratorAdjEditor.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PolygonBuildingGeneratorAdj))]
public class PolygonBuildingGeneratorAdjEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); // Draws the default inspector fields

        PolygonBuildingGeneratorAdj generator = (PolygonBuildingGeneratorAdj)target;

        GUILayout.Space(10);
        if (GUILayout.Button("Generate Building", GUILayout.Height(30)))
        {
            generator.GenerateBuilding();
        }

        if (GUILayout.Button("Clear Building", GUILayout.Height(30)))
        {
            generator.ClearBuilding();
        }
    }

    void OnSceneGUI()
    {
        PolygonBuildingGeneratorAdj generator = (PolygonBuildingGeneratorAdj)target;
        if (generator.vertexData == null) return;

        Transform handleTransform = generator.transform; // Get the generator's transform
        Quaternion handleRotation = Tools.pivotRotation == PivotRotation.Local ? handleTransform.rotation : Quaternion.identity;

        // Store default Handles color
        Color defaultHandlesColor = Handles.color;

        for (int i = 0; i < generator.vertexData.Count; i++)
        {
            Handles.color = Color.cyan; // Color for vertex handles and labels
            // Convert local vertex position to world position for the handle
            Vector3 worldPos = handleTransform.TransformPoint(generator.vertexData[i].position);

            EditorGUI.BeginChangeCheck();
            // Display handle in world space
            worldPos = Handles.PositionHandle(worldPos, handleRotation);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(generator, "Move Polygon Vertex");
                // Convert world position back to local space before snapping and assigning
                generator.vertexData[i].position = generator.SnapVertexPosition(handleTransform.InverseTransformPoint(worldPos));
                generator.SynchronizeSideData();
                EditorUtility.SetDirty(generator); // Mark as dirty to save changes
            }
            Handles.Label(worldPos + Vector3.up * 0.2f, $"V{i}");

            // Draw lines between vertices (in world space)
            Vector3 nextWorldPos = handleTransform.TransformPoint(generator.vertexData[(i + 1) % generator.vertexData.Count].position);
            Handles.DrawDottedLine(worldPos, nextWorldPos, 4.0f); // Use DottedLine for less clutter


            // --- Display Interior Angle for the current vertex 'i' ---
            if (generator.vertexData.Count >= 3)
            {
                int prevIndex = (i + generator.vertexData.Count - 1) % generator.vertexData.Count;
                // Current index is 'i'
                int nextIndex = (i + 1) % generator.vertexData.Count;

                // Get vertex positions in local space relative to the generator
                Vector3 pPrevLocal = generator.vertexData[prevIndex].position;
                Vector3 pCurrLocal = generator.vertexData[i].position;
                Vector3 pNextLocal = generator.vertexData[nextIndex].position;

                // Call the public method from the generator script
                float interiorAngle = generator.CalculateInteriorCornerAngle(pPrevLocal, pCurrLocal, pNextLocal);

                Handles.color = Color.yellow; // Different color for angle text
                // Position the label slightly offset from the vertex label
                Vector3 angleLabelOffset = Vector3.up * 0.4f + Vector3.right * 0.3f; // Adjust as needed
                if (Camera.current != null) // Make offset screen-aligned if possible
                {
                    angleLabelOffset = Camera.current.transform.up * 0.08f + Camera.current.transform.right * 0.08f;
                    // Adjust these multipliers for Scene view scale. This attempts to make it less affected by zoom.
                    // Screen-space offsets can be tricky with Handles.Label scale. 
                    // For simplicity, using a fixed world offset might be easier to manage.
                    angleLabelOffset = Vector3.up * 0.4f + (handleTransform.right * 0.3f); // Offset along generator's right and up

                }


                Handles.Label(worldPos + angleLabelOffset, $"Angle: {interiorAngle:F1}°");
            }
            // --- End Display Interior Angle ---
        }
        Handles.color = defaultHandlesColor; // Reset Handles color

        // Optional: Draw flat roof debug outline if available
        if (generator._debugFlatRoofMesh != null && generator._debugFlatRoofTransform != null && generator._debugFlatRoofTransform.gameObject.activeInHierarchy)
        {
            Handles.color = new Color(0.2f, 0.8f, 0.2f, 0.5f); // Greenish for roof
            Handles.matrix = generator._debugFlatRoofTransform.localToWorldMatrix;

            for (int k = 0; k < generator._debugFlatRoofMesh.triangles.Length; k += 3)
            {
                Vector3 v1 = generator._debugFlatRoofMesh.vertices[generator._debugFlatRoofMesh.triangles[k]];
                Vector3 v2 = generator._debugFlatRoofMesh.vertices[generator._debugFlatRoofMesh.triangles[k + 1]];
                Vector3 v3 = generator._debugFlatRoofMesh.vertices[generator._debugFlatRoofMesh.triangles[k + 2]];
                Handles.DrawLine(v1, v2);
                Handles.DrawLine(v2, v3);
                Handles.DrawLine(v3, v1);
            }
            Handles.matrix = Matrix4x4.identity; // Reset matrix
            Handles.color = defaultHandlesColor; // Reset Handles color
        }
    }
}
#endif