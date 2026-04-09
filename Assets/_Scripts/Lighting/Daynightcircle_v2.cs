using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// DayNightCycle v2 — Day/Night cycle manager with sun angle & lighting control
/// 
/// FIX: dayColor đổi thành trắng thuần (1,1,1) để không bị vàng.
///      dawnColor giữ cam nhạt để tạo hiệu ứng bình minh.
///      Nếu vẫn vàng → kiểm tra Global Light 2D Color trong Inspector (phải là White).
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }

    // ═══════════════════════════════════════════════════
    // TIME
    // ═══════════════════════════════════════════════════

    [Header("=== TIME ===")]
    [Tooltip("Full day in seconds. Tip: 300=5min, 600=10min")]
    public float dayLengthInSeconds = 300f;

    [Range(0f, 1f)]
    [Tooltip("0=midnight | 0.25=dawn | 0.5=noon | 0.75=dusk")]
    public float timeOfDay = 0.25f;

    // ═══════════════════════════════════════════════════
    // LIGHTS
    // ═══════════════════════════════════════════════════

    [Header("=== LIGHTS ===")]
    public Light2D globalLight;
    public Light2D sunLight;

    // ═══════════════════════════════════════════════════
    // COLORS  — FIX: dayColor = trắng thuần
    // ═══════════════════════════════════════════════════

    [Header("=== COLORS ===")]
    [Tooltip("Màu ban ngày. Để TRẮNG THUẦN (1,1,1) để không bị tint vàng!")]
    public Color dayColor   = new Color(1.00f, 1.00f, 1.00f);   // ← FIX: đổi từ (1,0.95,0.82) → trắng

    [Tooltip("Màu bình minh/hoàng hôn. Cam nhạt là đẹp nhất.")]
    public Color dawnColor  = new Color(1.00f, 0.75f, 0.50f);   // ← FIX: giảm cam để ít vàng hơn

    [Tooltip("Màu ban đêm. Xanh đậm tạo cảm giác đêm tốt.")]
    public Color nightColor = new Color(0.05f, 0.07f, 0.18f);

    // ═══════════════════════════════════════════════════
    // INTENSITIES
    // ═══════════════════════════════════════════════════

    [Header("=== INTENSITY ===")]
    [Range(0.1f, 1.5f)]
    [Tooltip("Global light ban ngày. Tip: 0.9-1.1")]
    public float dayGlobalIntensity   = 1.0f;

    [Range(0.05f, 0.5f)]
    [Tooltip("Global light ban đêm. Tip: 0.15-0.30")]
    public float nightGlobalIntensity = 0.20f;

    [Range(1.0f, 4.0f)]
    [Tooltip("Sun light peak (noon). Tip: 2.0-3.0")]
    public float sunMaxIntensity = 2.5f;

    // ═══════════════════════════════════════════════════
    // SUN ANGLE
    // ═══════════════════════════════════════════════════

    [Header("=== SUN ANGLE ===")]
    public float sunMinAngle = 25f;
    public float sunMaxAngle = 78f;

    // ═══════════════════════════════════════════════════
    // PUBLIC PROPERTIES
    // ═══════════════════════════════════════════════════

    public float SunAngle  { get; private set; }
    public float DayBlend  { get; private set; }
    public bool  IsDay     => DayBlend > 0.05f;

    private float dawnBlend;

#if UNITY_EDITOR
    [Header("=== DEBUG ===")]
    public bool showDebugLog = false;
#endif

    // ═══════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        timeOfDay += Time.deltaTime / dayLengthInSeconds;
        if (timeOfDay >= 1f) timeOfDay -= 1f;
        UpdateDayNightCycle();
    }

    // ═══════════════════════════════════════════════════
    // CORE LOGIC
    // ═══════════════════════════════════════════════════

    private void UpdateDayNightCycle()
    {
        // Sine wave: 0=midnight, 0.25=dawn, 0.5=noon, 0.75=dusk
        float phaseRad = (timeOfDay - 0.25f) * Mathf.PI * 2f;
        float sineVal  = Mathf.Sin(phaseRad);

        DayBlend  = Mathf.Clamp01((sineVal + 1f) * 0.5f);
        dawnBlend = Mathf.Clamp01(1f - Mathf.Abs(sineVal) * 2f);

        SunAngle = Mathf.Lerp(sunMinAngle, sunMaxAngle, DayBlend);

        UpdateGlobalLight();
        UpdateSunLight();

#if UNITY_EDITOR
        if (showDebugLog)
            Debug.Log($"[DayNightCycle] t={timeOfDay:F3} | DayBlend={DayBlend:F2} | SunAngle={SunAngle:F1}°");
#endif
    }

    private void UpdateGlobalLight()
    {
        if (globalLight == null) return;

        // 3-way blend: night → dawn → day
        Color targetColor;
        if (DayBlend < 0.5f)
            targetColor = Color.Lerp(nightColor, dawnColor, DayBlend * 2f);
        else
            targetColor = Color.Lerp(dawnColor, dayColor, (DayBlend - 0.5f) * 2f);

        globalLight.color     = targetColor;
        globalLight.intensity = Mathf.Lerp(nightGlobalIntensity, dayGlobalIntensity, DayBlend);
    }

    private void UpdateSunLight()
    {
        if (sunLight == null) return;

        float sunFade = Mathf.Clamp01(DayBlend * 2f - 0.1f);
        sunLight.intensity = sunFade * sunMaxIntensity;
        sunLight.transform.localRotation = Quaternion.Euler(0f, 0f, SunAngle);
    }

    // ═══════════════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════════════

    public float GetDayProgress()      => DayBlend;
    public float GetSunAngle()         => SunAngle;
    public void  SetTimeOfDay(float t) => timeOfDay = Mathf.Clamp01(t);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying) UpdateDayNightCycle();
    }
#endif
}