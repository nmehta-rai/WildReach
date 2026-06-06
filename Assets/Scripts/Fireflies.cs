using UnityEngine;

// A cheap cloud of glowing motes that drift inside a volume and fade in at night. No particle-shader fuss:
// each mote is a tiny unlit HDR-bright sphere, so the scene's bloom turns them into soft glows. Night is read
// from the main directional light's intensity (so it ties into the day/night cycle). Drop one over the forest.
[DisallowMultipleComponent]
public class Fireflies : MonoBehaviour
{
    [SerializeField] int   count = 60;
    [SerializeField] float radius = 28f;
    [SerializeField] float height = 5f;
    [SerializeField] float moteSize = 0.06f;
    [SerializeField] Color glow = new Color(1f, 0.82f, 0.38f);
    [SerializeField] float brightness = 5f;     // HDR multiplier so bloom catches them
    [SerializeField] float driftSpeed = 0.35f;
    [SerializeField] bool  nightOnly = true;
    [SerializeField] bool  alwaysOnPreview = true; // TESTING: glow regardless of time so you can see them now.
                                                   //   Uncheck for the real night-only behaviour.
    [SerializeField] float dayIntensity = 0.7f;  // sun above this = full day (motes hidden)
    [SerializeField] float nightIntensity = 0.12f;// sun below this = full night (motes glowing)

    Transform[] motes; Vector3[] seeds; Light sun; Material mat;

    void Start()
    {
        sun = FindSun();
        mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", glow * brightness);
        var sphere = CreateSphereMesh();
        motes = new Transform[count]; seeds = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("mote");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = sphere;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.transform.localScale = Vector3.one * moteSize;
            seeds[i] = new Vector3(Random.value * 1000f, Random.value * 1000f, Random.value * 1000f);
            motes[i] = go.transform;
        }
    }

    void Update()
    {
        float night = (!nightOnly || alwaysOnPreview) ? 1f
                    : Mathf.InverseLerp(dayIntensity, nightIntensity, sun != null ? sun.intensity : 0f);
        float t = Time.time;
        for (int i = 0; i < motes.Length; i++)
        {
            var s = seeds[i];
            // smooth wandering inside the volume via offset sines (cheap, no Perlin lookups)
            float x = Mathf.Sin(t * driftSpeed + s.x) * radius * Mathf.Cos(s.x);
            float z = Mathf.Cos(t * driftSpeed * 0.8f + s.z) * radius * Mathf.Sin(s.z);
            float y = height * 0.5f + Mathf.Sin(t * driftSpeed * 1.3f + s.y) * height * 0.4f;
            motes[i].localPosition = new Vector3(x, y, z);
            float flick = 0.55f + 0.45f * Mathf.Sin(t * 6f + s.x * 13f);   // individual twinkle
            motes[i].localScale = Vector3.one * moteSize * flick * night;  // night fade via scale (no transparency needed)
        }
    }

    static Light FindSun()
    {
        Light best = null;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional && (best == null || l.intensity > best.intensity)) best = l;
        return best;
    }

    static Mesh CreateSphereMesh()
    {
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var m = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(tmp);
        return m;
    }
}
