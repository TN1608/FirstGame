using UnityEngine;

/// <summary>
/// Generates heightmap for IsometricZAsY single-tilemap approach.
///
/// KEY CONSTRAINT — Z-as-Y tile alignment:
///   Unity offsets each tile vertically by: screenY += tileZ * cellSize.Y
///   With cellSize.Y = 0.5, each Z step = 0.5 world units on screen.
///   A tile sprite is 1 cell tall = 0.5 world units (PPU=32, sprite=16px tall top face).
///
///   Therefore: MAX usable Z range = sprite_pixel_height / (PPU * cellSize.Y)
///   For PPU=32, cellSize.Y=0.5: max meaningful Z difference ≈ 8-10 levels
///   before tiles start visually disconnecting from the grid.
///
///   CORRECT SETTINGS:
///     baseHeight   = 2
///     heightAmplitude = 4   → range 0..6  (safe, clean steps)
///     seaLevel     = 2
///
///   WRONG (current):
///     baseHeight=10, amplitude=20 → range -10..30 (tiles fly off grid)
/// </summary>
public static class HeightmapGenerator
{
    public static void Generate(ChunkData chunk, WorldGeneratorSettings settings)
    {
        var s      = settings;
        var origin = chunk.WorldOrigin;
        int size   = chunk.chunkSize;

        InitNoiseOffsets(s);

        for (int lx = 0; lx < size; lx++)
        for (int ly = 0; ly < size; ly++)
        {
            float wx = origin.x + lx;
            float wy = origin.y + ly;

            // Sample noise layers
            float cont   = Sample(wx, wy, s.continentalnessNoise);
            float erosion= Sample(wx, wy, s.erosionNoise);
            float weird  = Sample(wx, wy, s.weirdnessNoise);
            float temp   = Sample(wx, wy, s.temperatureNoise);
            float hum    = Sample(wx, wy, s.humidityNoise);
            float detail = Sample(wx, wy, s.detailNoise);
            float decoN  = Sample(wx, wy, s.decorationNoise);

            float peaksValleys = NoiseSampler.RidgesFolded(weird);

            chunk.continentalnessMap[lx, ly] = cont;
            chunk.erosionMap        [lx, ly] = erosion;
            chunk.weirdnessMap      [lx, ly] = peaksValleys;
            chunk.temperatureMap    [lx, ly] = temp;
            chunk.humidityMap       [lx, ly] = hum;
            chunk.decorationNoiseMap[lx, ly] = decoN;

            // Combine noise layers → 0..1
            float totalWeight = s.continentalnessWeight + s.erosionWeight
                              + s.peaksValleysWeight    + s.detailWeight;

            float combined =
                  cont          * s.continentalnessWeight
                + (1f - erosion)* s.erosionWeight
                + peaksValleys  * s.peaksValleysWeight
                + detail        * s.detailWeight;

            combined = combined / Mathf.Max(totalWeight, 0.001f); // → 0..1

            chunk.heightMap[lx, ly] = combined;

            // Map 0..1 → integer height
            // combined=0   → baseHeight - amplitude  (lowest)
            // combined=0.5 → baseHeight              (sea level)
            // combined=1   → baseHeight + amplitude  (highest)
            int h = Mathf.RoundToInt(
                s.baseHeight + (combined * 2f - 1f) * s.heightAmplitude
            );
            h = Mathf.Clamp(h, 0, s.baseHeight + s.heightAmplitude);

            chunk.tileHeights[lx, ly] = h;
            chunk.isWater    [lx, ly] = h <= s.seaLevel;

            if (s.biomeSettings != null)
                chunk.biomeMap[lx, ly] = s.biomeSettings.GetBiome(temp, hum, cont);
        }

        chunk.MarkGenerated();
    }

    static float Sample(float wx, float wy, NoiseSettings ns)
    {
        if (ns == null) return 0.5f;
        return NoiseSampler.Sample(wx, wy, ns);
    }

    // ── Init offsets once per seed ─────────────────────────────────────

    static int _lastSeed = int.MinValue;

    static void InitNoiseOffsets(WorldGeneratorSettings s)
    {
        int seed = s.continentalnessNoise ? s.continentalnessNoise.seed : 0;
        if (seed == _lastSeed) return;
        _lastSeed = seed;

        Init(s.continentalnessNoise, seed, 0);
        Init(s.erosionNoise,         seed, 1);
        Init(s.weirdnessNoise,       seed, 2);
        Init(s.temperatureNoise,     seed, 3);
        Init(s.humidityNoise,        seed, 4);
        Init(s.detailNoise,          seed, 5);
        Init(s.decorationNoise,      seed, 6);
    }

    static void Init(NoiseSettings n, int baseSeed, int slot)
    {
        if (n == null) return;
        var mixSeed = (unchecked((int)2654435761) * baseSeed) ^ slot;
        var rng   = new System.Random(mixSeed);
        n.offsetX = rng.Next(-100000, 100000);
        n.offsetY = rng.Next(-100000, 100000);
    }
}