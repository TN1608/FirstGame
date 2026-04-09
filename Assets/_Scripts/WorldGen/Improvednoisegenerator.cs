// ==================== FILENAME: ImprovedNoiseGenerator.cs ====================
using UnityEngine;

namespace ProceduralWorld.Generation
{
    /// <summary>
    /// Advanced Noise Generator
    /// Implements Minecraft wiki + Isoterra techniques:
    ///   • Multi-octave FBM
    ///   • Layered noise (continentalness, erosion, peaks)
    ///   • Perlin worms for rivers
    ///   • 3D density fields for caves
    ///   • Spline shaping
    ///   • Heightmap texture blending
    /// </summary>
    public static class ImprovedNoiseGenerator
    {
        #region ===== BASIC FBM =====
        /// <summary>
        /// Fractional Brownian Motion - multi-octave Perlin noise
        /// </summary>
        public static float GetFBM(
            float worldX, float worldY,
            float scale, int octaves, float persistence, float lacunarity,
            float redistributionPower, int seed, Vector2 seedOffset)
        {
            float value = 0f;
            float maxValue = 0f;
            float amplitude = 1f;
            float frequency = scale;

            for (int i = 0; i < octaves; i++)
            {
                float sampleX = (worldX + seedOffset.x) * frequency;
                float sampleY = (worldY + seedOffset.y) * frequency;

                // Hash coordinates for variation per octave
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
        #endregion

        #region ===== MULTI-LAYER NOISE (MINECRAFT STYLE) =====
        /// <summary>
        /// Combine multiple noise layers like Minecraft (continentalness, erosion, peaks, detail)
        /// </summary>
        public static float GetLayeredNoise(
            float worldX, float worldY,
            LayeredNoiseConfig layeredConfig,
            SplineShapeConfig splineConfig,
            int seed, Vector2 seedOffset)
        {
            // Get individual noise layers
            float continentalness = GetFBM(worldX, worldY,
                layeredConfig.continentalness.scale,
                layeredConfig.continentalness.octaves,
                layeredConfig.continentalness.persistence,
                layeredConfig.continentalness.lacunarity,
                layeredConfig.continentalness.redistributionPower,
                seed, seedOffset);

            float erosion = GetFBM(worldX, worldY,
                layeredConfig.erosion.scale,
                layeredConfig.erosion.octaves,
                layeredConfig.erosion.persistence,
                layeredConfig.erosion.lacunarity,
                layeredConfig.erosion.redistributionPower,
                seed + 1, seedOffset);

            float peaksValleys = GetFBM(worldX, worldY,
                layeredConfig.peaksValleys.scale,
                layeredConfig.peaksValleys.octaves,
                layeredConfig.peaksValleys.persistence,
                layeredConfig.peaksValleys.lacunarity,
                layeredConfig.peaksValleys.redistributionPower,
                seed + 2, seedOffset);

            float detail = GetFBM(worldX, worldY,
                layeredConfig.detail.scale,
                layeredConfig.detail.octaves,
                layeredConfig.detail.persistence,
                layeredConfig.detail.lacunarity,
                layeredConfig.detail.redistributionPower,
                seed + 3, seedOffset);

            // Apply spline shaping to individual layers
            continentalness = splineConfig.continentalnessCurve.Evaluate(continentalness);
            erosion = splineConfig.erosionCurve.Evaluate(erosion);

            // Combine with weights
            float combined =
                continentalness * layeredConfig.continentalWeight +
                erosion * layeredConfig.erosionWeight +
                peaksValleys * layeredConfig.peaksValleysWeight +
                detail * layeredConfig.detailWeight;

            combined = Mathf.Clamp01(combined);

            // Apply final height curve and multiplier
            combined = splineConfig.heightCurve.Evaluate(combined);
            combined *= splineConfig.heightMultiplier / 20f;  // Normalize to ~[0, heightMultiplier]

            return combined;
        }
        #endregion

        #region ===== PERLIN WORMS (RIVERS) =====
        /// <summary>
        /// Perlin worms - sinuous curves that carve rivers/canyons
        /// Based on Minecraft wiki technique
        /// </summary>
        public static float GetPerlinWorm(
            float worldX, float worldY,
            PerlinWormConfig wormConfig,
            int seed, Vector2 seedOffset)
        {
            if (!wormConfig.enabled)
                return 0f;

            // Main worm curve (low-frequency sine wave modulated by Perlin)
            float wormNoise = GetFBM(worldX, worldY,
                wormConfig.wormScale,
                wormConfig.wormOctaves,
                0.5f, 2f, 1f,
                seed + 100, seedOffset);

            // Create snaky path
            float wormPath = Mathf.Sin(worldX * wormConfig.wormScale * 2f + wormNoise * 5f);
            float distFromWorm = Mathf.Abs(worldY - wormPath * wormConfig.wormWidth);

            // Carving strength decreases away from worm center
            float carve = Mathf.Max(0f, 1f - (distFromWorm / (wormConfig.wormWidth * 2f)));
            carve = Mathf.Pow(carve, 2f);  // Smooth falloff

            return -carve * wormConfig.wormStrength;  // Negative to carve down
        }
        #endregion

        #region ===== 3D DENSITY FIELD (CAVES) =====
        /// <summary>
        /// 3D Perlin noise for cave/overhang generation
        /// Samples noise in 3D space (x, y, z=height)
        /// </summary>
        public static float GetDensityField(
            float worldX, float worldY, float height,
            DensityFieldConfig densityConfig,
            int seed, Vector2 seedOffset)
        {
            if (!densityConfig.enabled)
                return 1f;  // Fully solid if disabled

            // Sample 3D noise using separate samples for z-axis
            float noise3D = 0f;
            float maxNoise = 0f;
            float amplitude = 1f;
            float frequency = densityConfig.scale3D;

            for (int i = 0; i < densityConfig.octaves3D; i++)
            {
                // XY noise
                float sampleX = (worldX + seedOffset.x) * frequency;
                float sampleY = (worldY + seedOffset.y) * frequency;
                float xyNoise = Mathf.PerlinNoise(sampleX, sampleY);

                // Z noise (height-based)
                float zNoise = Mathf.Sin(height * frequency * 10f) * 0.5f + 0.5f;

                // Combine
                float combined = xyNoise * zNoise;
                noise3D += combined * amplitude;
                maxNoise += amplitude;

                amplitude *= 0.5f;
                frequency *= 2f;
            }

            float density = maxNoise > 0f ? noise3D / maxNoise : 0.5f;

            // Apply threshold for cave carving
            if (density < densityConfig.caveThreshold)
            {
                float carveAmount = (densityConfig.caveThreshold - density) / densityConfig.caveThreshold;
                return 1f - (carveAmount * densityConfig.caveStrength);
            }

            return 1f;  // Solid
        }
        #endregion

        #region ===== HEIGHTMAP TEXTURE BLENDING =====
        /// <summary>
        /// Blend procedural noise with heightmap texture
        /// </summary>
        public static float BlendHeightmapTexture(
            float proceduralHeight,
            float worldX, float worldY,
            HeightmapTextureConfig heightmapConfig)
        {
            if (heightmapConfig.heightmapTexture == null || heightmapConfig.textureBlendAmount == 0f)
                return proceduralHeight;

            // Sample texture
            float u = (worldX / heightmapConfig.textureScale) % 1f;
            float v = (worldY / heightmapConfig.textureScale) % 1f;
            if (u < 0f) u += 1f;
            if (v < 0f) v += 1f;

            Color texColor = heightmapConfig.heightmapTexture.GetPixelBilinear(u, v);
            float textureHeight = texColor.r;  // Use red channel as height

            // Blend
            return Mathf.Lerp(proceduralHeight, textureHeight,
                heightmapConfig.textureBlendAmount);
        }
        #endregion

        #region ===== FINAL HEIGHT CALCULATION =====
        /// <summary>
        /// Get final terrain height at world position
        /// Combines all noise sources + applies worms + density field
        /// </summary>
        public static float GetFinalHeight(
            float worldX, float worldY,
            LayeredNoiseConfig layeredConfig,
            SplineShapeConfig splineConfig,
            PerlinWormConfig wormConfig,
            DensityFieldConfig densityConfig,
            HeightmapTextureConfig heightmapConfig,
            int seed)
        {
            Vector2 seedOffset = GetSeedOffset(seed);

            // Base layered noise
            float baseHeight = GetLayeredNoise(worldX, worldY,
                layeredConfig, splineConfig, seed, seedOffset);

            // Apply perlin worms (river carving)
            float wormCarve = GetPerlinWorm(worldX, worldY, wormConfig, seed, seedOffset);
            baseHeight += wormCarve;

            // Apply 3D density field (cave effect)
            float density = GetDensityField(worldX, worldY, baseHeight,
                densityConfig, seed, seedOffset);
            baseHeight *= density;

            // Blend with heightmap texture if available
            baseHeight = BlendHeightmapTexture(baseHeight, worldX, worldY, heightmapConfig);

            return Mathf.Clamp01(baseHeight);
        }
        #endregion

        #region ===== UTILITY =====
        /// <summary>
        /// Fast integer hash function
        /// </summary>
        public static float Hash(int seed, int x, int y)
        {
            int h = seed ^ x * 374761393 ^ y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            h = h ^ (h >> 16);
            return (float)(h & 0x7FFFFFFF) / 0x7FFFFFFF;
        }

        /// <summary>
        /// Generate consistent seed offset from seed
        /// </summary>
        public static Vector2 GetSeedOffset(int seed)
        {
            System.Random prng = new System.Random(seed);
            return new Vector2(
                prng.Next(-100000, 100000),
                prng.Next(-100000, 100000)
            );
        }

        /// <summary>
        /// White noise for micro-detail
        /// </summary>
        public static float GetWhiteNoise(float worldX, float worldY, int seed, Vector2 offset)
        {
            int sx = (int)(worldX + offset.x);
            int sy = (int)(worldY + offset.y);
            return Hash(seed, sx, sy);
        }
        #endregion
    }
}