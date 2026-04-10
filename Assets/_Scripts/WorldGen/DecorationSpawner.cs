using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Spawns decoration prefabs (trees, rocks, etc.) after terrain is generated.
/// Attach to Grid GameObject alongside ChunkManager.
/// Called from ChunkManager after each chunk finishes generating.
/// </summary>
public class DecorationSpawner : MonoBehaviour
{
    [Header("References")]
    public WorldGeneratorSettings settings;

    [Header("Parent for spawned objects")]
    [Tooltip("Optional: parent transform to keep hierarchy clean")]
    public Transform decorationRoot;

    private Grid _grid;

    void Awake()
    {
        _grid = GetComponent<Grid>();
        if (decorationRoot == null)
        {
            var go = new GameObject("DecorationRoot");
            go.transform.SetParent(transform);
            decorationRoot = go.transform;
        }
    }

    /// <summary>
    /// Spawns decorations for a fully-generated chunk.
    /// Call this from ChunkManager after TilemapPainter.Paint().
    /// </summary>
    public void SpawnForChunk(ChunkData chunk)
    {
        if (settings?.biomeSettings == null) return;

        Vector2Int origin = chunk.WorldOrigin;
        int size          = chunk.chunkSize;

        for (int lx = 0; lx < size; lx++)
        for (int ly = 0; ly < size; ly++)
        {
            if (chunk.isWater[lx, ly]) continue;

            var biome = chunk.biomeMap[lx, ly];
            if (biome == null || biome.decorationPrefabs == null
                || biome.decorationPrefabs.Length == 0) continue;

            float decoNoise = chunk.decorationNoiseMap[lx, ly];
            if (decoNoise >= biome.decorationDensity) continue;

            // Pick a random prefab from the biome's list
            int idx = Random.Range(0, biome.decorationPrefabs.Length);
            var prefab = biome.decorationPrefabs[idx];
            if (prefab == null) continue;

            int h = chunk.tileHeights[lx, ly];
            int wx = origin.x + lx;
            int wy = origin.y + ly;

            // Convert tile cell to world position
            Vector3 cellCenter = _grid != null
                ? _grid.GetCellCenterWorld(new Vector3Int(wx, wy, 0))
                : new Vector3(wx, wy, 0f);

            // Offset Z to sit on top of terrain
            cellCenter.z = h * -0.01f;   // small Z offset for sorting

            var instance = Instantiate(prefab, cellCenter, Quaternion.identity, decorationRoot);
            instance.name = $"{prefab.name}_{wx}_{wy}";
        }
    }

    /// <summary>Destroys all spawned decoration objects.</summary>
    public void ClearAll()
    {
        if (decorationRoot == null) return;
        for (int i = decorationRoot.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(decorationRoot.GetChild(i).gameObject);
            else
#endif
                Destroy(decorationRoot.GetChild(i).gameObject);
        }
    }
}