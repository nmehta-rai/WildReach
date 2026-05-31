using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float distance = 5f;
    [SerializeField] float heightOffset = 1.6f;
    [SerializeField] float sensitivity = 3f;
    [SerializeField] float collisionRadius = 0.3f;   // camera "ball" size for collision
    [SerializeField] float minDistance = 1f;         // never pull closer than this
    [SerializeField] float pivotSmoothTime = 0.12f;  // damps vertical follow so jumps/landings don't jolt the camera

    float smoothPivotY, pivotYVel;
    bool pivotInit;

    float yaw;
    float pitch = 20f;

    float defaultDistance, defaultHeight;

    /// Swap who the camera follows (player ↔ ship when boarding/disembarking).
    public void SetTarget(Transform t) { if (t != null) { target = t; pivotInit = false; } }

    /// Override the follow framing (the ship needs a much bigger, higher chase view than the player).
    public void SetView(float dist, float height) { distance = dist; heightOffset = height; }
    public void ResetView() { distance = defaultDistance; heightOffset = defaultHeight; }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        defaultDistance = distance;
        defaultHeight = heightOffset;
    }

    void LateUpdate()
    {
        yaw += Input.GetAxis("Mouse X") * sensitivity;
        pitch -= Input.GetAxis("Mouse Y") * sensitivity;
        pitch = Mathf.Clamp(pitch, -10f, 70f);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 rawPivot = target.position + Vector3.up * heightOffset;
        if (!pivotInit) { smoothPivotY = rawPivot.y; pivotInit = true; }
        smoothPivotY = Mathf.SmoothDamp(smoothPivotY, rawPivot.y, ref pivotYVel, pivotSmoothTime);
        Vector3 pivot = new Vector3(rawPivot.x, smoothPivotY, rawPivot.z);
        Vector3 dir = rot * Vector3.back;

        // Pull the camera in if terrain/geometry is between it and the player, so it never
        // ends up inside a wall. Ignore the player's own colliders.
        float camDist = distance;
        foreach (var h in Physics.SphereCastAll(pivot, collisionRadius, dir, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(target)) continue;                      // skip whatever we're following (player or ship)
            if (h.collider.GetComponentInParent<PlayerMovement>() != null) continue;   // skip the player
            if (h.distance < camDist) camDist = h.distance;
        }
        camDist = Mathf.Max(minDistance, camDist - 0.1f);

        transform.position = pivot + dir * camDist;
        transform.LookAt(pivot);
    }
}