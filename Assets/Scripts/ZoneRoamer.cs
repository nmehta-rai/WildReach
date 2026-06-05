using UnityEngine;

// Bounds a passive creature to a painted zone and lets it wander the WHOLE zone — terrain-clamped, with
// capped-rate turning (it banks through turns instead of whipping) and the occasional idle pause. Same
// territory logic as the Predator, minus the hunting. Assign it via the Island Builder's per-zone Mob slot
// (or by hand). Drives an Animator "Speed" float if the creature has a controller.
[DisallowMultipleComponent]
public class ZoneRoamer : MonoBehaviour
{
    [Header("Territory")]
    [SerializeField] IslandDefinition island;
    [SerializeField] string zoneName = "Forrest";
    [SerializeField] string terrainName = "Island1_Terrain";
    [SerializeField] float  roamRadius = 90f;          // how far across the zone it will pick targets

    [Header("Movement")]
    [SerializeField] float speed = 1.2f;
    [SerializeField] float turnDegPerSec = 90f;
    [SerializeField] Vector2 pauseRange = new Vector2(1.5f, 4f);   // idle dwell between strolls

    Terrain terrain; Animator anim; readonly ZoneSampler zone = new ZoneSampler();
    Vector3 home, target; float resume; bool paused;

    // Called by the Island Builder when it places this mob into a zone.
    public void Configure(IslandDefinition def, string zoneName, string terrainName)
    { island = def; this.zoneName = zoneName; this.terrainName = terrainName; }

    void Start()
    {
        home = transform.position;
        anim = GetComponentInChildren<Animator>();
        var go = !string.IsNullOrEmpty(terrainName) ? GameObject.Find(terrainName) : null;
        terrain = go != null ? go.GetComponent<Terrain>() : (Terrain.activeTerrain ?? Object.FindFirstObjectByType<Terrain>());
        if (!zone.Bind(ref island, terrain, zoneName))
            Debug.LogWarning($"[ZoneRoamer] '{name}' couldn't bind zone '{zoneName}' — it'll wander a {roamRadius}m radius of its spawn instead.");
        Pick();
    }

    void Update()
    {
        if (paused)
        {
            SetSpeed(0f);
            if (Time.time >= resume) { paused = false; Pick(); }
            return;
        }
        if (Flat(target - transform.position).magnitude < 1.2f)   // arrived → idle a beat, then pick again
        {
            paused = true; resume = Time.time + Random.Range(pauseRange.x, pauseRange.y); SetSpeed(0f);
            return;
        }
        MoveToward(target);
    }

    void Pick()
    {
        for (int i = 0; i < 24; i++)
        {
            Vector2 r = Random.insideUnitCircle * roamRadius;
            Vector3 c = home + new Vector3(r.x, 0f, r.y);
            if (InZone(c)) { target = c; return; }
        }
        target = home;
    }

    bool InZone(Vector3 w) => zone.Ready ? zone.Contains(w) : Flat(w - home).magnitude <= roamRadius;

    void MoveToward(Vector3 t)
    {
        Vector3 dir = Flat(t - transform.position);
        if (dir.sqrMagnitude > 0.0025f)
        {
            dir.Normalize();
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnDegPerSec * Time.deltaTime);
            float scale = Mathf.Clamp(Vector3.Dot(transform.forward, dir) + 0.6f, 0.35f, 1f);   // arc through turns
            Vector3 next = transform.position + dir * speed * scale * Time.deltaTime;
            if (InZone(next)) transform.position = next;            // never wander out of the zone
            SetSpeed(speed * scale);
        }
        Clamp();
    }

    void Clamp()
    {
        if (terrain == null) return;
        var p = transform.position; p.y = terrain.SampleHeight(p) + terrain.transform.position.y; transform.position = p;
    }

    void SetSpeed(float s) { if (anim != null && anim.runtimeAnimatorController != null) anim.SetFloat("Speed", s); }
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
