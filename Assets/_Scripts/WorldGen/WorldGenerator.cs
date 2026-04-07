using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  WorldObjectData — dual-position container
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
public class WorldObjectData
{
    public Vector3    worldPosition;   // physics / collider / AI
    public Vector3Int cellPosition;    // grid cell → map logic
    public Vector2    screenPosition;  // pixel → UI / minimap
    public GameObject instance;
    public string     objectType;
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  SpawnableObjectConfig
//  Right-click Project → Create → WorldGen → Spawnable Object
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[CreateAssetMenu(fileName = "SpawnableObject", menuName = "WorldGen/Spawnable Object")]
public class SpawnableObjectConfig : ScriptableObject
{
    public string     objectName;
    public GameObject prefab;

    [Header("Spawn Rules")]
    [Tooltip("Tile nào object được phép spawn lên")]
    public TileBase[] allowedTiles;

    [Range(0f, 1f)]
    [Tooltip("Xác suất spawn mỗi tile phù hợp\nGợi ý: cây 0.06 | đá 0.03 | hoa 0.08")]
    public float spawnChance = 0.05f;

    [Header("Clustering — tạo cụm tự nhiên")]
    [Tooltip("Dùng Perlin noise để tạo cụm (rừng, mỏ đá...)")]
    public bool  useClusterNoise    = true;
    public float clusterNoiseScale  = 0.15f;
    [Range(0f, 1f)]
    [Tooltip("Ngưỡng noise cụm. Cao = cụm nhỏ/ít | Thấp = cụm lớn/nhiều")]
    public float clusterThreshold   = 0.55f;

    [Header("Placement")]
    [Tooltip("Offset Y để đặt object lên mặt tile\nGợi ý: 0.05 – 0.15")]
    public float yOffset        = 0.1f;
    [Tooltip("Jitter ngẫu nhiên trong ô tile để tránh grid cứng nhắc")]
    public float positionJitter = 0.2f;

    [Header("Scale Variation")]
    public float minScale = 0.8f;
    public float maxScale = 1.2f;
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  WorldGenerator — Infinite world with chunk streaming
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
public class WorldGenerator : MonoBehaviour
{
    // ── Seed ────────────────────────────────────────────────────────────
    [Header("=== SEED ===")]
    public int  seed               = 12345;
    public bool randomSeedEachTime = true;

    // ── Noise Settings ──────────────────────────────────────────────────
    [Header("=== NOISE SETTINGS ===")]

    [Tooltip("Tần số noise. Nhỏ = địa hình rộng, mượt | Lớn = địa hình vụn, nhỏ\n" +
             "Gợi ý: 0.03 – 0.06")]
    public float noiseScale = 0.04f;

    [Tooltip("Số lớp Perlin noise chồng nhau (FBM)\n" +
             "Nhiều lớp = chi tiết hơn nhưng chậm hơn\n" +
             "Gợi ý: 4 – 7")]
    public int octaves = 6;

    [Tooltip("Biên độ giảm dần giữa các octave\n" +
             "Thấp (0.3) = mượt, ít chi tiết | Cao (0.7) = gồ ghề, nhiều chi tiết\n" +
             "Gợi ý: 0.4 – 0.6")]
    [Range(0f, 1f)]
    public float persistence = 0.5f;

    [Tooltip("Tần số tăng giữa các octave\n" +
             "Gợi ý: 1.8 – 2.2")]
    public float lacunarity = 2.0f;

    [Tooltip("Biến đổi phân phối noise bằng power curve\n" +
             "< 1.0 = nhiều đất hơn (flat) | > 1.0 = nhiều nước hơn (mountainous)\n" +
             "Gợi ý: 0.8 – 1.2 cho infinite world không dùng falloff")]
    [Range(0.3f, 2.5f)]
    public float redistributionPower = 1.0f;

    // ── Noise Preview ───────────────────────────────────────────────────
    [Header("=== NOISE PREVIEW (Editor only) ===")]
    [Tooltip("Bật để vẽ noise map màu trong Inspector\nClick 'Bake Preview' để cập nhật")]
    public bool showNoisePreview = true;

    [Tooltip("Kích thước ảnh preview. Lớn hơn = rõ hơn nhưng chậm hơn\nGợi ý: 128")]
    [Range(32, 256)]
    public int previewResolution = 128;

    [HideInInspector]
    public Texture2D noisePreviewTexture;

    // ── Chunk System ────────────────────────────────────────────────────
    [Header("=== CHUNK SYSTEM (Infinite World) ===")]

    [Tooltip("Số ô tile mỗi cạnh chunk\nNhỏ = load/unload mượt hơn | Lớn = ít overhead hơn\nGợi ý: 16 – 32")]
    public int chunkSize = 16;

    [Tooltip("Số chunk render xung quanh camera (theo mỗi hướng)\n" +
             "View radius = viewDistance * chunkSize * cellSize\n" +
             "Gợi ý: 3 – 5 (balance giữa view range và RAM)")]
    public int viewDistance = 4;

    [Tooltip("Khoảng cách để unload chunk (nên lớn hơn viewDistance 1-2)\n" +
             "Lớn hơn = ít pop-in khi camera quay lại | Nhỏ hơn = tiết kiệm RAM hơn")]
    public int unloadDistance = 6;

    [Tooltip("Bao nhiêu chunk được load mỗi frame để tránh lag spike\nGợi ý: 1 – 3")]
    public int chunksPerFrame = 2;

    // ── Tilemap ─────────────────────────────────────────────────────────
    [Header("=== TILEMAP ===")]
    [Tooltip("Chỉ dùng 1 Tilemap duy nhất cho tất cả tile\n" +
             "Rule Tile tự chọn sprite đúng khi biết neighbors\n\n" +
             "⚠ SETUP QUAN TRỌNG:\n" +
             "• Tilemap Renderer → Mode: Individual (KHÔNG phải Chunk)\n" +
             "• Tilemap Renderer → Order in Layer: 0\n" +
             "• Grid → Cell Size: X=1, Y=0.5, Z=1\n" +
             "• Grid → Cell Layout: Isometric Z As Y")]
    public Tilemap groundLayer;

    // ── Tiles ───────────────────────────────────────────────────────────
    [Header("=== TILES ===")]
    [Tooltip("SETUP GỢI Ý cho mỗi Isometric Rule Tile:\n\n" +
             "SPRITE IMPORT SETTINGS:\n" +
             "• Pixels Per Unit: 68 (= pixel width của tile)\n" +
             "• Pivot: Custom  X=0.5  Y=0.34\n" +
             "  (Y=0.34 = bottom của diamond face, không phải center)\n" +
             "• Filter Mode: Point (no filter) — giữ pixel art sắc nét\n" +
             "• Compression: None\n\n" +
             "RULE TILE SETTINGS:\n" +
             "• Tile Type: Isometric Rule Tile\n" +
             "• Cell Height per tile:\n" +
             "  - grass_tile  → 0.5\n" +
             "  - dirt_tile   → 0.5\n" +
             "  - sand_tile   → 0.5\n" +
             "  - stone_tile  → 0.5\n" +
             "  - water_tile  → 0.16  (flat, không có cạnh bên cao)\n\n" +
             "CẦN THIẾT:\n" +
             "• Phải setup đủ Tiling Rules trong Rule Tile\n" +
             "• Neighbor = This / Not This cho 8 hướng\n" +
             "• Có sprite riêng cho: top-face, NW edge, NE edge, SE edge, SW edge,\n" +
             "  NW corner, NE corner, SE corner, SW corner, inner corners")]
    public TileBase tileWater;
    public TileBase tileSand;
    public TileBase tileDirt;
    public TileBase tileGrass;
    public TileBase tileStone;

    // ── Tile Thresholds ─────────────────────────────────────────────────
    [Header("=== TILE THRESHOLDS (0 → 1, sau normalize) ===")]
    [Tooltip("Noise dưới ngưỡng này = Water\nGợi ý: 0.25 – 0.32")]
    [Range(0f, 1f)] public float waterThreshold = 0.28f;

    [Tooltip("Noise dưới ngưỡng này = Sand\nNên cách waterThreshold ~0.04 – 0.07\nGợi ý: 0.32 – 0.37")]
    [Range(0f, 1f)] public float sandThreshold  = 0.34f;

    [Tooltip("Noise dưới ngưỡng này = Dirt\nGợi ý: 0.44 – 0.52")]
    [Range(0f, 1f)] public float dirtThreshold  = 0.48f;

    [Tooltip("Noise dưới ngưỡng này = Grass (phần lớn map)\nGợi ý: 0.78 – 0.88")]
    [Range(0f, 1f)] public float grassThreshold = 0.82f;
    // Còn lại = Stone

    // ── Object Spawning ─────────────────────────────────────────────────
    [Header("=== OBJECT SPAWNING ===")]
    public List<SpawnableObjectConfig> spawnableObjects;
    public Transform objectsParent;

    // ── Runtime ─────────────────────────────────────────────────────────
    private float  offsetX, offsetY;
    private Grid   grid;
    private Camera mainCam;

    private HashSet<Vector2Int>                      loadedChunks  = new HashSet<Vector2Int>();
    private Queue<Vector2Int>                        chunkLoadQueue = new Queue<Vector2Int>();
    private Dictionary<Vector2Int, List<GameObject>> chunkObjects  = new Dictionary<Vector2Int, List<GameObject>>();

    public  List<WorldObjectData> spawnedObjects = new List<WorldObjectData>();

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    void Start()
    {
        grid    = GetComponentInParent<Grid>() ?? FindFirstObjectByType<Grid>();
        mainCam = Camera.main;

        if (randomSeedEachTime) seed = Random.Range(0, 1000000);
        Random.InitState(seed);
        offsetX = Random.Range(0f, 10000f);
        offsetY = Random.Range(0f, 10000f);

        if (objectsParent == null)
            objectsParent = new GameObject("WorldObjects").transform;

        // Isometric sorting fix
        mainCam.transparencySortMode = TransparencySortMode.CustomAxis;
        mainCam.transparencySortAxis = new Vector3(0f, 1f, 0f);

        Debug.Log($"🌍 World seed: {seed}");

        StartCoroutine(ChunkStreamingLoop());
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  CHUNK STREAMING
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private IEnumerator ChunkStreamingLoop()
    {
        while (true)
        {
            Vector2Int camChunk = WorldPosToChunk(mainCam.transform.position);

            // Enqueue chunks cần load (gần trước)
            for (int r = 0; r <= viewDistance; r++)
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue; // chỉ vòng ngoài
                var c = new Vector2Int(camChunk.x + dx, camChunk.y + dy);
                if (!loadedChunks.Contains(c) && !chunkLoadQueue.Contains(c))
                    chunkLoadQueue.Enqueue(c);
            }

            // Load chunksPerFrame chunk mỗi frame
            int loaded = 0;
            while (chunkLoadQueue.Count > 0 && loaded < chunksPerFrame)
            {
                var coord = chunkLoadQueue.Dequeue();
                if (!loadedChunks.Contains(coord))
                {
                    yield return StartCoroutine(LoadChunk(coord));
                    loaded++;
                }
            }

            // Unload distant chunks
            UnloadDistantChunks(camChunk);

            yield return new WaitForSeconds(0.2f);
        }
    }

    private IEnumerator LoadChunk(Vector2Int coord)
    {
        loadedChunks.Add(coord);

        // --- Tính noise & set tiles ---
        int total = chunkSize * chunkSize;
        var positions = new Vector3Int[total];
        var tiles     = new TileBase[total];

        // Pass 1: raw noise
        var rawNoise = new float[total];
        float min = float.MaxValue, max = float.MinValue;

        for (int ly = 0; ly < chunkSize; ly++)
        for (int lx = 0; lx < chunkSize; lx++)
        {
            int   wx = coord.x * chunkSize + lx;
            int   wy = coord.y * chunkSize + ly;
            float v  = RawNoise(wx, wy);
            rawNoise[ly * chunkSize + lx] = v;
            if (v < min) min = v;
            if (v > max) max = v;
        }

        // Pass 2: normalize + assign tiles (Y từ cao → thấp cho Rule Tile)
        int idx = 0;
        for (int ly = chunkSize - 1; ly >= 0; ly--)
        for (int lx = 0; lx < chunkSize; lx++)
        {
            int   wx    = coord.x * chunkSize + lx;
            int   wy    = coord.y * chunkSize + ly;
            float noise = Mathf.InverseLerp(min, max, rawNoise[ly * chunkSize + lx]);

            positions[idx] = new Vector3Int(wx, wy, 0);
            tiles[idx]     = GetTileFromNoise(noise);
            idx++;
        }

        groundLayer.SetTiles(positions, tiles);

        // Chờ 2 frame để Rule Tile tính neighbors
        yield return null;
        yield return null;
        foreach (var p in positions) groundLayer.RefreshTile(p);

        // Spawn objects
        SpawnChunkObjects(coord, positions, tiles);
    }

    private void UnloadDistantChunks(Vector2Int camChunk)
    {
        var toRemove = new List<Vector2Int>();
        foreach (var c in loadedChunks)
        {
            if (ChebychevDist(c, camChunk) > unloadDistance)
                toRemove.Add(c);
        }

        foreach (var c in toRemove)
        {
            for (int lx = 0; lx < chunkSize; lx++)
            for (int ly = 0; ly < chunkSize; ly++)
                groundLayer.SetTile(new Vector3Int(c.x * chunkSize + lx, c.y * chunkSize + ly, 0), null);

            if (chunkObjects.TryGetValue(c, out var objs))
            {
                foreach (var o in objs) if (o) Destroy(o);
                chunkObjects.Remove(c);
            }

            loadedChunks.Remove(c);
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  OBJECT SPAWNING
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void SpawnChunkObjects(Vector2Int coord, Vector3Int[] positions, TileBase[] tiles)
    {
        if (spawnableObjects == null || spawnableObjects.Count == 0) return;

        var list = new List<GameObject>();
        for (int i = 0; i < positions.Length; i++)
        {
            if (tiles[i] == null) continue;
            foreach (var cfg in spawnableObjects)
            {
                if (cfg == null || cfg.prefab == null) continue;
                if (!IsTileAllowed(tiles[i], cfg.allowedTiles)) continue;

                if (cfg.useClusterNoise)
                {
                    float cn = Mathf.PerlinNoise(
                        (positions[i].x + offsetX * 0.3f) * cfg.clusterNoiseScale,
                        (positions[i].y + offsetY * 0.3f) * cfg.clusterNoiseScale);
                    if (cn < cfg.clusterThreshold) continue;
                }

                if (Random.value > cfg.spawnChance) continue;

                var data = SpawnSingleObject(cfg, positions[i]);
                if (data != null) { list.Add(data.instance); spawnedObjects.Add(data); }
                break;
            }
        }
        chunkObjects[coord] = list;
    }

    private WorldObjectData SpawnSingleObject(SpawnableObjectConfig cfg, Vector3Int cellPos)
    {
        Vector3 center  = groundLayer.GetCellCenterWorld(cellPos);
        float   j       = cfg.positionJitter;
        Vector3 worldPos = new Vector3(
            center.x + Random.Range(-j, j),
            center.y + cfg.yOffset + Random.Range(-j * 0.5f, j * 0.5f), 0f);

        var obj = Instantiate(cfg.prefab, worldPos, Quaternion.identity, objectsParent);
        obj.transform.localScale *= Random.Range(cfg.minScale, cfg.maxScale);
        obj.name = $"{cfg.objectName}_{cellPos.x}_{cellPos.y}";

        // Isometric sort order
        foreach (var sr in obj.GetComponentsInChildren<SpriteRenderer>())
            sr.sortingOrder += Mathf.RoundToInt(-worldPos.y * 10f);

        return new WorldObjectData
        {
            worldPosition  = worldPos,
            cellPosition   = cellPos,
            screenPosition = mainCam ? (Vector2)mainCam.WorldToScreenPoint(worldPos) : Vector2.zero,
            instance       = obj,
            objectType     = cfg.objectName
        };
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  NOISE
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private float RawNoise(int x, int y)
    {
        float v = 0f, amp = 1f, freq = noiseScale, maxAmp = 0f;
        for (int i = 0; i < octaves; i++)
        {
            v      += Mathf.PerlinNoise((x + offsetX) * freq, (y + offsetY) * freq) * amp;
            maxAmp += amp;
            amp    *= persistence;
            freq   *= lacunarity;
        }
        return Mathf.Pow(Mathf.Clamp01(v / maxAmp), redistributionPower);
    }

    private TileBase GetTileFromNoise(float n)
    {
        if (n < waterThreshold)  return tileWater;
        if (n < sandThreshold)   return tileSand;
        if (n < dirtThreshold)   return tileDirt;
        if (n < grassThreshold)  return tileGrass;
        return tileStone;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  NOISE PREVIEW — gọi từ WorldGeneratorEditor
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public void BakeNoisePreview()
    {
        int   res  = previewResolution;
        float half = res * 0.5f;
        var   raw  = new float[res * res];
        float min  = float.MaxValue, max = float.MinValue;

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float v = RawNoise((int)(x - half), (int)(y - half));
            raw[y * res + x] = v;
            if (v < min) min = v;
            if (v > max) max = v;
        }

        var tex = new Texture2D(res, res) { filterMode = FilterMode.Point };
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float n = Mathf.InverseLerp(min, max, raw[y * res + x]);
            Color c =
                n < waterThreshold  ? new Color(0.25f, 0.55f, 0.90f) :
                n < sandThreshold   ? new Color(0.92f, 0.86f, 0.52f) :
                n < dirtThreshold   ? new Color(0.58f, 0.38f, 0.20f) :
                n < grassThreshold  ? new Color(0.28f, 0.68f, 0.28f) :
                                      new Color(0.58f, 0.58f, 0.60f);
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        noisePreviewTexture = tex;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  PUBLIC HELPERS
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public TileBase GetTileAt(Vector3Int cellPos)  => groundLayer.GetTile(cellPos);
    public bool     IsWalkable(Vector3Int cellPos) => GetTileAt(cellPos) != null
                                                   && GetTileAt(cellPos) != tileWater;

    public void RefreshScreenPositions()
    {
        if (!mainCam) return;
        foreach (var d in spawnedObjects)
            d.screenPosition = mainCam.WorldToScreenPoint(d.worldPosition);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  UTILS
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private Vector2Int WorldPosToChunk(Vector3 w) =>
        new Vector2Int(Mathf.FloorToInt(w.x / chunkSize),
                       Mathf.FloorToInt(w.y / chunkSize));

    private int ChebychevDist(Vector2Int a, Vector2Int b) =>
        Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

    private bool IsTileAllowed(TileBase tile, TileBase[] allowed)
    {
        if (allowed == null) return false;
        foreach (var t in allowed) if (t == tile) return true;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnedObjects == null) return;
        foreach (var d in spawnedObjects)
        {
            Gizmos.color = d.objectType.Contains("oak") ? Color.green : Color.gray;
            Gizmos.DrawWireSphere(d.worldPosition, 0.15f);
        }
    }
}