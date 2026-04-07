using UnityEngine;

namespace _Scripts.WorldGen
{
    /// <summary>
    /// Generates a colored noise preview texture (like the terrain map in image 5).
    /// Attach to any GameObject or use as a utility class.
    /// </summary>
    public static class NoiseMapGenerator
    {
        // Color stops for the terrain colormap (water → sand → grass → forest → rock → snow)
        private static readonly (float threshold, Color color)[] TerrainColors =
        {
            (0.00f, new Color(0.05f, 0.35f, 0.70f)), // Deep water
            (0.18f, new Color(0.20f, 0.55f, 0.85f)), // Shallow water
            (0.25f, new Color(0.90f, 0.85f, 0.65f)), // Sand/beach
            (0.30f, new Color(0.75f, 0.80f, 0.45f)), // Light grass
            (0.50f, new Color(0.25f, 0.65f, 0.20f)), // Grass
            (0.70f, new Color(0.15f, 0.45f, 0.15f)), // Forest
            (0.82f, new Color(0.55f, 0.50f, 0.45f)), // Rock
            (0.90f, new Color(0.75f, 0.73f, 0.70f)), // High rock
            (1.00f, new Color(0.95f, 0.97f, 1.00f)), // Snow
        };

        /// <summary>
        /// Generate a noise map and return as a Texture2D with terrain colors.
        /// </summary>
        public static Texture2D GeneratePreviewTexture(
            int width, int height,
            float scale, int octaves, float persistence, float lacunarity,
            float redistributionPower, float falloffStrength,
            float offsetX, float offsetY,
            bool useIslandFalloff = true)
        {
            float[,] noiseMap = GenerateNoiseMap(
                width, height, scale, octaves, persistence, lacunarity,
                redistributionPower, falloffStrength, offsetX, offsetY, useIslandFalloff);

            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = SampleTerrainColor(noiseMap[x, y]);

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Generate raw noise map (normalized 0-1).
        /// </summary>
        public static float[,] GenerateNoiseMap(
            int width, int height,
            float scale, int octaves, float persistence, float lacunarity,
            float redistributionPower, float falloffStrength,
            float offsetX, float offsetY,
            bool useIslandFalloff = true)
        {
            var map = new float[width, height];
            float rawMin = float.MaxValue, rawMax = float.MinValue;

            // Pass 1: raw noise
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float n = GetRawNoise(x, y, scale, octaves, persistence, lacunarity, offsetX, offsetY);
                map[x, y] = n;
                if (n < rawMin) rawMin = n;
                if (n > rawMax) rawMax = n;
            }

            float mapHalfW = width  * 0.5f;
            float mapHalfH = height * 0.5f;
            float maxDist  = Mathf.Sqrt(mapHalfW * mapHalfW + mapHalfH * mapHalfH);

            // Pass 2: normalize + redistribution + falloff
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float n = Mathf.InverseLerp(rawMin, rawMax, map[x, y]);
                n = Mathf.Pow(n, redistributionPower);

                if (useIslandFalloff)
                {
                    float dx = x - mapHalfW;
                    float dy = y - mapHalfH;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / maxDist;
                    float falloff = Mathf.Pow(Mathf.Clamp01(1f - dist), falloffStrength);
                    n *= falloff;
                }

                map[x, y] = Mathf.Clamp01(n);
            }

            return map;
        }

        static float GetRawNoise(int x, int y,
            float scale, int octaves, float persistence, float lacunarity,
            float offsetX, float offsetY)
        {
            float val = 0, amp = 1, freq = scale, maxAmp = 0;
            for (int i = 0; i < octaves; i++)
            {
                val    += Mathf.PerlinNoise((x + offsetX) * freq, (y + offsetY) * freq) * amp;
                maxAmp += amp;
                amp    *= persistence;
                freq   *= lacunarity;
            }
            return val / maxAmp;
        }

        static Color SampleTerrainColor(float n)
        {
            for (int i = 1; i < TerrainColors.Length; i++)
            {
                if (n <= TerrainColors[i].threshold)
                {
                    float t = Mathf.InverseLerp(
                        TerrainColors[i - 1].threshold,
                        TerrainColors[i].threshold, n);
                    return Color.Lerp(TerrainColors[i - 1].color, TerrainColors[i].color, t);
                }
            }
            return TerrainColors[^1].color;
        }
    }
}