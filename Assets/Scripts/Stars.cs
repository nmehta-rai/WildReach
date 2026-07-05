using UnityEngine;

// A star dome that hangs around the camera (so the stars are effectively infinitely far), spins very slowly for a
// living night sky, and fades in/out with night — read from the sun's intensity like the fireflies. Uses a property
// block so it never dirties the shared material. Pair with the WildReach/Stars shader on an inverted sphere.
[ExecuteAlways]
[DisallowMultipleComponent]
public class Stars : MonoBehaviour
{
    [SerializeField] float spinDegPerSec = 0.35f;     // gentle rotation of the night sky
    [SerializeField] float dayIntensity = 0.7f;       // sun above this -> stars hidden
    [SerializeField] float nightIntensity = 0.12f;    // sun below this -> stars full

    Light sun; Renderer rend; MaterialPropertyBlock mpb;
    static readonly int NightID = Shader.PropertyToID("_NightFade");

    void OnEnable()
    {
        rend = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
        sun = FindSun();
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam != null) transform.position = cam.transform.position;     // always centred on the viewer
        transform.Rotate(Vector3.up, spinDegPerSec * Time.deltaTime, Space.World);

        if (sun == null) sun = FindSun();
        float night = Mathf.InverseLerp(dayIntensity, nightIntensity, sun != null ? sun.intensity : 0f);
        if (rend != null)
        {
            rend.GetPropertyBlock(mpb);
            mpb.SetFloat(NightID, night);
            rend.SetPropertyBlock(mpb);
        }
    }

    static Light FindSun()
    {
        Light best = null;
        foreach (var l in Object.FindObjectsByType<Light>())
            if (l.type == LightType.Directional && l.gameObject.name != "Moonlight" && (best == null || l.intensity > best.intensity))
                best = l;
        return best;
    }
}
