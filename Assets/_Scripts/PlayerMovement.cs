using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 6f;
    public float stopDistance = 0.15f;        // giảm xuống một chút để nhạy hơn

    private Rigidbody2D rb;
    private Animator animator;

    private Vector2 targetPosition;
    private bool isMoving = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        // === LUÔN NHÌN THEO CHUỘT ===
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);

        Vector2 dirToMouse = (new Vector2(mouseWorldPos.x, mouseWorldPos.y) - (Vector2)transform.position).normalized;

        animator.SetFloat("MoveX", dirToMouse.x);
        animator.SetFloat("MoveY", dirToMouse.y);

        // === CLICK CHUỘT PHẢI ĐỂ DI CHUYỂN ===
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            targetPosition = new Vector2(mouseWorldPos.x, mouseWorldPos.y);
            isMoving = true;
        }

        if (isMoving)
        {
            Vector2 dirToTarget = (targetPosition - (Vector2)transform.position).normalized;
            rb.linearVelocity = dirToTarget * moveSpeed;

            // Đến đích thì dừng ngay + ép Speed = 0
            if (Vector2.Distance(transform.position, targetPosition) <= stopDistance)
            {
                rb.linearVelocity = Vector2.zero;
                isMoving = false;
                animator.SetFloat("Speed", 0f);     // ← DÒNG FIX QUAN TRỌNG
            }
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }

        // Cập nhật Speed bình thường (dùng để chuyển Idle ↔ Walk)
        animator.SetFloat("Speed", rb.linearVelocity.magnitude);
    }
}