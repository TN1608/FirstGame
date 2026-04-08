using UnityEngine;
using System.Collections.Generic;
using _Scripts.WorldGen;

namespace ProceduralWorld.Generation
{
    /// <summary>
    /// WorldGenerationConfig — Centralized configuration for world generation
    /// Decouples settings from WorldGenerator logic for easier tuning and modification
    /// </summary>
    [CreateAssetMenu(fileName = "WorldGenConfig", menuName = "WorldGen/Config")]
    public class WorldGenerationConfig : ScriptableObject
    {
        #region Base Settings
        [Header("=== BASE SETTINGS ===")]
        public int seed;
        public bool randomSeedEachTime = true;
        [Range(8, 64)] public int chunkSize = 16;
        [Range(1, 16)] public int viewDistance = 6;
        #endregion

        #region Terrain Mesh Settings
        [Header("=== TERRAIN MESH SETTINGS ===")]
        public bool useMeshTerrain = true;
        public float meshHeightMultiplier = 20f;
        public Material terrainMaterial;
        #endregion

        #region Advanced Noise Parameters
        [Header("=== NOISE SYSTEM (FBM) ===")]
        public NoiseConfig primaryNoise;
        
        [Header("=== SHAPING (SPLINES/CURVES) ===")]
        [Tooltip("Continentalness: Large scale land/sea distribution")]
        public AnimationCurve continentalnessCurve = AnimationCurve.Linear(0, 0, 1, 1);
        
        [Tooltip("Erosion: Smooths or sharpens terrain based on another noise layer")]
        public AnimationCurve erosionCurve = AnimationCurve.Linear(0, 0, 1, 1);
        
        [Tooltip("Peaks & Valleys: Adds jaggedness to mountain areas")]
        public AnimationCurve peaksValleysCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("=== 3D NOISE & SPECIAL FEATURES ===")]
        public bool use3DNoiseForOverhangs = false;
        public float densityThreshold = 0.5f;
        public float densityScale = 0.05f;

        public bool usePerlinWorms = false;
        public float wormChance = 0.01f;
        #endregion

        #region Biome & Objects
        [Header("=== BIOMES & OBJECTS ===")]
        public List<BiomeSpawnConfig> biomeConfigs = new();
        public float elevationYOffset = 0.5f;
        #endregion
    }

    [System.Serializable]
    public class NoiseConfig
    {
        [Header("Fractal Brownian Motion")]
        [Tooltip("Base noise frequency. Low=large features, High=detailed. Tip: 0.03-0.08")]
        public float scale = 0.04f;

        [Range(1, 10)]
        [Tooltip("Number of noise octaves. More=rougher terrain. Tip: 4-7")]
        public int octaves = 6;

        [Range(0.1f, 1f)]
        [Tooltip("Amplitude decay per octave. Lower=rougher. Tip: 0.4-0.6")]
        public float persistence = 0.5f;

        [Range(1.0f, 4.0f)]
        [Tooltip("Frequency increase per octave. Tip: 1.8-2.2")]
        public float lacunarity = 2.0f;

        [Range(0.1f, 4.0f)]
        [Tooltip("Height redistribution power. <1=flatten, >1=accentuate. Tip: 0.8-1.2")]
        public float redistributionPower = 1.0f;
        
        [Tooltip("Offset for this specific noise layer")]
        public Vector2 layerOffset;
    }

    [System.Serializable]
    public class BiomeSpawnConfig
    {
        public BiomeType biomeType;
        [Tooltip("Noise range for this biome")]
        public Vector2 noiseRange = new Vector2(0, 1);
        
        [Tooltip("Objects that can spawn in this biome")]
        public List<SpawnableObjectWeight> spawnableObjects = new();

        [Range(0f, 1f)]
        [Tooltip("Overall spawn density in this biome")]
        public float overallDensity = 0.05f;
    }

    [System.Serializable]
    public class SpawnableObjectWeight
    {
        public SpawnableObjectConfig config;
        
        [Range(0.1f, 10f)]
        [Tooltip("Weight for weighted random selection. Higher=more likely")]
        public float weight = 1f;
    }
}