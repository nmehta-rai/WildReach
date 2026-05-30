using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float distance = 5f;
    [SerializeField] float heightOffset = 1.6f;
    [SerializeField] float sensitivity = 3f;

    float yaw;
    float pitch = 20f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void LateUpdate()
    {
        yaw += Input.GetAxis("Mouse X") * sensitivity;
        pitch -= Input.GetAxis("Mouse Y") * sensitivity;
        pitch = Mathf.Clamp(pitch, -10f, 70f);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = target.position + Vector3.up * heightOffset + rot * Vector3.back * distance;
        transform.LookAt(target.position + Vector3.up * heightOffset);
    }
}