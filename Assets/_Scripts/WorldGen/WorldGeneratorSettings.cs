using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// IMPORTANT — Height values for IsometricZAsY:
///
/// Unity formula: screen_Y_offset = tileZ * cellSize.Y
/// With cellSize = (1, 0.5, 1):  each Z step shifts tile up by 0.5 world units.
///
/// Your tile sprite (PPU=32) has an effective world height of:
///   top_face_pixels / PPU = ~16px / 32 = 0.5 units
///
/// So 1 Z step = exactly 1 tile height = CORRECT alignment.
///
/// To keep tiles aligned with the grid:
///   - Use SMALL height ranges: baseHeight=2, amplitude=4 → heights 0..6
///   - Never exceed ~8 Z levels or tiles visually disconnect
///   - seaLevel should be at or below baseHeight
/// </summary>
[CreateAssetMenu(fileName = "WorldGeneratorSettings",
                 menuName  = "WorldGen/World Generator Settings")]
public class WorldGeneratorSettings : ScriptableObject
{
    [Header("Map Size")]
    public int chunkSize              = 16;
    public int renderDistanceInChunks = 4;

    [Header("Height  ← KEEP SMALL for Z-as-Y alignment")]
    [Tooltip("Base (sea) level. Tiles at this Z sit flush on the grid.")]
    public int baseHeight      = 2;

    [Tooltip("±amplitude around base. Total range = baseHeight ± amplitude.\n" +
             "Keep ≤4 for PPU=32 tiles. Max safe = 6.")]
    public int heightAmplitude = 4;

    [Tooltip("Z level of water surface. Should be <= baseHeight.")]
    public int seaLevel        = 2;

    [Header("Noise Layers")]
    public NoiseSettings continentalnessNoise;
    public NoiseSettings erosionNoise;
    public NoiseSettings weirdnessNoise;
    public NoiseSettings temperatureNoise;
    public NoiseSettings humidityNoise;
    public NoiseSettings detailNoise;
    public NoiseSettings decorationNoise;

    [Header("Biomes")]
    public BiomeSettings biomeSettings;

    [HideInInspector] public Tilemap terrainTilemap;
    [HideInInspector] public Tilemap waterTilemap;
    [HideInInspector] public Tilemap decorationTilemap;

    [Header("Fallback Tiles")]
    public TileBase defaultGroundTile;
    public TileBase defaultWaterTile;

    [Header("Visual")]
    [Range(0f, 0.2f)]
    public float colorStepDarken          = 0.06f;
    public bool  setStaticAfterGeneration = true;

    [Header("Stairs")]
    [Range(0f, 1f)]
    public float    stairSpawnChance = 0.1f;
    public TileBase stairTile;

    [Header("Noise Blend Weights")]
    [Range(0f, 2f)] public float continentalnessWeight = 1.0f;
    [Range(0f, 2f)] public float erosionWeight         = 0.5f;
    [Range(0f, 2f)] public float peaksValleysWeight    = 0.8f;
    [Range(0f, 2f)] public float detailWeight          = 0.2f;
}