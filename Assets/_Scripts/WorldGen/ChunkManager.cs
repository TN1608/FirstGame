using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages chunk lifecycle. Attach to the same GameObject as WorldGenerator.
/// Tracks player position, generates new chunks in radius, unloads far ones.
/// </summary>
public class ChunkManager : MonoBehaviour
{
    [Header("References")]
    public WorldGeneratorSettings settings;
    public Transform playerTransform;

    [Header("Loading")]
    public int renderDistanceChunks = 3;
    [Tooltip("Chunks generated per frame to avoid frame spikes")]
    public int chunksPerFrame = 2;

    // Active chunk data cache
    private readonly Dictionary<Vector2Int, ChunkData> _chunks = new();
    // Chunks currently painted to tilemap
    private readonly HashSet<Vector2Int> _painted = new();

    private Vector2Int _lastPlayerChunk = new Vector2Int(int.MinValue, 0);
    private Queue<Vector2Int> _generateQueue = new();

    void Update()
    {
        if (playerTransform == null || settings == null) return;
        LoadChunksAroundPosition(playerTransform.position);
    }

    /// <summary>
    /// Loads / streams chunks around an arbitrary world position.
    /// This can be called from player code or from Update() when playerTransform is assigned.
    /// </summary>
    public void LoadChunksAroundPosition(Vector3 worldPos)
    {
        if (settings == null) return;

        Vector2Int currentChunk = WorldToChunkCoord(worldPos);
        if (currentChunk != _lastPlayerChunk)
        {
            _lastPlayerChunk = currentChunk;
            ScheduleChunksAround(currentChunk);
            UnloadDistantChunks(currentChunk);
        }

        ProcessGenerationQueue();
    }

    /// <summary>
    /// Returns the generated tile height at a world position.
    /// If the chunk is not loaded yet, falls back to the configured base height.
    /// </summary>
    public float GetHeightAtPosition(Vector3 worldPos)
    {
        if (settings == null) return 0f;

        Vector2Int chunkCoord = WorldToChunkCoord(worldPos);
        if (!_chunks.TryGetValue(chunkCoord, out var chunk))
            return settings.baseHeight;

        Vector2Int origin = chunk.WorldOrigin;
        int localX = Mathf.FloorToInt(worldPos.x) - origin.x;
        int localY = Mathf.FloorToInt(worldPos.y) - origin.y;

        if (localX < 0 || localY < 0 || localX >= chunk.chunkSize || localY >= chunk.chunkSize)
            return settings.baseHeight;

        return chunk.tileHeights[localX, localY];
    }

    void ProcessGenerationQueue()
    {
        int processed = 0;
        while (_generateQueue.Count > 0 && processed < chunksPerFrame)
        {
            var coord = _generateQueue.Dequeue();
            if (!_chunks.ContainsKey(coord))
                GenerateChunk(coord);
            processed++;
        }
    }

    void ScheduleChunksAround(Vector2Int center)
    {
        int r = renderDistanceChunks;
        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
        {
            var coord = center + new Vector2Int(dx, dy);
            if (!_chunks.ContainsKey(coord) && !IsQueued(coord))
                _generateQueue.Enqueue(coord);
        }
    }

    void GenerateChunk(Vector2Int coord)
    {
        var chunk = new ChunkData(coord, settings.chunkSize);
        HeightmapGenerator.Generate(chunk, settings);
        _chunks[coord] = chunk;

        // Paint immediately (could be moved to coroutine for async)
        TilemapPainter.Paint(chunk, settings);
        _painted.Add(coord);
    }

    void UnloadDistantChunks(Vector2Int center)
    {
        int threshold = renderDistanceChunks + 2;
        var toRemove = new List<Vector2Int>();

        foreach (var coord in _painted)
        {
            if (Mathf.Abs(coord.x - center.x) > threshold ||
                Mathf.Abs(coord.y - center.y) > threshold)
                toRemove.Add(coord);
        }

        foreach (var coord in toRemove)
        {
            EraseChunk(coord);
            _chunks.Remove(coord);
            _painted.Remove(coord);
        }
    }

    void EraseChunk(Vector2Int coord)
    {
        if (!_chunks.TryGetValue(coord, out var chunk)) return;
        Vector2Int origin = chunk.WorldOrigin;
        int size = chunk.chunkSize;

        // Erase terrain tilemap cells for this chunk
        if (settings.terrainTilemap != null)
        {
            for (int lx = 0; lx < size; lx++)
            for (int ly = 0; ly < size; ly++)
            {
                int h = chunk.tileHeights[lx, ly];
                int wx = origin.x + lx;
                int wy = origin.y + ly;
                for (int col = 0; col <= h + 1; col++)
                    settings.terrainTilemap.SetTile(new Vector3Int(wx, wy, col), null);
            }
        }

        if (settings.waterTilemap != null)
        {
            for (int lx = 0; lx < size; lx++)
            for (int ly = 0; ly < size; ly++)
            {
                int wx = origin.x + lx;
                int wy = origin.y + ly;
                settings.waterTilemap.SetTile(new Vector3Int(wx, wy, settings.seaLevel), null);
            }
        }
    }

    bool IsQueued(Vector2Int coord)
    {
        foreach (var c in _generateQueue)
            if (c == coord) return true;
        return false;
    }

    Vector2Int WorldToChunkCoord(Vector3 worldPos)
    {
        int cs = settings.chunkSize;
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / cs),
            Mathf.FloorToInt(worldPos.y / cs)
        );
    }

    /// <summary>
    /// Force-generate the full world around a fixed origin (non-infinite mode).
    /// </summary>
    public void GenerateFullMap(int halfSize)
    {
        ClearAll();
        int cs = settings.chunkSize;
        int chunksNeeded = Mathf.CeilToInt((float)halfSize / cs);
        for (int cx = -chunksNeeded; cx <= chunksNeeded; cx++)
        for (int cy = -chunksNeeded; cy <= chunksNeeded; cy++)
            GenerateChunk(new Vector2Int(cx, cy));
    }

    public void ClearAll()
    {
        settings.terrainTilemap?.ClearAllTiles();
        settings.waterTilemap?.ClearAllTiles();
        settings.decorationTilemap?.ClearAllTiles();
        _chunks.Clear();
        _painted.Clear();
        _generateQueue.Clear();
    }

    public ChunkData GetChunk(Vector2Int coord) =>
        _chunks.TryGetValue(coord, out var c) ? c : null;
}
