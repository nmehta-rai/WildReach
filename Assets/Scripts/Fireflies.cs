using UnityEngine;

// A cheap cloud of glowing motes for ONE zone. Motes live in a moving window around the camera and recycle when
// they fall behind, so a small fixed count gives even firefly density everywhere you walk in the zone — not a
// pile at one spawn point. They only show when the camera is actually inside the painted zone AND it's night.
// Each mote is a tiny unlit HDR sphere, so the scene's bloom turns it into a soft glow.
[DisallowMultipleComponent]
public class Fireflies : MonoBehaviour
{
    [SerializeField] int   count = 7;           // few and gentle — they live around the camera, not the whole zone
    [SerializeField] float radius = 20f;        // window radius around the camera (wider + fewer = sparser)
    [SerializeField] float height = 1.5f;       // top of their vertical drift (they hover low, near the grass)
    [SerializeField] float moteSize = 0.045f;
    [SerializeField] Color glow = new Color(1f, 0.82f, 0.38f);
    [SerializeField] float brightness = 4f;     // HDR multiplier so bloom catches them
    [SerializeField] float floatRate = 0.1f;    // how fast each mote wanders (rad/s) — small = lazy
    [SerializeField] float floatRadius = 0.7f;  // how far it roams from its anchor (m)
    [SerializeField] Vector2 blinkPeriod = new Vector2(4f, 7f);  // seconds per blink cycle, randomized per mote
    [SerializeField] float litFraction = 0.22f; // slice of the cycle the mote is actually glowing
    [SerializeField] bool  alwaysOnPreview = false; // tick to see them in daylight while dressing
    [SerializeField] float dayIntensity = 0.7f;
    [SerializeField] float nightIntensity = 0.12f;

    // zone binding (set by the Island Builder)
    [SerializeField] IslandDefinition island;
    [SerializeField] string zoneName = "Forrest";
    [SerializeField] string terrainName = "Island1_Terrain";

    Transform[] motes; Vector3[] seed; Vector3[] anchor; float[] period; Light sun; Material mat; Terrain terrain;
    Renderer[] rends; MaterialPropertyBlock mpb;
    readonly ZoneSampler zone = new ZoneSampler(); bool zoneOk;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public void Configure(IslandDefinition def, string zoneN, string terrainN)
    { island = def; zoneName = zoneN; terrainName = terrainN; }

    void Start()
    {
        sun = FindSun();
        var go = !string.IsNullOrEmpty(terrainName) ? GameObject.Find(terrainName) : null;
        terrain = go != null ? go.GetComponent<Terrain>() : (Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>());
        zoneOk = zone.Bind(ref island, terrain, zoneName);

        mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor(BaseColorId, glow * brightness);
        mpb = new MaterialPropertyBlock();
        var sphere = CreateSphereMesh();
        motes = new Transform[count]; seed = new Vector3[count]; anchor = new Vector3[count];
        period = new float[count]; rends = new Renderer[count];
        for (int i = 0; i < count; i++)
        {
            var m = new GameObject("mote");
            m.transform.SetParent(transform, false);
            m.AddComponent<MeshFilter>().sharedMesh = sphere;
            rends[i] = m.AddComponent<MeshRenderer>();
            rends[i].sharedMaterial = mat;
            seed[i] = new Vector3(Random.value * 1000f, Random.value * 1000f, Random.value * 1000f);
            period[i] = Random.Range(blinkPeriod.x, blinkPeriod.y);
            motes[i] = m.transform;
            anchor[i] = RandomSpotAround(CamPos());
            m.transform.position = anchor[i];
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
            // recycle the ANCHOR (not the live position) when it falls behind the camera, so motes don't teleport
            Vector2 flat = new Vector2(anchor[i].x - cam.x, anchor[i].z - cam.z);
            if (flat.sqrMagnitude > radius * radius) anchor[i] = RandomSpotAround(cam);

            // lazy bounded drift around the anchor — two slow sine frequencies per axis so the path meanders
            // gently instead of orbiting. Time-based, so it's frame-rate independent.
            float ox = Mathf.Sin(t * floatRate + s.x) * floatRadius + Mathf.Sin(t * floatRate * 2.3f + s.z) * floatRadius * 0.35f;
            float oz = Mathf.Cos(t * floatRate * 0.8f + s.z) * floatRadius + Mathf.Cos(t * floatRate * 2.1f + s.x) * floatRadius * 0.35f;
            float oy = (0.5f + 0.5f * Mathf.Sin(t * floatRate * 1.3f + s.y)) * height;   // drifts up and down, 0..height
            float groundY = terrain != null ? terrain.SampleHeight(anchor[i]) + terrain.transform.position.y : cam.y;
            tr.position = new Vector3(anchor[i].x + ox, groundY + 0.2f + oy, anchor[i].z + oz);

            // the actual firefly blink: dark most of the cycle, then a soft rise-and-fade glow. Each mote has its
            // own period + phase so they never sync. pulse is 0 outside the lit window, a sine bell inside it.
            float phase = Mathf.Repeat(t / period[i] + s.y, 1f);
            float pulse = phase < litFraction ? Mathf.Sin(phase / litFraction * Mathf.PI) : 0f;

            tr.localScale = Vector3.one * moteSize * (0.35f + 0.65f * pulse) * night;
            mpb.SetColor(BaseColorId, glow * (brightness * Mathf.Max(pulse, 0.04f)));   // faint ember between blinks
            rends[i].SetPropertyBlock(mpb);
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
        var pm = Object.FindAnyObjectByType<PlayerMovement>();
        return pm != null ? pm.transform.position : transform.position;
    }

    static Light FindSun()
    {
        Light best = null;
        foreach (var l in Object.FindObjectsByType<Light>())
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
