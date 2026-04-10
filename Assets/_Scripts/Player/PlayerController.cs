using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Isometric 2.5D Player Controller — New Input System only.
///
/// Controls:
///   WASD / Arrow Keys  → Move in isometric world directions
///   Space              → Jump (parabolic arc over terrain)
///   E                  → Interact (keyboard)
///   Left Mouse Click   → Interact (mouse, world-space raycast)
///
/// No legacy UnityEngine.Input calls — all via InputActionReference or
/// direct Keyboard/Mouse device reads from Input System.
///
/// Setup in Inspector:
///   Assign Move / Jump / Interact / MouseInteract InputActionReferences
///   from your Input Action Asset, OR leave them null to use the
///   built-in Keyboard/Mouse fallback (no asset needed).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Movement")]
    public float moveSpeed     = 5f;
    public float acceleration  = 18f;
    public float deceleration  = 24f;

    [Header("Jump")]
    public float jumpArcHeight = 0.6f;   // visual Z units at peak
    public float jumpDuration  = 0.32f;

    [Header("Step / Terrain")]
    public float stepHeight         = 1.2f;   // auto-step if height diff ≤ this
    public float heightFollowSpeed  = 12f;
    public float heightVisualScale  = 0.05f;  // world-Z offset per tile height unit

    [Header("Interaction")]
    public float interactRange  = 1.5f;
    public LayerMask interactLayer = ~0;

    [Header("Input Action References (optional)")]
    [Tooltip("Leave null to use built-in WASD / Space / E / LMB fallback.")]
    public InputActionReference moveAction;
    public InputActionReference jumpAction;
    public InputActionReference interactKeyAction;
    public InputActionReference interactMouseAction;

    [Header("References")]
    public ChunkManager    chunkManager;
    public Camera          mainCamera;
    public SpriteRenderer  spriteRenderer;

    // ── Private ───────────────────────────────────────────────────────────

    private Rigidbody2D _rb;
    private Vector2     _moveInput;
    private Vector2     _velocity;
    private float       _visualZ;
    private bool        _grounded     = true;
    private bool        _jumpConsumed = false;

    // Isometric axis vectors (WASD → isometric world XY)
    //   D = right-down,  A = left-up
    //   W = right-up,    S = left-down
    static readonly Vector2 IsoE = new Vector2( 1f, -0.5f).normalized;   // D
    static readonly Vector2 IsoN = new Vector2( 1f,  0.5f).normalized;   // W

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale   = 0f;    // gravity handled manually via terrain height
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (mainCamera   == null) mainCamera   = Camera.main;
        if (chunkManager == null) chunkManager = FindFirstObjectByType<ChunkManager>();

        EnableActions();
    }

    void OnEnable()  => EnableActions();
    void OnDisable() => DisableActions();

    void Update()
    {
        ReadMovementInput();
        ReadActionInput();

        if (chunkManager != null)
            chunkManager.LoadChunksAroundPosition(transform.position);
    }

    void FixedUpdate()
    {
        ApplyMovement();
        FollowTerrain();
    }

    // ── Input ─────────────────────────────────────────────────────────────

    void ReadMovementInput()
    {
        Vector2 raw = Vector2.zero;

        if (moveAction?.action != null)
        {
            raw = moveAction.action.ReadValue<Vector2>();
        }
        else
        {
            // Built-in fallback — Keyboard device, no legacy Input class
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) raw.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  raw.x -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    raw.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  raw.y -= 1f;
        }

        // Convert WASD screen-space to isometric world-space
        // D/A → along IsoE axis;  W/S → along IsoN axis
        _moveInput  = raw.x * IsoE + raw.y * IsoN;
        if (_moveInput.sqrMagnitude > 1f) _moveInput.Normalize();

        // Flip sprite on horizontal direction
        if (spriteRenderer != null && Mathf.Abs(_moveInput.x) > 0.05f)
            spriteRenderer.flipX = _moveInput.x < 0f;
    }

    void ReadActionInput()
    {
        bool jumpPressed      = false;
        bool interactPressed  = false;
        bool mousePressed     = false;

        var kb    = Keyboard.current;
        var mouse = Mouse.current;

        // Jump
        if (jumpAction?.action != null)
            jumpPressed = jumpAction.action.WasPressedThisFrame();
        else if (kb != null)
            jumpPressed = kb.spaceKey.wasPressedThisFrame;

        // Keyboard interact (E)
        if (interactKeyAction?.action != null)
            interactPressed = interactKeyAction.action.WasPressedThisFrame();
        else if (kb != null)
            interactPressed = kb.eKey.wasPressedThisFrame;

        // Mouse interact (Left Click)
        if (interactMouseAction?.action != null)
            mousePressed = interactMouseAction.action.WasPressedThisFrame();
        else if (mouse != null)
            mousePressed = mouse.leftButton.wasPressedThisFrame;

        if (jumpPressed && _grounded && !_jumpConsumed)
        {
            _jumpConsumed = true;
            StartCoroutine(JumpArc());
        }

        if (interactPressed) TryInteract(useMousePos: false);
        if (mousePressed)    TryInteract(useMousePos: true);
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

        float terrainH  = chunkManager.GetHeightAtPosition(transform.position);
        float targetZ   = terrainH * heightVisualScale;
        _visualZ        = Mathf.Lerp(_visualZ, targetZ, Time.fixedDeltaTime * heightFollowSpeed);

        var pos  = transform.position;
        pos.z    = _visualZ;
        transform.position = pos;
    }

    IEnumerator JumpArc()
    {
        _grounded = false;
        float startZ   = _visualZ;
        float elapsed  = 0f;

        while (elapsed < jumpDuration)
        {
            elapsed  += Time.deltaTime;
            float t   = elapsed / jumpDuration;
            // Parabola: peaks at t=0.5
            float arc = 4f * t * (1f - t);
            _visualZ  = startZ + arc * jumpArcHeight;

            var pos  = transform.position;
            pos.z    = _visualZ;
            transform.position = pos;

            yield return null;
        }

        _grounded     = true;
        _jumpConsumed = false;
    }

    // ── Interact ──────────────────────────────────────────────────────────

    void TryInteract(bool useMousePos)
    {
        Vector2 origin;
        Vector2 dir;

        if (useMousePos && mainCamera != null && Mouse.current != null)
        {
            // Mouse position → world space (ignore Z)
            Vector3 wp = mainCamera.ScreenToWorldPoint(
                new Vector3(Mouse.current.position.ReadValue().x,
                            Mouse.current.position.ReadValue().y, 10f));
            origin = transform.position;
            dir    = ((Vector2)wp - origin).normalized;
        }
        else
        {
            origin = transform.position;
            dir    = _moveInput.sqrMagnitude > 0.01f ? _moveInput : Vector2.right;
        }

        RaycastHit2D hit = Physics2D.Raycast(origin, dir, interactRange, interactLayer);
        if (hit.collider != null)
        {
            var interactable = hit.collider.GetComponent<IInteractable>();
            interactable?.Interact(gameObject);
            Debug.Log($"[Player] Interacted with {hit.collider.name}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    void EnableActions()
    {
        moveAction?.action?.Enable();
        jumpAction?.action?.Enable();
        interactKeyAction?.action?.Enable();
        interactMouseAction?.action?.Enable();
    }

    void DisableActions()
    {
        moveAction?.action?.Disable();
        jumpAction?.action?.Disable();
        interactKeyAction?.action?.Disable();
        interactMouseAction?.action?.Disable();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}

/// <summary>Implement on any GameObject to make it interactable.</summary>
public interface IInteractable
{
    void Interact(GameObject instigator);
}