using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using _Scripts.WorldGen;
using ProceduralWorld.Generation;
using ProceduralWorld.Data;

/// <summary>
/// WorldGenerator FINAL — Complete integration for 3D isometric terrain
/// 
/// ARCHITECTURE:
///   • ChunkDataGenerator: Pure data (thread-safe)
///   • ImprovedNoiseGenerator: Noise + biome + elevation logic
///   • 3-Level Tilemap: groundLayer, midLayer, highLayer
///   • Objects: Spawn with elevation Y-offset
///   • Y-Sorting: Dynamic (ceil(Y) + elevation * 10)
/// 
/// SETUP REQUIRED:
///   1. Grid with Cell Layout "Isometric Z As Y"
///   2. 3 Tilemaps (ground, mid, high)
///   3. BiomeTileSets configured (5 biomes)
///   4. noiseConfig set
///   5. elevationYOffset set (0.5 recommended)
/// </summary>
public class WorldGenerator : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════
    // SEED & CONFIGURATION
    // ═══════════════════════════════════════════════════════════

    [Header("=== SEED ===")]
    public int seed = 12345;
    public bool randomSeedEachTime = true;

    [Header("=== NOISE CONFIG ===")]
    public NoiseConfig noiseConfig = new NoiseConfig
    {
        scale = 0.04f,
        octaves = 6,
        persistence = 0.5f,
        lacunarity = 2.0f,
        redistributionPower = 1.0f,
        detailStrength = 0.15f,
    };

    public AnimationCurve heightCurve;

    // ═══════════════════════════════════════════════════════════
    // 3-LEVEL TILEMAP SYSTEM (CRITICAL FOR 3D TERRAIN)
    // ═══════════════════════════════════════════════════════════

    [Header("=== TILEMAPS (3-LEVEL ELEVATION) ===")]
    [Tooltip("Elevation 0: Base terrain")]
    public Tilemap groundLayer;

    [Tooltip("Elevation 1: Hills, cliffs")]
    public Tilemap midLayer;

    [Tooltip("Elevation 2: Mountains, peaks")]
    public Tilemap highLayer;

    // ═══════════════════════════════════════════════════════════
    // ELEVATION THRESHOLDS (MUST MATCH ChunkDataGenerator)
    // ═══════════════════════════════════════════════════════════

    [Header("=== ELEVATION THRESHOLDS ===")]
    [Tooltip("Noise threshold for elevation 1 (must be 0.4)")]
    public float midLevelThreshold = 0.4f;

    [Tooltip("Noise threshold for elevation 2 (must be 0.7)")]
    public float highLevelThreshold = 0.7f;

    [Tooltip("Y-offset per elevation level (0.5 recommended)")]
    public float elevationYOffset = 0.5f;

    // ═══════════════════════════════════════════════════════════
    // BIOME TILE SETS
    // ═══════════════════════════════════════════════════════════

    [Header("=== BIOME TILE SETS ===")]
    public List<BiomeTileSet> biomeTileSets = new List<BiomeTileSet>();

    // ═══════════════════════════════════════════════════════════
    // OBJECT SPAWNING
    // ═══════════════════════════════════════════════════════════

    [Header("=== OBJECT SPAWNING ===")]
    public string worldObjectsRootPath = "Prefabs/WorldObjects";
    public Transform objectsParent;
    public bool logLoadedConfigs = true;

    [HideInInspector] public List<SpawnableObjectConfig> spawnableObjects = new List<SpawnableObjectConfig>();

    // ═══════════════════════════════════════════════════════════
    // CHUNK SYSTEM
    // ═══════════════════════════════════════════════════════════

    [Header("=== CHUNK SYSTEM ===")]
    [Range(8, 64)] public int chunkSize = 16;
    [Range(1, 8)] public int viewDistance = 4;
    public int unloadDistance = 6;
    public int chunksPerFrame = 2;

    // ═══════════════════════════════════════════════════════════
    // EDITOR
    // ═══════════════════════════════════════════════════════════

    [Header("=== EDITOR ===")]
    public bool showNoisePreview = true;
    [Range(32, 256)] public int previewResolution = 128;
    [HideInInspector] public Texture2D noisePreviewTexture;

    // ═══════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ═══════════════════════════════════════════════════════════

    private ChunkDataGenerator chunkGen;
    private Grid grid;
    private Camera mainCam;

    private Dictionary<Vector2Int, ChunkData> loadedChunks = new();
    private Queue<Vector2Int> chunkLoadQueue = new();
    private Vector2Int lastPlayerChunk = Vector2Int.zero;

    // ═══════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════

    void Start()
    {
        // Find grid and camera
        grid = GetComponentInParent<Grid>() ?? FindFirstObjectByType<Grid>();
        mainCam = Camera.main;

        if (grid == null)
        {
            Debug.LogError("[WorldGenerator] Grid not found!");
            enabled = false;
            return;
        }

        // Initialize seed
        if (randomSeedEachTime)
            seed = Random.Range(0, 1000000);

        // Create default height curve if none
        if (heightCurve == null || heightCurve.length == 0)
            heightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        // Initialize ChunkDataGenerator
        chunkGen = new ChunkDataGenerator(noiseConfig, seed, heightCurve);

        // Create objects parent if needed
        if (objectsParent == null)
            objectsParent = new GameObject("WorldObjects").transform;

        // Setup camera sorting
        mainCam.transparencySortMode = TransparencySortMode.CustomAxis;
        mainCam.transparencySortAxis = new Vector3(0f, 1f, 0f);

        // Load tiles and objects
        LoadBiomeTileSets();
        LoadSpawnableObjects();

        Debug.Log($"[WorldGenerator] Initialized: Seed={seed}, Biomes={biomeTileSets.Count}, Objects={spawnableObjects.Count}");

        // Start chunk streaming
        StartCoroutine(ChunkStreamingLoop());
    }

    void Update()
    {
        if (mainCam == null) return;

        // Update chunk loading based on camera position
        Vector3 camPos = mainCam.transform.position;
        Vector2Int playerChunk = new Vector2Int(
            Mathf.FloorToInt(camPos.x / chunkSize),
            Mathf.FloorToInt(camPos.y / chunkSize)
        );

        if (playerChunk != lastPlayerChunk)
        {
            lastPlayerChunk = playerChunk;
            EnqueueChunksForLoading(playerChunk);
            UnloadDistantChunks(playerChunk);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // CHUNK LOADING & STREAMING
    // ═══════════════════════════════════════════════════════════

    private void EnqueueChunksForLoading(Vector2Int playerChunk)
    {
        for (int x = -viewDistance; x <= viewDistance; x++)
        {
            for (int y = -viewDistance; y <= viewDistance; y++)
            {
                Vector2Int chunkCoord = playerChunk + new Vector2Int(x, y);
                if (!loadedChunks.ContainsKey(chunkCoord))
                {
                    chunkLoadQueue.Enqueue(chunkCoord);
                }
            }
        }
    }

    private void UnloadDistantChunks(Vector2Int playerChunk)
    {
        var toUnload = new List<Vector2Int>();
        foreach (var loaded in loadedChunks)
        {
            if (Vector2Int.Distance(loaded.Key, playerChunk) > unloadDistance)
            {
                toUnload.Add(loaded.Key);
            }
        }

        foreach (var coord in toUnload)
        {
            UnloadChunk(coord);
        }
    }

    private IEnumerator ChunkStreamingLoop()
    {
        while (true)
        {
            int processed = 0;
            while (chunkLoadQueue.Count > 0 && processed < chunksPerFrame)
            {
                Vector2Int chunkCoord = chunkLoadQueue.Dequeue();
                LoadChunk(chunkCoord);
                processed++;
            }

            yield return null;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // CHUNK GENERATION & RENDERING
    // ═══════════════════════════════════════════════════════════

    private void LoadChunk(Vector2Int chunkCoord)
    {
        if (loadedChunks.ContainsKey(chunkCoord)) return;

        // Generate chunk data
        var chunk = new ChunkData(chunkCoord, chunkSize);
        chunkGen.GenerateChunkData(chunk, chunkSize, 1f);

        // Render tiles on 3 elevation layers
        RenderChunkTiles(chunk);

        // Spawn objects
        SpawnChunkObjects(chunk);

        loadedChunks[chunkCoord] = chunk;
    }

    private void UnloadChunk(Vector2Int chunkCoord)
    {
        if (!loadedChunks.TryGetValue(chunkCoord, out var chunk)) return;

        // Destroy spawned objects
        foreach (var obj in chunk.spawnedObjects)
        {
            if (obj != null)
                Destroy(obj);
        }

        // Clear tiles
        ClearChunkFromTilemaps(chunk);

        loadedChunks.Remove(chunkCoord);
    }

    // ═══════════════════════════════════════════════════════════
    // TILE RENDERING (3 ELEVATION LAYERS)
    // ═══════════════════════════════════════════════════════════

    private void RenderChunkTiles(ChunkData chunk)
    {
        for (int cy = 0; cy < chunkSize; cy++)
        {
            for (int cx = 0; cx < chunkSize; cx++)
            {
                int worldX = chunk.coord.x * chunkSize + cx;
                int worldY = chunk.coord.y * chunkSize + cy;
                float noiseValue = chunk.noiseValues[cy, cx];
                int elevation = chunk.elevationLevels[cy, cx];

                // Get tilemap for this elevation
                Tilemap targetLayer = GetLayerForElevation(elevation);
                if (targetLayer == null) continue;

                // Get tile for this noise value
                TileBase tile = GetTileForNoise(noiseValue);
                if (tile == null) continue;

                // Place tile
                Vector3Int tilePos = new Vector3Int(worldX, worldY, 0);
                targetLayer.SetTile(tilePos, tile);
            }
        }
    }

    private Tilemap GetLayerForElevation(int elevation)
    {
        return elevation switch
        {
            0 => groundLayer,
            1 => midLayer,
            2 => highLayer,
            _ => groundLayer
        };
    }

    private TileBase GetTileForNoise(float noiseValue)
    {
        // Find matching biome tileset
        foreach (var biome in biomeTileSets)
        {
            if (noiseValue >= biome.noiseMin && noiseValue <= biome.noiseMax)
            {
                return biome.GetRandomTile();
            }
        }

        // Fallback
        if (biomeTileSets.Count > 0)
            return biomeTileSets[0].GetRandomTile();

        return null;
    }

    private void ClearChunkFromTilemaps(ChunkData chunk)
    {
        for (int cy = 0; cy < chunkSize; cy++)
        {
            for (int cx = 0; cx < chunkSize; cx++)
            {
                int worldX = chunk.coord.x * chunkSize + cx;
                int worldY = chunk.coord.y * chunkSize + cy;
                Vector3Int tilePos = new Vector3Int(worldX, worldY, 0);

                groundLayer.SetTile(tilePos, null);
                midLayer.SetTile(tilePos, null);
                highLayer.SetTile(tilePos, null);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // OBJECT SPAWNING (WITH ELEVATION OFFSET)
    // ═══════════════════════════════════════════════════════════

    private void SpawnChunkObjects(ChunkData chunk)
    {
        for (int cy = 0; cy < chunkSize; cy++)
        {
            for (int cx = 0; cx < chunkSize; cx++)
            {
                int worldX = chunk.coord.x * chunkSize + cx;
                int worldY = chunk.coord.y * chunkSize + cy;
                BiomeType biome = chunk.biomeMap[cy, cx];
                int elevation = chunk.elevationLevels[cy, cx];

                // Don't spawn in water
                if (biome == BiomeType.Water) continue;

                // Select random object for this biome
                var config = SelectRandomObjectForBiome(biome);
                if (config == null) continue;

                // Check if can spawn
                if (!chunkGen.CanSpawnObject(
                    worldX, worldY,
                    biome,
                    elevation,
                    config.spawnChance,
                    config.useClusterNoise,
                    config.clusterNoiseScale,
                    config.clusterThreshold))
                {
                    continue;
                }

                // Get spawn position
                Vector3 spawnPos = chunkGen.GetSpawnPosition(
                    worldX, worldY,
                    1f,
                    config.yOffset,
                    config.positionJitter
                );

                // ★ CRITICAL: Add elevation offset ★
                spawnPos.y += elevation * elevationYOffset;

                // Spawn object
                var instance = Instantiate(
                    config.prefab,
                    spawnPos,
                    Quaternion.identity,
                    objectsParent
                );

                // Setup Y-sorting
                var spriteRenderer = instance.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    // Dynamic order: ceil(Y) + elevation bonus
                    int sortOrder = Mathf.CeilToInt(spawnPos.y) + (elevation * 10);
                    spriteRenderer.sortingOrder = sortOrder;
                    spriteRenderer.sortingLayerName = "WorldObjects";
                }

                // Random scale
                float scale = Random.Range(config.minScale, config.maxScale);
                instance.transform.localScale = Vector3.one * scale;

                // Track
                chunk.spawnedObjects.Add(instance);
            }
        }
    }

    private SpawnableObjectConfig SelectRandomObjectForBiome(BiomeType biome)
    {
        var candidates = new List<SpawnableObjectConfig>();

        foreach (var cfg in spawnableObjects)
        {
            // Filter by biome
            bool matches = false;

            if (cfg.allowedTiles == null || cfg.allowedTiles.Length == 0)
            {
                matches = true; // No restrictions
            }
            else
            {
                // Check if any allowed tile matches this biome
                foreach (var biomeTile in biomeTileSets)
                {
                    if (IsBiomeMatch(biomeTile, biome))
                    {
                        foreach (var allowedTile in cfg.allowedTiles)
                        {
                            if (allowedTile == biomeTile.PrimaryTile)
                            {
                                matches = true;
                                break;
                            }
                        }
                        if (matches) break;
                    }
                }
            }

            if (matches)
                candidates.Add(cfg);
        }

        return candidates.Count > 0
            ? candidates[Random.Range(0, candidates.Count)]
            : null;
    }

    private bool IsBiomeMatch(BiomeTileSet tileset, BiomeType biome)
    {
        // Match tileset's noise range to biome type
        return biome switch
        {
            BiomeType.Water => tileset.noiseMin < 0.20f,
            BiomeType.Brush => tileset.noiseMin >= 0.35f && tileset.noiseMin < 0.55f,
            BiomeType.Path  => tileset.noiseMin >= 0.20f && tileset.noiseMin < 0.35f,
            BiomeType.Grass => tileset.noiseMin >= 0.55f && tileset.noiseMin < 0.80f,
            BiomeType.Stone => tileset.noiseMin >= 0.80f,
            _ => false
        };
    }

    // ═══════════════════════════════════════════════════════════
    // CONFIGURATION LOADING
    // ═══════════════════════════════════════════════════════════

    private void LoadBiomeTileSets()
    {
        foreach (var biome in biomeTileSets)
        {
            if (string.IsNullOrEmpty(biome.folderName)) continue;

            string path = $"Tiles/32x_Tiles/{biome.folderName}";
            biome.tiles = Resources.LoadAll<TileBase>(path);

            if (biome.tiles.Length > 0 && logLoadedConfigs)
            {
                Debug.Log($"[WorldGenerator] Loaded {biome.tiles.Length} tiles from {path}");
            }
        }
    }

    private void LoadSpawnableObjects()
    {
        spawnableObjects.Clear();
        var configs = Resources.LoadAll<SpawnableObjectConfig>(worldObjectsRootPath);

        foreach (var cfg in configs)
        {
            spawnableObjects.Add(cfg);
        }

        if (logLoadedConfigs)
        {
            Debug.Log($"[WorldGenerator] Loaded {spawnableObjects.Count} SpawnableObjectConfigs");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // EDITOR PREVIEW
    // ═══════════════════════════════════════════════════════════

    public void BakeNoisePreview()
    {
        noisePreviewTexture = NoiseMapGenerator.GeneratePreviewTexture(
            previewResolution, previewResolution,
            noiseConfig.scale,
            noiseConfig.octaves,
            noiseConfig.persistence,
            noiseConfig.lacunarity,
            noiseConfig.redistributionPower,
            0.8f,
            0f, 0f,
            true
        );

        Debug.Log("[WorldGenerator] Noise preview baked");
    }

    // ═══════════════════════════════════════════════════════════
    // PUBLIC API (FOR PlayerController)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Get elevation level (0, 1, or 2) at world position
    /// </summary>
    public int GetElevationAt(Vector3 worldPos)
    {
        int worldX = Mathf.FloorToInt(worldPos.x);
        int worldY = Mathf.FloorToInt(worldPos.y);

        Vector2Int chunkCoord = new Vector2Int(
            Mathf.FloorToInt(worldX / (float)chunkSize),
            Mathf.FloorToInt(worldY / (float)chunkSize)
        );

        if (!loadedChunks.TryGetValue(chunkCoord, out var chunk))
            return 0;

        int localX = worldX - chunkCoord.x * chunkSize;
        int localY = worldY - chunkCoord.y * chunkSize;

        if (localX < 0 || localX >= chunkSize || localY < 0 || localY >= chunkSize)
            return 0;

        return chunk.elevationLevels[localY, localX];
    }

    /// <summary>
    /// Get height-adjusted Y position (includes elevation offset)
    /// </summary>
    public float GetHeightAdjustedY(Vector3 basePos, int elevation)
    {
        return basePos.y + elevation * elevationYOffset;
    }
}

[System.Serializable]
public class BiomeTileSet
{
    public string folderName;
    public float noiseMin;
    public float noiseMax;
    public TileBase[] tiles;

    public TileBase PrimaryTile => (tiles != null && tiles.Length > 0) ? tiles[0] : null;

    public TileBase GetRandomTile()
    {
        if (tiles == null || tiles.Length == 0) return null;
        return tiles[Random.Range(0, tiles.Length)];
    }
}