using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed    = 4f;
    public float acceleration = 10f;
    public float stopDistance = 0.08f;

    [Header("Tile Interaction")]
    public Tilemap    groundLayer;
    public GameObject hoverIndicatorPrefab;
    public GameObject clickIndicatorPrefab;

    [Header("Isometric Settings")]
    public Grid grid;

    [Header("Animation")]
    public float animBlendSpeed = 10f;

    private Rigidbody2D rb;
    private Animator    animator;

    private Vector2    targetPosition;
    private bool       isMoving = false;

    private GameObject hoverIndicator;
    private GameObject clickIndicator;
    private Vector3Int lastHoveredCell = Vector3Int.back;

    private Vector2 smoothAnimDir = Vector2.down;

    private void Awake()
    {
        rb       = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        if (hoverIndicatorPrefab != null)
        {
            hoverIndicator = Instantiate(hoverIndicatorPrefab);
            hoverIndicator.SetActive(false);
        }
        if (clickIndicatorPrefab != null)
        {
            clickIndicator = Instantiate(clickIndicatorPrefab);
            clickIndicator.SetActive(false);
        }
    }

    private void Update()
    {
        if (Camera.main == null) return;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector3 mouseWorld  = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 0f));
        mouseWorld.z = 0f;

        HandleHoverIndicator(mouseWorld);
        HandleClick(mouseWorld);
        HandleMovement();
        UpdateAnimator(mouseWorld);
    }

    // ── Hover Indicator ──────────────────────────────────
    private void HandleHoverIndicator(Vector3 mouseWorld)
    {
        if (grid == null || hoverIndicator == null) return;

        Vector3Int cellPos = grid.WorldToCell(mouseWorld);
        if (cellPos == lastHoveredCell) return;
        lastHoveredCell = cellPos;

        bool hasGround = groundLayer == null || groundLayer.HasTile(cellPos);
        hoverIndicator.SetActive(hasGround);

        if (hasGround)
        {
            Vector3 center = grid.GetCellCenterWorld(cellPos);
            center.z = -0.01f;
            hoverIndicator.transform.position = center;
        }
    }

    // ── Click to Move (Right-click) ───────────────────────
    private void HandleClick(Vector3 mouseWorld)
    {
        if (!Mouse.current.rightButton.wasPressedThisFrame) return;
        if (grid == null) return;

        Vector3Int cellPos = grid.WorldToCell(mouseWorld);

        // Chỉ di chuyển nếu có ground tile tại đó
        if (groundLayer != null && !groundLayer.HasTile(cellPos)) return;

        Vector3 cellCenter = grid.GetCellCenterWorld(cellPos);
        targetPosition     = new Vector2(cellCenter.x, cellCenter.y);
        isMoving           = true;

        if (clickIndicator != null)
        {
            clickIndicator.SetActive(true);
            clickIndicator.transform.position = new Vector3(targetPosition.x, targetPosition.y, -0.01f);
            CancelInvoke(nameof(HideClickIndicator));
            Invoke(nameof(HideClickIndicator), 0.5f);
        }
    }

    // ── Smooth Movement ───────────────────────────────────
    private void HandleMovement()
    {
        if (!isMoving)
        {
            rb.linearVelocity = Vector2.MoveTowards(
                rb.linearVelocity, Vector2.zero,
                acceleration * Time.deltaTime * moveSpeed);
            return;
        }

        float dist = Vector2.Distance(transform.position, targetPosition);
        if (dist <= stopDistance)
        {
            rb.linearVelocity  = Vector2.zero;
            transform.position = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);
            isMoving           = false;
            return;
        }

        Vector2 dir             = (targetPosition - (Vector2)transform.position).normalized;
        Vector2 desiredVelocity = dir * moveSpeed;
        rb.linearVelocity = Vector2.MoveTowards(
            rb.linearVelocity, desiredVelocity,
            acceleration * Time.deltaTime * moveSpeed);
    }

    // ── Animator ──────────────────────────────────────────
    private void UpdateAnimator(Vector3 mouseWorld)
    {
        if (animator == null) return;

        Vector2 dirToMouse = ((Vector2)mouseWorld - (Vector2)transform.position).normalized;
        smoothAnimDir = Vector2.Lerp(smoothAnimDir, dirToMouse, animBlendSpeed * Time.deltaTime);

        animator.SetFloat("MoveX", smoothAnimDir.x);
        animator.SetFloat("MoveY", smoothAnimDir.y);
        animator.SetFloat("Speed", rb.linearVelocity.magnitude);
    }

    private void HideClickIndicator()
    {
        if (clickIndicator != null) clickIndicator.SetActive(false);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!isMoving) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, targetPosition);
        Gizmos.DrawWireSphere(targetPosition, stopDistance);
    }
#endif
}