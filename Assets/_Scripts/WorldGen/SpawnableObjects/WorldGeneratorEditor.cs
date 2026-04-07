using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(WorldGenerator))]
public class WorldGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var wg = (WorldGenerator)target;

        EditorGUILayout.Space(10);

        // ── Noise Preview ──────────────────────────────────────
        if (wg.showNoisePreview)
        {
            if (GUILayout.Button("🗺  Bake Noise Preview", GUILayout.Height(30)))
            {
                wg.BakeNoisePreview();
                EditorUtility.SetDirty(wg);
            }

            if (wg.noisePreviewTexture != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Noise Map Preview", EditorStyles.boldLabel);

                // Vẽ texture trong Inspector
                Rect rect = GUILayoutUtility.GetRect(
                    GUIContent.none, GUIStyle.none,
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(220));

                EditorGUI.DrawPreviewTexture(rect, wg.noisePreviewTexture,
                    null, ScaleMode.ScaleToFit);

                // Legend
                EditorGUILayout.Space(4);
                DrawLegend();
            }
        }
    }

    private void DrawLegend()
    {
        EditorGUILayout.BeginHorizontal();
        DrawColorBox(new Color(0.25f, 0.55f, 0.90f)); EditorGUILayout.LabelField("Water",  GUILayout.Width(50));
        DrawColorBox(new Color(0.92f, 0.86f, 0.52f)); EditorGUILayout.LabelField("Sand",   GUILayout.Width(50));
        DrawColorBox(new Color(0.58f, 0.38f, 0.20f)); EditorGUILayout.LabelField("Dirt",   GUILayout.Width(50));
        DrawColorBox(new Color(0.28f, 0.68f, 0.28f)); EditorGUILayout.LabelField("Grass",  GUILayout.Width(50));
        DrawColorBox(new Color(0.58f, 0.58f, 0.60f)); EditorGUILayout.LabelField("Stone",  GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawColorBox(Color color)
    {
        var old = GUI.color;
        GUI.color = color;
        GUILayout.Box("", GUILayout.Width(16), GUILayout.Height(16));
        GUI.color = old;
    }
}