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

        Handles.color = Color.cyan;
        for (int i = 0; i < generator.vertexData.Count; i++)
        {
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
                generator.SynchronizeSideData(); // Keep side data in sync if vertex count changes (though not here)
                EditorUtility.SetDirty(generator); // Mark as dirty to save changes
            }
            Handles.Label(worldPos + Vector3.up * 0.2f, $"V{i}");


            // Draw lines between vertices (in world space)
            Vector3 nextWorldPos = handleTransform.TransformPoint(generator.vertexData[(i + 1) % generator.vertexData.Count].position);
            Handles.DrawLine(worldPos, nextWorldPos);
        }

        // Optional: Draw flat roof debug outline if available
        if (generator._debugFlatRoofMesh != null && generator._debugFlatRoofTransform != null && generator._debugFlatRoofTransform.gameObject.activeInHierarchy)
        {
            Handles.color = new Color(0.2f, 0.8f, 0.2f, 0.5f); // Greenish for roof
            Handles.matrix = generator._debugFlatRoofTransform.localToWorldMatrix; // Set matrix for drawing in local space of roof obj

            // Draw the wireframe of the mesh bounds, or the mesh itself if simple
            // Handles.DrawWireDisc(Vector3.zero, Vector3.up, 0.1f); // Example marker at roof pivot
            for (int i = 0; i < generator._debugFlatRoofMesh.triangles.Length; i += 3)
            {
                Vector3 v1 = generator._debugFlatRoofMesh.vertices[generator._debugFlatRoofMesh.triangles[i]];
                Vector3 v2 = generator._debugFlatRoofMesh.vertices[generator._debugFlatRoofMesh.triangles[i + 1]];
                Vector3 v3 = generator._debugFlatRoofMesh.vertices[generator._debugFlatRoofMesh.triangles[i + 2]];
                Handles.DrawLine(v1, v2);
                Handles.DrawLine(v2, v3);
                Handles.DrawLine(v3, v1);
            }
            Handles.matrix = Matrix4x4.identity; // Reset matrix
        }
    }
}
#endif