// ==================== WorldGenerator_COMPLETE.cs ====================
// Complete working world generator with proper 3D terrain and natural biome distribution

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using _Scripts.WorldGen;
using ProceduralWorld.Generation;
using ProceduralWorld.Data;

/// <summary>
/// COMPLETE WorldGenerator
/// 
/// Properly implements:
/// • 3D mesh terrain with vertex displacement
/// • Natural biome distribution (height-based, not random)
/// • River generation via Perlin worms
/// • Multi-layer tilemap rendering for visual depth
/// • Complete chunk streaming system
/// </summary>
public class WorldGenerator : MonoBehaviour
{
    #region ===== INSPECTOR ASSIGNMENTS =====
    [Header("=== CONFIGURATION ===")]
    [SerializeField] private WorldGenerationConfig worldGenConfig;

    [Header("=== SCENE REFERENCES ===")]
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap groundLayer;
    [SerializeField] private Transform meshParent;
    [SerializeField] private Transform tilemapParent;
    [SerializeField] private Transform objectsParent;

    [Header("=== GENERATION SETTINGS ===")]
    [SerializeField] private int seed = 12345;
    [SerializeField] private bool randomSeedEachTime = true;
    [SerializeField] private int viewDistance = 4;
    [SerializeField] private int unloadDistance = 6;
    [SerializeField] private int chunksPerFrame = 2;
    [SerializeField] private int chunkSize = 16;
    [SerializeField] private float tileSize = 1f;

    [Header("=== RENDERING ===")]
    [SerializeField] private Material terrainMaterial;
    [SerializeField] private bool useMeshRendering = true;
    [SerializeField] private bool useTilemapLayers = true;
    #endregion

    #region ===== PRIVATE STATE =====
    private ChunkDataGenerator chunkDataGen;
    private Camera mainCamera;
    private bool isInitialized = false;

    private Dictionary<Vector2Int, ChunkData> loadedChunks = new();
    private Queue<Vector2Int> chunkLoadQueue = new();
    private Vector2Int lastPlayerChunk = Vector2Int.zero;

    // Tilemap layers for elevation levels
    private Dictionary<int, Tilemap> elevationLayerTilemaps = new();
    #endregion

    #region ===== LIFECYCLE =====
    void Start()
    {
        Debug.Log("[WorldGenerator COMPLETE] Starting initialization...");

        // Validate config
        if (worldGenConfig == null)
        {
            Debug.LogError("[WorldGenerator] ❌ WorldGenerationConfig not assigned!");
            enabled = false;
            return;
        }

        // Find Grid
        if (grid == null)
            grid = FindFirstObjectByType<Grid>();

        if (grid == null)
        {
            Debug.LogError("[WorldGenerator] ❌ Grid not found!");
            enabled = false;
            return;
        }

        // Find or create groundLayer
        if (groundLayer == null)
        {
            groundLayer = FindFirstObjectByType<Tilemap>();
        }

        if (groundLayer == null && useTilemapLayers)
        {
            Debug.LogWarning("[WorldGenerator] ⚠️ No Tilemap found, creating one...");
            GameObject tilemapGO = new GameObject("groundLayer");
            groundLayer = tilemapGO.AddComponent<Tilemap>();
            tilemapGO.AddComponent<TilemapRenderer>();
            tilemapGO.transform.SetParent(tilemapParent ?? transform);
        }

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[WorldGenerator] ❌ Main Camera not found!");
            enabled = false;
            return;
        }

        // Create parents if needed
        if (meshParent == null)
        {
            GameObject go = new GameObject("TerrainMeshes");
            meshParent = go.transform;
            meshParent.SetParent(transform);
        }

        if (tilemapParent == null)
        {
            GameObject go = new GameObject("TerrainTilemaps");
            tilemapParent = go.transform;
            tilemapParent.SetParent(transform);
        }

        if (objectsParent == null)
        {
            GameObject go = new GameObject("SpawnedObjects");
            objectsParent = go.transform;
            objectsParent.SetParent(transform);
        }

        // Initialize seed
        if (randomSeedEachTime)
            seed = Random.Range(0, 1000000);

        // Initialize generator
        chunkDataGen = new ChunkDataGenerator(worldGenConfig, seed);

        // Setup camera
        mainCamera.transparencySortMode = TransparencySortMode.CustomAxis;
        mainCamera.transparencySortAxis = new Vector3(0f, 1f, 0f);

        isInitialized = true;
        Debug.Log($"[WorldGenerator] ✅ Initialized with seed {seed}");

        // Force initial load
        Vector3 camPos = mainCamera.transform.position;
        lastPlayerChunk = GetChunkCoordinate(camPos);
        UpdateChunkQueue(lastPlayerChunk);

        StartCoroutine(ChunkStreamingLoop());
    }

    void Update()
    {
        if (!isInitialized || mainCamera == null) return;

        Vector3 camPos = mainCamera.transform.position;
        Vector2Int playerChunk = GetChunkCoordinate(camPos);

        if (playerChunk != lastPlayerChunk)
        {
            lastPlayerChunk = playerChunk;
            UpdateChunkQueue(playerChunk);
        }
    }

    void OnDestroy()
    {
        foreach (var chunk in loadedChunks.Values)
            chunk.Cleanup();
    }
    #endregion

    #region ===== CHUNK COORDINATE SYSTEM =====
    private Vector2Int GetChunkCoordinate(Vector3 worldPos)
    {
        float chunkWidth = chunkSize * tileSize;
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / chunkWidth),
            Mathf.FloorToInt(worldPos.y / chunkWidth)
        );
    }

    private Vector3 GetChunkCenter(Vector2Int chunkCoord)
    {
        float chunkWidth = chunkSize * tileSize;
        return new Vector3(
            (chunkCoord.x + 0.5f) * chunkWidth,
            (chunkCoord.y + 0.5f) * chunkWidth,
            0f
        );
    }
    #endregion

    #region ===== CHUNK QUEUE MANAGEMENT =====
    private void UpdateChunkQueue(Vector2Int playerChunk)
    {
        // Enqueue chunks
        for (int x = -viewDistance; x <= viewDistance; x++)
        {
            for (int y = -viewDistance; y <= viewDistance; y++)
            {
                Vector2Int chunkCoord = playerChunk + new Vector2Int(x, y);
                if (!loadedChunks.ContainsKey(chunkCoord) && !chunkLoadQueue.Contains(chunkCoord))
                {
                    chunkLoadQueue.Enqueue(chunkCoord);
                }
            }
        }

        // Unload distant chunks
        var toUnload = new List<Vector2Int>();
        foreach (var loaded in loadedChunks)
        {
            if (Vector2Int.Distance(loaded.Key, playerChunk) > unloadDistance)
                toUnload.Add(loaded.Key);
        }

        foreach (var coord in toUnload)
            UnloadChunk(coord);
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
                yield return null;
            }
            yield return null;
        }
    }
    #endregion

    #region ===== CHUNK LOADING =====
    private void LoadChunk(Vector2Int chunkCoord)
    {
        if (loadedChunks.ContainsKey(chunkCoord))
            return;

        Debug.Log($"[WorldGenerator] Loading chunk {chunkCoord}");

        // Create chunk
        var chunk = new ChunkData(chunkCoord, chunkSize);

        // Generate height map
        chunkDataGen.GenerateChunkData(chunk, chunkSize, tileSize);

        // Generate PROPER biome distribution (natural, not random)
        BiomeTerrainLayerSystem.GenerateCompleteBiomeMap(chunk, chunkSize, worldGenConfig, seed);

        // Render
        if (useMeshRendering)
        {
            GenerateTerrainMesh(chunk);
        }

        if (useTilemapLayers)
        {
            RenderTilemapLayers(chunk);
        }

        loadedChunks[chunkCoord] = chunk;
        Debug.Log($"[WorldGenerator] ✅ Chunk {chunkCoord} loaded");
    }

    private void UnloadChunk(Vector2Int chunkCoord)
    {
        if (!loadedChunks.TryGetValue(chunkCoord, out var chunk))
            return;

        chunk.Cleanup();
        loadedChunks.Remove(chunkCoord);
    }
    #endregion

    #region ===== TERRAIN MESH GENERATION =====
    private void GenerateTerrainMesh(ChunkData chunk)
    {
        try
        {
            // Generate proper 3D displaced mesh
            chunk.terrainMesh = ProperHeightMapTerrainGenerator.GenerateDisplacedTerrainMesh(
                chunk.heightMap,
                chunk.biomeMap,
                chunk.coord,
                chunkSize,
                tileSize,
                worldGenConfig
            );

            // Create GameObject
            GameObject meshGO = new GameObject($"Terrain_{chunk.coord.x}_{chunk.coord.y}");
            meshGO.transform.SetParent(meshParent);
            meshGO.transform.position = GetChunkCenter(chunk.coord);

            // Add components
            MeshFilter meshFilter = meshGO.AddComponent<MeshFilter>();
            meshFilter.mesh = chunk.terrainMesh;

            MeshRenderer meshRenderer = meshGO.AddComponent<MeshRenderer>();
            meshRenderer.material = terrainMaterial ?? GetDefaultTerrainMaterial();

            // Collider
            ProperHeightMapTerrainGenerator.SetupMeshCollider(meshGO, chunk.terrainMesh);

            chunk.meshObject = meshGO;
            chunk.isMeshGenerated = true;

            Debug.Log($"[WorldGenerator] Mesh generated for chunk {chunk.coord}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[WorldGenerator] Mesh generation failed: {e.Message}");
        }
    }

    private Material GetDefaultTerrainMaterial()
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.6f, 0.8f, 0.3f);
        return mat;
    }
    #endregion

    #region ===== TILEMAP LAYER RENDERING =====
    private void RenderTilemapLayers(ChunkData chunk)
    {
        // Get or create tilemap layers for elevation
        for (int elevation = 0; elevation <= 3; elevation++)
        {
            if (!elevationLayerTilemaps.TryGetValue(elevation, out var tilemap))
            {
                tilemap = CreateElevationTilemap(elevation);
                elevationLayerTilemaps[elevation] = tilemap;
            }
        }

        // Render tiles to appropriate layers
        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                int worldX = chunk.coord.x * chunkSize + x;
                int worldY = chunk.coord.y * chunkSize + y;

                float height = chunk.heightMap[y, x];
                BiomeType biome = chunk.biomeMap[y, x];
                int elevation = chunk.elevationLevels[y, x];

                // Get tile
                BiomeTileSetConfig biomeConfig = worldGenConfig.GetBiomeForHeight(height);
                if (biomeConfig == null)
                    continue;

                TileBase tile = biomeConfig.GetRandomTile();
                if (tile == null)
                    continue;

                Vector3Int tilePos = new Vector3Int(worldX, worldY, 0);

                // Place on appropriate layer(s)
                for (int e = 0; e <= elevation; e++)
                {
                    elevationLayerTilemaps[e].SetTile(tilePos, tile);
                }
            }
        }

        chunk.isRendered = true;
    }

    private Tilemap CreateElevationTilemap(int elevation)
    {
        string layerName = $"Layer_{elevation}";
        GameObject tilemapGO = new GameObject(layerName);
        tilemapGO.transform.SetParent(grid.transform);
        tilemapGO.transform.position = new Vector3(0, elevation * 0.5f, 0);

        Tilemap tilemap = tilemapGO.AddComponent<Tilemap>();
        TilemapRenderer renderer = tilemapGO.AddComponent<TilemapRenderer>();

        renderer.sortingLayerName = "Ground";
        renderer.sortingOrder = elevation;
        renderer.mode = TilemapRenderer.Mode.Individual;

        return tilemap;
    }
    #endregion

    #region ===== PUBLIC API =====
    public int GetElevationAt(Vector3 worldPos)
    {
        Vector2Int chunkCoord = GetChunkCoordinate(worldPos);
        if (!loadedChunks.TryGetValue(chunkCoord, out var chunk))
            return 0;

        int localX = Mathf.FloorToInt((worldPos.x % (chunkSize * tileSize)) / tileSize);
        int localY = Mathf.FloorToInt((worldPos.y % (chunkSize * tileSize)) / tileSize);

        if (localX < 0 || localX >= chunkSize || localY < 0 || localY >= chunkSize)
            return 0;

        return chunk.elevationLevels[localY, localX];
    }

    public float GetHeightAt(Vector3 worldPos)
    {
        Vector2Int chunkCoord = GetChunkCoordinate(worldPos);
        if (!loadedChunks.TryGetValue(chunkCoord, out var chunk))
            return 0f;

        int localX = Mathf.FloorToInt((worldPos.x % (chunkSize * tileSize)) / tileSize);
        int localY = Mathf.FloorToInt((worldPos.y % (chunkSize * tileSize)) / tileSize);

        if (localX < 0 || localX >= chunkSize || localY < 0 || localY >= chunkSize)
            return 0f;

        return chunk.heightMap[localY, localX];
    }

    public BiomeType GetBiomeAt(Vector3 worldPos)
    {
        Vector2Int chunkCoord = GetChunkCoordinate(worldPos);
        if (!loadedChunks.TryGetValue(chunkCoord, out var chunk))
            return BiomeType.Water;

        int localX = Mathf.FloorToInt((worldPos.x % (chunkSize * tileSize)) / tileSize);
        int localY = Mathf.FloorToInt((worldPos.y % (chunkSize * tileSize)) / tileSize);

        if (localX < 0 || localX >= chunkSize || localY < 0 || localY >= chunkSize)
            return BiomeType.Water;

        return chunk.biomeMap[localY, localX];
    }
    #endregion
}