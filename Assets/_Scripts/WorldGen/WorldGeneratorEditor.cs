// ==================== FILENAME: WorldGenerationConfigEditor_FIXED.cs ====================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProceduralWorld.Generation;

[CustomEditor(typeof(WorldGenerationConfig))]
public class WorldGenerationConfigEditor : Editor
{
    private bool showNoiseLayers = true;
    private bool showNoisePreview = true;
    private bool showSplineShaping = true;
    private bool showPerlinWorms = true;
    private bool showDensityField = true;
    private bool showHeightmapTexture = true;
    private bool showBiomes = true;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var config = (WorldGenerationConfig)target;

        EditorGUILayout.LabelField("🌍 MINECRAFT-STYLE WORLD GENERATION", EditorStyles.boldLabel);
        EditorGUILayout.Space(8);

        #region Quick Setup Button
        EditorGUILayout.LabelField("⚡ QUICK SETUP", EditorStyles.boldLabel);
        if (GUILayout.Button("🎲 Generate Default Biomes", GUILayout.Height(35)))
        {
            GenerateDefaultBiomes(config);
            EditorUtility.SetDirty(config);
        }

        if (GUILayout.Button("📊 Generate Noise Previews", GUILayout.Height(35)))
        {
            GenerateNoisePreviews(config);
            EditorUtility.SetDirty(config);
        }

        EditorGUILayout.Space(8);
        #endregion

        #region Noise Layers
        showNoiseLayers = EditorGUILayout.Foldout(showNoiseLayers, "🎲 MULTI-LAYER NOISE", true, EditorStyles.foldoutHeader);
        if (showNoiseLayers)
        {
            EditorGUI.indentLevel++;

            // Continentalness
            EditorGUILayout.LabelField("Continentalness (Land vs Ocean)", EditorStyles.boldLabel);
            DrawNoiseConfigWithPreview(config.layeredNoise.continentalness, "Continentalness");
            config.layeredNoise.continentalWeight = EditorGUILayout.Slider("Weight", config.layeredNoise.continentalWeight, 0f, 1f);
            EditorGUILayout.Space(4);

            // Erosion
            EditorGUILayout.LabelField("Erosion (Ridges vs Valleys)", EditorStyles.boldLabel);
            DrawNoiseConfigWithPreview(config.layeredNoise.erosion, "Erosion");
            config.layeredNoise.erosionWeight = EditorGUILayout.Slider("Weight", config.layeredNoise.erosionWeight, 0f, 1f);
            EditorGUILayout.Space(4);

            // Peaks & Valleys
            EditorGUILayout.LabelField("Peaks & Valleys", EditorStyles.boldLabel);
            DrawNoiseConfigWithPreview(config.layeredNoise.peaksValleys, "Peaks & Valleys");
            config.layeredNoise.peaksValleysWeight = EditorGUILayout.Slider("Weight", config.layeredNoise.peaksValleysWeight, 0f, 1f);
            EditorGUILayout.Space(4);

            // Detail
            EditorGUILayout.LabelField("Fine Detail", EditorStyles.boldLabel);
            DrawNoiseConfigWithPreview(config.layeredNoise.detail, "Detail");
            config.layeredNoise.detailWeight = EditorGUILayout.Slider("Weight", config.layeredNoise.detailWeight, 0f, 1f);

            EditorGUI.indentLevel--;
        }
        #endregion

        #region Spline Shaping
        showSplineShaping = EditorGUILayout.Foldout(showSplineShaping, "📈 SPLINE SHAPING", true, EditorStyles.foldoutHeader);
        if (showSplineShaping)
        {
            EditorGUI.indentLevel++;

            config.splineShape.heightCurve = EditorGUILayout.CurveField(
                new GUIContent("Height Curve", "Final elevation distribution"),
                config.splineShape.heightCurve
            );
            config.splineShape.continentalnessCurve = EditorGUILayout.CurveField(
                new GUIContent("Continentalness Curve", "Shape landmass"),
                config.splineShape.continentalnessCurve
            );
            config.splineShape.erosionCurve = EditorGUILayout.CurveField(
                new GUIContent("Erosion Curve", "Shape valleys/ridges"),
                config.splineShape.erosionCurve
            );

            EditorGUILayout.Space(4);

            config.splineShape.heightMultiplier = EditorGUILayout.Slider(
                new GUIContent("Height Multiplier", "Scale terrain (1-50)"),
                config.splineShape.heightMultiplier, 1f, 50f
            );

            EditorGUI.indentLevel--;
        }
        #endregion

        #region Perlin Worms
        showPerlinWorms = EditorGUILayout.Foldout(showPerlinWorms, "🐍 PERLIN WORMS (Rivers)", true, EditorStyles.foldoutHeader);
        if (showPerlinWorms)
        {
            EditorGUI.indentLevel++;
            config.perlinWorms.enabled = EditorGUILayout.Toggle("Enabled", config.perlinWorms.enabled);

            if (config.perlinWorms.enabled)
            {
                config.perlinWorms.wormScale = EditorGUILayout.Slider("Scale", config.perlinWorms.wormScale, 0.01f, 0.1f);
                config.perlinWorms.wormOctaves = EditorGUILayout.IntSlider("Octaves", config.perlinWorms.wormOctaves, 1, 6);
                config.perlinWorms.wormWidth = EditorGUILayout.Slider("Width", config.perlinWorms.wormWidth, 0.1f, 2f);
                config.perlinWorms.wormStrength = EditorGUILayout.Slider("Carving Strength", config.perlinWorms.wormStrength, 0f, 0.5f);
            }

            EditorGUI.indentLevel--;
        }
        #endregion

        #region Density Field
        showDensityField = EditorGUILayout.Foldout(showDensityField, "🕳️ 3D DENSITY FIELD (Caves)", true, EditorStyles.foldoutHeader);
        if (showDensityField)
        {
            EditorGUI.indentLevel++;
            config.densityField.enabled = EditorGUILayout.Toggle("Enabled", config.densityField.enabled);

            if (config.densityField.enabled)
            {
                config.densityField.scale3D = EditorGUILayout.Slider("Scale", config.densityField.scale3D, 0.05f, 0.2f);
                config.densityField.octaves3D = EditorGUILayout.IntSlider("Octaves", config.densityField.octaves3D, 1, 6);
                config.densityField.caveThreshold = EditorGUILayout.Slider("Threshold", config.densityField.caveThreshold, 0f, 1f);
                config.densityField.caveStrength = EditorGUILayout.Slider("Strength", config.densityField.caveStrength, 0f, 1f);
            }

            EditorGUI.indentLevel--;
        }
        #endregion

        #region Heightmap Texture
        showHeightmapTexture = EditorGUILayout.Foldout(showHeightmapTexture, "🖼️ HEIGHTMAP TEXTURE", true, EditorStyles.foldoutHeader);
        if (showHeightmapTexture)
        {
            EditorGUI.indentLevel++;

            config.heightmapTexture.heightmapTexture = EditorGUILayout.ObjectField(
                "Heightmap Texture",
                config.heightmapTexture.heightmapTexture,
                typeof(Texture2D), false
            ) as Texture2D;

            if (config.heightmapTexture.heightmapTexture != null)
            {
                config.heightmapTexture.textureBlendAmount = EditorGUILayout.Slider(
                    "Blend Amount", config.heightmapTexture.textureBlendAmount, 0f, 1f
                );
                config.heightmapTexture.textureScale = EditorGUILayout.Slider(
                    "Texture Scale", config.heightmapTexture.textureScale, 0.1f, 10f
                );
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("📸 Generate Heightmap Texture from Noise", GUILayout.Height(30)))
            {
                HeightmapTextureGenerator.GenerateAndSave(config);
            }

            EditorGUI.indentLevel--;
        }
        #endregion

        #region Water & Beach
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("💧 WATER LEVEL", EditorStyles.boldLabel);
        config.waterLevel = EditorGUILayout.Slider("Water Level", config.waterLevel, 0f, 0.5f);
        config.beachHeight = EditorGUILayout.Slider("Beach Height", config.beachHeight, 0f, 0.1f);
        #endregion

        #region Biomes
        EditorGUILayout.Space(8);
        showBiomes = EditorGUILayout.Foldout(showBiomes, "🌳 BIOME CONFIGURATION", true, EditorStyles.foldoutHeader);
        if (showBiomes)
        {
            EditorGUI.indentLevel++;

            if (config.biomes.Count == 0)
            {
                EditorGUILayout.HelpBox("No biomes configured! Click 'Generate Default Biomes' to create them.", MessageType.Warning);
            }

            for (int i = 0; i < config.biomes.Count; i++)
            {
                var biome = config.biomes[i];

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Biome {i + 1}: {biome.biomeName}", EditorStyles.boldLabel);

                biome.biomeName = EditorGUILayout.TextField("Biome Name", biome.biomeName);
                biome.minHeight = EditorGUILayout.Slider("Min Height", biome.minHeight, 0f, 1f);
                biome.maxHeight = EditorGUILayout.Slider("Max Height", biome.maxHeight, 0f, 1f);

                EditorGUILayout.LabelField("Tiles for this biome:", EditorStyles.boldLabel);
                SerializedProperty tilesProperty = serializedObject.FindProperty($"biomes.Array.data[{i}].tilesToUse");
                EditorGUILayout.PropertyField(tilesProperty, true);

                EditorGUILayout.EndVertical();
            }

            EditorGUI.indentLevel--;
        }
        #endregion

        #region Mesh Settings
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("🔲 MESH SETTINGS", EditorStyles.boldLabel);
        config.meshResolution = EditorGUILayout.IntSlider("Mesh Resolution", config.meshResolution, 1, 20);
        config.meshDisplacement = EditorGUILayout.Slider("Displacement Amount", config.meshDisplacement, 0f, 1f);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("🔀 RENDERING MODE", EditorStyles.boldLabel);
        config.useTilemapFallback = EditorGUILayout.Toggle(
            new GUIContent("Use Tilemap (Fallback)", "Check to render with tilemaps instead of mesh"),
            config.useTilemapFallback
        );
        #endregion

        EditorGUILayout.Space(12);
        EditorGUILayout.HelpBox(
            "Setup Instructions:\n\n" +
            "1. Click 'Generate Default Biomes'\n" +
            "2. Assign tiles to each biome\n" +
            "3. Adjust noise parameters\n" +
            "4. Create WorldGenerator in scene\n" +
            "5. Assign this config to WorldGenerator\n" +
            "6. Press Play!",
            MessageType.Info
        );

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawNoiseConfigWithPreview(NoiseConfig config, string label)
    {
        EditorGUI.indentLevel++;

        config.scale = EditorGUILayout.Slider("Scale", config.scale, 0.01f, 0.2f);
        config.octaves = EditorGUILayout.IntSlider("Octaves", config.octaves, 1, 10);
        config.persistence = EditorGUILayout.Slider("Persistence", config.persistence, 0.3f, 0.9f);
        config.lacunarity = EditorGUILayout.Slider("Lacunarity", config.lacunarity, 1.5f, 2.5f);
        config.redistributionPower = EditorGUILayout.Slider("Redistribution Power", config.redistributionPower, 0.3f, 2.5f);

        // Small preview
        if (config.previewTexture != null)
        {
            Rect previewRect = GUILayoutUtility.GetRect(64, 64, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(previewRect, config.previewTexture, ScaleMode.ScaleToFit);
        }

        EditorGUI.indentLevel--;
    }

    private void GenerateDefaultBiomes(WorldGenerationConfig config)
    {
        config.biomes.Clear();

        config.biomes.Add(new BiomeTileSetConfig
        {
            biomeName = "Water",
            minHeight = 0f,
            maxHeight = 0.38f
        });

        config.biomes.Add(new BiomeTileSetConfig
        {
            biomeName = "Beach",
            minHeight = 0.38f,
            maxHeight = 0.46f
        });

        config.biomes.Add(new BiomeTileSetConfig
        {
            biomeName = "Grass",
            minHeight = 0.46f,
            maxHeight = 0.70f
        });

        config.biomes.Add(new BiomeTileSetConfig
        {
            biomeName = "Stone",
            minHeight = 0.70f,
            maxHeight = 1f
        });

        Debug.Log("✅ Default biomes created!");
    }

    private void GenerateNoisePreviews(WorldGenerationConfig config)
    {
        const int previewSize = 128;

        config.layeredNoise.continentalness.previewTexture = GenerateNoisePreview(
            config.layeredNoise.continentalness, previewSize
        );

        config.layeredNoise.erosion.previewTexture = GenerateNoisePreview(
            config.layeredNoise.erosion, previewSize
        );

        config.layeredNoise.peaksValleys.previewTexture = GenerateNoisePreview(
            config.layeredNoise.peaksValleys, previewSize
        );

        config.layeredNoise.detail.previewTexture = GenerateNoisePreview(
            config.layeredNoise.detail, previewSize
        );

        Debug.Log("✅ Noise previews generated!");
    }

    private Texture2D GenerateNoisePreview(NoiseConfig noiseConfig, int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, false);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float sampleX = x / (float)size * 10f;
                float sampleY = y / (float)size * 10f;

                float noise = ImprovedNoiseGenerator.GetFBM(
                    sampleX, sampleY,
                    noiseConfig.scale,
                    noiseConfig.octaves,
                    noiseConfig.persistence,
                    noiseConfig.lacunarity,
                    noiseConfig.redistributionPower,
                    12345,
                    Vector2.zero
                );

                Color color = new Color(noise, noise, noise);
                pixels[y * size + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}

public static class HeightmapTextureGenerator
{
    public static void GenerateAndSave(WorldGenerationConfig config)
    {
        const int textureSize = 512;
        Texture2D heightmap = new Texture2D(textureSize, textureSize, 
                                            TextureFormat.RGBAFloat, false);
        Color[] pixels = new Color[textureSize * textureSize];
 
        Debug.Log("[HeightmapTextureGenerator] Generating 512x512 heightmap...");
 
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float sampleX = x / (float)textureSize * 20f;
                float sampleY = y / (float)textureSize * 20f;
 
                float height = ImprovedNoiseGenerator.GetFinalHeight(
                    sampleX, sampleY,
                    config.layeredNoise,
                    config.splineShape,
                    config.perlinWorms,
                    config.densityField,
                    new HeightmapTextureConfig { heightmapTexture = null },  // Don't blend with null
                    12345
                );
 
                pixels[y * textureSize + x] = new Color(height, height, height, 1f);
            }
        }
 
        heightmap.SetPixels(pixels);
        heightmap.Apply();
 
        // Create directory if needed
        string dirPath = "Assets/Heightmaps";
        if (!System.IO.Directory.Exists(dirPath))
        {
            System.IO.Directory.CreateDirectory(dirPath);
        }
 
        // Save PNG
        string fileName = $"generated_heightmap_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
        string filePath = System.IO.Path.Combine(dirPath, fileName);
        byte[] pngData = heightmap.EncodeToPNG();
        System.IO.File.WriteAllBytes(filePath, pngData);
 
        Debug.Log($"[HeightmapTextureGenerator] Saved to {filePath}");
 
        // Refresh assets
        UnityEditor.AssetDatabase.Refresh();
 
        // Load the texture
        Texture2D loadedTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(filePath);
        if (loadedTexture == null)
        {
            Debug.LogError("[HeightmapTextureGenerator] Failed to load saved texture!");
            return;
        }
 
        Debug.Log("[HeightmapTextureGenerator] Texture loaded, now setting as readable...");
 
        // Get texture importer and set as readable
        UnityEditor.TextureImporter importer = 
            UnityEditor.AssetImporter.GetAtPath(filePath) as UnityEditor.TextureImporter;
        
        if (importer != null)
        {
            importer.isReadable = true;  // ← KEY FIX: Set as readable
            importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            
            UnityEditor.EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            
            Debug.Log("[HeightmapTextureGenerator] ✅ Texture set as readable");
        }
        else
        {
            Debug.LogError("[HeightmapTextureGenerator] Could not get TextureImporter!");
            return;
        }
 
        // Reload after reimport
        loadedTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(filePath);
        
        if (loadedTexture != null)
        {
            config.heightmapTexture.heightmapTexture = loadedTexture;
            UnityEditor.EditorUtility.SetDirty(config);
            Debug.Log($"[HeightmapTextureGenerator] ✅ Heightmap assigned to config!");
        }
        else
        {
            Debug.LogError("[HeightmapTextureGenerator] Failed to load texture after reimport!");
        }
    }
}
#endif