using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Paints a fully-computed ChunkData onto Unity Tilemaps.
///
/// Z-AS-Y APPROACH (recommended):
///   Grid: Cell Swizzle = XYZ, Tilemap Orientation = IsometricZAsY
///   We place ALL terrain in ONE Tilemap using 3D cell positions (x, y, z).
///   Unity sorts by -(y*2 + z) automatically — higher Z = renders on top.
///   No need for one Tilemap per height level.
///
/// COLUMN APPROACH (stacked tilemap per level):
///   For each height h we set tiles at (x, y) on Tilemap[h].
///   Simpler tile setup but requires cloning many Tilemap GameObjects.
///   Enabled by STACKED_TILEMAPS define below.
/// </summary>
public static class TilemapPainter
{
    // -------------------------------------------------------
    // Switch between approaches:
    // #define STACKED_TILEMAPS   → one tilemap per level (old way)
    // Default: Z-as-Y single tilemap
    // -------------------------------------------------------

    public static void Paint(ChunkData chunk, WorldGeneratorSettings settings,
                             TilemapColumnManager columnManager = null)
    {
#if STACKED_TILEMAPS
        PaintStacked(chunk, settings, columnManager);
#else
        PaintZAsY(chunk, settings);
#endif
    }

    // -------------------------------------------------------
    // Z-AS-Y APPROACH — single Tilemap, 3D cell positions
    // -------------------------------------------------------
    static void PaintZAsY(ChunkData chunk, WorldGeneratorSettings s)
    {
        if (s.terrainTilemap == null)
        {
            Debug.LogWarning("[TilemapPainter] terrainTilemap is null.");
            return;
        }

        var terrain    = s.terrainTilemap;
        var water      = s.waterTilemap;
        var decoration = s.decorationTilemap;

        Vector2Int origin = chunk.WorldOrigin;
        int size = chunk.chunkSize;

        // Batch set for performance
        var positions = new System.Collections.Generic.List<Vector3Int>();
        var tiles     = new System.Collections.Generic.List<TileBase>();

        for (int lx = 0; lx < size; lx++)
        for (int ly = 0; ly < size; ly++)
        {
            int wx = origin.x + lx;
            int wy = origin.y + ly;
            int h  = chunk.tileHeights[lx, ly];
            bool isWater = chunk.isWater[lx, ly];

            var biome = chunk.biomeMap[lx, ly];

            if (isWater)
            {
                // Water sits at seaLevel Z
                if (water != null)
                {
                    TileBase wt = biome?.waterTile ?? s.defaultWaterTile;
                    if (wt != null)
                        water.SetTile(new Vector3Int(wx, wy, s.seaLevel), wt);
                }
                // Still place ground under water using subsurface tile
                for (int col = 0; col <= h; col++)
                {
                    TileBase t = biome?.subsurfaceTile ?? s.defaultGroundTile;
                    if (t != null)
                        terrain.SetTile(new Vector3Int(wx, wy, col), t);
                }
            }
            else
            {
                // Surface tile (top)
                TileBase surfTile = biome?.surfaceTile ?? s.defaultGroundTile;
                TileBase subTile  = biome?.subsurfaceTile ?? s.defaultGroundTile;

                // Place column: subsurface from 0 to h-1, surface at h
                for (int col = 0; col < h; col++)
                {
                    if (subTile != null)
                        terrain.SetTile(new Vector3Int(wx, wy, col), subTile);
                }
                if (surfTile != null)
                    terrain.SetTile(new Vector3Int(wx, wy, h), surfTile);

                // Stairs: place stair tile between neighboring height differences
                // (check right neighbour within chunk bounds)
                if (s.stairTile != null && lx + 1 < size)
                {
                    int nh = chunk.tileHeights[lx + 1, ly];
                    if (Mathf.Abs(h - nh) == 1 && Random.value < s.stairSpawnChance)
                        terrain.SetTile(new Vector3Int(wx, wy, h), s.stairTile);
                }

                // Decoration
                if (decoration != null && biome != null
                    && chunk.decorationNoiseMap[lx, ly] < biome.decorationDensity)
                {
                    TileBase dt = biome.surfaceTile; // placeholder; use prefab spawn instead
                    // In a real project: Instantiate biome.decorationPrefabs[random] at worldPos
                }
            }
        }

        // Apply darken color to lower tiles
        ApplyColorDarken(terrain, chunk, s);

        if (s.setStaticAfterGeneration)
            UnityEngine.SceneManagement.SceneManager.GetActiveScene()
                .GetRootGameObjects(); // noop — call StaticBatchingUtility.Combine in production
    }

    static void ApplyColorDarken(Tilemap map, ChunkData chunk, WorldGeneratorSettings s)
    {
        if (s.colorStepDarken <= 0f) return;
        Vector2Int origin = chunk.WorldOrigin;
        int size = chunk.chunkSize;

        for (int lx = 0; lx < size; lx++)
        for (int ly = 0; ly < size; ly++)
        {
            int h = chunk.tileHeights[lx, ly];
            if (h <= 0) continue;

            int wx = origin.x + lx;
            int wy = origin.y + ly;

            // Surface = full brightness; each step below is darker
            for (int col = 0; col < h; col++)
            {
                int depth = h - col;
                float darken = Mathf.Clamp01(1f - depth * s.colorStepDarken);
                Color c = new Color(darken, darken, darken, 1f);
                map.SetColor(new Vector3Int(wx, wy, col), c);
            }
        }
    }

    // -------------------------------------------------------
    // STACKED TILEMAP APPROACH — one Tilemap per Z level
    // -------------------------------------------------------
    static void PaintStacked(ChunkData chunk, WorldGeneratorSettings s,
                             TilemapColumnManager mgr)
    {
        if (mgr == null)
        {
            Debug.LogError("[TilemapPainter] STACKED_TILEMAPS requires a TilemapColumnManager.");
            return;
        }

        Vector2Int origin = chunk.WorldOrigin;
        int size = chunk.chunkSize;

        for (int lx = 0; lx < size; lx++)
        for (int ly = 0; ly < size; ly++)
        {
            int wx = origin.x + lx;
            int wy = origin.y + ly;
            int h  = chunk.tileHeights[lx, ly];
            var biome = chunk.biomeMap[lx, ly];

            for (int col = 0; col <= h; col++)
            {
                Tilemap tm = mgr.GetOrCreate(col);
                TileBase tile = (col == h)
                    ? (biome?.surfaceTile  ?? s.defaultGroundTile)
                    : (biome?.subsurfaceTile ?? s.defaultGroundTile);

                if (tile != null) tm.SetTile(new Vector3Int(wx, wy, 0), tile);

                // Color darken for sides
                if (s.colorStepDarken > 0f)
                {
                    int depth = h - col;
                    float d = Mathf.Clamp01(1f - depth * s.colorStepDarken);
                    tm.SetColor(new Vector3Int(wx, wy, 0), new Color(d, d, d, 1f));
                }
            }

            // Water
            if (chunk.isWater[lx, ly] && s.waterTilemap != null)
            {
                TileBase wt = biome?.waterTile ?? s.defaultWaterTile;
                if (wt != null)
                    s.waterTilemap.SetTile(new Vector3Int(wx, wy, 0), wt);
            }
        }
    }
}
