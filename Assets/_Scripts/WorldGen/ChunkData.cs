using UnityEngine;

/// <summary>
/// Pure data container for one chunk. No MonoBehaviour.
/// </summary>
public class ChunkData
{
    public readonly Vector2Int chunkCoord;
    public readonly int chunkSize;

    // [localX, localY]
    public readonly float[,] heightMap;
    public readonly float[,] temperatureMap;
    public readonly float[,] humidityMap;
    public readonly float[,] continentalnessMap;
    public readonly float[,] erosionMap;
    public readonly float[,] weirdnessMap;       // → peaks & valleys after folding
    public readonly float[,] decorationNoiseMap;

    public readonly int[,] tileHeights;          // final integer height per cell
    public readonly BiomeSettings.BiomeDefinition[,] biomeMap;
    public readonly bool[,] isWater;

    public bool IsGenerated { get; private set; }

    public ChunkData(Vector2Int coord, int size)
    {
        chunkCoord = coord;
        chunkSize  = size;

        heightMap           = new float[size, size];
        temperatureMap      = new float[size, size];
        humidityMap         = new float[size, size];
        continentalnessMap  = new float[size, size];
        erosionMap          = new float[size, size];
        weirdnessMap        = new float[size, size];
        decorationNoiseMap  = new float[size, size];

        tileHeights = new int[size, size];
        biomeMap    = new BiomeSettings.BiomeDefinition[size, size];
        isWater     = new bool[size, size];
    }

    public void MarkGenerated() => IsGenerated = true;

    /// <summary>World-space origin (bottom-left) of this chunk.</summary>
    public Vector2Int WorldOrigin => chunkCoord * chunkSize;
}
