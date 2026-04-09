#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector for ProceduralWorldGenerator.
/// Shows live noise preview textures and one-click Generate/Clear buttons.
/// </summary>
[CustomEditor(typeof(ProceduralWorldGenerator))]
public class ProceduralWorldGeneratorEditor : Editor
{
    private Texture2D _contPreview;
    private Texture2D _erosionPreview;
    private Texture2D _weirdPreview;   // ridges folded
    private Texture2D _tempPreview;
    private Texture2D _humPreview;
    private Texture2D _detailPreview;

    private const int PREVIEW_SIZE = 64;
    private bool _previewsFoldout = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var gen = (ProceduralWorldGenerator)target;
        if (gen.settings == null) return;

        EditorGUILayout.Space(8);

        // --- Action buttons ---
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("▶  Generate", GUILayout.Height(32)))
            gen.Generate();
        if (GUILayout.Button("✕  Clear", GUILayout.Height(32)))
            gen.Clear();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // --- Noise Previews ---
        _previewsFoldout = EditorGUILayout.Foldout(_previewsFoldout, "Noise Previews (1:1 seed)");
        if (_previewsFoldout)
        {
            if (GUILayout.Button("Refresh Previews"))
                RefreshPreviews(gen);

            var s = gen.settings;
            DrawPreviewRow("Continentalness", _contPreview,
                           "Erosion", _erosionPreview);
            DrawPreviewRow("Weirdness → Peaks & Valleys", _weirdPreview,
                           "Temperature", _tempPreview);
            DrawPreviewRow("Humidity", _humPreview,
                           "Detail", _detailPreview);
        }

        // --- Height stats ---
        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            $"Height range: {gen.settings.baseHeight - gen.settings.heightAmplitude} " +
            $"to {gen.settings.baseHeight + gen.settings.heightAmplitude}\n" +
            $"Sea level: {gen.settings.seaLevel}\n" +
            $"Chunk size: {gen.settings.chunkSize}",
            MessageType.None);
    }

    void RefreshPreviews(ProceduralWorldGenerator gen)
    {
        var s = gen.settings;
        _contPreview   = s.continentalnessNoise ? gen.GetNoisePreview(s.continentalnessNoise, PREVIEW_SIZE)            : null;
        _erosionPreview = s.erosionNoise         ? gen.GetNoisePreview(s.erosionNoise,         PREVIEW_SIZE)            : null;
        _weirdPreview  = s.weirdnessNoise        ? gen.GetNoisePreview(s.weirdnessNoise,       PREVIEW_SIZE, applyRidgeFold: true) : null;
        _tempPreview   = s.temperatureNoise      ? gen.GetNoisePreview(s.temperatureNoise,     PREVIEW_SIZE)            : null;
        _humPreview    = s.humidityNoise         ? gen.GetNoisePreview(s.humidityNoise,        PREVIEW_SIZE)            : null;
        _detailPreview = s.detailNoise           ? gen.GetNoisePreview(s.detailNoise,          PREVIEW_SIZE)            : null;
        Repaint();
    }

    void DrawPreviewRow(string labelA, Texture2D texA, string labelB, Texture2D texB)
    {
        EditorGUILayout.BeginHorizontal();
        DrawPreview(labelA, texA);
        DrawPreview(labelB, texB);
        EditorGUILayout.EndHorizontal();
    }

    void DrawPreview(string label, Texture2D tex)
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(130));
        EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(130));
        if (tex != null)
            GUILayout.Label(tex, GUILayout.Width(PREVIEW_SIZE * 2), GUILayout.Height(PREVIEW_SIZE * 2));
        else
            EditorGUILayout.HelpBox("No noise\nassigned", MessageType.None, wide: false);
        EditorGUILayout.EndVertical();
    }
}
#endif
