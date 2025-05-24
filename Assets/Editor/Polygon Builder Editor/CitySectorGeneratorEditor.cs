#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq; // For Enumerable.Any
using MIConvexHull; // For MIConvexHull types

[CustomEditor(typeof(CitySectorGenerator))]
public class CitySectorGeneratorEditor : Editor
{
    private CitySectorGenerator _targetGenerator;

    private void OnEnable()
    {
        _targetGenerator = (CitySectorGenerator)target;
        // Ensure PolygonBuildingGenerator.cs and PolygonGeometry.cs are compiled
        // by referencing a type from them if direct dependencies aren't enough.
        // This usually isn't necessary if scripts are in standard asset folders.
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); // Draws all public fields

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Generate Full Sector"))
        {
            if (EditorUtility.DisplayDialog("Confirm Generation",
                "This will clear any existing sector content and generate a new one. Are you sure?",
                "Generate", "Cancel"))
            {
                Undo.RecordObject(_targetGenerator, "Generate City Sector");
                // For game objects that will be created/destroyed:
                // It's complex to Undo full hierarchy creation perfectly.
                // The Clear operation handles removal. New objects are created.
                // Focusing on Undo for the generator's properties.
                _targetGenerator.GenerateFullSector();
                EditorUtility.SetDirty(_targetGenerator); // Mark generator as dirty
                MarkSceneDirty(); // Mark scene as dirty
            }
        }

        if (GUILayout.Button("Clear Generated Sector"))
        {
            if (EditorUtility.DisplayDialog("Confirm Clear",
                "This will remove all generated sector content. Are you sure?",
                "Clear", "Cancel"))
            {
                // Again, Undo for object destruction is tricky.
                // We'll record the state of the generator itself.
                Transform generatedContentRoot = _targetGenerator.transform.Find("GeneratedSectorContent");
                if (generatedContentRoot != null)
                {
                    Undo.DestroyObjectImmediate(generatedContentRoot.gameObject);
                }
                // _targetGenerator.ClearGeneratedSector(); // Call the C# clear method which also resets data
                // The above Undo.DestroyObjectImmediate handles the GameObjects.
                // Now, call the method to clear the internal data structure.
                _targetGenerator.ClearGeneratedSector();


                EditorUtility.SetDirty(_targetGenerator);
                MarkSceneDirty();
            }
        }
        if (GUILayout.Button("Force Repaint Scene"))
        {
            SceneView.RepaintAll();
        }
    }

    private void OnSceneGUI()
    {
        if (_targetGenerator == null || _targetGenerator.GeneratedData == null) return;

        CitySectorData data = _targetGenerator.GeneratedData;
        Transform generatorTransform = _targetGenerator.transform; // For transforming local to world

        // 1. Draw Sector Bounds
        Handles.color = Color.gray;
        Vector3 sectorCenterWorld = generatorTransform.TransformPoint(Vector3.zero); // Assuming generator is at sector center
        Vector3 sectorSizeWorld = generatorTransform.TransformVector(new Vector3(_targetGenerator.sectorSize.x, 0, _targetGenerator.sectorSize.y));
        // Note: DrawWireCube takes center and *full* size. If generatorTransform has scale, this could be tricky.
        // Assuming uniform scale or scale of 1 for simplicity here.
        Matrix4x4 originalMatrix = Handles.matrix;
        Handles.matrix = generatorTransform.localToWorldMatrix; // Apply transform for drawing local coords
        Handles.DrawWireCube(Vector3.zero, new Vector3(_targetGenerator.sectorSize.x, 0, _targetGenerator.sectorSize.y));
        Handles.matrix = originalMatrix;


        // 2. Draw Seed Points
        if (data.SeedPoints != null && data.SeedPoints.Any())
        {
            Handles.color = Color.red;
            foreach (var point in data.SeedPoints)
            {
                Handles.SphereHandleCap(0, generatorTransform.TransformPoint(new Vector3(point.x, 0, point.y)), generatorTransform.rotation, HandleUtility.GetHandleSize(generatorTransform.TransformPoint(new Vector3(point.x, 0, point.y))) * 0.1f, EventType.Repaint);
            }
        }

        // 3. Draw Delaunay Triangles
        if (data.DelaunayTriangulation != null && data.DelaunayTriangulation.Cells != null)
        {
            Handles.color = new Color(0, 1, 0, 0.5f); // Green, semi-transparent
            foreach (var cell in data.DelaunayTriangulation.Cells)
            {
                if (cell.Vertices == null || cell.Vertices.Length < 3) continue;
                Vector3 p0 = generatorTransform.TransformPoint(new Vector3((float)cell.Vertices[0].Position[0], 0, (float)cell.Vertices[0].Position[1]));
                Vector3 p1 = generatorTransform.TransformPoint(new Vector3((float)cell.Vertices[1].Position[0], 0, (float)cell.Vertices[1].Position[1]));
                Vector3 p2 = generatorTransform.TransformPoint(new Vector3((float)cell.Vertices[2].Position[0], 0, (float)cell.Vertices[2].Position[1]));
                Handles.DrawLine(p0, p1);
                Handles.DrawLine(p1, p2);
                Handles.DrawLine(p2, p0);
            }
        }

        // 4. Draw Raw Voronoi Cells (before shrinking/processing)
        if (data.RawVoronoiCells != null && data.RawVoronoiCells.Any())
        {
            Handles.color = new Color(0, 0, 1, 0.3f); // Blue, semi-transparent
            foreach (var polygonVertices in data.RawVoronoiCells)
            {
                if (polygonVertices == null || polygonVertices.Count < 2) continue;
                for (int i = 0; i < polygonVertices.Count; i++)
                {
                    Vector3 current = generatorTransform.TransformPoint(new Vector3(polygonVertices[i].x, 0.1f, polygonVertices[i].y));
                    Vector3 next = generatorTransform.TransformPoint(new Vector3(polygonVertices[(i + 1) % polygonVertices.Count].x, 0.1f, polygonVertices[(i + 1) % polygonVertices.Count].y));
                    Handles.DrawLine(current, next);
                }
            }
        }


        // 5. Draw Processed Building Plots (after shrinking)
        if (data.ProcessedBuildingPlots != null && data.ProcessedBuildingPlots.Any())
        {
            Handles.color = Color.yellow; // Yellow for final plots
            foreach (var polygonVertices in data.ProcessedBuildingPlots)
            {
                if (polygonVertices == null || polygonVertices.Count < 2) continue;
                for (int i = 0; i < polygonVertices.Count; i++)
                {
                    Vector3 current = generatorTransform.TransformPoint(new Vector3(polygonVertices[i].x, 0.2f, polygonVertices[i].y)); // Slightly higher Y
                    Vector3 next = generatorTransform.TransformPoint(new Vector3(polygonVertices[(i + 1) % polygonVertices.Count].x, 0.2f, polygonVertices[(i + 1) % polygonVertices.Count].y));
                    Handles.DrawLine(current, next);
                }
            }
        }
    }

    private void MarkSceneDirty()
    {
        if (!Application.isPlaying && _targetGenerator.gameObject.scene.IsValid())
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(_targetGenerator.gameObject.scene);
        }
        SceneView.RepaintAll(); // Request repaint of scene views
    }
}
#endif