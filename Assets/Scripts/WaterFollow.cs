using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Makes the ocean effectively infinite: the water plane snaps to the active camera's X/Z every
// frame, so there's always sea under you and you never reach an edge. The StylizedWater shader
// samples waves in WORLD space, so sliding the plane under a fixed wave field is seamless.
// Runs in edit mode too (follows the Scene view camera) so the editor preview also looks endless.
[ExecuteAlways]
[DisallowMultipleComponent]
public class WaterFollow : MonoBehaviour
{
    public float seaLevel = 0f;

    void OnEnable()    { Snap(); }
    void LateUpdate()  { Snap(); }

    void Snap()
    {
        Camera cam = null;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv != null) cam = sv.camera;
        }
#endif
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        var p = cam.transform.position;
        transform.position = new Vector3(p.x, seaLevel, p.z);
    }
}
