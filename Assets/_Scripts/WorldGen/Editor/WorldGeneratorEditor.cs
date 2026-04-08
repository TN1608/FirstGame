using _Scripts.WorldGen;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(WorldGenerator))]
public class WorldGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var wg = (WorldGenerator)target;

        EditorGUILayout.Space(8);

        // ── Folder structure hint ──────────────────────────
        EditorGUILayout.HelpBox(
            "📁 CẤU TRÚC FOLDER CẦN THIẾT:\n\n" +
            "Assets/Resources/\n" +
            "  ├── Tiles/32x_Tiles/\n" +
            "  │     ├── water/      ← tile assets (.asset)\n" +
            "  │     ├── path/\n" +
            "  │     ├── brush/\n" +
            "  │     ├── grass/\n" +
            "  │     └── stone/\n" +
            "  └── Prefabs/WorldObjects/\n" +
            "        ├── rocks/      ← SpawnableObjectConfig (.asset)\n" +
            "        └── treelogs/",
            MessageType.Info);

        EditorGUILayout.Space(4);

        // ── Noise Preview ──────────────────────────────────
        if (wg.showNoisePreview)
        {
            if (GUILayout.Button("🗺  Bake Noise Preview", GUILayout.Height(32)))
            {
                wg.BakeNoisePreview();
                EditorUtility.SetDirty(wg);
            }

            if (wg.noisePreviewTexture != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Noise Map Preview", EditorStyles.boldLabel);

                Rect rect = GUILayoutUtility.GetRect(
                    GUIContent.none, GUIStyle.none,
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(220));
                EditorGUI.DrawPreviewTexture(rect, wg.noisePreviewTexture,
                    null, ScaleMode.ScaleToFit);

                EditorGUILayout.Space(4);
                DrawLegend(wg);
            }
        }

        EditorGUILayout.Space(4);

        // ── Loaded configs status ──────────────────────────
        if (wg.spawnableObjects != null && wg.spawnableObjects.Count > 0)
        {
            EditorGUILayout.LabelField($"✅ SpawnableObjects loaded: {wg.spawnableObjects.Count}",
                EditorStyles.boldLabel);
            foreach (var cfg in wg.spawnableObjects)
            {
                if (cfg == null) continue;
                EditorGUILayout.LabelField($"   • {cfg.objectName} (chance: {cfg.spawnChance:F2})",
                    EditorStyles.miniLabel);
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "⚠ Chưa có SpawnableObjectConfig nào được load.\n" +
                "Đảm bảo folder nằm trong Assets/Resources/Prefabs/WorldObjects/",
                MessageType.Warning);
        }
    }

    private void DrawLegend(WorldGenerator wg)
    {
        EditorGUILayout.BeginHorizontal();
        DrawBox(new Color(0.20f, 0.50f, 0.90f)); GUILayout.Label("Water",  GUILayout.Width(46));
        DrawBox(new Color(0.92f, 0.86f, 0.52f)); GUILayout.Label("Sand",   GUILayout.Width(46));
        DrawBox(new Color(0.58f, 0.38f, 0.20f)); GUILayout.Label("Dirt",   GUILayout.Width(46));
        DrawBox(new Color(0.28f, 0.68f, 0.28f)); GUILayout.Label("Grass",  GUILayout.Width(46));
        DrawBox(new Color(0.55f, 0.55f, 0.58f)); GUILayout.Label("Stone",  GUILayout.Width(46));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawBox(Color color)
    {
        var old = GUI.color;
        GUI.color = color;
        GUILayout.Box("", GUILayout.Width(16), GUILayout.Height(16));
        GUI.color = old;
    }
}