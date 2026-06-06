using UnityEngine;

// A flat sky body (sun or moon) that hangs in the direction its light comes FROM, at a fixed distance from the
// camera, always facing the camera. DayNightCycle rotates the light; this quad follows automatically, so the sun
// disc sits exactly where the sunlight originates and the moon where the moonlight does. HDR-bright + black-bg
// texture (additive) means the scene's bloom halos it into a real glow.
[ExecuteAlways]
[DisallowMultipleComponent]
public class SkyBillboard : MonoBehaviour
{
    [Tooltip("The directional light this body represents (sun or Moonlight). The body sits opposite its forward.")]
    public Transform sourceLight;
    [Tooltip("How far from the camera to park the disc (just needs to clear the geometry).")]
    public float distance = 600f;

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null || sourceLight == null) return;
        Vector3 dir = -sourceLight.forward;                       // toward the body = opposite the light travel
        transform.position = cam.transform.position + dir * distance;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
    }
}
