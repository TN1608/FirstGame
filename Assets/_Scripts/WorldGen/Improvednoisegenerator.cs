using _Scripts.WorldGen;
using UnityEngine;

namespace ProceduralWorld.Generation
{
    /// <summary>
    /// ImprovedNoiseGenerator — Deterministic noise for terrain generation
    /// 
    /// FEATURES:
    ///   • Multi-octave Perlin (FBM) for natural terrain
    ///   • White noise blending for micro-variation
    ///   • Deterministic hashing for object spawning
    ///   • Thread-safe (no MonoBehaviour)
    ///   • Iso-core style terrain with terraces/levels
    /// 
    /// USAGE:
    ///   float elevation = ImprovedNoiseGenerator.GetTerrainNoise(
    ///       worldX, worldY, noiseConfig, seed
    ///   );
    /// </summary>
    public static class ImprovedNoiseGenerator
    {
        /// <summary>
        /// Multi-octave Perlin noise (FBM) with optional white noise blending
        /// Returns [0, 1]
        /// </summary>
        public static float GetTerrainNoise(
            int worldX, int worldY,
            NoiseConfig config,
            int seed,
            Vector2 seedOffset)
        {
            float fbmValue = GetFBM(
                worldX, worldY,
                config.scale,
                config.octaves,
                config.persistence,
                config.lacunarity,
                config.redistributionPower,
                seed,
                seedOffset
            );

            // ── Optional: Blend white noise for detail ──────────────
            if (config.detailStrength > 0f)
            {
                float whiteNoise = GetWhiteNoise(worldX, worldY, seed + 1, seedOffset);
                fbmValue = Mathf.Lerp(fbmValue, whiteNoise, config.detailStrength);
            }

            return Mathf.Clamp01(fbmValue);
        }

        /// <summary>
        /// Fractional Brownian Motion (FBM) — classic multi-octave Perlin
        /// </summary>
        private static float GetFBM(
            int worldX, int worldY,
            float baseScale,
            int octaves,
            float persistence,
            float lacunarity,
            float redistributionPower,
            int seed,
            Vector2 seedOffset)
        {
            float value = 0f;
            float maxValue = 0f;
            float amplitude = 1f;
            float frequency = baseScale;

            for (int i = 0; i < octaves; i++)
            {
                float sampleX = (worldX + seedOffset.x) * frequency;
                float sampleY = (worldY + seedOffset.y) * frequency;

                // Hash the coordinates for variation per octave
                int hashedSeed = seed + (i * 73856093);
                sampleX += Hash(hashedSeed, (int)sampleX, (int)sampleY);
                sampleY += Hash(hashedSeed + 1, (int)sampleX, (int)sampleY);

                value += Mathf.PerlinNoise(sampleX, sampleY) * amplitude;
                maxValue += amplitude;

                amplitude *= persistence;
                frequency *= lacunarity;
            }

            float normalized = maxValue > 0f ? value / maxValue : 0.5f;
            return Mathf.Pow(Mathf.Clamp01(normalized), redistributionPower);
        }

        /// <summary>
        /// White noise: fast pseudo-random per (x,y) coordinate
        /// Useful for micro-variation without Perlin artifacts
        /// </summary>
        private static float GetWhiteNoise(int x, int y, int seed, Vector2 offset)
        {
            int sx = (int)(x + offset.x);
            int sy = (int)(y + offset.y);
            return Hash(seed, sx, sy);
        }

        /// <summary>
        /// Fast integer hash — deterministic, good distribution
        /// Returns [0, 1]
        /// </summary>
        public static float Hash(int seed, int x, int y)
        {
            int h = seed ^ x * 374761393 ^ y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            h = h ^ (h >> 16);
            return (float)(h & 0x7FFFFFFF) / 0x7FFFFFFF;
        }

        /// <summary>
        /// Sample noise with custom animation curve (for terracing/elevation levels)
        /// Useful for creating flat biome regions
        /// </summary>
        public static float GetTerrainNoiseWithCurve(
            int worldX, int worldY,
            NoiseConfig config,
            AnimationCurve heightCurve,
            int seed,
            Vector2 seedOffset)
        {
            float noise = GetTerrainNoise(worldX, worldY, config, seed, seedOffset);
            return heightCurve.Evaluate(noise);
        }

        /// <summary>
        /// Determine elevation level based on noise value (0=ground, 1=mid, 2=high)
        /// </summary>
        public static int GetElevationLevel(float noiseValue, float midThreshold = 0.4f, float highThreshold = 0.7f)
        {
            if (noiseValue < midThreshold)
                return 0;  // Ground level
            else if (noiseValue < highThreshold)
                return 1;  // Mid level
            else
                return 2;  // High level
        }

        /// <summary>
        /// Determine biome from noise value and thresholds
        /// </summary>
        public static BiomeType GetBiomeType(float noiseValue)
        {
            // Example distribution:
            // 0.0-0.2  = Water
            // 0.2-0.35 = Sand
            // 0.35-0.55 = Dirt
            // 0.55-0.80 = Grass
            // 0.80-1.0 = Stone

            if (noiseValue < 0.20f) return BiomeType.Water;
            if (noiseValue < 0.35f) return BiomeType.Sand;
            if (noiseValue < 0.55f) return BiomeType.Dirt;
            if (noiseValue < 0.80f) return BiomeType.Grass;
            return BiomeType.Stone;
        }
    }
}