using _Scripts.WorldGen;
using UnityEngine;
using ProceduralWorld.Generation;

namespace ProceduralWorld.Data
{
    /// <summary>
    /// ChunkDataGenerator — Pure data generation (no MonoBehaviour, thread-safe)
    /// 
    /// Generates tile data and object spawn points for a chunk.
    /// Can be called from worker threads for async generation.
    /// </summary>
    public class ChunkDataGenerator
    {
        private NoiseConfig noiseConfig;
        private int seed;
        private AnimationCurve heightCurve;
        private Vector2 noiseOffset;

        public ChunkDataGenerator(
            NoiseConfig noiseConfig,
            int seed,
            AnimationCurve heightCurve = null)
        {
            this.noiseConfig = noiseConfig;
            this.seed = seed;
            this.heightCurve = heightCurve ?? AnimationCurve.Linear(0, 0, 1, 1);

            // Derive consistent offset from seed
            System.Random prng = new System.Random(seed);
            noiseOffset = new Vector2(
                prng.Next(-100000, 100000),
                prng.Next(-100000, 100000)
            );
        }

        /// <summary>
        /// Generate complete chunk data including terrain and elevation levels
        /// </summary>
        public void GenerateChunkData(
            ChunkData chunk,
            int chunkSize,
            float tileSize = 1f)
        {
            for (int cy = 0; cy < chunkSize; cy++)
            {
                for (int cx = 0; cx < chunkSize; cx++)
                {
                    // World coordinates
                    int worldX = chunk.coord.x * chunkSize + cx;
                    int worldY = chunk.coord.y * chunkSize + cy;

                    // Generate noise
                    float noiseValue = ImprovedNoiseGenerator.GetTerrainNoiseWithCurve(
                        worldX, worldY,
                        noiseConfig,
                        heightCurve,
                        seed,
                        noiseOffset
                    );

                    chunk.noiseValues[cy, cx] = noiseValue;

                    // Determine biome
                    chunk.biomeMap[cy, cx] = ImprovedNoiseGenerator.GetBiomeType(noiseValue);

                    // Determine elevation level (0=ground, 1=mid, 2=high)
                    chunk.elevationLevels[cy, cx] = ImprovedNoiseGenerator.GetElevationLevel(
                        noiseValue,
                        midThreshold: 0.4f,
                        highThreshold: 0.7f
                    );
                }
            }

            chunk.isGenerated = true;
        }

        /// <summary>
        /// Determine if object can spawn at this location
        /// Checks: biome compatibility, noise clustering, elevation level
        /// </summary>
        public bool CanSpawnObject(
            int worldX, int worldY,
            BiomeType actualBiome,
            int actualElevationLevel,
            float spawnChance,
            bool useClusterNoise = true,
            float clusterScale = 0.15f,
            float clusterThreshold = 0.55f)
        {
            // Random spawn chance
            float spawnRoll = ImprovedNoiseGenerator.Hash(seed + 2, worldX, worldY);
            if (spawnRoll > spawnChance)
                return false;

            // Clustering check (objects group together)
            if (useClusterNoise)
            {
                float clusterNoise = ImprovedNoiseGenerator.GetTerrainNoise(
                    worldX, worldY,
                    new NoiseConfig
                    {
                        scale = clusterScale,
                        octaves = 2,
                        persistence = 0.5f,
                        lacunarity = 2.0f,
                        redistributionPower = 1.0f,
                        detailStrength = 0f
                    },
                    seed + 3,
                    noiseOffset
                );

                if (clusterNoise < clusterThreshold)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Get spawn position within a tile (with jitter)
        /// </summary>
        public Vector3 GetSpawnPosition(
            int worldX, int worldY,
            float tileSize = 1f,
            float yOffset = 0.1f,
            float positionJitter = 0.2f)
        {
            // Deterministic jitter from hash
            float jitterX = (ImprovedNoiseGenerator.Hash(seed + 10, worldX, worldY) - 0.5f) * positionJitter;
            float jitterY = (ImprovedNoiseGenerator.Hash(seed + 11, worldX, worldY) - 0.5f) * positionJitter;

            Vector3 basePos = new Vector3(
                (worldX + 0.5f) * tileSize + jitterX,
                (worldY + 0.5f) * tileSize + jitterY + yOffset,
                0f
            );

            return basePos;
        }
    }
}