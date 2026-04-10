using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Minecraft-style chunk streaming:
///
///  1. PREDICTIVE PRE-LOAD — when player approaches a chunk border (within
///     <c>preloadBorderDistance</c> tiles), adjacent chunks are queued before
///     the player actually crosses. By the time they arrive, chunk is ready.
///
///  2. BACKGROUND GENERATION — HeightmapGenerator runs on a ThreadPool thread
///     (Tasks API, no Burst dependency). Main thread only does SetTiles().
///
///  3. PRIORITY QUEUE — chunks closest to player always paint first.
///     New chunks near player jump ahead of far chunks in the queue.
///
///  4. UNLOAD WITH HYSTERESIS — chunks unload only when they exceed
///     (renderDistance + unloadBuffer) to avoid thrashing at borders.
///
/// Grid setup reminder (Inspector):
///   Grid  → Cell Swizzle  = XYZ
///   Tilemap → Orientation = IsometricZAsY
/// </summary>
public class ChunkManager : MonoBehaviour
{
    [Header("References")]
    public WorldGeneratorSettings settings;
    public Transform playerTransform;

    [Header("Chunk Loading")]
    [Tooltip("How many chunks in each direction to keep loaded.")]
    public int renderDistanceChunks = 4;

    [Tooltip("Extra chunks beyond render distance before unloading. Prevents thrash.")]
    public int unloadBufferChunks = 2;

    [Tooltip("How many tiles from a chunk border triggers pre-load of next chunk.")]
    public float preloadBorderDistance = 4f;

    [Tooltip("Max chunks painted to tilemap per frame (main thread budget).")]
    public int paintChunksPerFrame = 2;

    // ── State ─────────────────────────────────────────────────────────────

    // Fully computed, ready to paint
    private readonly Dictionary<Vector2Int, ChunkData>  _ready    = new();
    // Painted to tilemap
    private readonly Dictionary<Vector2Int, ChunkData>  _painted  = new();
    // Currently being computed on background thread
    private readonly HashSet<Vector2Int>                _computing = new();
    // Sorted by distance: closest first
    private readonly SortedList<float, Vector2Int>      _paintQueue = new(new DuplicateKeyComparer());

    private Vector2Int _lastPlayerChunk  = new(int.MinValue, 0);
    private Vector2Int _lastPlayerCell   = new(int.MinValue, 0);
    private object     _readyLock        = new object();

    // ─────────────────────────────────────────────────────────────────────

    void Update()
    {
        if (settings == null || playerTransform == null) return;

        Vector3    playerPos   = playerTransform.position;
        Vector2Int playerChunk = WorldToChunkCoord(playerPos);
        Vector2Int playerCell  = new(Mathf.FloorToInt(playerPos.x),
                                     Mathf.FloorToInt(playerPos.y));

        // ── Schedule new chunks when player chunk changes ─────────────────
        if (playerChunk != _lastPlayerChunk)
        {
            _lastPlayerChunk = playerChunk;
            ScheduleChunksAround(playerChunk);
            UnloadDistantChunks(playerChunk);
        }

        // ── Predictive pre-load when approaching a border ─────────────────
        if (playerCell != _lastPlayerCell)
        {
            _lastPlayerCell = playerCell;
            CheckBorderPreload(playerPos, playerChunk);
        }

        // ── Paint ready chunks (main thread, budget-limited) ──────────────
        PaintReadyChunks(playerChunk);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────

    public void LoadChunksAroundPosition(Vector3 worldPos)
    {
        // Called from PlayerController — Update() already handles this,
        // but expose for external callers too.
        Vector2Int coord = WorldToChunkCoord(worldPos);
        ScheduleChunksAround(coord);
    }

    /// <summary>Returns the integer tile height at a world position.</summary>
    public float GetHeightAtPosition(Vector3 worldPos)
    {
        if (settings == null) return 0f;

        Vector2Int coord = WorldToChunkCoord(worldPos);

        ChunkData chunk = null;
        if (!_painted.TryGetValue(coord, out chunk))
            _ready.TryGetValue(coord, out chunk);

        if (chunk == null) return settings.baseHeight;

        Vector2Int origin = chunk.WorldOrigin;
        int lx = Mathf.FloorToInt(worldPos.x) - origin.x;
        int ly = Mathf.FloorToInt(worldPos.y) - origin.y;

        if (lx < 0 || ly < 0 || lx >= chunk.chunkSize || ly >= chunk.chunkSize)
            return settings.baseHeight;

        return chunk.tileHeights[lx, ly];
    }

    public void GenerateFullMap(int halfSize)
    {
        ClearAll();
        int needed = Mathf.CeilToInt((float)halfSize / settings.chunkSize);
        for (int cx = -needed; cx <= needed; cx++)
        for (int cy = -needed; cy <= needed; cy++)
            ScheduleChunk(new Vector2Int(cx, cy), priority: 0f);

        // For finite maps: flush synchronously so world is ready at Start
        FlushSynchronous();
    }

    public void ClearAll()
    {
        // Erase all painted chunks
        foreach (var kvp in _painted)
            TilemapPainter.Erase(kvp.Value, settings);

        settings?.terrainTilemap?.ClearAllTiles();
        settings?.waterTilemap?.ClearAllTiles();
        settings?.decorationTilemap?.ClearAllTiles();

        _painted.Clear();
        _ready.Clear();
        _paintQueue.Clear();
        // Note: cannot cancel in-flight Tasks easily; _computing clears on completion
    }

    public ChunkData GetChunk(Vector2Int coord)
    {
        if (_painted.TryGetValue(coord, out var c)) return c;
        if (_ready.TryGetValue(coord, out var r))   return r;
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Internal — Scheduling
    // ─────────────────────────────────────────────────────────────────────

    void ScheduleChunksAround(Vector2Int center)
    {
        int r = renderDistanceChunks;
        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
        {
            var coord = center + new Vector2Int(dx, dy);
            float dist = new Vector2(dx, dy).magnitude;
            ScheduleChunk(coord, dist);
        }
    }

    void CheckBorderPreload(Vector3 playerPos, Vector2Int playerChunk)
    {
        int cs = settings.chunkSize;
        Vector2Int origin = playerChunk * cs;

        // Local position within current chunk
        float localX = playerPos.x - origin.x;
        float localY = playerPos.y - origin.y;

        // Pre-load neighbours when near a border
        float d = preloadBorderDistance;

        if (localX < d)                  ScheduleChunk(playerChunk + new Vector2Int(-1, 0), priority: -1f);
        if (localX > cs - d)             ScheduleChunk(playerChunk + new Vector2Int( 1, 0), priority: -1f);
        if (localY < d)                  ScheduleChunk(playerChunk + new Vector2Int( 0,-1), priority: -1f);
        if (localY > cs - d)             ScheduleChunk(playerChunk + new Vector2Int( 0, 1), priority: -1f);

        // Diagonals
        if (localX < d && localY < d)    ScheduleChunk(playerChunk + new Vector2Int(-1,-1), priority: -1f);
        if (localX > cs-d && localY < d) ScheduleChunk(playerChunk + new Vector2Int( 1,-1), priority: -1f);
        if (localX < d && localY > cs-d) ScheduleChunk(playerChunk + new Vector2Int(-1, 1), priority: -1f);
        if (localX > cs-d && localY > cs-d) ScheduleChunk(playerChunk + new Vector2Int(1, 1), priority: -1f);
    }

    /// <summary>
    /// Schedules a chunk for background computation.
    /// priority < 0 = highest priority (paint first).
    /// Skips if already computing, ready, or painted.
    /// </summary>
    void ScheduleChunk(Vector2Int coord, float priority)
    {
        if (_painted.ContainsKey(coord))   return;
        if (_ready.ContainsKey(coord))
        {
            // Already computed — bump to front of paint queue
            EnqueuePaint(coord, priority);
            return;
        }
        if (_computing.Contains(coord))    return;

        _computing.Add(coord);

        // Background: compute heightmap on thread pool
        Task.Run(() =>
        {
            var chunk = new ChunkData(coord, settings.chunkSize);
            HeightmapGenerator.Generate(chunk, settings);

            lock (_readyLock)
            {
                _ready[coord] = chunk;
                _computing.Remove(coord);
            }
        });

        EnqueuePaint(coord, priority);
    }

    void EnqueuePaint(Vector2Int coord, float priority)
    {
        // SortedList doesn't allow duplicate keys — offset by small epsilon per entry
        float key = priority;
        while (_paintQueue.ContainsKey(key)) key += 0.0001f;
        _paintQueue[key] = coord;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Internal — Painting
    // ─────────────────────────────────────────────────────────────────────

    void PaintReadyChunks(Vector2Int playerChunk)
    {
        int painted = 0;
        var toRemove = new List<float>();

        foreach (var kvp in _paintQueue)
        {
            if (painted >= paintChunksPerFrame) break;

            Vector2Int coord = kvp.Value;
            toRemove.Add(kvp.Key);

            if (_painted.ContainsKey(coord)) continue; // already painted

            ChunkData chunk;
            lock (_readyLock)
            {
                if (!_ready.TryGetValue(coord, out chunk)) continue; // not ready yet
                _ready.Remove(coord);
            }

            TilemapPainter.Paint(chunk, settings);
            _painted[coord] = chunk;
            painted++;
        }

        foreach (var k in toRemove) _paintQueue.Remove(k);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Internal — Unloading
    // ─────────────────────────────────────────────────────────────────────

    void UnloadDistantChunks(Vector2Int center)
    {
        int threshold = renderDistanceChunks + unloadBufferChunks;
        var toUnload  = new List<Vector2Int>();

        foreach (var coord in _painted.Keys)
        {
            int dist = Mathf.Max(
                Mathf.Abs(coord.x - center.x),
                Mathf.Abs(coord.y - center.y));

            if (dist > threshold) toUnload.Add(coord);
        }

        foreach (var coord in toUnload)
        {
            TilemapPainter.Erase(_painted[coord], settings);
            _painted.Remove(coord);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Internal — Sync flush (for finite map generation)
    // ─────────────────────────────────────────────────────────────────────

    void FlushSynchronous()
    {
        // For finite map: block until all queued chunks are computed + painted
        var allCoords = new List<Vector2Int>(_paintQueue.Values);

        foreach (var coord in allCoords)
        {
            if (_painted.ContainsKey(coord)) continue;

            // If still computing, generate synchronously here
            if (!_ready.ContainsKey(coord) && !_painted.ContainsKey(coord))
            {
                var chunk = new ChunkData(coord, settings.chunkSize);
                HeightmapGenerator.Generate(chunk, settings);
                _ready[coord] = chunk;
            }

            if (_ready.TryGetValue(coord, out var readyChunk))
            {
                TilemapPainter.Paint(readyChunk, settings);
                _painted[coord] = readyChunk;
                _ready.Remove(coord);
            }
        }

        _paintQueue.Clear();
        _computing.Clear();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    Vector2Int WorldToChunkCoord(Vector3 worldPos)
    {
        int cs = settings.chunkSize;
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / cs),
            Mathf.FloorToInt(worldPos.y / cs));
    }

    // SortedList requires unique keys; this comparer allows duplicates by treating equal keys as non-equal
    class DuplicateKeyComparer : IComparer<float>
    {
        public int Compare(float x, float y) => x <= y ? -1 : 1;
    }
}