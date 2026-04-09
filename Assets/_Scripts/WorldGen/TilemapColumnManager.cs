using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Only needed for the STACKED_TILEMAPS approach.
/// Dynamically creates/reuses one Tilemap per height level.
/// Each tilemap gets an Order In Layer derived from its level so that
/// higher levels render on top (since all share the same XY plane).
/// </summary>
public class TilemapColumnManager : MonoBehaviour
{
    [Tooltip("Sorting layer name used by all tilemaps")]
    public string sortingLayerName = "Default";

    [Tooltip("Base order for level 0. Each level adds 1.")]
    public int baseOrder = 0;

    private readonly Dictionary<int, Tilemap> _levels = new();
    private Grid _grid;

    void Awake() => _grid = GetComponent<Grid>();

    /// <summary>
    /// Get or create the Tilemap for a given height level.
    /// </summary>
    public Tilemap GetOrCreate(int level)
    {
        if (_levels.TryGetValue(level, out var existing)) return existing;

        var go = new GameObject($"TerrainLevel_{level:000}");
        go.transform.SetParent(transform, worldPositionStays: false);

        var tm = go.AddComponent<Tilemap>();
        var tr = go.AddComponent<TilemapRenderer>();
        tr.sortingLayerName = sortingLayerName;
        tr.sortingOrder     = baseOrder + level;
        tr.mode             = TilemapRenderer.Mode.Chunk;

        _levels[level] = tm;
        return tm;
    }

    public void ClearAll()
    {
        foreach (var tm in _levels.Values)
            tm.ClearAllTiles();
    }

    public void DestroyAll()
    {
        foreach (var tm in _levels.Values)
            if (tm != null) DestroyImmediate(tm.gameObject);
        _levels.Clear();
    }
}
