using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Chunk streaming with integrated decoration spawning.
/// Attach to Grid. Requires DecorationSpawner on same GameObject.
/// </summary>
public class ChunkManager : MonoBehaviour
{
    [Header("References")]
    public WorldGeneratorSettings settings;
    public Transform playerTransform;

    [Header("Chunk Loading")]
    public int   renderDistanceChunks   = 4;
    public int   unloadBufferChunks     = 2;
    public float preloadBorderDistance  = 4f;
    public int   paintChunksPerFrame    = 2;

    // ── State ──────────────────────────────────────────────────────────────
    private readonly Dictionary<Vector2Int, ChunkData>   _ready     = new();
    private readonly Dictionary<Vector2Int, ChunkData>   _painted   = new();
    private readonly HashSet<Vector2Int>                  _computing = new();
    private readonly SortedList<float, Vector2Int>        _paintQueue = new(new DupKeyComparer());

    private Vector2Int _lastPlayerChunk = new(int.MinValue, 0);
    private Vector2Int _lastPlayerCell  = new(int.MinValue, 0);
    private readonly object _lock = new();

    private DecorationSpawner _decoSpawner;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    void Awake()
    {
        _decoSpawner = GetComponent<DecorationSpawner>();
    }

    void Update()
    {
        if (settings == null || playerTransform == null) return;

        Vector3    pos   = playerTransform.position;
        Vector2Int chunk = WorldToChunkCoord(pos);
        Vector2Int cell  = new(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));

        if (chunk != _lastPlayerChunk)
        {
            _lastPlayerChunk = chunk;
            ScheduleChunksAround(chunk);
            UnloadDistantChunks(chunk);
        }

        if (cell != _lastPlayerCell)
        {
            _lastPlayerCell = cell;
            CheckBorderPreload(pos, chunk);
        }

        PaintReadyChunks(chunk);
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public void LoadChunksAroundPosition(Vector3 worldPos)
    {
        ScheduleChunksAround(WorldToChunkCoord(worldPos));
    }

    public float GetHeightAtPosition(Vector3 worldPos)
    {
        if (settings == null) return 0f;
        Vector2Int coord = WorldToChunkCoord(worldPos);

        ChunkData chunk;
        lock (_lock) { _painted.TryGetValue(coord, out chunk); }
        if (chunk == null) lock (_lock) { _ready.TryGetValue(coord, out chunk); }
        if (chunk == null) return settings.baseHeight;

        var origin = chunk.WorldOrigin;
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
            ScheduleChunk(new Vector2Int(cx, cy), 0f);

        FlushSynchronous();
    }

    public void ClearAll()
    {
        foreach (var kvp in _painted)
            TilemapPainter.Erase(kvp.Value, settings);

        settings?.terrainTilemap?.ClearAllTiles();
        settings?.waterTilemap?.ClearAllTiles();
        settings?.decorationTilemap?.ClearAllTiles();

        _decoSpawner?.ClearAll();

        lock (_lock)
        {
            _painted.Clear();
            _ready.Clear();
            _paintQueue.Clear();
        }
    }

    public ChunkData GetChunk(Vector2Int coord)
    {
        lock (_lock)
        {
            if (_painted.TryGetValue(coord, out var c)) return c;
            if (_ready.TryGetValue(coord, out var r))   return r;
        }
        return null;
    }

    // ── Internal: Scheduling ───────────────────────────────────────────────

    void ScheduleChunksAround(Vector2Int center)
    {
        int r = renderDistanceChunks;
        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
            ScheduleChunk(center + new Vector2Int(dx, dy),
                          new Vector2(dx, dy).magnitude);
    }

    void CheckBorderPreload(Vector3 playerPos, Vector2Int playerChunk)
    {
        int   cs = settings.chunkSize;
        float lx = playerPos.x - playerChunk.x * cs;
        float ly = playerPos.y - playerChunk.y * cs;
        float d  = preloadBorderDistance;

        if (lx < d)      ScheduleChunk(playerChunk + new Vector2Int(-1, 0), -1f);
        if (lx > cs - d) ScheduleChunk(playerChunk + new Vector2Int( 1, 0), -1f);
        if (ly < d)      ScheduleChunk(playerChunk + new Vector2Int( 0,-1), -1f);
        if (ly > cs - d) ScheduleChunk(playerChunk + new Vector2Int( 0, 1), -1f);
    }

    void ScheduleChunk(Vector2Int coord, float priority)
    {
        lock (_lock)
        {
            if (_painted.ContainsKey(coord)) return;
            if (_ready.ContainsKey(coord))   { EnqueuePaint(coord, priority); return; }
            if (_computing.Contains(coord))  return;
            _computing.Add(coord);
        }

        Task.Run(() =>
        {
            var chunk = new ChunkData(coord, settings.chunkSize);
            HeightmapGenerator.Generate(chunk, settings);
            lock (_lock)
            {
                _ready[coord] = chunk;
                _computing.Remove(coord);
            }
        });

        lock (_lock) { EnqueuePaint(coord, priority); }
    }

    void EnqueuePaint(Vector2Int coord, float priority)
    {
        float key = priority;
        while (_paintQueue.ContainsKey(key)) key += 0.0001f;
        _paintQueue[key] = coord;
    }

    // ── Internal: Painting ─────────────────────────────────────────────────

    void PaintReadyChunks(Vector2Int playerChunk)
    {
        int painted   = 0;
        var toRemove  = new List<float>();

        foreach (var kvp in _paintQueue)
        {
            if (painted >= paintChunksPerFrame) break;

            Vector2Int coord = kvp.Value;
            toRemove.Add(kvp.Key);

            lock (_lock)
            {
                if (_painted.ContainsKey(coord)) continue;
                if (!_ready.TryGetValue(coord, out var chunk)) continue;
                _ready.Remove(coord);

                TilemapPainter.Paint(chunk, settings);
                _painted[coord] = chunk;

                // Spawn decorations on main thread (Unity API)
                _decoSpawner?.SpawnForChunk(chunk);
            }

            painted++;
        }

        foreach (var k in toRemove) _paintQueue.Remove(k);
    }

    // ── Internal: Unloading ────────────────────────────────────────────────

    void UnloadDistantChunks(Vector2Int center)
    {
        int threshold = renderDistanceChunks + unloadBufferChunks;
        var toUnload  = new List<Vector2Int>();

        lock (_lock)
        {
            foreach (var coord in _painted.Keys)
            {
                int dist = Mathf.Max(
                    Mathf.Abs(coord.x - center.x),
                    Mathf.Abs(coord.y - center.y));
                if (dist > threshold) toUnload.Add(coord);
            }
        }

        foreach (var coord in toUnload)
        {
            lock (_lock)
            {
                TilemapPainter.Erase(_painted[coord], settings);
                _painted.Remove(coord);
            }
            _decoSpawner?.DespawnChunk(coord);
        }
    }

    void FlushSynchronous()
    {
        var allCoords = new List<Vector2Int>(_paintQueue.Values);
        foreach (var coord in allCoords)
        {
            lock (_lock)
            {
                if (_painted.ContainsKey(coord)) continue;
                if (!_ready.ContainsKey(coord))
                {
                    var c = new ChunkData(coord, settings.chunkSize);
                    HeightmapGenerator.Generate(c, settings);
                    _ready[coord] = c;
                }
                var chunk = _ready[coord];
                _ready.Remove(coord);
                TilemapPainter.Paint(chunk, settings);
                _painted[coord] = chunk;
                _decoSpawner?.SpawnForChunk(chunk);
            }
        }
        _paintQueue.Clear();
        _computing.Clear();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    Vector2Int WorldToChunkCoord(Vector3 worldPos)
    {
        int cs = settings.chunkSize;
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / cs),
            Mathf.FloorToInt(worldPos.y / cs));
    }

    class DupKeyComparer : IComparer<float>
    {
        public int Compare(float x, float y) => x <= y ? -1 : 1;
    }
}