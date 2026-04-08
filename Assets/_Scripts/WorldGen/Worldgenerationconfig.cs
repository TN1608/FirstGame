using UnityEngine;
using System.Collections.Generic;
using _Scripts.WorldGen;

namespace ProceduralWorld.Generation
{
    /// <summary>
    /// WorldGenerationConfig — Centralized configuration for world generation
    /// Decouples settings from WorldGenerator logic for easier tuning and modification
    /// </summary>
    [System.Serializable]
    public class NoiseConfig
    {
        [Header("Fractal Brownian Motion")]
        [Tooltip("Base noise frequency. Low=large features, High=detailed. Tip: 0.03-0.08")]
        public float scale = 0.04f;

        [Tooltip("Detail noise frequency. Adds variation. Tip: 0.15-0.30")]
        public float detailScale = 0.20f;

        [Range(0f, 0.5f)]
        [Tooltip("Detail strength: 0=disabled, 0.5=strong. Tip: 0.10-0.20")]
        public float detailStrength = 0.15f;

        [Range(1, 10)]
        [Tooltip("Number of noise octaves. More=rougher terrain. Tip: 4-7")]
        public int octaves = 6;

        [Range(0.3f, 0.9f)]
        [Tooltip("Amplitude decay per octave. Lower=rougher. Tip: 0.4-0.6")]
        public float persistence = 0.5f;

        [Range(1.5f, 2.5f)]
        [Tooltip("Frequency increase per octave. Tip: 1.8-2.2")]
        public float lacunarity = 2.0f;

        [Range(0.3f, 2.5f)]
        [Tooltip("Height redistribution power. <1=flatten, >1=accentuate. Tip: 0.8-1.2")]
        public float redistributionPower = 1.0f;
    }

    [System.Serializable]
    public class BiomeNoiseThreshold
    {
        public BiomeType biomeType;
        [Range(0f, 1f)] public float minThreshold = 0f;
        [Range(0f, 1f)] public float maxThreshold = 1f;
    }

    [System.Serializable]
    public class BiomeSpawnConfig
    {
        public BiomeType biomeType;
        
        [Tooltip("Objects that can spawn in this biome")]
        public List<SpawnableObjectWeight> spawnableObjects = new();

        [Range(0f, 0.5f)]
        [Tooltip("Overall spawn density in this biome. Tip: 0.02-0.10")]
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