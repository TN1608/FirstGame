using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  WorldObjectData
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
//  Right-click → Create → WorldGen → Spawnable Object
//  Đặt file này vào: Assets/Prefabs/WorldObjects/[tên_thư_mục]/
//  WorldGenerator sẽ tự tìm tất cả config trong các sub-folder đó
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[CreateAssetMenu(fileName = "SpawnableObject", menuName = "WorldGen/Spawnable Object")]
public class SpawnableObjectConfig : ScriptableObject
{
    [Tooltip("Tên hiển thị (không bắt buộc phải trùng tên file)")]
    public string objectName;

    [Tooltip("Prefab sẽ được Instantiate")]
    public GameObject prefab;

    [Header("Spawn Rules")]
    [Tooltip("Tile nào object được phép spawn lên\n" +
             "Để trống = spawn trên mọi tile đất liền")]
    public TileBase[] allowedTiles;

    [Range(0f, 1f)]
    [Tooltip("Xác suất spawn mỗi tile phù hợp\n" +
             "Gợi ý: cây lớn 0.05 | đá 0.03 | hoa 0.08 | cỏ nhỏ 0.12")]
    public float spawnChance = 0.05f;

    [Header("Clustering — tạo cụm tự nhiên")]
    [Tooltip("Bật để object tụm thành cụm (rừng, mỏ đá...)")]
    public bool useClusterNoise    = true;
    public float clusterNoiseScale = 0.15f;
    [Range(0f, 1f)]
    [Tooltip("Cao = cụm nhỏ/ít | Thấp = cụm rộng/nhiều\nGợi ý: 0.45 – 0.65")]
    public float clusterThreshold  = 0.55f;

    [Header("Placement")]
    [Tooltip("Offset Y để đặt object lên mặt tile\nGợi ý: 0.05 – 0.20")]
    public float yOffset        = 0.1f;
    [Tooltip("Jitter vị trí ngẫu nhiên trong ô tile\nGợi ý: 0.1 – 0.3")]
    public float positionJitter = 0.2f;

    [Header("Scale Variation")]
    public float minScale = 0.8f;
    public float maxScale = 1.2f;
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  BiomeTileSet — tự động load từ Assets/Tiles/32x_Tiles/[folder]
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[System.Serializable]
public class BiomeTileSet
{
    [Tooltip("Tên folder trong Assets/Tiles/32x_Tiles/ — dùng để phân biệt biome\n" +
             "Ví dụ: 'grass', 'water', 'stone', 'path'")]
    public string folderName;

    [Tooltip("Tile được chọn ngẫu nhiên từ folder này khi noise khớp")]
    [HideInInspector] public TileBase[] tiles;   // auto-loaded

    [Tooltip("Ngưỡng noise tối thiểu để dùng tileset này")]
    [Range(0f, 1f)] public float noiseMin = 0f;

    [Tooltip("Ngưỡng noise tối đa để dùng tileset này")]
    [Range(0f, 1f)] public float noiseMax = 1f;

    [Tooltip("Cell Height cho Rule Tile (set trong Inspector của từng Rule Tile asset)\n" +
             "Gợi ý: grass/dirt/sand/stone = 0.5 | water = 0.16 (flat)")]
    public float cellHeight = 0.5f;

    public bool IsValid() => tiles != null && tiles.Length > 0;

    public TileBase GetRandomTile() =>
        IsValid() ? tiles[Random.Range(0, tiles.Length)] : null;

    // Tile đại diện cho biome này (tile đầu tiên) — dùng để check allowedTiles
    public TileBase PrimaryTile => IsValid() ? tiles[0] : null;
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  WorldGenerator
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
public class WorldGenerator : MonoBehaviour
{
    // ── Seed ────────────────────────────────────────────────
    [Header("=== SEED ===")]
    public int  seed               = 12345;
    public bool randomSeedEachTime = true;

    // ── Noise ───────────────────────────────────────────────
    [Header("=== NOISE SETTINGS ===")]
    [Tooltip("Tần số noise biome. Nhỏ = địa hình rộng | Lớn = vụn nhỏ\nGợi ý: 0.03 – 0.06")]
    public float noiseScale = 0.04f;

    [Tooltip("Tần số noise detail (micro variation)\nGợi ý: 0.15 – 0.30")]
    public float detailScale = 0.20f;

    [Tooltip("Mức ảnh hưởng của detail noise. 0 = tắt\nGợi ý: 0.10 – 0.20")]
    [Range(0f, 0.5f)]
    public float detailStrength = 0.15f;

    [Tooltip("Số lớp FBM octave. Nhiều = chi tiết hơn\nGợi ý: 4 – 7")]
    public int octaves = 6;

    [Range(0f, 1f)]
    [Tooltip("Biên độ giảm giữa octave\nGợi ý: 0.4 – 0.6")]
    public float persistence = 0.5f;

    [Tooltip("Tần số tăng giữa octave\nGợi ý: 1.8 – 2.2")]
    public float lacunarity = 2.0f;

    [Range(0.3f, 2.5f)]
    [Tooltip("Power curve: < 1 = nhiều đất | > 1 = nhiều nước\nGợi ý: 0.8 – 1.2")]
    public float redistributionPower = 1.0f;

    // ── Noise Preview ───────────────────────────────────────
    [Header("=== NOISE PREVIEW (Editor only) ===")]
    public bool showNoisePreview  = true;
    [Range(32, 256)] public int previewResolution = 128;
    [HideInInspector] public Texture2D noisePreviewTexture;

    // ── Chunk ───────────────────────────────────────────────
    [Header("=== CHUNK SYSTEM ===")]
    [Tooltip("Số tile mỗi cạnh chunk\nGợi ý: 16 – 32")]
    public int chunkSize     = 16;
    [Tooltip("Số chunk render mỗi hướng quanh camera\nGợi ý: 3 – 5")]
    public int viewDistance  = 4;
    [Tooltip("Khoảng cách unload (> viewDistance + 1)")]
    public int unloadDistance = 6;
    [Tooltip("Chunk load mỗi frame\nGợi ý: 1 – 3")]
    public int chunksPerFrame = 2;

    // ── Tilemap ─────────────────────────────────────────────
    [Header("=== TILEMAP ===")]
    [Tooltip("Chỉ cần 1 Tilemap duy nhất.\n" +
             "⚠ Tilemap Renderer → Mode: Individual\n" +
             "⚠ Grid → Cell Size: X=1, Y=0.5, Z=1\n" +
             "⚠ Grid → Cell Layout: Isometric Z As Y")]
    public Tilemap groundLayer;
    public Tilemap midLayer;
    public Tilemap highLayer;

    // ── Tile Folders ────────────────────────────────────────
    [Header("=== TILE FOLDERS ===")]
    [Tooltip("Auto-load tiles từ Assets/Tiles/32x_Tiles/[folderName]/\n" +
             "Mỗi BiomeTileSet = 1 sub-folder = 1 biome\n\n" +
             "Ví dụ setup:\n" +
             "  folderName: water   | noiseMin: 0.00 | noiseMax: 0.28\n" +
             "  folderName: path    | noiseMin: 0.28 | noiseMax: 0.34  (sand/bờ)\n" +
             "  folderName: brush   | noiseMin: 0.34 | noiseMax: 0.50  (dirt)\n" +
             "  folderName: grass   | noiseMin: 0.50 | noiseMax: 0.82\n" +
             "  folderName: stone   | noiseMin: 0.82 | noiseMax: 1.00\n\n" +
             "⚠ SPRITE SETUP (quan trọng):\n" +
             "• PPU = pixel width của tile (vd: 32 hoặc 68)\n" +
             "• Pivot: Custom X=0.5, Y=0.34\n" +
             "• Filter: Point (no filter)\n" +
             "• Compression: None\n\n" +
             "⚠ RULE TILE SETUP:\n" +
             "• Cell Height: 0.5 cho tile có cạnh bên\n" +
             "• Cell Height: 0.16 cho water (flat)\n" +
             "• Phải setup đủ Tiling Rules 8 hướng")]
    public List<BiomeTileSet> biomeTileSets = new List<BiomeTileSet>();

    [Tooltip("Tile mid-level (đồi/cliff). Dùng Rule Tile có cạnh bên.")]
    public TileBase tileMidLevel;
    [Range(0f, 1f)] public float midLevelThreshold  = 0.58f;

    [Tooltip("Tile high-level (đỉnh núi/snow). Dùng Rule Tile.")]
    public TileBase tileHighLevel;
    [Range(0f, 1f)] public float highLevelThreshold = 0.80f;

    // ── Object Folders ──────────────────────────────────────
    [Header("=== OBJECT FOLDERS ===")]
    [Tooltip("Auto-load SpawnableObjectConfig từ Assets/Prefabs/WorldObjects/[sub-folder]/\n" +
             "Mỗi sub-folder = 1 nhóm object (rocks, treelogs, flowers...)\n" +
             "Chỉ load các .asset được tạo từ Create → WorldGen → Spawnable Object")]
    public string worldObjectsRootPath = "Prefabs/WorldObjects";

    [Tooltip("Bật để log số config đã load lúc Start")]
    public bool logLoadedConfigs = true;

    public Transform objectsParent;

    // ── Runtime ─────────────────────────────────────────────
    [HideInInspector] public List<SpawnableObjectConfig> spawnableObjects = new List<SpawnableObjectConfig>();

    private float  offsetX, offsetY, offsetDetail, offsetHeight;
    private Grid   grid;
    private Camera mainCam;

    private HashSet<Vector2Int>                      loadedChunks   = new HashSet<Vector2Int>();
    private HashSet<Vector2Int>                      queuedChunks   = new HashSet<Vector2Int>();
    private Queue<Vector2Int>                        chunkLoadQueue  = new Queue<Vector2Int>();
    private Dictionary<Vector2Int, List<GameObject>> chunkObjects    = new Dictionary<Vector2Int, List<GameObject>>();

    // tile lookup: noise → tile (built from biomeTileSets)
    private List<BiomeTileSet> sortedBiomes = new List<BiomeTileSet>();

    public List<WorldObjectData> spawnedObjects = new List<WorldObjectData>();

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    void Start()
    {
        grid    = GetComponentInParent<Grid>() ?? FindFirstObjectByType<Grid>();
        mainCam = Camera.main;

        if (randomSeedEachTime) seed = Random.Range(0, 1_000_000);
        Random.InitState(seed);
        offsetX      = Random.Range(0f, 10000f);
        offsetY      = Random.Range(0f, 10000f);
        offsetDetail = Random.Range(0f, 10000f);
        offsetHeight = Random.Range(0f, 10000f);

        if (objectsParent == null)
            objectsParent = new GameObject("WorldObjects").transform;

        mainCam.transparencySortMode = TransparencySortMode.CustomAxis;
        mainCam.transparencySortAxis = new Vector3(0f, 1f, 0f);

        // Auto-load tiles từ folders
        LoadTileSetsFromFolders();

        // Auto-load SpawnableObjectConfig từ folders
        LoadSpawnableObjectsFromFolders();

        Debug.Log($"🌍 Seed: {seed} | Biomes: {sortedBiomes.Count} | SpawnConfigs: {spawnableObjects.Count}");

        StartCoroutine(ChunkStreamingLoop());
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  AUTO-LOAD: TILES
    //  Đọc từ Assets/Tiles/32x_Tiles/[folderName]/
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void LoadTileSetsFromFolders()
    {
        sortedBiomes.Clear();

        foreach (var biome in biomeTileSets)
        {
            if (string.IsNullOrEmpty(biome.folderName)) continue;

            string path = $"Tiles/32x_Tiles/{biome.folderName}";
            var loaded  = Resources.LoadAll<TileBase>(path);

            if (loaded.Length == 0)
            {
                Debug.LogWarning($"⚠ BiomeTileSet '{biome.folderName}': không tìm thấy tile nào tại Resources/{path}\n" +
                                 $"  Đảm bảo folder nằm trong Assets/Resources/Tiles/32x_Tiles/{biome.folderName}/");
                continue;
            }

            biome.tiles = loaded;
            sortedBiomes.Add(biome);

            if (logLoadedConfigs)
                Debug.Log($"🗿 Biome '{biome.folderName}': {loaded.Length} tile(s) loaded | noise [{biome.noiseMin:F2} – {biome.noiseMax:F2}]");
        }

        // Sort theo noiseMin để lookup nhanh
        sortedBiomes.Sort((a, b) => a.noiseMin.CompareTo(b.noiseMin));
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  AUTO-LOAD: SPAWNABLE OBJECTS
    //  Đọc từ Assets/Resources/Prefabs/WorldObjects/[subfolder]/
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void LoadSpawnableObjectsFromFolders()
    {
        spawnableObjects.Clear();

        // Resources.LoadAll tự đệ quy sub-folder
        string path    = worldObjectsRootPath;
        var    configs = Resources.LoadAll<SpawnableObjectConfig>(path);

        foreach (var cfg in configs)
        {
            if (cfg == null || cfg.prefab == null) continue;
            spawnableObjects.Add(cfg);
        }

        if (logLoadedConfigs)
            Debug.Log($"🌲 SpawnableObjects loaded: {spawnableObjects.Count} configs từ Resources/{path}");
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  CHUNK STREAMING
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private IEnumerator ChunkStreamingLoop()
    {
        while (true)
        {
            Vector2Int cam = WorldPosToChunk(mainCam.transform.position);

            for (int r = 0; r <= viewDistance; r++)
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                var c = new Vector2Int(cam.x + dx, cam.y + dy);
                if (!loadedChunks.Contains(c) && !queuedChunks.Contains(c))
                {
                    chunkLoadQueue.Enqueue(c);
                    queuedChunks.Add(c);
                }
            }

            int loaded = 0;
            while (chunkLoadQueue.Count > 0 && loaded < chunksPerFrame)
            {
                var coord = chunkLoadQueue.Dequeue();
                queuedChunks.Remove(coord);
                if (!loadedChunks.Contains(coord))
                {
                    yield return StartCoroutine(LoadChunk(coord));
                    loaded++;
                }
            }

            UnloadDistantChunks(cam);
            yield return new WaitForSeconds(0.2f);
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  LOAD CHUNK
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private IEnumerator LoadChunk(Vector2Int coord)
    {
        loadedChunks.Add(coord);

        int   total     = chunkSize * chunkSize;
        var   positions = new Vector3Int[total];
        var   tiles     = new TileBase[total];
        var   midPos    = new List<Vector3Int>();
        var   midTiles  = new List<TileBase>();
        var   highPos   = new List<Vector3Int>();
        var   highTiles = new List<TileBase>();

        for (int ly = 0; ly < chunkSize; ly++)
        for (int lx = 0; lx < chunkSize; lx++)
        {
            int wx  = coord.x * chunkSize + lx;
            int wy  = coord.y * chunkSize + ly;
            int idx = ly * chunkSize + lx;

            float biome  = SampleNoise(wx, wy, noiseScale,  offsetX,      offsetY);
            float detail = SampleNoise(wx, wy, detailScale, offsetDetail, offsetDetail * 0.7f);
            float final  = Mathf.Clamp01(biome + (detail - 0.5f) * detailStrength);
            final        = Mathf.Clamp01(Mathf.Pow(final, redistributionPower));

            float height = SampleNoise(wx, wy, noiseScale * 1.5f, offsetHeight, offsetHeight * 1.3f);

            positions[idx] = new Vector3Int(wx, wy, 0);
            tiles[idx]     = GetTileFromNoise(final);

            bool isLand = tiles[idx] != GetBiomeTile(0f); // bất kỳ tile nào không phải water
            if (isLand)
            {
                if (tileMidLevel  != null && height > midLevelThreshold)  { midPos.Add(positions[idx]);  midTiles.Add(tileMidLevel); }
                if (tileHighLevel != null && height > highLevelThreshold) { highPos.Add(positions[idx]); highTiles.Add(tileHighLevel); }
            }
        }

        groundLayer.SetTiles(positions, tiles);
        if (midLayer  != null && midPos.Count  > 0) midLayer .SetTiles(midPos.ToArray(),  midTiles.ToArray());
        if (highLayer != null && highPos.Count > 0) highLayer.SetTiles(highPos.ToArray(), highTiles.ToArray());

        yield return null;
        yield return null;

        foreach (var p in positions)
        {
            groundLayer.RefreshTile(p);
            if (midLayer  != null) midLayer .RefreshTile(p);
            if (highLayer != null) highLayer.RefreshTile(p);
        }

        SpawnChunkObjects(coord, positions, tiles);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  UNLOAD
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void UnloadDistantChunks(Vector2Int cam)
    {
        var toRemove = new List<Vector2Int>();
        foreach (var c in loadedChunks)
            if (ChebychevDist(c, cam) > unloadDistance) toRemove.Add(c);

        foreach (var c in toRemove)
        {
            for (int lx = 0; lx < chunkSize; lx++)
            for (int ly = 0; ly < chunkSize; ly++)
            {
                var cell = new Vector3Int(c.x * chunkSize + lx, c.y * chunkSize + ly, 0);
                groundLayer.SetTile(cell, null);
                if (midLayer  != null) midLayer .SetTile(cell, null);
                if (highLayer != null) highLayer.SetTile(cell, null);
            }

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

                // allowedTiles trống = spawn mọi tile đất liền
                if (cfg.allowedTiles != null && cfg.allowedTiles.Length > 0)
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
        Vector3 center   = groundLayer.GetCellCenterWorld(cellPos);
        float   j        = cfg.positionJitter;
        Vector3 worldPos = new Vector3(
            center.x + Random.Range(-j, j),
            center.y + cfg.yOffset + Random.Range(-j * 0.5f, j * 0.5f), 0f);

        var obj = Instantiate(cfg.prefab, worldPos, Quaternion.identity, objectsParent);
        obj.transform.localScale *= Random.Range(cfg.minScale, cfg.maxScale);
        obj.name = $"{cfg.objectName}_{cellPos.x}_{cellPos.y}";

        foreach (var sr in obj.GetComponentsInChildren<SpriteRenderer>())
        {
            sr.sortingLayerName = "GroundObjects";
            sr.sortingOrder     = Mathf.RoundToInt(-worldPos.y * 100f) + 50;
        }

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
    //  TILE LOOKUP
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private TileBase GetTileFromNoise(float n)
    {
        // Tìm biome phù hợp nhất (sorted by noiseMin)
        BiomeTileSet best = null;
        foreach (var b in sortedBiomes)
        {
            if (n >= b.noiseMin && n < b.noiseMax && b.IsValid())
                best = b;
        }
        return best != null ? best.GetRandomTile() : null;
    }

    private TileBase GetBiomeTile(float n)  => GetTileFromNoise(n);

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  NOISE PREVIEW
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public void BakeNoisePreview()
    {
        float ox = 1234f, oy = 5678f, od = 3456f;
        int   res  = previewResolution;
        float half = res * 0.5f;

        var tex = new Texture2D(res, res) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            int   wx = (int)(x - half), wy = (int)(y - half);
            float b  = SampleNoise(wx, wy, noiseScale, ox, oy);
            float d  = SampleNoise(wx, wy, detailScale, od, od * 0.7f);
            float n  = Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(b + (d - 0.5f) * detailStrength), redistributionPower));

            // Lấy màu từ biomeTileSets nếu có, fallback màu cứng
            Color c = GetBiomePreviewColor(n);
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        noisePreviewTexture = tex;
    }

    private Color GetBiomePreviewColor(float n)
    {
        // Màu fallback theo thứ tự phổ biến
        Color[] fallback =
        {
            new Color(0.20f, 0.50f, 0.90f), // water
            new Color(0.92f, 0.86f, 0.52f), // sand
            new Color(0.58f, 0.38f, 0.20f), // dirt
            new Color(0.28f, 0.68f, 0.28f), // grass
            new Color(0.55f, 0.55f, 0.58f), // stone
        };

        // Dùng sortedBiomes nếu đã load
        if (sortedBiomes != null && sortedBiomes.Count > 0)
        {
            for (int i = sortedBiomes.Count - 1; i >= 0; i--)
            {
                var b = sortedBiomes[i];
                if (n >= b.noiseMin) return GetColorForBiome(b.folderName, i, fallback);
            }
        }

        // Fallback
        if (n < 0.28f) return fallback[0];
        if (n < 0.34f) return fallback[1];
        if (n < 0.48f) return fallback[2];
        if (n < 0.82f) return fallback[3];
        return fallback[4];
    }

    private Color GetColorForBiome(string name, int idx, Color[] fallback)
    {
        string n = name.ToLower();
        if (n.Contains("water")) return fallback[0];
        if (n.Contains("path") || n.Contains("sand")) return fallback[1];
        if (n.Contains("brush") || n.Contains("dirt")) return fallback[2];
        if (n.Contains("grass")) return fallback[3];
        if (n.Contains("stone") || n.Contains("rock")) return fallback[4];
        return idx < fallback.Length ? fallback[idx] : Color.magenta;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  NOISE
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private float SampleNoise(int x, int y, float scale, float ox, float oy)
    {
        float v = 0f, amp = 1f, freq = scale, maxAmp = 0f;
        for (int i = 0; i < octaves; i++)
        {
            v      += Mathf.PerlinNoise((x + ox) * freq, (y + oy) * freq) * amp;
            maxAmp += amp;
            amp    *= persistence;
            freq   *= lacunarity;
        }
        return Mathf.Clamp01(v / maxAmp);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  PUBLIC HELPERS
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public TileBase GetTileAt(Vector3Int cellPos)  => groundLayer.GetTile(cellPos);
    public bool     IsWalkable(Vector3Int cellPos) =>
        GetTileAt(cellPos) != null && !IsWaterTile(GetTileAt(cellPos));

    private bool IsWaterTile(TileBase t)
    {
        if (sortedBiomes.Count == 0) return false;
        // Biome đầu tiên (noiseMin thấp nhất) = water
        var waterBiome = sortedBiomes[0];
        return waterBiome.IsValid() && System.Array.IndexOf(waterBiome.tiles, t) >= 0;
    }

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
        if (allowed == null || allowed.Length == 0) return true;
        foreach (var t in allowed) if (t == tile) return true;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnedObjects == null) return;
        foreach (var d in spawnedObjects)
        {
            Gizmos.color = d.objectType != null && d.objectType.ToLower().Contains("tree")
                ? Color.green : Color.gray;
            Gizmos.DrawWireSphere(d.worldPosition, 0.15f);
        }
    }
}