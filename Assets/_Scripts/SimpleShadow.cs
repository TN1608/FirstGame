using UnityEngine;

public class SimpleShadow : MonoBehaviour
{
    [Tooltip("Kéo child Shadow vào đây")]
    public Transform shadow;

    [Header("Offset & Stretch")]
    public Vector3 baseOffset = new Vector3(0, -0.28f, 0);
    public float maxShadowStretch = 3.2f;
    public float minScaleY = 0.35f;
    public float maxScaleY = 1.6f;

    void LateUpdate()
    {
        if (shadow == null || DayNightCycle.Instance == null) return;

        float sunAngle = DayNightCycle.Instance.GetSunAngle();

        // Tính độ dài bóng theo góc mặt trời
        float stretch = Mathf.Lerp(1f, maxShadowStretch, Mathf.Abs(sunAngle - 55f) / 25f);

        // Position bóng (dưới chân + kéo dài)
        shadow.position = transform.position + baseOffset * stretch;

        // Scale Y để bóng dốc và dài hơn khi mặt trời thấp
        float scaleY = Mathf.Lerp(minScaleY, maxScaleY, Mathf.Abs(sunAngle - 55f) / 25f);
        shadow.localScale = new Vector3(1f, scaleY, 1f);

        // KHÔNG rotate Z (vì bạn đã set Rotation X = 45 trong Prefab)
    }
}