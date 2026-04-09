// ==================== FILENAME: WorldGenerationConfig_FIXED.cs ====================
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

namespace ProceduralWorld.Generation
{
    #region ===== BIOME TILE CONFIGURATION =====
    [System.Serializable]
    public class BiomeTileSetConfig
    {
        [Header("Biome Identification")]
        public string biomeName = "Grass";
        [Range(0f, 1f)] public float minHeight = 0.5f;
        [Range(0f, 1f)] public float maxHeight = 0.75f;

        [Header("Tile Assets")]
        [Tooltip("Primary tile for this biome")]
        public TileBase[] tilesToUse = new TileBase[0];

        [Range(0.5f, 3f)]
        [Tooltip("Visual height offset for this biome")]
        public float visualHeightOffset = 0f;

        public TileBase GetRandomTile()
        {
            if (tilesToUse == null || tilesToUse.Length == 0)
                return null;
            return tilesToUse[Random.Range(0, tilesToUse.Length)];
        }
    }
    #endregion

    #region ===== BASIC NOISE CONFIG =====
    [System.Serializable]
    public class NoiseConfig
    {
        [Range(0.01f, 0.2f)]
        [Tooltip("Base noise frequency (0.04-0.08 recommended)")]
        public float scale = 0.04f;

        [Range(1, 10)]
        [Tooltip("Number of octaves (4-8 recommended)")]
        public int octaves = 6;

        [Range(0.3f, 0.9f)]
        [Tooltip("Amplitude decay per octave (0.4-0.6 recommended)")]
        public float persistence = 0.5f;

        [Range(1.5f, 2.5f)]
        [Tooltip("Frequency increase per octave (1.8-2.2 recommended)")]
        public float lacunarity = 2.0f;

        [Range(0.3f, 2.5f)]
        [Tooltip("Height redistribution (0.8-1.2 recommended)")]
        public float redistributionPower = 1.0f;

        [HideInInspector] public Texture2D previewTexture;
    }
    #endregion

    #region ===== MULTI-LAYER NOISE SYSTEM =====
    [System.Serializable]
    public class LayeredNoiseConfig
    {
        [Header("Continentalness (Land vs Ocean)")]
        public NoiseConfig continentalness = new NoiseConfig { scale = 0.06f, octaves = 5 };
        [Range(0f, 1f)] public float continentalWeight = 0.4f;

        [Header("Erosion (Ridges vs Valleys)")]
        public NoiseConfig erosion = new NoiseConfig { scale = 0.08f, octaves = 5 };
        [Range(0f, 1f)] public float erosionWeight = 0.3f;

        [Header("Peaks & Valleys")]
        public NoiseConfig peaksValleys = new NoiseConfig { scale = 0.05f, octaves = 6 };
        [Range(0f, 1f)] public float peaksValleysWeight = 0.2f;

        [Header("Fine Detail")]
        public NoiseConfig detail = new NoiseConfig { scale = 0.15f, octaves = 3 };
        [Range(0f, 1f)] public float detailWeight = 0.1f;
    }
    #endregion

    #region ===== SPLINE/CURVE SHAPING =====
    [System.Serializable]
    public class SplineShapeConfig
    {
        [Header("Height Curve (Minecraft-style shaping)")]
        public AnimationCurve heightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Continentalness Curve")]
        public AnimationCurve continentalnessCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Erosion Curve")]
        public AnimationCurve erosionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Range(0.1f, 50f)]
        [Tooltip("Multiply height value before applying curve")]
        public float heightMultiplier = 20f;
    }
    #endregion

    #region ===== PERLIN WORMS (RIVERS) =====
    [System.Serializable]
    public class PerlinWormConfig
    {
        [Tooltip("Enable river carving")]
        public bool enabled = false;

        [Range(0.01f, 0.1f)]
        public float wormScale = 0.04f;

        [Range(1, 6)]
        public int wormOctaves = 3;

        [Range(0.1f, 2f)]
        public float wormWidth = 0.5f;

        [Range(0f, 0.5f)]
        public float wormStrength = 0.3f;
    }
    #endregion

    #region ===== 3D DENSITY FIELD (CAVES/OVERHANGS) =====
    [System.Serializable]
    public class DensityFieldConfig
    {
        [Tooltip("Enable 3D noise for cave/overhang generation")]
        public bool enabled = false;

        [Range(0.05f, 0.2f)]
        public float scale3D = 0.1f;

        [Range(1, 6)]
        public int octaves3D = 3;

        [Range(0f, 1f)]
        public float caveThreshold = 0.4f;

        [Range(0f, 1f)]
        public float caveStrength = 0.5f;
    }
    #endregion

    #region ===== HEIGHTMAP TEXTURE SUPPORT =====
    [System.Serializable]
    public class HeightmapTextureConfig
    {
        [Tooltip("If set, blend this texture's R channel with procedural noise")]
        public Texture2D heightmapTexture;

        [Range(0f, 1f)]
        [Tooltip("Blend amount from texture (0=pure procedural, 1=pure texture)")]
        public float textureBlendAmount = 0.3f;

        [Range(1f, 10f)]
        [Tooltip("Scale texture to world coordinates")]
        public float textureScale = 1f;
    }
    #endregion

    #region ===== MAIN WORLD GENERATION CONFIG =====
    [CreateAssetMenu(fileName = "WorldGenConfig", menuName = "WorldGen/World Generation Config")]
    public class WorldGenerationConfig : ScriptableObject
    {
        [Header("=== NOISE SYSTEM ===")]
        public LayeredNoiseConfig layeredNoise = new LayeredNoiseConfig();

        [Header("=== SPLINE SHAPING ===")]
        public SplineShapeConfig splineShape = new SplineShapeConfig();

        [Header("=== PERLIN WORMS (Rivers) ===")]
        public PerlinWormConfig perlinWorms = new PerlinWormConfig();

        [Header("=== 3D DENSITY (Caves) ===")]
        public DensityFieldConfig densityField = new DensityFieldConfig();

        [Header("=== HEIGHTMAP TEXTURE ===")]
        public HeightmapTextureConfig heightmapTexture = new HeightmapTextureConfig();

        [Header("=== WATER & BEACH ===")]
        [Range(0f, 0.5f)]
        [Tooltip("Height threshold for water level")]
        public float waterLevel = 0.38f;

        [Range(0f, 0.1f)]
        [Tooltip("Beach threshold (waterLevel to waterLevel + beachHeight)")]
        public float beachHeight = 0.08f;

        [Header("=== BIOME TILE CONFIGURATION ===")]
        [Tooltip("Configure tiles for each biome - ORDER MATTERS! Water→Path→Brush→Grass→Stone")]
        public List<BiomeTileSetConfig> biomes = new List<BiomeTileSetConfig>();

        [Header("=== MESH SETTINGS ===")]
        [Range(1, 20)]
        [Tooltip("Vertices per side of chunk (higher=smoother but slower)")]
        public int meshResolution = 8;

        [Range(0f, 1f)]
        [Tooltip("Amplitude of vertex displacement from noise")]
        public float meshDisplacement = 0.5f;

        [Header("=== TILEMAP FALLBACK ===")]
        [Tooltip("If true, use tilemaps instead of mesh (for debugging)")]
        public bool useTilemapFallback = true;

        public BiomeTileSetConfig GetBiomeForHeight(float height)
        {
            foreach (var biome in biomes)
            {
                if (height >= biome.minHeight && height <= biome.maxHeight)
                    return biome;
            }
            // Fallback
            return biomes.Count > 0 ? biomes[biomes.Count - 1] : null;
        }
    }
    #endregion
}