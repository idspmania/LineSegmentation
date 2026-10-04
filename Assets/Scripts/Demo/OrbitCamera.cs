using UnityEngine;
using UnityEngine.InputSystem;

// 대상을 따라가는 궤도 카메라. 우클릭 드래그로 회전, 휠로 줌.
public class OrbitCamera : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float distance = 14f;
    [SerializeField] float minDistance = 2f;
    [SerializeField] float maxDistance = 90f;
    [SerializeField] float yaw = 0f;
    [SerializeField] float pitch = 25f;
    [SerializeField] float degreesPerPixel = 0.2f;
    [SerializeField] float zoomStep = 0.1f;

    void LateUpdate()
    {
        if (target == null) return;

        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * degreesPerPixel;
                pitch = Mathf.Clamp(pitch - delta.y * degreesPerPixel, -89f, 89f);
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
                distance = Mathf.Clamp(distance * (1f - Mathf.Sign(scroll) * zoomStep), minDistance, maxDistance);
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.SetPositionAndRotation(target.position - rotation * Vector3.forward * distance, rotation);
    }
}
