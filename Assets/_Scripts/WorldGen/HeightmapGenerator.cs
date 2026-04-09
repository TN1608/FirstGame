using UnityEngine;

/// <summary>
/// Generates all noise maps and final integer heightmap for a ChunkData.
/// Pure computation - no Tilemap calls, safe to run on background thread.
/// </summary>
public static class HeightmapGenerator
{
    public static void Generate(ChunkData chunk, WorldGeneratorSettings settings)
    {
        var s = settings;
        Vector2Int origin = chunk.WorldOrigin;
        int size = chunk.chunkSize;

        // Seed all noise offsets once per world generation call
        InitNoiseOffsets(s);

        for (int lx = 0; lx < size; lx++)
        for (int ly = 0; ly < size; ly++)
        {
            float wx = origin.x + lx;
            float wy = origin.y + ly;

            // --- Sample each noise layer ---
            float cont    = NoiseSampler.Sample(wx, wy, s.continentalnessNoise);
            float erosion = NoiseSampler.Sample(wx, wy, s.erosionNoise);
            float weird   = NoiseSampler.Sample(wx, wy, s.weirdnessNoise);
            float temp    = NoiseSampler.Sample(wx, wy, s.temperatureNoise);
            float hum     = NoiseSampler.Sample(wx, wy, s.humidityNoise);
            float detail  = NoiseSampler.Sample(wx, wy, s.detailNoise);
            float decoN   = NoiseSampler.Sample(wx, wy, s.decorationNoise);

            // Peaks & Valleys = ridges folded from weirdness
            float peaksValleys = NoiseSampler.RidgesFolded(weird);

            // Store raw maps
            chunk.continentalnessMap[lx, ly] = cont;
            chunk.erosionMap[lx, ly]         = erosion;
            chunk.weirdnessMap[lx, ly]       = peaksValleys;
            chunk.temperatureMap[lx, ly]     = temp;
            chunk.humidityMap[lx, ly]        = hum;
            chunk.decorationNoiseMap[lx, ly] = decoN;

            // --- Combine into final height ---
            // Continentalness: high = tall land, low = ocean
            // Erosion: high = flat (eroded), low = mountainous
            // Erosion inverted: 1 - erosion amplifies height variation
            float combinedHeight =
                cont    * s.continentalnessWeight +
                (1f - erosion) * s.erosionWeight +
                peaksValleys * s.peaksValleysWeight +
                detail  * s.detailWeight;

            // Normalize by total weight
            float totalWeight = s.continentalnessWeight + s.erosionWeight
                              + s.peaksValleysWeight    + s.detailWeight;
            combinedHeight /= Mathf.Max(totalWeight, 0.001f);

            chunk.heightMap[lx, ly] = combinedHeight;

            // Map 0-1 → integer tile height
            // 0 = baseHeight - amplitude, 1 = baseHeight + amplitude
            int tileH = Mathf.RoundToInt(
                s.baseHeight + (combinedHeight * 2f - 1f) * s.heightAmplitude
            );
            tileH = Mathf.Max(0, tileH);
            chunk.tileHeights[lx, ly] = tileH;

            // Water: anything at or below seaLevel
            chunk.isWater[lx, ly] = tileH <= s.seaLevel;

            // Biome lookup
            if (s.biomeSettings != null)
                chunk.biomeMap[lx, ly] = s.biomeSettings.GetBiome(temp, hum, cont);
        }

        chunk.MarkGenerated();
    }

    // Call once before generating a new world so offsets are stable per seed
    static int _lastSeed = int.MinValue;
    static void InitNoiseOffsets(WorldGeneratorSettings s)
    {
        int seed = s.continentalnessNoise ? s.continentalnessNoise.seed : 0;
        if (seed == _lastSeed) return;
        _lastSeed = seed;

        void Init(NoiseSettings n, int offset)
        {
            if (n == null) return;
            var rng = new System.Random(n.seed + offset);
            n.offsetX = rng.Next(-100000, 100000);
            n.offsetY = rng.Next(-100000, 100000);
        }

        Init(s.continentalnessNoise, 0);
        Init(s.erosionNoise,         1);
        Init(s.weirdnessNoise,       2);
        Init(s.temperatureNoise,     3);
        Init(s.humidityNoise,        4);
        Init(s.detailNoise,          5);
        Init(s.decorationNoise,      6);
    }
}
