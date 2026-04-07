using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 4f;
    public float stopDistance = 0.1f;

    [Header("Tile Interaction")]
    public Tilemap groundLayer;
    public Tilemap waterLayer;
    public GameObject hoverIndicatorPrefab;   // prefab vòng tròn/diamond nhỏ để hover
    public GameObject clickIndicatorPrefab;   // prefab effect khi click

    [Header("Isometric Settings")]
    public Grid grid;

    private Rigidbody2D rb;
    private Animator animator;

    private Vector2 targetPosition;
    private bool isMoving = false;

    private GameObject hoverIndicator;
    private GameObject clickIndicator;
    private Vector3Int lastHoveredCell = Vector3Int.back;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        // Spawn indicators
        if (hoverIndicatorPrefab != null)
            hoverIndicator = Instantiate(hoverIndicatorPrefab);
        if (clickIndicatorPrefab != null)
            clickIndicator = Instantiate(clickIndicatorPrefab);

        if (hoverIndicator) hoverIndicator.SetActive(false);
        if (clickIndicator) clickIndicator.SetActive(false);
    }

    private void Update()
    {
        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 0));
        mouseWorld.z = 0;

        // === HOVER INDICATOR ===
        if (grid != null)
        {
            Vector3Int cellPos = grid.WorldToCell(mouseWorld);

            if (cellPos != lastHoveredCell)
            {
                lastHoveredCell = cellPos;

                // Lấy center của cell trong world
                Vector3 cellWorldCenter = grid.GetCellCenterWorld(cellPos);
                cellWorldCenter.z = 0.1f; // nhỏ hơn tile để render trên

                // Chỉ hiện hover nếu không phải water
                bool isWater = waterLayer != null && waterLayer.HasTile(cellPos);
                if (hoverIndicator != null)
                {
                    hoverIndicator.SetActive(!isWater);
                    hoverIndicator.transform.position = cellWorldCenter;
                }
            }
        }

        // === LUÔN NHÌN THEO CHUỘT ===
        Vector2 dirToMouse = ((Vector2)mouseWorld - (Vector2)transform.position).normalized;
        animator.SetFloat("MoveX", dirToMouse.x);
        animator.SetFloat("MoveY", dirToMouse.y);

        // === RIGHT CLICK ĐỂ DI CHUYỂN ===
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            Vector3Int cellPos = grid.WorldToCell(mouseWorld);
            bool isWater = waterLayer != null && waterLayer.HasTile(cellPos);

            if (!isWater)
            {
                targetPosition = grid.GetCellCenterWorld(cellPos);
                isMoving = true;

                // Click effect
                if (clickIndicator != null)
                {
                    clickIndicator.SetActive(true);
                    clickIndicator.transform.position = new Vector3(targetPosition.x, targetPosition.y, 0.1f);
                    // Auto tắt sau 0.5s
                    CancelInvoke(nameof(HideClickIndicator));
                    Invoke(nameof(HideClickIndicator), 0.5f);
                }
            }
        }

        // === MOVEMENT ===
        if (isMoving)
        {
            Vector2 dir = (targetPosition - (Vector2)transform.position).normalized;
            rb.linearVelocity = dir * moveSpeed;

            if (Vector2.Distance(transform.position, targetPosition) <= stopDistance)
            {
                rb.linearVelocity = Vector2.zero;
                transform.position = targetPosition; // snap chính xác
                isMoving = false;
            }
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }

        animator.SetFloat("Speed", rb.linearVelocity.magnitude);
    }

    private void HideClickIndicator()
    {
        if (clickIndicator != null) clickIndicator.SetActive(false);
    }
}