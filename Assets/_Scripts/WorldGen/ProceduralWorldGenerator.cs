using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Main entry point. Attach to the Grid GameObject.
/// Handles both finite map generation (like your current setup)
/// and infinite chunk streaming (via ChunkManager).
/// </summary>
[RequireComponent(typeof(ChunkManager))]
public class ProceduralWorldGenerator : MonoBehaviour
{
    public WorldGeneratorSettings settings;

    [Header("Scene References")]
    [Tooltip("Assign Tilemap components from the active scene here (not in the settings asset).")]
    public Tilemap terrainTilemap;
    public Tilemap waterTilemap;
    public Tilemap decorationTilemap;

    [Header("Generation Mode")]
    public bool infiniteWorld = false;
    [Tooltip("Half-size of map when infiniteWorld = false")]
    public int finiteHalfSize = 50;

    [Header("Debug")]
    public bool autoGenerateOnStart = true;
    public bool showChunkBorders = false;

    private ChunkManager _chunkManager;
    private NoisePreviewData _lastPreview;

    void Awake()
    {
        if (settings != null)
        {
            // Scene references cannot be serialized into ScriptableObject assets.
            settings.terrainTilemap = terrainTilemap;
            settings.waterTilemap = waterTilemap;
            settings.decorationTilemap = decorationTilemap;
        }

        _chunkManager = GetComponent<ChunkManager>();
        _chunkManager.settings = settings;
    }

    void Start()
    {
        if (autoGenerateOnStart)
            Generate();
    }

    [ContextMenu("Generate World")]
    public void Generate()
    {
        if (settings == null)
        {
            Debug.LogError("[ProceduralWorldGenerator] WorldGeneratorSettings not assigned.");
            return;
        }

        _chunkManager.ClearAll();

        if (infiniteWorld)
        {
            // ChunkManager.Update() will handle streaming
            Debug.Log("[WorldGen] Infinite mode active — chunks stream around player.");
        }
        else
        {
            Debug.Log($"[WorldGen] Generating finite map ±{finiteHalfSize} tiles...");
            _chunkManager.GenerateFullMap(finiteHalfSize);
            Debug.Log("[WorldGen] Done.");
        }
    }

    [ContextMenu("Clear World")]
    public void Clear() => _chunkManager.ClearAll();

    /// <summary>
    /// Returns a noise preview texture for a specific layer (Editor use).
    /// </summary>
    public Texture2D GetNoisePreview(NoiseSettings noiseSettings, int previewSize = 64,
                                     bool applyRidgeFold = false)
    {
        noiseSettings.RandomizeOffset();
        var tex = new Texture2D(previewSize, previewSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp
        };

        for (int x = 0; x < previewSize; x++)
        for (int y = 0; y < previewSize; y++)
        {
            float v = NoiseSampler.Sample(x, y, noiseSettings);
            if (applyRidgeFold) v = NoiseSampler.RidgesFolded(v);
            tex.SetPixel(x, y, new Color(v, v, v, 1f));
        }
        tex.Apply();
        return tex;
    }

    void OnDrawGizmosSelected()
    {
        if (!showChunkBorders || settings == null || _chunkManager == null) return;
        // Draw chunk boundaries in scene view
        // (ChunkManager doesn't expose internal dict publicly — add accessor if needed)
    }
}
