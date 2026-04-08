using UnityEngine;
using UnityEngine.Tilemaps;

namespace _Scripts.WorldGen
{

[CreateAssetMenu(fileName = "SpawnableObjectConfig", menuName = "WorldGen/SpawnableObjectConfig")]
public class SpawnableObjectConfig : ScriptableObject
{
    public string objectName = "Object";
    public GameObject prefab;

    [Range(0f, 1f)] public float spawnChance = 0.1f;
    public TileBase[] allowedTiles;

    public bool useClusterNoise = false;
    public float clusterNoiseScale = 0.1f;
    [Range(0f, 1f)] public float clusterThreshold = 0.5f;

    public float minScale = 1f;
    public float maxScale = 1f;
    public float yOffset = 0f;
    public float positionJitter = 0f;
}

}


