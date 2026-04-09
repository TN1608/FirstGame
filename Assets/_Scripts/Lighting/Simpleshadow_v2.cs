using UnityEngine;

/// <summary>
/// SimpleShadow v2 — Fake isometric shadow system (iso-core style)
/// 
/// ╔════════════════════════════════════════════════════════════╗
/// ║  SETUP PREFAB STRUCTURE                                    ║
/// ╠════════════════════════════════════════════════════════════╣
/// ║  [ObjectRoot]                  (has this script)            ║
/// ║  ├─ Sprite                     (SpriteRenderer — object)    ║
/// ║  └─ Shadow                     (SpriteRenderer — shadow)    ║
/// ║       • Sprite: shadow sprite or duplicate of object       ║
/// ║       • Color: Black (0,0,0) Alpha: 0.4-0.6                ║
/// ║       • Sorting Layer: same as object                      ║
/// ║       • Order in Layer: -1 (behind object)                 ║
/// ║       • Rotation: X=45° (isometric flat effect) ❌ CRITICAL║
/// ║       • Position: (0, -0.2, 0) relative to root            ║
/// ╚════════════════════════════════════════════════════════════╝
/// 
/// HOW IT WORKS:
///   1. Read sun angle from DayNightCycle (0-90°, where 45° = noon)
///   2. Calculate shadow direction (opposite to sun)
///   3. Scale shadow length: Sun high → short | Sun low → long
///   4. Position shadow under object with directional offset
///   5. Rotate shadow Z-axis to match sun direction
///   6. Fade alpha at night using DayBlend
/// </summary>
public class SimpleShadow : MonoBehaviour
{
    // ── References ───────────────────────────────────────────
    [SerializeField]
    [Tooltip("Child Shadow SpriteRenderer. MUST have Rotation X=45° in prefab.")]
    private Transform shadowTransform;

    private SpriteRenderer shadowRenderer;
    private Color shadowBaseColor;

    // ── Shadow Positioning ───────────────────────────────────
    [Header("Shadow Offset")]
    [SerializeField]
    [Tooltip("Base position offset from root (under feet). Tip: (0, -0.2, 0)")]
    private Vector2 baseOffset = new Vector2(0f, -0.20f);

    // ── Shadow Length (stretching) ───────────────────────────
    [Header("Shadow Length")]
    [SerializeField]
    [Range(0.3f, 1.0f)]
    [Tooltip("Length when sun is highest (noon). Tip: 0.4-0.6")]
    private float minShadowLength = 0.5f;

    [SerializeField]
    [Range(1.0f, 3.0f)]
    [Tooltip("Length when sun is lowest (dawn/dusk). Tip: 1.8-2.5")]
    private float maxShadowLength = 2.0f;

    // ── Shadow Alpha (visibility) ────────────────────────────
    [Header("Shadow Visibility")]
    [SerializeField]
    [Range(0.2f, 0.6f)]
    [Tooltip("Alpha during daytime")]
    private float dayAlpha = 0.38f;

    [SerializeField]
    [Range(0.02f, 0.15f)]
    [Tooltip("Alpha during nighttime")]
    private float nightAlpha = 0.08f;

    // ── Sun Angle Reference ──────────────────────────────────
    [Header("Sun Angle Config")]
    [SerializeField]
    [Tooltip("Sun angle at horizon (dawn/dusk). Tip: 20-30")]
    private float sunMinAngle = 25f;

    [SerializeField]
    [Tooltip("Sun angle at zenith (noon). Tip: 75-85")]
    private float sunMaxAngle = 80f;

    // ── Debug ────────────────────────────────────────────────
    [Header("Debug")]
    [SerializeField]
    private bool showShadowDebug = false;

    void Start()
    {
        if (shadowTransform == null)
        {
            Debug.LogError($"[SimpleShadow] Shadow transform not assigned on {gameObject.name}");
            enabled = false;
            return;
        }

        shadowRenderer = shadowTransform.GetComponent<SpriteRenderer>();
        if (shadowRenderer != null)
            shadowBaseColor = shadowRenderer.color;
    }

    void LateUpdate()
    {
        if (shadowTransform == null || DayNightCycle.Instance == null)
            return;

        float sunAngle = DayNightCycle.Instance.SunAngle;
        float dayBlend = DayNightCycle.Instance.DayBlend;

        UpdateShadowPosition(sunAngle);
        UpdateShadowScale(sunAngle);
        UpdateShadowRotation(sunAngle);
        UpdateShadowAlpha(dayBlend);

        if (showShadowDebug)
            DebugLog(sunAngle, dayBlend);
    }

    /// <summary>
    /// Position shadow under object, offset by sun direction.
    /// </summary>
    private void UpdateShadowPosition(float sunAngle)
    {
        float angleNorm = Mathf.InverseLerp(sunMinAngle, sunMaxAngle, sunAngle);
        float shadowLength = Mathf.Lerp(maxShadowLength, minShadowLength, angleNorm);

        // Shadow direction: opposite to sun (sun + 180°)
        float shadowDir = sunAngle + 180f;
        float rad = shadowDir * Mathf.Deg2Rad;

        // Isometric squish: reduce Y component for perspective effect
        Vector2 offsetDir = new Vector2(
            Mathf.Cos(rad),
            Mathf.Sin(rad) * 0.5f  // 0.5 = isometric squish ratio
        );

        Vector3 finalOffset = (Vector3)(baseOffset + offsetDir * shadowLength * 0.35f);
        shadowTransform.localPosition = finalOffset;
    }

    /// <summary>
    /// Scale shadow: Sun high → short&compact | Sun low → long&stretched
    /// </summary>
    private void UpdateShadowScale(float sunAngle)
    {
        float angleNorm = Mathf.InverseLerp(sunMinAngle, sunMaxAngle, sunAngle);
        float shadowLength = Mathf.Lerp(maxShadowLength, minShadowLength, angleNorm);

        // Keep X scale at 1.0 (width), only stretch Y (length)
        shadowTransform.localScale = new Vector3(1f, shadowLength, 1f);
    }

    /// <summary>
    /// Rotate shadow Z-axis to point in sun direction.
    /// X rotation stays at 45° (from prefab) for isometric effect.
    /// </summary>
    private void UpdateShadowRotation(float sunAngle)
    {
        // Shadow rotates opposite to sun
        float shadowRotZ = sunAngle + 180f - 90f;  // -90° offset for visual alignment

        // Preserve X rotation (45° isometric), rotate only Z
        shadowTransform.localRotation = Quaternion.Euler(45f, 0f, shadowRotZ);
    }

    /// <summary>
    /// Fade shadow alpha: night → dim, day → visible
    /// </summary>
    private void UpdateShadowAlpha(float dayBlend)
    {
        if (shadowRenderer == null)
            return;

        float targetAlpha = Mathf.Lerp(nightAlpha, dayAlpha, dayBlend);
        shadowBaseColor.a = targetAlpha;
        shadowRenderer.color = shadowBaseColor;
    }

    // ── Debug & Gizmo ────────────────────────────────────────
    private void DebugLog(float sunAngle, float dayBlend)
    {
        float angleNorm = Mathf.InverseLerp(sunMinAngle, sunMaxAngle, sunAngle);
        float shadowLen = Mathf.Lerp(maxShadowLength, minShadowLength, angleNorm);
        Debug.Log($"[SimpleShadow] Sun: {sunAngle:F1}° | " +
                 $"Shadow Len: {shadowLen:F2} | Day%: {dayBlend:F2}");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (shadowTransform == null) return;
        Gizmos.color = new Color(0, 0, 0, 0.5f);
        Gizmos.DrawWireSphere(shadowTransform.position, 0.2f);
        Gizmos.color = new Color(1, 1, 0, 0.5f);
        Gizmos.DrawLine(transform.position, shadowTransform.position);
    }
#endif
}