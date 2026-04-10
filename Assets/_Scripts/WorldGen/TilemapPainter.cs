using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Z-AS-Y CORRECT APPROACH
///
/// KEY INSIGHT:
/// In Unity IsometricZAsY mode, each grid cell (x, y) can hold tiles at
/// multiple Z values. Unity renders ALL of them, sorted by -(y*2 + z).
/// A tile at (x, y, z=5) renders ABOVE a tile at (x, y, z=3).
/// BUT — two tiles at the same (x,y) with different Z appear at different
/// VISUAL HEIGHTS on screen (higher Z = higher on screen).
///
/// CORRECT TERRAIN PAINTING:
/// For each cell (x, y) with height h:
///   → Place ONE surface tile at (x, y, h)   ← the top face
///   → DO NOT place column tiles below it
///      (Z-as-Y doesn't show a "side" — it just shows tiles floating higher)
///
/// The "step" / cliff visual is created by NEIGHBOURING cells having
/// different heights. When cell A at h=5 is next to cell B at h=3,
/// Unity's isometric sorting makes A appear visually above B — that IS
/// the terrain depth effect. No extra side tiles needed.
///
/// The reference image (Image 8) confirms: flat top per cell, clean steps
/// between height levels, no overlapping or diagonal stacking artifacts.
/// </summary>
public static class TilemapPainter
{
    public static void Paint(ChunkData chunk, WorldGeneratorSettings s)
    {
        if (s.terrainTilemap == null)
        {
            Debug.LogError("[TilemapPainter] terrainTilemap is null.");
            return;
        }

        Vector2Int origin = chunk.WorldOrigin;
        int        size   = chunk.chunkSize;

        // ── Terrain: one tile per cell at its height Z ───────────────────
        var terrainPos   = new Vector3Int[size * size];
        var terrainTiles = new TileBase [size * size];

        // ── Water: flat water surface at seaLevel ────────────────────────
        var waterPos     = new List<Vector3Int>(size * size / 4);
        var waterTiles   = new List<TileBase>  (size * size / 4);

        int idx = 0;
        for (int lx = 0; lx < size; lx++)
        for (int ly = 0; ly < size; ly++)
        {
            int wx    = origin.x + lx;
            int wy    = origin.y + ly;
            int h     = chunk.tileHeights[lx, ly];
            var biome = chunk.biomeMap[lx, ly];

            bool isWater = chunk.isWater[lx, ly];

            if (isWater)
            {
                // Terrain floor tile at actual height (visible under shallow water)
                TileBase groundUnder = biome?.subsurfaceTile ?? s.defaultGroundTile;
                terrainPos[idx]   = new Vector3Int(wx, wy, h);
                terrainTiles[idx] = groundUnder;
                idx++;

                // Water surface at seaLevel Z
                TileBase wt = biome?.waterTile ?? s.defaultWaterTile;
                if (wt != null)
                {
                    waterPos.Add(new Vector3Int(wx, wy, s.seaLevel));
                    waterTiles.Add(wt);
                }
            }
            else
            {
                // Land: surface tile at height h — ONE tile per cell
                TileBase surf = biome?.surfaceTile ?? s.defaultGroundTile;
                terrainPos[idx]   = new Vector3Int(wx, wy, h);
                terrainTiles[idx] = surf;
                idx++;
            }
        }

        // Batch set — much faster than individual SetTile calls
        s.terrainTilemap.SetTiles(terrainPos, terrainTiles);

        if (s.waterTilemap != null && waterPos.Count > 0)
            s.waterTilemap.SetTiles(waterPos.ToArray(), waterTiles.ToArray());
    }

    // ─────────────────────────────────────────────────────────────────────
    // ERASE — remove all tiles of a chunk
    // ─────────────────────────────────────────────────────────────────────
    public static void Erase(ChunkData chunk, WorldGeneratorSettings s)
    {
        if (chunk == null) return;

        Vector2Int origin = chunk.WorldOrigin;
        int        size   = chunk.chunkSize;

        var terrainPos   = new Vector3Int[size * size];
        var nullTiles    = new TileBase  [size * size]; // null = erase

        var waterPos     = new Vector3Int[size * size];
        var nullWater    = new TileBase  [size * size];

        for (int lx = 0; lx < size; lx++)
        for (int ly = 0; ly < size; ly++)
        {
            int i  = lx * size + ly;
            int wx = origin.x + lx;
            int wy = origin.y + ly;
            int h  = chunk.tileHeights[lx, ly];

            terrainPos[i] = new Vector3Int(wx, wy, h);
            waterPos[i]   = new Vector3Int(wx, wy, s.seaLevel);
        }

        s.terrainTilemap?.SetTiles(terrainPos, nullTiles);
        s.waterTilemap?.SetTiles(waterPos, nullWater);
    }
}