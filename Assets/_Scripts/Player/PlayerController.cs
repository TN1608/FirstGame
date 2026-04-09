using UnityEngine;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Player Controller cho isometric 2.5D game
/// - Click để di chuyển
/// - Auto jump khi gặp multi-level terrain
/// - Tuân theo terrain height
/// </summary>
public class PlayerController : MonoBehaviour
{
    [System.Serializable]
    public class MovementSettings
    {
        public float moveSpeed = 5f;
        public float jumpHeight = 2f;
        public float gravityScale = 1f;
        public float stepHeight = 1f;          // Chiều cao max để auto step lên
        public float groundDrag = 0.1f;
    }

    [System.Serializable]
    public class AnimationSettings
    {
        public bool useAnimator = false;
        public Animator animator;
        public string moveSpeedParam = "MoveSpeed";
        public string jumpParam = "Jump";
    }

    [Header("Settings")]
    public MovementSettings moveSettings = new MovementSettings();
    public AnimationSettings animSettings = new AnimationSettings();
    public ChunkManager chunkManager;
    public Camera mainCamera;

    [Header("References")]
    public Rigidbody2D rb;
    public SpriteRenderer spriteRenderer;

    private Vector3 moveTarget;
    private bool isMoving = false;
    private bool isJumping = false;
    private Vector3 velocity = Vector3.zero;
    private float currentHeight = 0f;
    private Queue<Vector3> pathQueue = new Queue<Vector3>();

    private void Start()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (chunkManager == null)
            chunkManager = FindObjectOfType<ChunkManager>();

        currentHeight = chunkManager.GetHeightAtPosition(transform.position);
    }

    private void Update()
    {
        // Handle input
        HandleMouseInput();

        // Update chunks dựa vào player position
        chunkManager.LoadChunksAroundPosition(transform.position);

        // Update animation
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        // Movement logic
        UpdateMovement();

        // Apply gravity
        ApplyGravity();

        // Update height
        UpdateHeight();
    }

    /// <summary>
    /// Xử lý input chuột
    /// </summary>
    private void HandleMouseInput()
    {
        if (TryGetMouseClickScreenPosition(out Vector3 clickScreenPos))
        {
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(clickScreenPos);
            mouseWorldPos.z = 0;

            // Tính Z position dựa vào isometric view
            // Chuyển đổi 2D screen coord thành isometric world coord
            Vector3 targetPos = ConvertIsometricScreenToWorld(clickScreenPos);
            
            // Thiết lập target để di chuyển
            SetMoveTarget(targetPos);
        }
    }

    /// <summary>
    /// Returns true only on the frame left mouse is pressed.
    /// Uses the new Input System when enabled, otherwise legacy input.
    /// </summary>
    private bool TryGetMouseClickScreenPosition(out Vector3 screenPos)
    {
        screenPos = Vector3.zero;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 pos = Mouse.current.position.ReadValue();
            screenPos = new Vector3(pos.x, pos.y, 0f);
            return true;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 pos = Input.mousePosition;
            screenPos = new Vector3(pos.x, pos.y, 0f);
            return true;
        }
#endif

        return false;
    }

    /// <summary>
    /// Chuyển đổi isometric screen position sang world position
    /// </summary>
    private Vector3 ConvertIsometricScreenToWorld(Vector3 screenPos)
    {
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(screenPos);
        
        // Isometric conversion (phụ thuộc vào setup của bạn)
        // Nếu dùng standard isometric: 
        // - Screen X = (World X - World Y) / sqrt(2)
        // - Screen Y = (World X + World Y) / (2 * sqrt(2))
        
        // Reverse:
        float screenX = worldPos.x;
        float screenY = worldPos.y;
        
        float sqrt2 = Mathf.Sqrt(2);
        float worldX = (screenX / sqrt2 + screenY / sqrt2) / 2;
        float worldY = (-screenX / sqrt2 + screenY / sqrt2) / 2;
        
        return new Vector3(worldX, worldY, 0);
    }

    /// <summary>
    /// Đặt target di chuyển
    /// </summary>
    public void SetMoveTarget(Vector3 target)
    {
        moveTarget = target;
        isMoving = true;
        pathQueue.Clear();
        
        // Có thể thêm A* pathfinding tại đây
        pathQueue.Enqueue(target);
    }

    /// <summary>
    /// Cập nhật di chuyển
    /// </summary>
    private void UpdateMovement()
    {
        if (!isMoving || pathQueue.Count == 0)
        {
            velocity.x = Mathf.Lerp(velocity.x, 0, moveSettings.groundDrag);
            velocity.y = Mathf.Lerp(velocity.y, 0, moveSettings.groundDrag);
            return;
        }

        Vector3 currentTarget = pathQueue.Peek();
        Vector3 direction = (currentTarget - transform.position).normalized;
        
        // Kiểm tra nếu tới gần target
        if (Vector3.Distance(transform.position, currentTarget) < 0.5f)
        {
            pathQueue.Dequeue();
            
            if (pathQueue.Count == 0)
            {
                isMoving = false;
                velocity.x = 0;
                velocity.y = 0;
            }
            return;
        }

        // Di chuyển theo hướng
        velocity.x = direction.x * moveSettings.moveSpeed;
        velocity.y = direction.y * moveSettings.moveSpeed;

        // Flip sprite nếu cần
        if (spriteRenderer != null && direction.x != 0)
        {
            spriteRenderer.flipX = direction.x < 0;
        }

        // Check auto jump
        CheckAndPerformAutoJump(currentTarget);
    }

    /// <summary>
    /// Kiểm tra và tự động jump khi gặp bất kỳ obstacle
    /// </summary>
    private void CheckAndPerformAutoJump(Vector3 targetPos)
    {
        float targetHeight = chunkManager.GetHeightAtPosition(targetPos);
        float heightDifference = targetHeight - currentHeight;

        // Nếu mục tiêu ở trên và trong step height, jump
        if (heightDifference > 0 && heightDifference <= moveSettings.stepHeight)
        {
            if (!isJumping)
            {
                PerformAutoJump(heightDifference);
            }
        }
        // Nếu khác quá cao, không thể climb
        else if (heightDifference > moveSettings.stepHeight)
        {
            // Block movement hoặc tìm đường khác
            velocity.x = 0;
            velocity.y = 0;
        }
    }

    /// <summary>
    /// Thực hiện auto jump
    /// </summary>
    private void PerformAutoJump(float targetHeightDiff)
    {
        if (isJumping)
            return;

        isJumping = true;
        
        // Tính jump velocity để đạt đúng target height
        float jumpVelocity = Mathf.Sqrt(2 * moveSettings.jumpHeight * Mathf.Abs(Physics.gravity.y) * moveSettings.gravityScale);
        velocity.z = jumpVelocity;

        if (animSettings.useAnimator && animSettings.animator != null)
        {
            animSettings.animator.SetTrigger(animSettings.jumpParam);
        }
    }

    /// <summary>
    /// Cập nhật height dựa vào terrain
    /// </summary>
    private void UpdateHeight()
    {
        float targetHeight = chunkManager.GetHeightAtPosition(transform.position);
        
        // Smooth transition về target height
        float heightDifference = targetHeight - currentHeight;
        
        if (Mathf.Abs(heightDifference) > 0.01f)
        {
            currentHeight = Mathf.Lerp(currentHeight, targetHeight, Time.deltaTime * 5f);
        }
        else
        {
            currentHeight = targetHeight;
            isJumping = false;
        }

        // Update position Z dựa vào height
        Vector3 newPos = transform.position;
        newPos.z = currentHeight * 0.1f; // Scale để visual effect
        transform.position = newPos;
    }

    /// <summary>
    /// Áp dụng gravity
    /// </summary>
    private void ApplyGravity()
    {
        if (isJumping)
        {
            velocity.z -= Physics.gravity.y * moveSettings.gravityScale * Time.deltaTime;
        }

        // Apply velocity
        Vector3 newPos = transform.position;
        newPos.x += velocity.x * Time.deltaTime;
        newPos.y += velocity.y * Time.deltaTime;
        newPos.z += velocity.z * Time.deltaTime;
        
        transform.position = newPos;
    }

    /// <summary>
    /// Cập nhật animation
    /// </summary>
    private void UpdateAnimation()
    {
        if (!animSettings.useAnimator || animSettings.animator == null)
            return;

        float moveSpeed = new Vector2(velocity.x, velocity.y).magnitude;
        animSettings.animator.SetFloat(animSettings.moveSpeedParam, moveSpeed);
    }

    /// <summary>
    /// Debug
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!isMoving)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(moveTarget, 0.5f);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, moveTarget);
    }
}