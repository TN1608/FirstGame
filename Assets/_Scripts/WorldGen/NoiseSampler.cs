using UnityEngine;

/// <summary>
/// Stateless noise sampler supporting FBM octaves for both Perlin and a
/// Value-noise approximation of OpenSimplex2 (no external dependency).
/// Returns values in 0..1 range after normalization.
/// </summary>
public static class NoiseSampler
{
    /// <summary>
    /// Sample a 2D FBM noise value, normalized to [0,1].
    /// </summary>
    public static float Sample(float worldX, float worldY, NoiseSettings s)
    {
        float value     = 0f;
        float amplitude = 1f;
        float frequency = s.frequency;
        float maxValue  = 0f;

        int ox = s.offsetX;
        int oy = s.offsetY;

        for (int i = 0; i < s.octaves; i++)
        {
            float nx = (worldX + ox) * frequency;
            float ny = (worldY + oy) * frequency;

            float raw = s.noiseType == NoiseSettings.NoiseType.Perlin
                ? Mathf.PerlinNoise(nx, ny) * 2f - 1f          // remap 0-1 → -1-1
                : ValueNoise(nx, ny);                           // already -1-1

            value    += raw * amplitude;
            maxValue += amplitude;

            amplitude *= s.persistence;
            frequency *= s.lacunarity;
        }

        // Normalize to -1..1 then remap to 0..1
        float normalized = Mathf.Clamp01((value / maxValue + 1f) * 0.5f);

        // Apply height curve
        return s.heightCurve.Evaluate(normalized) * s.heightMultiplier;
    }

    /// <summary>
    /// "Ridges Folded" formula: peaks at 0 and 1, valley in the middle.
    /// Input: raw sample in 0..1. Returns 0..1 (high = ridge, low = valley).
    /// </summary>
    public static float RidgesFolded(float sample)
    {
        // fold: 1 - |2*sample - 1|  →  1 at edges, 0 at center
        return 1f - Mathf.Abs(2f * sample - 1f);
    }

    // ------------------------------------------------------------------
    // Simple value noise fallback (no external lib required)
    // ------------------------------------------------------------------
    static float ValueNoise(float x, float y)
    {
        int xi = Mathf.FloorToInt(x);
        int yi = Mathf.FloorToInt(y);
        float xf = x - xi;
        float yf = y - yi;

        // Smoothstep
        float u = xf * xf * (3f - 2f * xf);
        float v = yf * yf * (3f - 2f * yf);

        float n00 = Hash(xi,     yi);
        float n10 = Hash(xi + 1, yi);
        float n01 = Hash(xi,     yi + 1);
        float n11 = Hash(xi + 1, yi + 1);

        float x0 = Mathf.Lerp(n00, n10, u);
        float x1 = Mathf.Lerp(n01, n11, u);
        return (Mathf.Lerp(x0, x1, v) * 2f) - 1f;  // → -1..1
    }

    static float Hash(int x, int y)
    {
        int n = x + y * 57;
        n = (n << 13) ^ n;
        return (1f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7FFFFFFF) / 1073741824f);
    }
}
