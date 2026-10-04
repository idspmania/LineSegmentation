using UnityEngine;
using UnityEngine.InputSystem;

// 테스트 씬 플레이어. WASD는 카메라 기준 수평 이동, E/Q는 상승/하강, Shift는 가속.
// 몸의 정면(총구 방향)은 카메라의 yaw를 따른다.
public class PlayerMover : MonoBehaviour
{
    [SerializeField] Transform viewCamera;
    [SerializeField] float speed = 6f;
    [SerializeField] float fastMultiplier = 3f;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        float yaw = viewCamera != null ? viewCamera.eulerAngles.y : transform.eulerAngles.y;
        Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
        transform.rotation = facing;

        Vector3 input = Vector3.zero;
        if (kb.wKey.isPressed) input.z += 1f;
        if (kb.sKey.isPressed) input.z -= 1f;
        if (kb.dKey.isPressed) input.x += 1f;
        if (kb.aKey.isPressed) input.x -= 1f;
        if (kb.eKey.isPressed) input.y += 1f;
        if (kb.qKey.isPressed) input.y -= 1f;
        if (input == Vector3.zero) return;

        float s = speed * (kb.leftShiftKey.isPressed ? fastMultiplier : 1f);
        transform.position += facing * input.normalized * (s * Time.deltaTime);
    }
}
