using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple inventory. Attach to Player GameObject.
/// Stores resource counts. Fire events for UI to listen to.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    [Header("Capacity")]
    public int maxStackSize = 99;

    private readonly Dictionary<ResourceType, int> _items = new();

    public System.Action<ResourceType, int> OnInventoryChanged; // (type, newCount)

    public void AddItem(ResourceType type, int count)
    {
        if (!_items.ContainsKey(type)) _items[type] = 0;
        _items[type] = Mathf.Min(_items[type] + count, maxStackSize);
        Debug.Log($"[Inventory] +{count} {type} → total: {_items[type]}");
        OnInventoryChanged?.Invoke(type, _items[type]);
    }

    public bool RemoveItem(ResourceType type, int count)
    {
        if (!HasItem(type, count)) return false;
        _items[type] -= count;
        OnInventoryChanged?.Invoke(type, _items[type]);
        return true;
    }

    public int  GetCount(ResourceType type) =>
        _items.TryGetValue(type, out int c) ? c : 0;

    public bool HasItem(ResourceType type, int count = 1) =>
        GetCount(type) >= count;

    public Dictionary<ResourceType, int> GetAll() => _items;
}