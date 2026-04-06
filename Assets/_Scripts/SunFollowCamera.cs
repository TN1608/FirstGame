using Unity.Cinemachine;
using UnityEngine;

public class SunFollowCamera : MonoBehaviour
{
    public CinemachineCamera virtualCamera;
    public float distance = 25f;     // khoảng cách mặt trời
    public float baseAngle = 45f;    // góc mặc định

    void LateUpdate()
    {
        if (virtualCamera == null || DayNightCycle.Instance == null) return;

        Vector3 camPos = virtualCamera.transform.position;

        // Lấy góc từ DayNightCycle để shadow thay đổi theo thời gian
        float currentSunAngle = DayNightCycle.Instance.GetSunAngle();

        Vector3 offset = new Vector3(
            -distance * Mathf.Cos(currentSunAngle * Mathf.Deg2Rad),
            distance * Mathf.Sin(currentSunAngle * Mathf.Deg2Rad),
            0
        );

        transform.position = camPos + offset;
        transform.right = camPos - transform.position;   // ánh sáng luôn hướng vào camera
    }
}