// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
//  This script provides a custom editor for the CitySectorGenerator component, allowing users to generate and visualize city sectors in the Unity Editor.
//  It includes controls for generating and clearing sectors, as well as visualizing the generation process with debug gizmos.
//

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic; // Required for List<T>

/// <summary>
/// Custom editor for the CitySectorGenerator. Provides generation controls and scene view visualization.
/// </summary>
[CustomEditor(typeof(CitySectorGenerator))]
public class CitySectorGeneratorEditor : Editor
{
    private CitySectorGenerator _target;
    private SerializedProperty _showDebugGizmosProp;

    /// <summary>
    /// Caches references to the target script and its properties.
    /// </summary>
    private void OnEnable()
    {
        _target = (CitySectorGenerator)target;
        // Cache the 'showDebugGizmos' property for efficient access and modification.
        _showDebugGizmosProp = serializedObject.FindProperty("showDebugGizmos");
    }

    /// <summary>
    /// Draws the custom inspector GUI.
    /// </summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw all default properties except those we handle manually.
        DrawPropertiesExcluding(serializedObject, "m_Script", "showDebugGizmos");

        EditorGUILayout.Space();

        DrawActionButtons();

        // Apply any changes made to serialized properties.
        if (serializedObject.ApplyModifiedProperties())
        {
            SceneView.RepaintAll();
        }
    }

    /// <summary>
    /// Draws the main action buttons for generating and clearing the sector.
    /// </summary>
    private void DrawActionButtons()
    {
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Generate Full Sector", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Confirm Generation", "This will clear all existing content and generate a new sector.", "Generate", "Cancel"))
            {
                Undo.RecordObject(_target, "Generate City Sector");
                _target.GenerateFullSector();
                MarkSceneDirty();
            }
        }

        if (GUILayout.Button("Clear Generated Sector"))
        {
            if (EditorUtility.DisplayDialog("Confirm Clear", "This will remove all generated sector content.", "Clear", "Cancel"))
            {
                // For Undo to work on destroyed objects, we must record the destruction.
                Transform contentRoot = _target.transform.Find("GeneratedSectorContent");
                if (contentRoot != null) Undo.DestroyObjectImmediate(contentRoot.gameObject);
                else _target.ClearGeneratedSector(); // Fallback if the root GO isn't found
                MarkSceneDirty();
            }
        }

        // A custom button to toggle the boolean property provides a better user experience.
        string buttonText = _showDebugGizmosProp.boolValue ? "Hide Generation Gizmos" : "Show Generation Gizmos";
        if (GUILayout.Button(buttonText))
        {
            _showDebugGizmosProp.boolValue = !_showDebugGizmosProp.boolValue;
        }
    }

    /// <summary>
    /// Draws debug visualizations for the generation process in the scene view.
    /// </summary>
    private void OnSceneGUI()
    {
        // Do not draw gizmos if the target is invalid or if they are disabled.
        if (_target == null || !_target.showDebugGizmos || _target.GeneratedData == null) return;

        Transform generatorTransform = _target.transform;
        CitySectorData data = _target.GeneratedData;

        // Draw the main sector boundary.
        Handles.color = Color.gray;
        Handles.matrix = generatorTransform.localToWorldMatrix; // Set matrix for local space drawing
        Handles.DrawWireCube(Vector3.zero, new Vector3(_target.sectorSize.x, 0, _target.sectorSize.y));
        Handles.matrix = Matrix4x4.identity; // Reset matrix to avoid affecting other handles

        // Draw visualization for each step of the generation process.
        DrawPoints(data.SeedPoints, generatorTransform, Color.red, 0.1f);
        DrawDelaunay(data.DelaunayTriangulation, generatorTransform, new Color(0, 1, 0, 0.5f));
        DrawPolygons(data.RawVoronoiCells, generatorTransform, new Color(0, 0, 1, 0.3f), 0.1f); // Raw plots
        DrawPolygons(data.ProcessedBuildingPlots, generatorTransform, Color.yellow, 0.2f);       // Final plots
    }

    /// <summary>
    /// Helper to draw a list of 2D points as spheres in the scene.
    /// </summary>
    private void DrawPoints(List<Vector2> points, Transform t, Color color, float size)
    {
        if (points == null) return;
        Handles.color = color;
        foreach (var p in points)
        {
            Vector3 worldPos = t.TransformPoint(new Vector3(p.x, 0, p.y));
            Handles.SphereHandleCap(0, worldPos, t.rotation, HandleUtility.GetHandleSize(worldPos) * size, EventType.Repaint);
        }
    }

    /// <summary>
    /// Helper to draw a list of 2D polygons in the scene.
    /// </summary>
    private void DrawPolygons(List<List<Vector2>> polygons, Transform t, Color color, float yOffset)
    {
        if (polygons == null) return;
        Handles.color = color;
        foreach (var poly in polygons)
        {
            if (poly == null || poly.Count < 2) continue;
            for (int i = 0; i < poly.Count; i++)
            {
                // Transform each point from local to world space for drawing.
                Vector3 p1 = t.TransformPoint(new Vector3(poly[i].x, yOffset, poly[i].y));
                // The modulo operator ensures the last vertex connects back to the first.
                Vector3 p2 = t.TransformPoint(new Vector3(poly[(i + 1) % poly.Count].x, yOffset, poly[(i + 1) % poly.Count].y));
                Handles.DrawLine(p1, p2);
            }
        }
    }

    /// <summary>
    /// Helper to draw the triangles of a Delaunay triangulation.
    /// </summary>
    private void DrawDelaunay(MIConvexHull.ITriangulation<MIConvexHull.DefaultVertex, MIConvexHull.DefaultTriangulationCell<MIConvexHull.DefaultVertex>> delaunay, Transform t, Color color)
    {
        if (delaunay?.Cells == null) return;
        Handles.color = color;
        foreach (var cell in delaunay.Cells)
        {
            // Extract the 3 vertices of the triangle cell and transform to world space.
            Vector3 p0 = t.TransformPoint(new Vector3((float)cell.Vertices[0].Position[0], 0, (float)cell.Vertices[0].Position[1]));
            Vector3 p1 = t.TransformPoint(new Vector3((float)cell.Vertices[1].Position[0], 0, (float)cell.Vertices[1].Position[1]));
            Vector3 p2 = t.TransformPoint(new Vector3((float)cell.Vertices[2].Position[0], 0, (float)cell.Vertices[2].Position[1]));
            Handles.DrawLine(p0, p1);
            Handles.DrawLine(p1, p2);
            Handles.DrawLine(p2, p0);
        }
    }

    /// <summary>
    /// Marks the active scene as dirty to ensure changes are saved.
    /// </summary>
    private void MarkSceneDirty()
    {
        if (!Application.isPlaying && _target.gameObject.scene.IsValid())
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(_target.gameObject.scene);
        }
        SceneView.RepaintAll();
    }
}
#endif