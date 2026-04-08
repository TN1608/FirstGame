using UnityEngine;
#if CINEMACHINE_AVAILABLE
using Cinemachine;
#endif

/// <summary>
/// SunFollowCamera v2 — Orbits Sun Light around player camera
/// 
/// ROLE: Keep Sun Light 2D positioned relative to camera
/// → Creates dynamic isometric lighting as camera moves/rotates
/// 
/// SETUP:
///   1. Create empty "Sun" GameObject (child of DayNightManager, or standalone)
///   2. Add Light2D component (Sprite type)
///   3. Attach this script to Sun
///   4. Assign virtual camera (or leave empty to use Camera.main)
///   5. Adjust orbitRadius & heightOffset in inspector
/// 
/// ORBIT MECHANICS (iso-core style):
///   • Sun orbits in 2D plane (X-Y)
///   • Radius controlled by orbitRadius
///   • Height offset controlled by heightOffset
///   • Angle comes from DayNightCycle.SunAngle
///   • Sun "looks at" camera (transform.right points toward camera)
/// </summary>
public class SunFollowCamera : MonoBehaviour
{
    // ═════════════════════════════════════════════════════════
    // CAMERA REFERENCE
    // ═════════════════════════════════════════════════════════

#if CINEMACHINE_AVAILABLE
    [SerializeField]
    [Tooltip("CinemachineCamera to follow. Leave empty to use Camera.main")]
    private CinemachineCamera virtualCamera;
#endif

    // ═════════════════════════════════════════════════════════
    // ORBIT SETTINGS
    // ═════════════════════════════════════════════════════════

    [Header("=== ORBIT SETTINGS ===")]
    [SerializeField]
    [Range(5f, 25f)]
    [Tooltip("Distance from camera to sun. Tip: 10-18")]
    private float orbitRadius = 14f;

    [SerializeField]
    [Range(2f, 12f)]
    [Tooltip("Height offset above camera (prevents sun from going below horizon). Tip: 4-8")]
    private float heightOffset = 5f;

    [SerializeField]
    [Range(-5f, 5f)]
    [Tooltip("Horizontal offset (adjusts shadow direction in isometric). Tip: -1 to 1")]
    private float horizontalOffset = -1f;

    // ═════════════════════════════════════════════════════════
    // SMOOTHING
    // ═════════════════════════════════════════════════════════

    [Header("=== SMOOTHING ===")]
    [SerializeField]
    [Range(0.1f, 20f)]
    [Tooltip("Speed of sun following camera. High=instant, Low=smooth. Tip: 5-15")]
    private float followSpeed = 8f;

    [SerializeField]
    [Tooltip("Enable debug gizmo visualization")]
    private bool showDebugGizmo = false;

    // ═════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ═════════════════════════════════════════════════════════

    private Camera mainCamera;
    private Vector3 targetPosition;

    // ═════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═════════════════════════════════════════════════════════

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
            Debug.LogWarning("[SunFollowCamera] Camera.main not found");
    }

    void LateUpdate()
    {
        if (DayNightCycle.Instance == null)
            return;

        // Get camera position
        Vector3 cameraPos = GetCameraPosition();
        if (cameraPos == Vector3.zero)
            return;

        // Get sun angle from day/night cycle
        float sunAngle = DayNightCycle.Instance.SunAngle;
        float angleRad = sunAngle * Mathf.Deg2Rad;

        // ── Calculate sun orbit position ────────────────────
        // In isometric 2D, sun orbits in X-Y plane
        // X: cosine of angle
        // Y: sine + height offset (keeps sun above camera)
        Vector3 orbitOffset = new Vector3(
            Mathf.Cos(angleRad) * orbitRadius + horizontalOffset,
            Mathf.Sin(angleRad) * orbitRadius + heightOffset,
            0f
        );

        targetPosition = cameraPos + orbitOffset;

        // ── Smooth follow ──────────────────────────────────
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            followSpeed * Time.deltaTime
        );

        // ── Sun looks at camera ────────────────────────────
        // Light direction: from sun toward camera
        Vector3 dirToCamera = (cameraPos - transform.position).normalized;
        if (dirToCamera != Vector3.zero)
        {
            // For Light2D, right direction = light direction
            transform.right = dirToCamera;
        }
    }

    // ═════════════════════════════════════════════════════════
    // CAMERA HELPER
    // ═════════════════════════════════════════════════════════

    private Vector3 GetCameraPosition()
    {
#if CINEMACHINE_AVAILABLE
        if (virtualCamera != null)
            return virtualCamera.transform.position;
#endif
        if (mainCamera != null)
            return mainCamera.transform.position;

        return Vector3.zero;
    }

    // ═════════════════════════════════════════════════════════
    // DEBUG VISUALIZATION
    // ═════════════════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmo || DayNightCycle.Instance == null)
            return;

        Vector3 cameraPos = GetCameraPosition();

        // Draw orbit sphere
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.2f);
        Gizmos.DrawWireSphere(cameraPos, orbitRadius);

        // Draw line from camera to sun
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.8f);
        Gizmos.DrawLine(cameraPos, transform.position);

        // Draw height offset plane
        Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
        float planeRadius = orbitRadius * 1.2f;
        Vector3 planePos = cameraPos + Vector3.up * heightOffset;
        Gizmos.DrawWireCube(planePos, new Vector3(planeRadius, 0.1f, planeRadius));
    }
#endif
}