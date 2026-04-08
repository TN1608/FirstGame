using _Scripts.WorldGen;
using UnityEngine;
using ProceduralWorld.Generation;

namespace ProceduralWorld.Data
{
    /// <summary>
    /// ChunkDataGenerator — Pure data generation (no MonoBehaviour, thread-safe)
    /// Manages both terrain mesh data and tilemap object placement data.
    /// </summary>
    public class ChunkDataGenerator
    {
        private WorldGenerationConfig config;
        private Vector2 noiseOffset;

        public ChunkDataGenerator(WorldGenerationConfig config)
        {
            this.config = config;

            // Derive consistent offset from seed
            System.Random prng = new System.Random(config.seed);
            noiseOffset = new Vector2(
                prng.Next(-100000, 100000),
                prng.Next(-100000, 100000)
            );
        }

        public Vector2 GetNoiseOffset() => noiseOffset;

        /// <summary>
        /// Generate complete chunk data including terrain heights and biome map.
        /// </summary>
        public void GenerateChunkData(ChunkData chunk)
        {
            int size = chunk.chunkSize;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int worldX = chunk.coord.x * size + x;
                    int worldY = chunk.coord.y * size + y;

                    float height01 = ImprovedNoiseGenerator.GetAdvancedHeight(worldX, worldY, config, noiseOffset);
                    chunk.noiseValues[y, x] = height01;
                    chunk.biomeMap[y, x] = ImprovedNoiseGenerator.GetBiome(height01, config);
                    
                    // Elevation level for tilemap compatibility (legacy/hybrid)
                    chunk.elevationLevels[y, x] = Mathf.FloorToInt(height01 * 3f);
                }
            }
            chunk.isGenerated = true;
        }

        /// <summary>
        /// Checks if an object can spawn based on biome and density.
        /// </summary>
        public bool CanSpawnObject(int worldX, int worldY, BiomeType biome, float chance)
        {
            float roll = ImprovedNoiseGenerator.Hash(config.seed + 10, worldX, worldY);
            return roll < chance;
        }
    }
}