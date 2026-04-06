using UnityEngine;

public class SimpleShadow : MonoBehaviour
{
    public Transform shadow;

    [Header("Offset & Stretch")]
    public Vector3 baseOffset = new Vector3(0, -0.28f, 0);
    public float maxShadowStretch = 3.0f;
    public float minScaleY = 0.35f;
    public float maxScaleY = 1.6f;

    private SpriteRenderer shadowRenderer;

    void Awake()
    {
        shadowRenderer = shadow.GetComponent<SpriteRenderer>();

        if (shadowRenderer == null)
        {
            Debug.LogError("Shadow child không có SpriteRenderer!");
            return;
        }

        // Gán lại để chắc chắn
        SpriteRenderer parent = GetComponent<SpriteRenderer>();
        if (parent != null)
            shadowRenderer.sprite = parent.sprite;

        shadowRenderer.material = Resources.Load<Material>("ShadowMaterial"); // nếu không được thì comment dòng này
        shadowRenderer.sortingLayerName = "WorldObjects";
        shadowRenderer.sortingOrder = -10;
        shadowRenderer.color = new Color(0, 0, 0, 0.58f);

        Debug.Log($"Shadow của {gameObject.name} đã được setup thành công");
    }

    void LateUpdate()
    {
        if (shadow == null || DayNightCycle.Instance == null) return;

        float sunAngle = DayNightCycle.Instance.GetSunAngle();
        float stretch = Mathf.Lerp(1f, maxShadowStretch, Mathf.Abs(sunAngle - 55f) / 25f);

        shadow.position = transform.position + baseOffset * stretch;
        shadow.localScale = new Vector3(1f, Mathf.Lerp(minScaleY, maxScaleY, Mathf.Abs(sunAngle - 55f) / 25f), 1f);

        // Nếu bạn muốn giữ Rotation X = 45 thì KHÔNG rotate Z
    }
}