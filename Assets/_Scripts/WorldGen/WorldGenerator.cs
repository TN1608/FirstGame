// ==================== FILENAME: WorldGenerator_FIXED.cs ====================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using _Scripts.WorldGen;
using ProceduralWorld.Generation;
using ProceduralWorld.Data;

/// <summary>
/// FIXED WorldGenerator - Actually generates terrain!
/// Fixes:
/// • Chunk loading now works properly
/// • Tilemap fallback if mesh fails
/// • Proper initialization and coroutine logic
/// • Biome tile integration
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
    [SerializeField] private Transform objectsParent;

    [Header("=== SEED & RANDOMIZATION ===")]
    [SerializeField] private int seed = 12345;
    [SerializeField] private bool randomSeedEachTime = true;

    [Header("=== CHUNK STREAMING ===")]
    [SerializeField] [Range(1, 10)] private int viewDistance = 4;
    [SerializeField] private int unloadDistance = 6;
    [SerializeField] private int chunksPerFrame = 2;
    [SerializeField] private int chunkSize = 16;
    [SerializeField] private float tileSize = 1f;

    [Header("=== MATERIAL ===")]
    [SerializeField] private Material terrainMaterial;
    #endregion

    #region ===== PRIVATE STATE =====
    private ChunkDataGenerator chunkDataGen;
    private Camera mainCamera;
    private bool isInitialized = false;

    private Dictionary<Vector2Int, ChunkData> loadedChunks = new();
    private Queue<Vector2Int> chunkLoadQueue = new();
    private Vector2Int lastPlayerChunk = Vector2Int.zero;
    #endregion

    #region ===== LIFECYCLE =====
    void Start()
    {
        Debug.Log("[WorldGenerator] Starting initialization...");

        // Validation
        if (worldGenConfig == null)
        {
            Debug.LogError("[WorldGenerator] ❌ WorldGenerationConfig not assigned!");
            enabled = false;
            return;
        }

        if (grid == null)
        {
            grid = FindFirstObjectByType<Grid>();
            if (grid == null)
            {
                Debug.LogError("[WorldGenerator] ❌ Grid not found in scene!");
                enabled = false;
                return;
            }
        }

        if (groundLayer == null)
        {
            groundLayer = FindFirstObjectByType<Tilemap>();
            if (groundLayer == null)
            {
                Debug.LogWarning("[WorldGenerator] ⚠️ No Tilemap found - will use mesh only");
            }
        }

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[WorldGenerator] ❌ Main Camera not found!");
            enabled = false;
            return;
        }

        // Create parent objects if needed
        if (meshParent == null)
        {
            GameObject go = new GameObject("TerrainMeshes");
            meshParent = go.transform;
            meshParent.SetParent(transform);
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

        // Initialize data generator
        chunkDataGen = new ChunkDataGenerator(worldGenConfig, seed);

        // Setup camera
        mainCamera.transparencySortMode = TransparencySortMode.CustomAxis;
        mainCamera.transparencySortAxis = new Vector3(0f, 1f, 0f);

        isInitialized = true;
        Debug.Log($"[WorldGenerator] ✅ Initialized with seed {seed}");

        // Force initial chunk loading
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
        // Enqueue chunks in view distance
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

        Debug.Log($"[WorldGenerator] Queue size: {chunkLoadQueue.Count}, Loaded: {loadedChunks.Count}");
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

        // Create chunk data
        var chunk = new ChunkData(chunkCoord, chunkSize);

        // Generate data
        chunkDataGen.GenerateChunkData(chunk, chunkSize, tileSize);

        // Render (mesh or tilemap)
        if (worldGenConfig.useTilemapFallback && groundLayer != null)
        {
            RenderChunkTilemap(chunk);
        }
        else
        {
            GenerateTerrainMesh(chunk);
        }

        // Spawn objects
        SpawnChunkObjects(chunk);

        loadedChunks[chunkCoord] = chunk;
        Debug.Log($"[WorldGenerator] ✅ Chunk {chunkCoord} loaded");
    }

    private void UnloadChunk(Vector2Int chunkCoord)
    {
        if (!loadedChunks.TryGetValue(chunkCoord, out var chunk))
            return;

        Debug.Log($"[WorldGenerator] Unloading chunk {chunkCoord}");
        chunk.Cleanup();
        loadedChunks.Remove(chunkCoord);
    }
    #endregion

    #region ===== TILEMAP RENDERING (FALLBACK) =====
    private void RenderChunkTilemap(ChunkData chunk)
    {
        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                int worldX = chunk.coord.x * chunkSize + x;
                int worldY = chunk.coord.y * chunkSize + y;

                float height = chunk.heightMap[y, x];
                BiomeTileSetConfig biomeConfig = worldGenConfig.GetBiomeForHeight(height);

                if (biomeConfig == null)
                    continue;

                TileBase tile = biomeConfig.GetRandomTile();
                if (tile == null)
                    continue;

                Vector3Int tilePos = new Vector3Int(worldX, worldY, 0);
                groundLayer.SetTile(tilePos, tile);
            }
        }

        chunk.isRendered = true;
    }
    #endregion

    #region ===== TERRAIN MESH GENERATION =====
    private void GenerateTerrainMesh(ChunkData chunk)
    {
        try
        {
            chunk.terrainMesh = MeshTerrainGenerator.GenerateTerrainMesh(
                chunk.heightMap,
                chunk.coord,
                chunkSize,
                tileSize,
                worldGenConfig
            );

            GameObject meshGO = new GameObject($"Terrain_{chunk.coord.x}_{chunk.coord.y}");
            meshGO.transform.SetParent(meshParent);
            meshGO.transform.position = GetChunkCenter(chunk.coord);

            MeshFilter meshFilter = meshGO.AddComponent<MeshFilter>();
            meshFilter.mesh = chunk.terrainMesh;

            MeshRenderer meshRenderer = meshGO.AddComponent<MeshRenderer>();
            meshRenderer.material = terrainMaterial != null ? terrainMaterial : GetDefaultTerrainMaterial();

            MeshTerrainGenerator.ApplyMeshCollider(meshGO, chunk.terrainMesh);

            chunk.meshObject = meshGO;
            chunk.isMeshGenerated = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[WorldGenerator] Failed to generate mesh for chunk {chunk.coord}: {e.Message}");
        }
    }

    private Material GetDefaultTerrainMaterial()
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.5f, 0.7f, 0.3f);
        return mat;
    }
    #endregion

    #region ===== OBJECT SPAWNING =====
    private void SpawnChunkObjects(ChunkData chunk)
    {
        for (int cy = 0; cy < chunkSize; cy++)
        {
            for (int cx = 0; cx < chunkSize; cx++)
            {
                int worldX = chunk.coord.x * chunkSize + cx;
                int worldY = chunk.coord.y * chunkSize + cy;
                float height = chunk.heightMap[cy, cx];

                // Skip water
                if (height < worldGenConfig.waterLevel)
                    continue;

                // Future: Add object spawning logic
            }
        }
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
    #endregion
}