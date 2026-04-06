using UnityEngine;
using UnityEngine.Rendering.Universal;

public class LightDayNightControl : MonoBehaviour
{
    public Light2D myLight;
    public float dayIntensity = 0f;      // ban ngày tắt hoặc rất mờ
    public float nightIntensity = 2.5f;

    void Update()
    {
        if (myLight == null || DayNightCycle.Instance == null) return;

        float t = Mathf.Sin(DayNightCycle.Instance.timeOfDay * Mathf.PI * 2f);
        myLight.intensity = Mathf.Lerp(dayIntensity, nightIntensity, Mathf.Max(0f, -t)); // chỉ sáng mạnh ban đêm
    }
}