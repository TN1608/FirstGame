using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "WorldGeneratorSettings", menuName = "WorldGen/World Generator Settings")]
public class WorldGeneratorSettings : ScriptableObject
{
    [Header("Map Size")]
    public int chunkSize = 16;
    public int renderDistanceInChunks = 3;

    [Header("Height")]
    public int baseHeight = 10;
    public int heightAmplitude = 20;        // total range = ±heightAmplitude
    public int seaLevel = 10;               // absolute tile level for water surface

    [Header("Noise Layers")]
    public NoiseSettings continentalnessNoise;
    public NoiseSettings erosionNoise;
    public NoiseSettings weirdnessNoise;    // → folded for peaks & valleys
    public NoiseSettings temperatureNoise;
    public NoiseSettings humidityNoise;
    public NoiseSettings detailNoise;       // small-scale surface detail
    public NoiseSettings decorationNoise;   // density map for props

    [Header("Biomes")]
    public BiomeSettings biomeSettings;

    [Header("Tilemaps (Runtime Scene Refs)")]
    [Tooltip("Assigned at runtime from ProceduralWorldGenerator on a scene object")]
    [HideInInspector]
    public Tilemap terrainTilemap;
    [HideInInspector]
    public Tilemap waterTilemap;
    [HideInInspector]
    public Tilemap decorationTilemap;

    [Header("Fallback Tiles")]
    public TileBase defaultGroundTile;
    public TileBase defaultWaterTile;

    [Header("Visual")]
    [Tooltip("Darken sides by this multiplier per level below surface")]
    [Range(0f, 0.2f)] public float colorStepDarken = 0.06f;
    public bool setStaticAfterGeneration = true;

    [Header("Stairs")]
    [Range(0f, 1f)] public float stairSpawnChance = 0.1f;
    public TileBase stairTile;

    // Noise layer blend weights
    [Header("Blend Weights")]
    [Range(0f, 2f)] public float continentalnessWeight = 1.0f;
    [Range(0f, 2f)] public float erosionWeight         = 0.5f;
    [Range(0f, 2f)] public float peaksValleysWeight    = 0.8f;
    [Range(0f, 2f)] public float detailWeight          = 0.2f;
}
