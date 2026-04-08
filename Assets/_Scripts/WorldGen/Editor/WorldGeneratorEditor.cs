using _Scripts.WorldGen;
using UnityEngine;
using UnityEditor;
using ProceduralWorld.Generation;

[CustomEditor(typeof(WorldGenerator))]
public class WorldGeneratorEditor : Editor
{
    private bool showBaseSettings = true;
    private bool showMeshSettings = true;
    private bool showNoiseSettings = true;

    public override void OnInspectorGUI()
    {
        var wg = (WorldGenerator)target;
        
        serializedObject.Update();

        // 1. Gán Config trước
        EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
        wg.config = (WorldGenerationConfig)EditorGUILayout.ObjectField("World Gen Config", wg.config, typeof(WorldGenerationConfig), false);

        if (wg.config == null)
        {
            EditorGUILayout.HelpBox("Hãy gán WorldGenerationConfig để bắt đầu.", MessageType.Warning);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        EditorGUILayout.Space();

        // 2. Base Settings (Sử dụng foldout thường thay vì HeaderGroup để tránh lỗi nesting nếu configEditor có header)
        showBaseSettings = EditorGUILayout.Foldout(showBaseSettings, "Base World Settings", true);
        if (showBaseSettings)
        {
            EditorGUI.indentLevel++;
            wg.objectsParent = (Transform)EditorGUILayout.ObjectField("Objects Parent", wg.objectsParent, typeof(Transform), true);
            wg.decorationLayer = (UnityEngine.Tilemaps.Tilemap)EditorGUILayout.ObjectField("Decoration Layer", wg.decorationLayer, typeof(UnityEngine.Tilemaps.Tilemap), true);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        // 3. Hiển thị nội dung Config (Không bọc trong Foldout Header Group)
        EditorGUILayout.LabelField("Advanced Noise & Shaping Settings", EditorStyles.boldLabel);
        Editor.CreateCachedEditor(wg.config, null, ref configEditor);
        if (configEditor != null)
        {
            configEditor.OnInspectorGUI();
        }

        EditorGUILayout.Space();

        // 4. Bake Button
        if (GUILayout.Button("🗺 Bake Noise Preview", GUILayout.Height(32)))
        {
            wg.BakeNoisePreview();
        }

        if (WorldGenerator.LastBakePreview != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Noise Preview Result:", EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetRect(256, 256, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(rect, WorldGenerator.LastBakePreview);
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Kiến trúc Hybrid: Địa hình (Mesh) + Trang trí (Tilemap).", MessageType.Info);

        serializedObject.ApplyModifiedProperties();
        if (GUI.changed)
        {
            EditorUtility.SetDirty(wg);
            EditorUtility.SetDirty(wg.config);
        }
    }

    private Editor configEditor;
}