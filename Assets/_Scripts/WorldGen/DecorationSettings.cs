using Mono.Cecil;
using UnityEngine;

/// <summary>
/// Configure decoration spawning rules.
/// Create via: Assets → Create → WorldGen → Decoration Settings
/// Assign to WorldGeneratorSettings or directly to DecorationSpawner.
/// </summary>
[CreateAssetMenu(fileName = "DecorationSettings",
    menuName  = "WorldGen/Decoration Settings")]
public class DecorationSettings : ScriptableObject
{
    [System.Serializable]
    public class DecorationRule
    {
        [Header("Identity")]
        public string ruleName = "Trees";

        [Header("Prefabs (random pick)")]
        [Tooltip("One is picked randomly per spawn point.")]
        public GameObject[] prefabs;

        [Header("Spawn Conditions")]
        [Tooltip("Noise threshold — cell spawns if decorationNoise < this value.")]
        [Range(0f, 1f)]
        public float noiseThreshold = 0.15f;

        [Tooltip("Only spawn on land tiles (not water).")]
        public bool landOnly = true;

        [Tooltip("Minimum tile height to spawn on.")]
        public int minHeight = 0;

        [Tooltip("Maximum tile height to spawn on. 0 = no limit.")]
        public int maxHeight = 0;

        [Header("Placement")]
        [Tooltip("Random XY jitter within cell (0 = centered).")]
        public float jitter = 0.15f;

        [Tooltip("Z sort offset — negative pushes in front of tile.")]
        public float zOffset = -0.01f;

        [Header("Resource Node (optional)")]
        [Tooltip("If true, adds a ResourceNode component automatically.")]
        public bool addResourceNode = true;
        public ResourceType resourceType = ResourceType.Wood;
        public int          hitsRequired = 3;
        public float        respawnTime  = 60f;
        public LootEntry[]  lootTable;
    }

    public DecorationRule[] rules;
}