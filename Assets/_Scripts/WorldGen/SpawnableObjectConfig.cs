// ==================== FILENAME: SpawnableObjectConfig.cs ====================
using UnityEngine;

namespace _Scripts.WorldGen
{
    /// <summary>
    /// Spawnable Object Configuration
    /// Defines how and where objects (trees, rocks, etc) spawn in the world
    /// </summary>
    [CreateAssetMenu(fileName = "SpawnableObject", menuName = "WorldGen/Spawnable Object Config")]
    public class SpawnableObjectConfig : ScriptableObject
    {
        [Header("=== BASIC ===")]
        public string objectName = "Tree";
        [Tooltip("Prefab to spawn")]
        public GameObject prefab;

        [Header("=== SPAWN SETTINGS ===")]
        [Range(0f, 1f)]
        [Tooltip("Probability of spawning at each valid location (0-1)")]
        public float spawnChance = 0.05f;

        [Header("=== SCALE ===")]
        [Range(0.5f, 3f)]
        [Tooltip("Minimum random scale")]
        public float minScale = 0.8f;

        [Range(0.5f, 3f)]
        [Tooltip("Maximum random scale")]
        public float maxScale = 1.2f;

        [Header("=== POSITIONING ===")]
        [Tooltip("Y offset when spawning")]
        public float yOffset = 0f;

        [Range(0f, 0.5f)]
        [Tooltip("Random position jitter")]
        public float positionJitter = 0.1f;

        [Header("=== CLUSTERING (OPTIONAL) ===")]
        [Tooltip("Enable cluster noise for natural grouping")]
        public bool useClusterNoise = false;

        [Range(0.05f, 0.3f)]
        [Tooltip("Scale of cluster noise")]
        public float clusterNoiseScale = 0.15f;

        [Range(0f, 1f)]
        [Tooltip("Threshold for clustering")]
        public float clusterThreshold = 0.55f;
    }
}