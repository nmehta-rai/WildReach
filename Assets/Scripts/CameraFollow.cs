using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float distance = 5f;
    [SerializeField] float heightOffset = 1.6f;
    [SerializeField] float sensitivity = 3f;
    [SerializeField] float collisionRadius = 0.3f;   // camera "ball" size for collision
    [SerializeField] float minDistance = 1f;         // never pull closer than this

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
        Vector3 pivot = target.position + Vector3.up * heightOffset;
        Vector3 dir = rot * Vector3.back;

        // Pull the camera in if terrain/geometry is between it and the player, so it never
        // ends up inside a wall. Ignore the player's own colliders.
        float camDist = distance;
        foreach (var h in Physics.SphereCastAll(pivot, collisionRadius, dir, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.GetComponentInParent<PlayerMovement>() != null) continue;  // skip the player
            if (h.distance < camDist) camDist = h.distance;
        }
        camDist = Mathf.Max(minDistance, camDist - 0.1f);

        transform.position = pivot + dir * camDist;
        transform.LookAt(pivot);
    }
}