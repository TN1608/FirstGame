using UnityEngine;

[CreateAssetMenu(fileName = "NoiseSettings", menuName = "WorldGen/Noise Settings")]
public class NoiseSettings : ScriptableObject
{
    public enum NoiseType { Perlin, OpenSimplex2 }

    [Header("Noise Type")]
    public NoiseType noiseType = NoiseType.Perlin;

    [Header("Base Settings")]
    public int seed = 1337;
    public float frequency = 0.019f;

    [Header("Octaves (FBM)")]
    [Range(1, 8)] public int octaves = 4;
    [Range(0.1f, 1f)] public float persistence = 0.5f;   // amplitude falloff per octave
    [Range(1f, 4f)] public float lacunarity = 2f;         // frequency multiplier per octave

    [Header("Output")]
    [Range(0.1f, 5f)] public float heightMultiplier = 1f;
    public AnimationCurve heightCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [HideInInspector] public int offsetX;
    [HideInInspector] public int offsetY;

    public void RandomizeOffset()
    {
        var rng = new System.Random(seed);
        offsetX = rng.Next(-100000, 100000);
        offsetY = rng.Next(-100000, 100000);
    }
}
