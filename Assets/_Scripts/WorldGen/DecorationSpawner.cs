using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns decoration prefabs (trees, rocks, logs) on generated terrain.
/// Attach to Grid GameObject alongside ChunkManager.
///
/// Call SpawnForChunk() from ChunkManager after TilemapPainter.Paint().
/// Call DespawnChunk() when a chunk is unloaded.
///
/// Prefab positioning:
///   World XY = Grid.CellToWorld(cellPos) + jitter
///   Z = small negative offset so decoration sorts in front of its tile
/// </summary>
public class DecorationSpawner : MonoBehaviour
{
    [Header("Settings")]
    public DecorationSettings decorationSettings;
    public WorldGeneratorSettings worldSettings;

    [Header("Root")]
    [Tooltip("Parent for spawned objects — keeps hierarchy clean.")]
    public Transform decorationRoot;

    // chunk coord → list of spawned GameObjects for that chunk
    private readonly Dictionary<Vector2Int, List<GameObject>> _chunkObjects = new();

    private Grid _grid;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    void Awake()
    {
        _grid = GetComponent<Grid>();

        if (decorationRoot == null)
        {
            var go = new GameObject("DecorationRoot");
            go.transform.SetParent(transform, false);
            decorationRoot = go.transform;
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Spawn all decorations for a freshly-generated chunk.</summary>
    public void SpawnForChunk(ChunkData chunk)
    {
        if (decorationSettings == null) return;

        Vector2Int coord  = chunk.chunkCoord;
        if (_chunkObjects.ContainsKey(coord)) return; // already spawned

        var spawnedList = new List<GameObject>();
        _chunkObjects[coord] = spawnedList;

        Vector2Int origin = chunk.WorldOrigin;
        int        size   = chunk.chunkSize;

        for (int lx = 0; lx < size; lx++)
        for (int ly = 0; ly < size; ly++)
        {
            if (chunk.isWater[lx, ly]) continue;

            int   h        = chunk.tileHeights[lx, ly];
            float decoNoise = chunk.decorationNoiseMap[lx, ly];
            int   wx       = origin.x + lx;
            int   wy       = origin.y + ly;

            foreach (var rule in decorationSettings.rules)
            {
                if (rule.prefabs == null || rule.prefabs.Length == 0) continue;
                if (decoNoise >= rule.noiseThreshold)                 continue;
                if (rule.landOnly && chunk.isWater[lx, ly])           continue;
                if (h < rule.minHeight)                               continue;
                if (rule.maxHeight > 0 && h > rule.maxHeight)         continue;

                // Pick random prefab
                int idx    = Random.Range(0, rule.prefabs.Length);
                var prefab = rule.prefabs[idx];
                if (prefab == null) continue;

                // World position
                Vector3 cellWorld = _grid != null
                    ? _grid.GetCellCenterWorld(new Vector3Int(wx, wy, h))
                    : new Vector3(wx, wy * 0.5f, 0f);

                // Apply jitter and Z offset
                Vector3 spawnPos = cellWorld
                    + new Vector3(
                        Random.Range(-rule.jitter, rule.jitter),
                        Random.Range(-rule.jitter, rule.jitter),
                        rule.zOffset);

                var instance = Instantiate(prefab, spawnPos, Quaternion.identity, decorationRoot);
                instance.name = $"{rule.ruleName}_{wx}_{wy}";

                // Auto-add ResourceNode if configured
                if (rule.addResourceNode && instance.GetComponent<ResourceNode>() == null)
                {
                    var node             = instance.AddComponent<ResourceNode>();
                    node.resourceType    = rule.resourceType;
                    node.hitsRequired    = rule.hitsRequired;
                    node.respawnTime     = rule.respawnTime;
                    node.lootTable       = rule.lootTable;
                    node.displayName     = rule.ruleName;
                }

                spawnedList.Add(instance);

                // Only one rule per cell — take the first matching
                break;
            }
        }
    }

    /// <summary>Despawn all decorations for a chunk being unloaded.</summary>
    public void DespawnChunk(Vector2Int chunkCoord)
    {
        if (!_chunkObjects.TryGetValue(chunkCoord, out var list)) return;

        foreach (var go in list)
        {
            if (go != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(go);
                else
#endif
                Destroy(go);
            }
        }

        _chunkObjects.Remove(chunkCoord);
    }

    /// <summary>Despawn everything.</summary>
    public void ClearAll()
    {
        var coords = new List<Vector2Int>(_chunkObjects.Keys);
        foreach (var c in coords) DespawnChunk(c);
    }
}