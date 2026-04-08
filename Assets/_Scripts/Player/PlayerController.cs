using _Scripts.WorldGen;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

/// <summary>
/// PlayerController v2 — Auto-jump for multi-level isometric terrain
/// 
/// KEY FEATURES:
///   • Smooth movement with acceleration/deceleration
///   • Auto-jump when moving to higher elevation
///   • Height-aware pathfinding (won't move through walls)
///   • Dynamic Y-sorting for isometric perspective
///   • Tile interaction (hover/click indicators)
/// 
/// SETUP:
///   1. Attach to Player GameObject
///   2. Assign WorldGenerator reference
///   3. Configure moveSpeed, jumpForce
///   4. Ensure Rigidbody2D is present
/// </summary>
public class PlayerController : MonoBehaviour
{
    // ═════════════════════════════════════════════════════════════
    // REFERENCES
    // ═════════════════════════════════════════════════════════════

    [Header("=== REFERENCES ===")]
    [SerializeField] private WorldGenerator worldGenerator;
    [SerializeField] private Tilemap groundLayer;
    [SerializeField] private Grid grid;
    [SerializeField] private GameObject hoverIndicatorPrefab;
    [SerializeField] private GameObject clickIndicatorPrefab;

    // ═════════════════════════════════════════════════════════════
    // MOVEMENT
    // ═════════════════════════════════════════════════════════════

    [Header("=== MOVEMENT ===")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float stopDistance = 0.1f;
    [SerializeField] private float animBlendSpeed = 10f;

    // ═════════════════════════════════════════════════════════════
    // AUTO-JUMP (Multi-level terrain)
    // ═════════════════════════════════════════════════════════════

    [Header("=== AUTO-JUMP (3D Terrain) ===")]
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float jumpMinHeight = 0.3f;  // Minimum height to auto-jump
    [SerializeField] private float maxJumpDistance = 2f;  // Max horizontal distance for auto-jump
    [SerializeField] private bool enableAutoJump = true;

    // ═════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ═════════════════════════════════════════════════════════════

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private Vector2 targetPosition;
    private bool isMoving = false;
    private int currentElevation = 0;
    private float currentHeight = 0f;

    private GameObject hoverIndicator;
    private GameObject clickIndicator;
    private Vector3Int lastHoveredCell = Vector3Int.back;

    private Vector2 smoothAnimDir = Vector2.down;

    // ═════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═════════════════════════════════════════════════════════════

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

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

        // Auto-find WorldGenerator if not assigned
        if (worldGenerator == null)
            worldGenerator = FindObjectOfType<WorldGenerator>();
    }

    void Update()
    {
        if (Camera.main == null) return;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(
            new Vector3(mouseScreen.x, mouseScreen.y, 0f));
        mouseWorld.z = 0f;

        HandleHoverIndicator(mouseWorld);
        HandleClick(mouseWorld);
        HandleMovement();
        UpdateAnimator(mouseWorld);
        UpdateYSorting();
    }

    // ═════════════════════════════════════════════════════════════
    // HOVER INDICATOR
    // ═════════════════════════════════════════════════════════════

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

            // Add elevation offset if available
            if (worldGenerator != null)
            {
                int elevation = worldGenerator.GetElevationAt(center);
                center.y = worldGenerator.GetHeightAdjustedY(center, elevation);
            }

            center.z = -0.01f;
            hoverIndicator.transform.position = center;
        }
    }

    // ═════════════════════════════════════════════════════════════
    // CLICK TO MOVE
    // ═════════════════════════════════════════════════════════════

    private void HandleClick(Vector3 mouseWorld)
    {
        if (!Mouse.current.rightButton.wasPressedThisFrame) return;
        if (grid == null || worldGenerator == null) return;

        Vector3Int cellPos = grid.WorldToCell(mouseWorld);

        // Check if ground exists
        if (groundLayer != null && !groundLayer.HasTile(cellPos)) return;

        Vector3 cellCenter = grid.GetCellCenterWorld(cellPos);
        int targetElevation = worldGenerator.GetElevationAt(cellCenter);

        // Check if movement is possible
        if (!CanMoveTo(cellCenter, targetElevation))
        {
            Debug.Log("[PlayerController] Cannot move there (too high)");
            return;
        }

        // Adjust target Y based on elevation
        cellCenter.y = worldGenerator.GetHeightAdjustedY(cellCenter, targetElevation);

        targetPosition = new Vector2(cellCenter.x, cellCenter.y);
        isMoving = true;

        if (clickIndicator != null)
        {
            clickIndicator.SetActive(true);
            clickIndicator.transform.position = new Vector3(targetPosition.x, targetPosition.y, -0.01f);
            CancelInvoke(nameof(HideClickIndicator));
            Invoke(nameof(HideClickIndicator), 0.5f);
        }
    }

    // ═════════════════════════════════════════════════════════════
    // HEIGHT-AWARE PATHFINDING
    // ═════════════════════════════════════════════════════════════

    private bool CanMoveTo(Vector3 targetPos, int targetElevation)
    {
        // Can't move up more than 1 level (will auto-jump)
        int elevationDifference = targetElevation - currentElevation;

        if (elevationDifference > 1)
        {
            // Too high to jump
            return false;
        }

        if (elevationDifference < -2)
        {
            // Too far down (cliff)
            return false;
        }

        return true;
    }

    // ═════════════════════════════════════════════════════════════
    // MOVEMENT & AUTO-JUMP
    // ═════════════════════════════════════════════════════════════

    private void HandleMovement()
    {
        if (!isMoving)
        {
            // Decelerate to stop
            rb.linearVelocity = Vector2.MoveTowards(
                rb.linearVelocity, Vector2.zero,
                acceleration * Time.deltaTime * moveSpeed);
            return;
        }

        float dist = Vector2.Distance(transform.position, targetPosition);

        if (dist <= stopDistance)
        {
            rb.linearVelocity = Vector2.zero;
            transform.position = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);
            isMoving = false;

            // Update current elevation
            currentElevation = worldGenerator.GetElevationAt(transform.position);
            currentHeight = currentElevation * 0.5f; // Assuming 0.5 units per level
            return;
        }

        // Calculate direction to target
        Vector2 dir = (targetPosition - (Vector2)transform.position).normalized;
        Vector2 desiredVelocity = dir * moveSpeed;

        // Check if we need to jump
        int targetElevation = worldGenerator.GetElevationAt(targetPosition);
        if (enableAutoJump && targetElevation > currentElevation)
        {
            AttemptAutoJump(targetElevation);
        }

        // Move horizontally
        rb.linearVelocity = Vector2.MoveTowards(
            rb.linearVelocity, desiredVelocity,
            acceleration * Time.deltaTime * moveSpeed);
    }

    private void AttemptAutoJump(int targetElevation)
    {
        // Only auto-jump if moving up one level
        if (targetElevation == currentElevation + 1)
        {
            float jumpHeight = targetElevation * 0.5f - currentHeight;

            // Calculate jump force needed
            if (rb.linearVelocity.y <= 0) // Only jump if not already jumping
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                currentHeight += jumpHeight;
                currentElevation = targetElevation;

                if (animator != null)
                    animator.SetTrigger("Jump");
            }
        }
    }

    // ═════════════════════════════════════════════════════════════
    // ANIMATION
    // ═════════════════════════════════════════════════════════════

    private void UpdateAnimator(Vector3 mouseWorld)
    {
        if (animator == null) return;

        Vector2 dirToMouse = ((Vector2)mouseWorld - (Vector2)transform.position).normalized;
        smoothAnimDir = Vector2.Lerp(smoothAnimDir, dirToMouse, animBlendSpeed * Time.deltaTime);

        animator.SetFloat("MoveX", smoothAnimDir.x);
        animator.SetFloat("MoveY", smoothAnimDir.y);
        animator.SetFloat("Speed", rb.linearVelocity.magnitude);
    }

    // ═════════════════════════════════════════════════════════════
    // Y-SORTING (Isometric depth)
    // ═════════════════════════════════════════════════════════════

    private void UpdateYSorting()
    {
        if (spriteRenderer == null) return;

        // Y-sorting: order = ceil(Y position) + elevation offset
        int sortOrder = Mathf.CeilToInt(transform.position.y) + (currentElevation * 10);
        spriteRenderer.sortingOrder = sortOrder;
    }

    // ═════════════════════════════════════════════════════════════
    // UTILITY
    // ═════════════════════════════════════════════════════════════

    private void HideClickIndicator()
    {
        if (clickIndicator != null)
            clickIndicator.SetActive(false);
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