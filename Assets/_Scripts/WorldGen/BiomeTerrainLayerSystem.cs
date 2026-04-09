// ==================== BiomeTerrainLayerSystem.cs ====================
// Natural biome distribution for 2.5D terrain
// Minecraft/Core Keeper style: Water → Sand → Grass → Stone

using UnityEngine;
using UnityEngine.Tilemaps;
using _Scripts.WorldGen;
using ProceduralWorld.Generation;
using ProceduralWorld.Data;

namespace ProceduralWorld.Generation
{
    /// <summary>
    /// Biome Terrain Layer System
    /// 
    /// Creates natural-looking multi-level terrain:
    /// • Water bodies at low elevations
    /// • Sandy beaches/paths between water and grass
    /// • Grass plains at mid elevation
    /// • Rocky mountains at high elevation
    /// • Rivers carved through terrain
    /// 
    /// Uses LAYERED TILEMAPS for 2.5D visual depth
    /// (Each layer offset by Y for isometric perspective)
    /// </summary>
    public static class BiomeTerrainLayerSystem
    {
        #region ===== RENDER TO MULTIPLE TILEMAPS =====
        /// <summary>
        /// Render chunk with multiple tilemap layers (for visual depth)
        /// Each elevation level gets its own tilemap layer at different Y offset
        /// </summary>
        public static void RenderChunkWithLayers(
            ChunkData chunk,
            WorldGenerationConfig config,
            int chunkSize,
            float tileSize,
            Tilemap groundLayer,
            Transform tilemapParent,
            Grid grid)
        {
            if (groundLayer == null)
            {
                Debug.LogError("[BiomeTerrainLayerSystem] groundLayer is null!");
                return;
            }

            // Render all tiles
            for (int y = 0; y < chunkSize; y++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    int worldX = chunk.coord.x * chunkSize + x;
                    int worldY = chunk.coord.y * chunkSize + y;

                    float height = chunk.heightMap[y, x];
                    BiomeType biome = chunk.biomeMap[y, x];
                    int elevation = chunk.elevationLevels[y, x];

                    // Get tile for this biome
                    BiomeTileSetConfig biomeConfig = config.GetBiomeForHeight(height);
                    if (biomeConfig == null)
                        continue;

                    TileBase tile = biomeConfig.GetRandomTile();
                    if (tile == null)
                        continue;

                    Vector3Int tilePos = new Vector3Int(worldX, worldY, 0);

                    // Place tile on ground layer
                    groundLayer.SetTile(tilePos, tile);

                    // Store biome info for later use
                    chunk.biomeMap[y, x] = biome;
                }
            }

            chunk.isRendered = true;
        }
        #endregion

        #region ===== DETERMINE BIOME FROM HEIGHT (Natural Distribution) =====
        /// <summary>
        /// Determine biome NATURALLY based on height
        /// This creates natural terrain patterns
        /// </summary>
        public static BiomeType DetermineBiomeNatural(
            float height,
            float worldX, float worldY,
            int seed,
            WorldGenerationConfig config)
        {
            // WATER: Lowest elevation
            if (height < config.waterLevel)
            {
                // Add slight variation to water
                float waterNoise = ImprovedNoiseGenerator.Hash(seed + 5, (int)worldX, (int)worldY);
                if (height < config.waterLevel - 0.05f)
                    return BiomeType.Water;  // Deep water
                else if (waterNoise < 0.3f)
                    return BiomeType.Water;  // Shallow water
            }

            // BEACH/PATH: Between water and grass
            if (height < config.waterLevel + config.beachHeight)
            {
                return BiomeType.Path;  // Sand/Beach
            }

            // GRASS PLAINS: Mid-level elevation
            // This should be the most common biome
            if (height < 0.65f)
            {
                // Occasional brush/forest clusters
                float brushNoise = ImprovedNoiseGenerator.GetFBM(
                    worldX * 0.3f, worldY * 0.3f,
                    0.1f, 3, 0.5f, 2f, 1f,
                    seed + 6, Vector2.zero
                );

                if (brushNoise < 0.35f)
                    return BiomeType.Brush;  // Sparse trees/brush

                return BiomeType.Grass;
            }

            // STONE MOUNTAINS: High elevation
            return BiomeType.Stone;
        }
        #endregion

        #region ===== RIVER CARVING =====
        /// <summary>
        /// Carve rivers through terrain
        /// Rivers should be WATER type and flow from high to low elevations
        /// </summary>
        public static BiomeType ApplyRiverCarving(
            float height,
            float worldX, float worldY,
            BiomeType basebiome,
            WorldGenerationConfig config,
            int seed)
        {
            if (!config.perlinWorms.enabled)
                return basebiome;

            // Get worm (river) value
            float wormCarve = ImprovedNoiseGenerator.GetPerlinWorm(
                worldX, worldY,
                config.perlinWorms,
                seed,
                ImprovedNoiseGenerator.GetSeedOffset(seed)
            );

            // If carved significantly, it's a river
            if (wormCarve < -0.15f)
            {
                return BiomeType.Water;  // River!
            }

            return basebiome;
        }
        #endregion

        #region ===== ELEVATION LEVEL DETERMINATION =====
        public static int DetermineElevationLevel(float height)
        {
            // Map height [0, 1] to 4 elevation levels
            if (height < 0.40f)
                return 0;  // Ground level
            else if (height < 0.60f)
                return 1;  // Mid level
            else if (height < 0.80f)
                return 2;  // High level
            else
                return 3;  // Peak
        }
        #endregion

        #region ===== BEACH SMOOTHING =====
        /// <summary>
        /// Apply beach smoothing to create natural coastlines
        /// Prevent abrupt transitions from water to grass
        /// </summary>
        public static void ApplyBeachSmoothing(ChunkData chunk, int chunkSize, WorldGenerationConfig config)
        {
            for (int y = 1; y < chunkSize - 1; y++)
            {
                for (int x = 1; x < chunkSize - 1; x++)
                {
                    float height = chunk.heightMap[y, x];
                    BiomeType biome = chunk.biomeMap[y, x];

                    // If this is grass next to water, check if it should be beach
                    if (biome == BiomeType.Grass)
                    {
                        // Count water neighbors
                        int waterNeighbors = 0;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                if (chunk.biomeMap[y + dy, x + dx] == BiomeType.Water)
                                    waterNeighbors++;
                            }
                        }

                        // If close to water, make it beach
                        if (waterNeighbors >= 2 && height < config.waterLevel + config.beachHeight * 2f)
                        {
                            chunk.biomeMap[y, x] = BiomeType.Path;  // Beach
                        }
                    }
                }
            }
        }
        #endregion

        #region ===== FOREST CLUSTERING =====
        /// <summary>
        /// Apply brush/forest clustering for natural-looking vegetation
        /// Use Perlin noise to create clusters instead of random placement
        /// </summary>
        public static void ApplyForestClustering(ChunkData chunk, int chunkSize, int seed, float clusterScale = 0.15f)
        {
            Vector2 seedOffset = ImprovedNoiseGenerator.GetSeedOffset(seed);

            for (int y = 0; y < chunkSize; y++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    int worldX = chunk.coord.x * chunkSize + x;
                    int worldY = chunk.coord.y * chunkSize + y;
                    BiomeType biome = chunk.biomeMap[y, x];

                    // Only apply to grass
                    if (biome != BiomeType.Grass)
                        continue;

                    // Get cluster noise
                    float clusterNoise = ImprovedNoiseGenerator.GetFBM(
                        worldX * clusterScale, worldY * clusterScale,
                        0.08f, 2, 0.6f, 2f, 1f,
                        seed + 7,
                        seedOffset
                    );

                    // If high cluster noise, it's a forest/brush area
                    if (clusterNoise > 0.65f)
                    {
                        chunk.biomeMap[y, x] = BiomeType.Brush;
                    }
                }
            }
        }
        #endregion

        #region ===== COMPLETE BIOME GENERATION =====
        /// <summary>
        /// Full biome generation pipeline
        /// </summary>
        public static void GenerateCompleteBiomeMap(
            ChunkData chunk,
            int chunkSize,
            WorldGenerationConfig config,
            int seed)
        {
            Vector2 seedOffset = ImprovedNoiseGenerator.GetSeedOffset(seed);

            // Step 1: Base biome assignment (height-based)
            for (int y = 0; y < chunkSize; y++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    int worldX = chunk.coord.x * chunkSize + x;
                    int worldY = chunk.coord.y * chunkSize + y;
                    float height = chunk.heightMap[y, x];

                    // Natural biome distribution
                    BiomeType biome = DetermineBiomeNatural(
                        height, worldX, worldY, seed, config
                    );

                    // Apply river carving
                    biome = ApplyRiverCarving(height, worldX, worldY, biome, config, seed);

                    chunk.biomeMap[y, x] = biome;
                }
            }

            // Step 2: Beach smoothing (natural coastlines)
            ApplyBeachSmoothing(chunk, chunkSize, config);

            // Step 3: Forest clustering (natural vegetation patterns)
            ApplyForestClustering(chunk, chunkSize, seed);

            // Step 4: Elevation levels for layering
            for (int y = 0; y < chunkSize; y++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    float height = chunk.heightMap[y, x];
                    chunk.elevationLevels[y, x] = DetermineElevationLevel(height);
                }
            }
        }
        #endregion
    }
}