using System.Collections;
using System.Collections.Generic;
using _Scripts.WorldGen;
using UnityEngine;
using UnityEngine.Tilemaps;

public class WorldObjectData
{
    public Vector3    worldPosition;
    public Vector3Int cellPosition;
    public Vector2    screenPosition;
    public GameObject instance;
    public string     objectType;
}

public class WorldGenerator : MonoBehaviour
{
    [Header("=== SEED ===")]
    public int  seed               = 12345;
    public bool randomSeedEachTime = true;

    [Header("=== NOISE SETTINGS ===")]
    public float noiseScale          = 0.04f;
    public int   octaves             = 6;
    [Range(0f,1f)] public float persistence  = 0.9f;
    public float lacunarity          = 2.1f;
    [Range(0.5f,3f)] public float redistributionPower = 1.55f;

    [Header("=== ISLAND FALLOFF ===")]
    public bool  useIslandFalloff = true;
    [Range(0.3f,3f)] public float falloffStrength = 0.8f;

    [Header("=== CHUNK ===")]
    public int chunkSize      = 32;
    public int generateRadius = 3;

    [Header("=== TILEMAP LAYERS ===")]
    public Tilemap groundLayer;
    public Tilemap midLayer;
    public Tilemap highLayer;

    [Header("=== TILES ===")]
    public TileBase tileWater;
    public TileBase tileSand;
    public TileBase tileDirt;
    public TileBase tileGrass;
    public TileBase tileStone;

    [Header("=== TILE THRESHOLDS (0-1 sau normalize) ===")]
    public float waterThreshold = 0.25f;
    public float sandThreshold  = 0.32f;
    public float dirtThreshold  = 0.46f;
    public float grassThreshold = 0.82f;

    [Header("=== MULTI-LEVEL THRESHOLDS ===")]
    [Tooltip("Noise > giá trị này thì tile đó có thêm MidLayer bên trên")]
    public float midLevelThreshold  = 0.60f;
    [Tooltip("Noise > giá trị này thì tile đó có thêm HighLayer bên trên")]
    public float highLevelThreshold = 0.78f;

    [Header("=== OBJECT SPAWNING ===")]
    public List<SpawnableObjectConfig> spawnableObjects;
    public Transform objectsParent;

    // Runtime
    private float offsetX, offsetY;
    private Grid  grid;
    private Dictionary<Vector3Int, float>    noiseCache = new();
    private Dictionary<Vector3Int, TileBase> tileCache  = new();
    public  List<WorldObjectData>            spawnedObjects = new();

    void Start()
    {
        grid = GetComponentInParent<Grid>();
        if (grid == null) grid = FindFirstObjectByType<Grid>();

        if (randomSeedEachTime) seed = Random.Range(0, 1000000);
        Random.InitState(seed);
        offsetX = Random.Range(0f, 10000f);
        offsetY = Random.Range(0f, 10000f);

        if (objectsParent == null)
            objectsParent = new GameObject("WorldObjects").transform;

        Debug.Log($"🌍 Generating world | seed: {seed}");
        StartCoroutine(GenerateWorld());
    }

    // =========================================================
    //  PIPELINE
    // =========================================================
    IEnumerator GenerateWorld()
    {
        BuildNoiseMap();
        PlaceTiles();

        yield return null;
        yield return null;

        groundLayer.RefreshAllTiles();
        midLayer.RefreshAllTiles();
        highLayer.RefreshAllTiles();

        SpawnWorldObjects();
        Debug.Log($"✅ World ready | Objects: {spawnedObjects.Count}");
    }

    // =========================================================
    //  PASS 1 — NOISE MAP (raw → normalize)
    // =========================================================
    void BuildNoiseMap()
    {
        int minW = -generateRadius * chunkSize;
        int maxW =  generateRadius * chunkSize + chunkSize;

        float rawMin = float.MaxValue, rawMax = float.MinValue;
        var rawMap = new Dictionary<Vector3Int, float>();

        for (int y = maxW; y >= minW; y--)
        for (int x = minW; x <  maxW; x++)
        {
            float n   = GetRawNoise(x, y);
            var   pos = new Vector3Int(x, y, 0);
            rawMap[pos] = n;
            if (n < rawMin) rawMin = n;
            if (n > rawMax) rawMax = n;
        }

        foreach (var kvp in rawMap)
            noiseCache[kvp.Key] = Mathf.InverseLerp(rawMin, rawMax, kvp.Value);

        Debug.Log($"📊 Noise raw: {rawMin:F3} → {rawMax:F3}");
    }

    // =========================================================
    //  PASS 2 — PLACE TILES với multi-level stacking đúng
    // =========================================================
    void PlaceTiles()
    {
        int minW = -generateRadius * chunkSize;
        int maxW =  generateRadius * chunkSize + chunkSize;

        for (int y = maxW; y >= minW; y--)
        for (int x = minW; x <  maxW; x++)
        {
            var   pos   = new Vector3Int(x, y, 0);
            float noise = noiseCache.TryGetValue(pos, out float n) ? n : 0f;

            TileBase tile = GetTileFromNoise(noise);
            tileCache[pos] = tile;

            // Ground: tất cả tile
            groundLayer.SetTile(pos, tile);

            // Mid layer: vùng đất có noise > midThreshold
            // → dùng CÙNG tile với ground để tạo hiệu ứng "block cao hơn"
            if (noise > midLevelThreshold && tile != tileWater)
                midLayer.SetTile(pos, tile);

            // High layer: chỉ vùng rất cao, không có water/sand
            if (noise > highLevelThreshold && tile != tileWater && tile != tileSand)
                highLayer.SetTile(pos, tile);
        }
    }

    // =========================================================
    //  PASS 3 — SPAWN OBJECTS
    // =========================================================
    void SpawnWorldObjects()
    {
        if (spawnableObjects == null || spawnableObjects.Count == 0) return;

        foreach (var kvp in tileCache)
        {
            var cellPos = kvp.Key;
            var tile    = kvp.Value;

            foreach (var cfg in spawnableObjects)
            {
                if (cfg == null || cfg.prefab == null)      continue;
                if (!IsTileAllowed(tile, cfg.allowedTiles)) continue;

                if (cfg.useClusterNoise)
                {
                    float c = Mathf.PerlinNoise(
                        (cellPos.x + offsetX * 0.3f) * cfg.clusterNoiseScale,
                        (cellPos.y + offsetY * 0.3f) * cfg.clusterNoiseScale);
                    if (c < cfg.clusterThreshold) continue;
                }

                if (Random.value > cfg.spawnChance) continue;

                float noise = noiseCache.TryGetValue(cellPos, out float n) ? n : 0f;
                int level   = noise > highLevelThreshold ? 2
                            : noise > midLevelThreshold  ? 1 : 0;

                SpawnObject(cfg, cellPos, level);
                break; // 1 tile = 1 object tối đa
            }
        }
    }

    void SpawnObject(SpawnableObjectConfig cfg, Vector3Int cellPos, int level)
    {
        Vector3 cellCenter = groundLayer.GetCellCenterWorld(cellPos);

        // Mỗi level cao thêm 0.5 Y — khớp với Cell Size Y = 0.5 của Grid
        float   levelY   = level * 0.5f;
        float   jitter   = cfg.positionJitter;
        Vector3 worldPos = new Vector3(
            cellCenter.x + Random.Range(-jitter, jitter),
            cellCenter.y + levelY + cfg.yOffset + Random.Range(-jitter * 0.5f, jitter * 0.5f),
            0f);

        Vector2 screenPos = Camera.main != null
            ? (Vector2)Camera.main.WorldToScreenPoint(worldPos) : Vector2.zero;

        float      scale = Random.Range(cfg.minScale, cfg.maxScale);
        GameObject obj   = Instantiate(cfg.prefab, worldPos, Quaternion.identity, objectsParent);
        obj.transform.localScale *= scale;
        obj.name = $"{cfg.objectName}_{cellPos.x}_{cellPos.y}";

        // Isometric depth sort
        foreach (var sr in obj.GetComponentsInChildren<SpriteRenderer>())
            sr.sortingOrder += Mathf.RoundToInt(-worldPos.y * 10f);

        spawnedObjects.Add(new WorldObjectData
        {
            worldPosition  = worldPos,
            cellPosition   = cellPos,
            screenPosition = screenPos,
            instance       = obj,
            objectType     = cfg.objectName
        });
    }

    // =========================================================
    //  HELPERS
    // =========================================================
    float GetRawNoise(int x, int y)
    {
        float val = 0, amp = 1, freq = noiseScale, maxAmp = 0;
        for (int i = 0; i < octaves; i++)
        {
            val    += Mathf.PerlinNoise((x + offsetX) * freq, (y + offsetY) * freq) * amp;
            maxAmp += amp;
            amp    *= persistence;
            freq   *= lacunarity;
        }

        float noise = Mathf.Pow(val / maxAmp, redistributionPower);

        if (useIslandFalloff)
        {
            float mapR    = generateRadius * chunkSize;
            float falloff = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(x*x + y*y) / mapR), falloffStrength);
            noise *= falloff;
        }
        return noise;
    }

    TileBase GetTileFromNoise(float n)
    {
        if (n < waterThreshold) return tileWater;
        if (n < sandThreshold)  return tileSand;
        if (n < dirtThreshold)  return tileDirt;
        if (n < grassThreshold) return tileGrass;
        return tileStone;
    }

    bool IsTileAllowed(TileBase tile, TileBase[] allowed)
    {
        if (allowed == null || allowed.Length == 0) return false;
        foreach (var t in allowed) if (t == tile) return true;
        return false;
    }

    public TileBase GetTileAt(Vector3Int cell)  => tileCache.TryGetValue(cell, out var t) ? t : null;
    public bool     IsWalkable(Vector3Int cell) => GetTileAt(cell) is { } t && t != tileWater;
    public float    GetNoiseAt(Vector3Int cell) => noiseCache.TryGetValue(cell, out float n) ? n : 0f;

    public void RefreshScreenPositions()
    {
        if (Camera.main == null) return;
        foreach (var d in spawnedObjects)
            d.screenPosition = Camera.main.WorldToScreenPoint(d.worldPosition);
    }

    public List<WorldObjectData> GetObjectsInRadius(Vector3 center, float radius)
    {
        var result = new List<WorldObjectData>();
        foreach (var d in spawnedObjects)
            if (Vector3.Distance(d.worldPosition, center) <= radius)
                result.Add(d);
        return result;
    }

    void OnDrawGizmosSelected()
    {
        if (spawnedObjects == null) return;
        foreach (var d in spawnedObjects)
        {
            Gizmos.color = d.objectType.Contains("oak") ? Color.green : Color.gray;
            Gizmos.DrawWireSphere(d.worldPosition, 0.15f);
        }
    }
}