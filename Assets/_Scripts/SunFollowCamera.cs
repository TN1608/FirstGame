using Unity.Cinemachine;
using UnityEngine;

public class SunFollowCamera : MonoBehaviour
{
    public CinemachineCamera virtualCamera;
    
    [Header("Sun Settings")]
    public float distance = 14f;        // Giảm xuống để ánh sáng mạnh và gần camera hơn
    public float heightOffset = 6f;     // Thêm độ cao để Sun không quá thấp

    void LateUpdate()
    {
        if (virtualCamera == null || DayNightCycle.Instance == null) return;

        Vector3 camPos = virtualCamera.transform.position;

        float sunAngle = DayNightCycle.Instance.GetSunAngle();

        Vector3 offset = new Vector3(
            -distance * Mathf.Cos(sunAngle * Mathf.Deg2Rad),
            distance * Mathf.Sin(sunAngle * Mathf.Deg2Rad) + heightOffset,
            0
        );

        transform.position = camPos + offset;
        transform.right = camPos - transform.position;   // Ánh sáng luôn hướng vào trung tâm
    }
}