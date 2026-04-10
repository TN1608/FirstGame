using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Main entry point — attach to the Grid GameObject.
///
/// REQUIRED Unity Inspector setup:
///   Grid component  → Cell Swizzle  = XYZ
///   TerrainTilemap  → Orientation   = IsometricZAsY
///   WaterTilemap    → Orientation   = IsometricZAsY  (optional)
///
/// Components on same GameObject: ChunkManager (auto-required).
/// TilemapColumnManager is NOT used — we use single tilemap Z-as-Y.
/// </summary>
[RequireComponent(typeof(ChunkManager))]
public class ProceduralWorldGenerator : MonoBehaviour
{
    [Header("Settings")]
    public WorldGeneratorSettings settings;

    [Header("Scene Tilemap References")]
    [Tooltip("Tilemap with Orientation = IsometricZAsY. All terrain goes here.")]
    public Tilemap terrainTilemap;
    [Tooltip("Water surface tiles (same or separate tilemap).")]
    public Tilemap waterTilemap;
    [Tooltip("Decoration overlay tilemap (optional).")]
    public Tilemap decorationTilemap;

    [Header("Generation Mode")]
    public bool infiniteWorld = true;
    [Tooltip("Half-size in tiles for finite maps.")]
    public int finiteHalfSize = 48;

    [Header("Debug")]
    public bool autoGenerateOnStart = true;
    public bool showPlayerChunkGizmo = true;

    private ChunkManager _chunkManager;

    void Awake()
    {
        _chunkManager = GetComponent<ChunkManager>();

        // Auto-find tilemaps from children if not assigned
        if (terrainTilemap == null || waterTilemap == null)
            AutoAssignTilemaps();

        // Inject scene refs into settings (ScriptableObject can't hold scene refs)
        if (settings != null)
        {
            settings.terrainTilemap    = terrainTilemap;
            settings.waterTilemap      = waterTilemap;
            settings.decorationTilemap = decorationTilemap;
        }

        _chunkManager.settings = settings;
    }

    void Start()
    {
        if (autoGenerateOnStart) Generate();
    }

    [ContextMenu("Generate World")]
    public void Generate()
    {
        if (settings == null)
        {
            Debug.LogError("[WorldGen] WorldGeneratorSettings not assigned.");
            return;
        }

        _chunkManager.ClearAll();

        if (infiniteWorld)
            Debug.Log("[WorldGen] Infinite mode — chunks stream around player.");
        else
        {
            Debug.Log($"[WorldGen] Finite map ±{finiteHalfSize} tiles...");
            _chunkManager.GenerateFullMap(finiteHalfSize);
            Debug.Log("[WorldGen] Done.");
        }
    }

    [ContextMenu("Clear World")]
    public void Clear() => _chunkManager.ClearAll();

    // ── Editor preview ─────────────────────────────────────────────────

    public Texture2D GetNoisePreview(NoiseSettings ns, int size = 64,
                                     bool ridgeFold = false)
    {
        if (ns == null) return null;
        ns.RandomizeOffset();

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp
        };

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float v = NoiseSampler.Sample(x, y, ns);
            if (ridgeFold) v = NoiseSampler.RidgesFolded(v);
            tex.SetPixel(x, y, new Color(v, v, v, 1f));
        }
        tex.Apply();
        return tex;
    }

    void AutoAssignTilemaps()
    {
        foreach (var tm in GetComponentsInChildren<Tilemap>(true))
        {
            string n = tm.name.ToLowerInvariant();
            if (terrainTilemap    == null && n.Contains("terrain"))    terrainTilemap    = tm;
            else if (waterTilemap == null && n.Contains("water"))      waterTilemap      = tm;
            else if (decorationTilemap == null && n.Contains("deco"))  decorationTilemap = tm;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showPlayerChunkGizmo || settings == null || _chunkManager == null) return;

        if (_chunkManager.playerTransform == null) return;

        Vector3 playerPos = _chunkManager.playerTransform.position;
        int cs = settings.chunkSize;
        int cx = Mathf.FloorToInt(playerPos.x / cs);
        int cy = Mathf.FloorToInt(playerPos.y / cs);

        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Vector3 chunkWorld = new Vector3(cx * cs + cs * 0.5f, cy * cs + cs * 0.5f, 0f);
        Gizmos.DrawWireCube(chunkWorld, new Vector3(cs, cs, 0f));
    }
}