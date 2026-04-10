using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to any decoration prefab (treelog, rock, etc.) to make it harvestable.
/// Player presses E or hits it to harvest — drops loot, plays feedback, respawns.
///
/// Works with PlayerController's IInteractable interface AND with a separate
/// HarvestHit() call for "minecraft-style" repeated hitting.
/// </summary>
public class ResourceNode : MonoBehaviour, IInteractable
{
    [Header("Resource Info")]
    public ResourceType resourceType = ResourceType.Wood;
    public string       displayName  = "Log";

    [Header("Harvesting")]
    [Tooltip("Total hits needed to harvest. 1 = instant on interact.")]
    public int   hitsRequired    = 3;
    [Tooltip("Seconds before this node respawns. 0 = never.")]
    public float respawnTime     = 30f;

    [Header("Loot")]
    public LootEntry[] lootTable;

    [Header("Feedback")]
    public GameObject  hitParticlePrefab;
    public GameObject  harvestParticlePrefab;
    public AudioClip   hitSound;
    public AudioClip   harvestSound;
    [Tooltip("Shake intensity when hit")]
    public float       shakeAmount = 0.08f;
    public float       shakeDuration = 0.15f;

    [Header("Visual")]
    public SpriteRenderer spriteRenderer;
    [Tooltip("Tint when low health")]
    public Color damagedTint = new Color(0.7f, 0.5f, 0.5f);

    // ── State ──────────────────────────────────────────────────────────────
    private int           _hitsLeft;
    private bool          _harvested;
    private Vector3       _originPos;
    private AudioSource   _audio;

    // ── Events ─────────────────────────────────────────────────────────────
    public System.Action<ResourceNode, int> OnHit;       // (node, hitsRemaining)
    public System.Action<ResourceNode>      OnHarvested; // (node)

    // ── Lifecycle ──────────────────────────────────────────────────────────

    void Awake()
    {
        _hitsLeft  = hitsRequired;
        _originPos = transform.position;
        _audio     = GetComponent<AudioSource>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    // ── IInteractable ──────────────────────────────────────────────────────

    /// <summary>
    /// Called by PlayerController when player presses E or Left-clicks.
    /// Single interact = one hit.
    /// </summary>
    public void Interact(GameObject instigator)
    {
        if (_harvested) return;
        HarvestHit(1, instigator);
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Apply <c>damage</c> hits (tool damage, e.g. axe=2, fist=1).
    /// </summary>
    public void HarvestHit(int damage, GameObject instigator = null)
    {
        if (_harvested) return;

        _hitsLeft -= damage;
        _hitsLeft  = Mathf.Max(0, _hitsLeft);

        PlayHitFeedback();
        StartCoroutine(ShakeRoutine());
        OnHit?.Invoke(this, _hitsLeft);

        // Update visual damage indication
        if (spriteRenderer != null)
        {
            float t = 1f - (float)_hitsLeft / hitsRequired;
            spriteRenderer.color = Color.Lerp(Color.white, damagedTint, t);
        }

        if (_hitsLeft <= 0)
            CompleteHarvest(instigator);
    }

    // ── Internal ───────────────────────────────────────────────────────────

    void CompleteHarvest(GameObject instigator)
    {
        _harvested = true;

        // Spawn loot items
        foreach (var entry in lootTable)
        {
            if (entry.prefab == null) continue;
            int count = Random.Range(entry.minCount, entry.maxCount + 1);
            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPos = transform.position
                    + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.1f, 0.4f), 0f);
                var drop = Instantiate(entry.prefab, spawnPos, Quaternion.identity);

                // Pop animation
                var rb2d = drop.GetComponent<Rigidbody2D>();
                if (rb2d != null)
                    rb2d.linearVelocity = new Vector2(Random.Range(-2f, 2f), Random.Range(2f, 4f));
            }
        }

        if (harvestParticlePrefab != null)
            Instantiate(harvestParticlePrefab, transform.position, Quaternion.identity);

        if (_audio != null && harvestSound != null)
            _audio.PlayOneShot(harvestSound);

        OnHarvested?.Invoke(this);

        // Notify inventory if instigator has one
        if (instigator != null)
        {
            var inv = instigator.GetComponent<PlayerInventory>();
            if (inv != null)
                foreach (var entry in lootTable)
                    inv.AddItem(resourceType, Random.Range(entry.minCount, entry.maxCount + 1));
        }

        // Hide node
        gameObject.SetActive(false);

        if (respawnTime > 0f)
            StartCoroutine(RespawnRoutine());
        else
            Destroy(gameObject, 0.1f);
    }

    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnTime);
        _hitsLeft  = hitsRequired;
        _harvested = false;
        transform.position = _originPos;
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
        gameObject.SetActive(true);
    }

    IEnumerator ShakeRoutine()
    {
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-shakeAmount, shakeAmount);
            float y = Random.Range(-shakeAmount, shakeAmount);
            transform.position = _originPos + new Vector3(x, y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = _originPos;
    }

    void PlayHitFeedback()
    {
        if (hitParticlePrefab != null)
            Instantiate(hitParticlePrefab, transform.position, Quaternion.identity);
        if (_audio != null && hitSound != null)
            _audio.PlayOneShot(hitSound);
    }
}

// ── Supporting types ──────────────────────────────────────────────────────

public enum ResourceType
{
    Wood,
    Stone,
    Ore,
    Fiber,
    Food
}

[System.Serializable]
public class LootEntry
{
    public GameObject prefab;
    public int        minCount = 1;
    public int        maxCount = 3;
}