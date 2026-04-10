using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Isometric Player Controller with harvesting support.
///
/// Controls:
///   WASD          → Move
///   Space         → Jump
///   E (tap)       → Interact / single hit
///   E (hold)      → Repeated harvest hits (minecraft-style)
///   Left Click    → Interact at mouse world position
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed    = 5f;
    public float acceleration = 18f;
    public float deceleration = 24f;

    [Header("Jump")]
    public float jumpArcHeight = 0.6f;
    public float jumpDuration  = 0.32f;

    [Header("Terrain")]
    public float heightFollowSpeed = 12f;
    public float heightVisualScale = 0.05f;

    [Header("Interaction")]
    public float     interactRange    = 1.5f;
    public LayerMask interactLayer    = ~0;
    [Tooltip("Hits per second when holding E.")]
    public float     harvestHitRate   = 2f;

    [Header("Input Actions (optional)")]
    public InputActionReference moveAction;
    public InputActionReference jumpAction;
    public InputActionReference interactAction;
    public InputActionReference mouseInteractAction;

    [Header("References")]
    public ChunkManager   chunkManager;
    public Camera         mainCamera;
    public SpriteRenderer spriteRenderer;

    // ── Private ────────────────────────────────────────────────────────────

    private Rigidbody2D _rb;
    private Vector2     _moveInput;
    private Vector2     _velocity;
    private float       _visualZ;
    private bool        _grounded    = true;
    private float       _holdTimer;
    private float       _harvestInterval;

    static readonly Vector2 IsoE = new Vector2( 1f, -0.5f).normalized;
    static readonly Vector2 IsoN = new Vector2( 1f,  0.5f).normalized;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale   = 0f;
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (mainCamera  == null) mainCamera  = Camera.main;
        if (chunkManager == null) chunkManager = FindFirstObjectByType<ChunkManager>();

        _harvestInterval = 1f / Mathf.Max(harvestHitRate, 0.1f);

        moveAction?.action?.Enable();
        jumpAction?.action?.Enable();
        interactAction?.action?.Enable();
        mouseInteractAction?.action?.Enable();
    }

    void OnDisable()
    {
        moveAction?.action?.Disable();
        jumpAction?.action?.Disable();
        interactAction?.action?.Disable();
        mouseInteractAction?.action?.Disable();
    }

    void Update()
    {
        ReadMovement();
        ReadActions();
        if (chunkManager != null)
            chunkManager.LoadChunksAroundPosition(transform.position);
    }

    void FixedUpdate()
    {
        ApplyMovement();
        FollowTerrain();
    }

    // ── Input ──────────────────────────────────────────────────────────────

    void ReadMovement()
    {
        Vector2 raw = Vector2.zero;
        var kb = Keyboard.current;

        if (moveAction?.action != null)
            raw = moveAction.action.ReadValue<Vector2>();
        else if (kb != null)
        {
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) raw.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  raw.x -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    raw.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  raw.y -= 1f;
        }

        _moveInput = raw.x * IsoE + raw.y * IsoN;
        if (_moveInput.sqrMagnitude > 1f) _moveInput.Normalize();

        if (spriteRenderer != null && Mathf.Abs(_moveInput.x) > 0.05f)
            spriteRenderer.flipX = _moveInput.x < 0f;
    }

    void ReadActions()
    {
        var kb    = Keyboard.current;
        var mouse = Mouse.current;

        // ── Jump ──
        bool jumpPressed = jumpAction?.action != null
            ? jumpAction.action.WasPressedThisFrame()
            : kb?.spaceKey.wasPressedThisFrame ?? false;

        if (jumpPressed && _grounded)
            StartCoroutine(JumpArc());

        // ── E: tap = single interact, hold = repeated harvest ──
        bool eDown = interactAction?.action != null
            ? interactAction.action.WasPressedThisFrame()
            : kb?.eKey.wasPressedThisFrame ?? false;

        bool eHeld = interactAction?.action != null
            ? interactAction.action.IsPressed()
            : kb?.eKey.isPressed ?? false;

        if (eDown)
        {
            // Single tap — try interact first, then single harvest hit
            TryInteract(useMousePos: false, singleHit: true);
            _holdTimer = 0f;
        }
        else if (eHeld)
        {
            // Hold — repeated harvest
            _holdTimer += Time.deltaTime;
            if (_holdTimer >= _harvestInterval)
            {
                _holdTimer -= _harvestInterval;
                TryHarvestHit();
            }
        }
        else
        {
            _holdTimer = 0f;
        }

        // ── Left Click ──
        bool mouseClick = mouseInteractAction?.action != null
            ? mouseInteractAction.action.WasPressedThisFrame()
            : mouse?.leftButton.wasPressedThisFrame ?? false;

        if (mouseClick)
            TryInteract(useMousePos: true, singleHit: true);
    }

    // ── Movement ──────────────────────────────────────────────────────────

    void ApplyMovement()
    {
        Vector2 target = _moveInput * moveSpeed;
        float   rate   = _moveInput.sqrMagnitude > 0.01f ? acceleration : deceleration;
        _velocity      = Vector2.MoveTowards(_velocity, target, rate * Time.fixedDeltaTime);
        _rb.linearVelocity = _velocity;
    }

    void FollowTerrain()
    {
        if (chunkManager == null || !_grounded) return;
        float targetZ = chunkManager.GetHeightAtPosition(transform.position) * heightVisualScale;
        _visualZ      = Mathf.Lerp(_visualZ, targetZ, Time.fixedDeltaTime * heightFollowSpeed);
        var pos = transform.position;
        pos.z = _visualZ;
        transform.position = pos;
    }

    IEnumerator JumpArc()
    {
        _grounded = false;
        float startZ  = _visualZ;
        float elapsed = 0f;
        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float arc = 4f * (elapsed / jumpDuration) * (1f - elapsed / jumpDuration);
            _visualZ  = startZ + arc * jumpArcHeight;
            var p = transform.position; p.z = _visualZ; transform.position = p;
            yield return null;
        }
        _grounded = true;
    }

    // ── Interaction & Harvesting ───────────────────────────────────────────

    void TryInteract(bool useMousePos, bool singleHit)
    {
        var (hit, node) = RaycastForTarget(useMousePos);

        if (node != null)
        {
            // It's a resource node — deliver one hit
            node.HarvestHit(1, gameObject);
        }
        else if (hit.collider != null)
        {
            // Generic interactable
            hit.collider.GetComponent<IInteractable>()?.Interact(gameObject);
        }
    }

    void TryHarvestHit()
    {
        var (_, node) = RaycastForTarget(false);
        node?.HarvestHit(1, gameObject);
    }

    (RaycastHit2D hit, ResourceNode node) RaycastForTarget(bool useMousePos)
    {
        Vector2 origin;
        Vector2 dir;

        if (useMousePos && mainCamera != null && Mouse.current != null)
        {
            var mp  = Mouse.current.position.ReadValue();
            var wp  = mainCamera.ScreenToWorldPoint(new Vector3(mp.x, mp.y, 10f));
            origin  = transform.position;
            dir     = ((Vector2)wp - origin).normalized;
        }
        else
        {
            origin = transform.position;
            dir    = _moveInput.sqrMagnitude > 0.01f ? _moveInput : Vector2.right;
        }

        var hit  = Physics2D.Raycast(origin, dir, interactRange, interactLayer);
        var node = hit.collider?.GetComponent<ResourceNode>();
        return (hit, node);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}