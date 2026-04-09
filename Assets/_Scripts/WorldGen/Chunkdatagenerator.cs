// ==================== FILENAME: ChunkDataGenerator_IMPROVED.cs ====================
using UnityEngine;
using _Scripts.WorldGen;
using ProceduralWorld.Generation;

namespace ProceduralWorld.Data
{
    /// <summary>
    /// IMPROVED ChunkDataGenerator
    /// Now properly integrates with BiomeTileSetConfig system
    /// </summary>
    public class ChunkDataGenerator
    {
        private WorldGenerationConfig config;
        private int seed;
        private Vector2 seedOffset;

        public ChunkDataGenerator(WorldGenerationConfig config, int seed)
        {
            this.config = config;
            this.seed = seed;
            this.seedOffset = ImprovedNoiseGenerator.GetSeedOffset(seed);

            Debug.Log($"[ChunkDataGenerator] Initialized with {config.biomes.Count} biomes");
        }

        #region ===== CHUNK GENERATION =====
        public void GenerateChunkData(ChunkData chunk, int chunkSize, float tileSize = 1f)
        {
            Debug.Log($"[ChunkDataGenerator] Generating chunk {chunk.coord}");

            GenerateHeightMap(chunk, chunkSize);
            GenerateBiomeMap(chunk, chunkSize);
            GenerateElevationLevels(chunk, chunkSize);

            chunk.isGenerated = true;

            Debug.Log($"[ChunkDataGenerator] ✅ Chunk {chunk.coord} generation complete");
        }
        #endregion

        #region ===== HEIGHT MAP =====
        private void GenerateHeightMap(ChunkData chunk, int chunkSize)
        {
            for (int y = 0; y < chunkSize; y++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    int worldX = chunk.coord.x * chunkSize + x;
                    int worldY = chunk.coord.y * chunkSize + y;

                    // Get final height from noise system
                    float height = ImprovedNoiseGenerator.GetFinalHeight(
                        worldX, worldY,
                        config.layeredNoise,
                        config.splineShape,
                        config.perlinWorms,
                        config.densityField,
                        config.heightmapTexture,
                        seed
                    );

                    chunk.heightMap[y, x] = Mathf.Clamp01(height);
                }
            }
        }
        #endregion

        #region ===== BIOME MAP =====
        private void GenerateBiomeMap(ChunkData chunk, int chunkSize)
        {
            for (int y = 0; y < chunkSize; y++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    float height = chunk.heightMap[y, x];
                    
                    // Determine biome based on height using config
                    BiomeType biome = DetermineBiome(height);
                    chunk.biomeMap[y, x] = biome;
                }
            }
        }

        private BiomeType DetermineBiome(float height)
        {
            // Simple height-based biome determination
            // More sophisticated version would use noise + height combination
            
            if (height < config.waterLevel)
                return BiomeType.Water;

            if (height < config.waterLevel + config.beachHeight)
                return BiomeType.Path;  // Beach/sand

            if (height < 0.55f)
                return BiomeType.Brush;

            if (height < 0.75f)
                return BiomeType.Grass;

            return BiomeType.Stone;
        }
        #endregion

        #region ===== ELEVATION LEVELS =====
        private void GenerateElevationLevels(ChunkData chunk, int chunkSize)
        {
            for (int y = 0; y < chunkSize; y++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    float height = chunk.heightMap[y, x];
                    int elevation = DetermineElevation(height);
                    chunk.elevationLevels[y, x] = elevation;
                }
            }
        }

        private int DetermineElevation(float height)
        {
            if (height < 0.40f)
                return 0;
            else if (height < 0.60f)
                return 1;
            else if (height < 0.80f)
                return 2;
            else
                return 3;
        }
        #endregion

        #region ===== OBJECT SPAWNING =====
        public bool CanSpawnObject(
            int worldX, int worldY,
            BiomeType requiredBiome,
            int elevationLevel,
            float spawnChance)
        {
            float roll = ImprovedNoiseGenerator.Hash(seed + 2, worldX, worldY);
            if (roll > spawnChance)
                return false;

            if (requiredBiome == BiomeType.Water)
                return false;

            return true;
        }

        public Vector3 GetSpawnPosition(
            int worldX, int worldY,
            float tileSize = 1f,
            float yOffset = 0.1f,
            float positionJitter = 0.2f)
        {
            float jitterX = (ImprovedNoiseGenerator.Hash(seed + 10, worldX, worldY) - 0.5f) * positionJitter;
            float jitterY = (ImprovedNoiseGenerator.Hash(seed + 11, worldX, worldY) - 0.5f) * positionJitter;

            return new Vector3(
                (worldX + 0.5f) * tileSize + jitterX,
                (worldY + 0.5f) * tileSize + jitterY + yOffset,
                0f
            );
        }
        #endregion
    }
}