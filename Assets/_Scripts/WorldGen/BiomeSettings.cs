using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "BiomeSettings", menuName = "WorldGen/Biome Settings")]
public class BiomeSettings : ScriptableObject
{
    [System.Serializable]
    public class BiomeDefinition
    {
        public string biomeName = "Grassland";
        [Range(0f, 1f)] public float minTemperature;
        [Range(0f, 1f)] public float maxTemperature = 1f;
        [Range(0f, 1f)] public float minHumidity;
        [Range(0f, 1f)] public float maxHumidity = 1f;
        [Range(-1f, 1f)] public float minContinentalness = -1f;
        [Range(-1f, 1f)] public float maxContinentalness = 1f;

        [Header("Tiles")]
        public TileBase surfaceTile;          // top face
        public TileBase subsurfaceTile;       // sides / lower levels
        public TileBase waterTile;

        [Header("Decoration")]
        public GameObject[] decorationPrefabs;
        [Range(0f, 1f)] public float decorationDensity = 0.05f;
        public Color biomeTint = Color.white;
    }

    public BiomeDefinition[] biomes;
    public TileBase defaultTile;
    public TileBase waterTile;

    /// <summary>
    /// Returns best matching biome given normalized 0-1 noise values.
    /// </summary>
    public BiomeDefinition GetBiome(float temperature, float humidity, float continentalness)
    {
        float bestScore = float.MaxValue;
        BiomeDefinition best = biomes.Length > 0 ? biomes[0] : null;

        foreach (var b in biomes)
        {
            if (temperature < b.minTemperature || temperature > b.maxTemperature) continue;
            if (humidity < b.minHumidity || humidity > b.maxHumidity) continue;
            if (continentalness < b.minContinentalness || continentalness > b.maxContinentalness) continue;

            // Score by distance to center of biome range
            float tMid = (b.minTemperature + b.maxTemperature) * 0.5f;
            float hMid = (b.minHumidity + b.maxHumidity) * 0.5f;
            float score = Mathf.Abs(temperature - tMid) + Mathf.Abs(humidity - hMid);
            if (score < bestScore) { bestScore = score; best = b; }
        }
        return best;
    }
}
