using _Scripts.WorldGen;
using UnityEngine;
using ProceduralWorld.Generation;

namespace ProceduralWorld.Generation
{
    public static class ImprovedNoiseGenerator
    {
        /// <summary>
        /// Advanced terrain height noise calculation using continentalness, erosion, and peaks & valleys.
        /// Returns final height value.
        /// </summary>
        public static float GetAdvancedHeight(int worldX, int worldY, WorldGenerationConfig config, Vector2 offset)
        {
            // 1. Continentalness (Large scale land/sea)
            float contNoise = NoiseGenerator.GetNoise2D(worldX, worldY, config.primaryNoise.scale * 0.1f, 3, 0.5f, 2f, 1f, offset);
            float continentalness = config.continentalnessCurve.Evaluate(contNoise);

            // 2. Erosion (Smoothness)
            float erosionNoise = NoiseGenerator.GetNoise2D(worldX, worldY, config.primaryNoise.scale * 0.5f, 4, 0.5f, 2f, 1f, offset + new Vector2(100, 100));
            float erosion = config.erosionCurve.Evaluate(erosionNoise);

            // 3. Peaks & Valleys (Jaggedness)
            float pvNoise = NoiseGenerator.GetNoise2D(worldX, worldY, config.primaryNoise.scale, config.primaryNoise.octaves, config.primaryNoise.persistence, config.primaryNoise.lacunarity, config.primaryNoise.redistributionPower, offset + config.primaryNoise.layerOffset);
            float peaksValleys = config.peaksValleysCurve.Evaluate(pvNoise);

            // Combine layers
            float finalHeight = continentalness + (peaksValleys * (1f - erosion));
            return finalHeight;
        }

        /// <summary>
        /// Density field for 3D terrain features.
        /// </summary>
        public static bool IsSolid(int x, int y, int z, WorldGenerationConfig config, Vector2 offset)
        {
            float height = GetAdvancedHeight(x, y, config, offset) * config.meshHeightMultiplier;
            
            // Base ground check
            if (z < height) return true;
            
            // 3D Noise for overhangs
            if (config.use3DNoiseForOverhangs && z < height + 5) // Only check near surface for performance
            {
                float density = NoiseGenerator.GetNoise3D(x, y, z, config.densityScale, offset);
                if (density > config.densityThreshold) return true;
            }

            return false;
        }

        public static BiomeType GetBiome(float noiseValue, WorldGenerationConfig config)
        {
            foreach (var bc in config.biomeConfigs)
            {
                if (noiseValue >= bc.noiseRange.x && noiseValue <= bc.noiseRange.y)
                    return bc.biomeType;
            }
            return BiomeType.Grass;
        }

        public static float Hash(int seed, int x, int y) => NoiseGenerator.Hash(x, y, seed);
    }
}