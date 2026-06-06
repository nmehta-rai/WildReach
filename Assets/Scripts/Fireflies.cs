using UnityEngine;

// A cheap cloud of glowing motes for ONE zone. Motes live in a moving window around the camera and recycle when
// they fall behind, so a small fixed count gives even firefly density everywhere you walk in the zone — not a
// pile at one spawn point. They only show when the camera is actually inside the painted zone AND it's night.
// Each mote is a tiny unlit HDR sphere, so the scene's bloom turns it into a soft glow.
[DisallowMultipleComponent]
public class Fireflies : MonoBehaviour
{
    [SerializeField] int   count = 28;          // few — they live around the camera, not across the whole 1km zone
    [SerializeField] float radius = 16f;        // window radius around the camera
    [SerializeField] float height = 3.5f;       // how high above the ground they drift
    [SerializeField] float moteSize = 0.07f;
    [SerializeField] Color glow = new Color(1f, 0.82f, 0.38f);
    [SerializeField] float brightness = 5f;     // HDR multiplier so bloom catches them
    [SerializeField] float driftSpeed = 0.12f;  // slow, lazy drift (was zooming before)
    [SerializeField] bool  alwaysOnPreview = false; // tick to see them in daylight while dressing
    [SerializeField] float dayIntensity = 0.7f;
    [SerializeField] float nightIntensity = 0.12f;

    // zone binding (set by the Island Builder)
    [SerializeField] IslandDefinition island;
    [SerializeField] string zoneName = "Forrest";
    [SerializeField] string terrainName = "Island1_Terrain";

    Transform[] motes; Vector3[] seed; Light sun; Material mat; Terrain terrain;
    readonly ZoneSampler zone = new ZoneSampler(); bool zoneOk;

    public void Configure(IslandDefinition def, string zoneN, string terrainN)
    { island = def; zoneName = zoneN; terrainName = terrainN; }

    void Start()
    {
        sun = FindSun();
        var go = !string.IsNullOrEmpty(terrainName) ? GameObject.Find(terrainName) : null;
        terrain = go != null ? go.GetComponent<Terrain>() : (Terrain.activeTerrain ?? Object.FindFirstObjectByType<Terrain>());
        zoneOk = zone.Bind(ref island, terrain, zoneName);

        mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", glow * brightness);
        var sphere = CreateSphereMesh();
        motes = new Transform[count]; seed = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            var m = new GameObject("mote");
            m.transform.SetParent(transform, false);
            m.AddComponent<MeshFilter>().sharedMesh = sphere;
            m.AddComponent<MeshRenderer>().sharedMaterial = mat;
            seed[i] = new Vector3(Random.value * 1000f, Random.value * 1000f, Random.value * 1000f);
            motes[i] = m.transform;
            m.transform.position = RandomSpotAround(CamPos());
        }
    }

    void Update()
    {
        if (motes == null) return;
        Vector3 cam = CamPos();
        float night = alwaysOnPreview ? 1f
                    : Mathf.InverseLerp(dayIntensity, nightIntensity, sun != null ? sun.intensity : 0f);
        bool show = night > 0.01f && (!zoneOk || zone.Contains(cam));   // only in this zone, only at night
        float t = Time.time;

        for (int i = 0; i < motes.Length; i++)
        {
            var tr = motes[i];
            if (!show) { tr.localScale = Vector3.zero; continue; }

            var s = seed[i];
            // lazy drift via offset sines
            Vector3 p = tr.position;
            p.x += Mathf.Sin(t * driftSpeed + s.x) * driftSpeed * 0.6f;
            p.z += Mathf.Cos(t * driftSpeed * 0.9f + s.z) * driftSpeed * 0.6f;
            // recycle motes that fall outside the camera window so density stays even as you walk
            Vector2 flat = new Vector2(p.x - cam.x, p.z - cam.z);
            if (flat.sqrMagnitude > radius * radius) p = RandomSpotAround(cam);

            float groundY = terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : cam.y;
            p.y = groundY + height * (0.4f + 0.4f * Mathf.Sin(t * driftSpeed * 1.3f + s.y));
            tr.position = p;

            float flick = 0.55f + 0.45f * Mathf.Sin(t * 5f + s.x * 13f);
            tr.localScale = Vector3.one * moteSize * flick * night;
        }
    }

    Vector3 RandomSpotAround(Vector3 c)
    {
        Vector2 r = Random.insideUnitCircle * radius;
        var p = new Vector3(c.x + r.x, 0f, c.z + r.y);
        float gy = terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : c.y;
        p.y = gy + Random.Range(0.2f, height);
        return p;
    }

    Vector3 CamPos()
    {
        var cam = Camera.main;
        if (cam != null) return cam.transform.position;
        var pm = Object.FindFirstObjectByType<PlayerMovement>();
        return pm != null ? pm.transform.position : transform.position;
    }

    static Light FindSun()
    {
        Light best = null;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l.gameObject.name != "Moonlight" && (best == null || l.intensity > best.intensity)) best = l;
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
