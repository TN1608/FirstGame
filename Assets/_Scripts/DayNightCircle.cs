using UnityEngine;
using UnityEngine.Rendering.Universal;

public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }

    [Header("Time Settings")] [Tooltip("Thời gian 1 ngày tính bằng giây (ví dụ 300 = 5 phút)")]
    public float dayLengthInSeconds = 300f;

    [Range(0f, 1f)] public float timeOfDay = 0f; // 0 = 0h, 0.5 = 12h, 1 = 24h

    [Header("Lights")] public Light2D globalLight; // Global Light 2D (ban ngày)
    public Light2D sunLight; // Freeform Light 2D (Sun)

    [Header("Colors & Intensity")] public Color dayColor = new Color(1f, 0.95f, 0.85f); // nắng ấm
    public Color nightColor = new Color(0.6f, 0.7f, 1f); // ánh trăng lạnh
    public float dayIntensity = 1.2f;
    public float nightIntensity = 0.35f;

    [Header("Sun Angle")] public float sunMinAngle = 30f; // góc thấp nhất (bình minh / hoàng hôn)
    public float sunMaxAngle = 80f; // góc cao nhất (trưa)

    private float timeSpeed;

    void Awake()
    {
        Instance = this;
        timeSpeed = 1f / dayLengthInSeconds;
    }

    void Update()
    {
        timeOfDay += timeSpeed * Time.deltaTime;
        if (timeOfDay >= 1f) timeOfDay = 0f;

        UpdateLights();
    }

    private void UpdateLights()
    {
        float t = Mathf.Sin(timeOfDay * Mathf.PI * 2f); // -1 → 1

        // Global Light (nền)
        float globalIntensity = Mathf.Lerp(nightIntensity, dayIntensity, (t + 1f) / 2f);
        globalLight.intensity = globalIntensity;
        globalLight.color = Color.Lerp(nightColor, dayColor, (t + 1f) / 2f);

        // Sun Light (ánh nắng chính)
        if (sunLight != null)
        {
            float sunIntensity = Mathf.Lerp(0f, 3.2f, Mathf.Max(0f, t)); // chỉ sáng ban ngày
            sunLight.intensity = sunIntensity;

            // Xoay Sun theo thời gian (góc 30° → 80°)
            float sunAngle = Mathf.Lerp(sunMinAngle, sunMaxAngle, (t + 1f) / 2f);
            sunLight.transform.rotation = Quaternion.Euler(0, 0, sunAngle);
        }
    }

    // Public method cho các script khác lấy góc Sun hiện tại
    public float GetSunAngle()
    {
        return sunLight != null ? sunLight.transform.eulerAngles.z : 45f;
    }
}