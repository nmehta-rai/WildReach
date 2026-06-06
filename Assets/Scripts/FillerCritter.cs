using UnityEngine;

// Cheap ambient FILLER: a small low-poly critter that wanders a little, bobs, and occasionally hops — all via
// the transform (NO Animator, NO skinned mesh), so hundreds run for almost nothing. Optionally zone-bound so
// it stays in its biome. This is the opposite of a main-actor creature (skinned + IK-animated); use it for the
// background life — tuftlings, beetles, hoppers — that makes the forest feel alive.
[DisallowMultipleComponent]
public class FillerCritter : MonoBehaviour
{
    [SerializeField] IslandDefinition island;
    [SerializeField] string zoneName = "Forrest";
    [SerializeField] string terrainName = "Island1_Terrain";
    [SerializeField] float wanderRadius = 5f;
    [SerializeField] float speed = 0.7f;
    [SerializeField] float turnDegPerSec = 220f;
    [SerializeField] float bobHeight = 0.04f, bobSpeed = 5f;
    [SerializeField] float hopChance = 0.004f, hopHeight = 0.25f;

    Terrain terrain; readonly ZoneSampler zone = new ZoneSampler();
    Vector3 home, target; float phase, hopVel, hopY; bool zoneOk;

    public void Configure(IslandDefinition def, string zone, string terrain) { island = def; zoneName = zone; terrainName = terrain; }

    void Start()
    {
        home = transform.position;
        var go = !string.IsNullOrEmpty(terrainName) ? GameObject.Find(terrainName) : null;
        terrain = go != null ? go.GetComponent<Terrain>() : (Terrain.activeTerrain ?? Object.FindFirstObjectByType<Terrain>());
        zoneOk = zone.Bind(ref island, terrain, zoneName);
        phase  = home.x * 7.13f + home.z * 3.7f;   // per-spot phase so they don't bob in lockstep
        Pick();
    }

    void Pick()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector2 r = Random.insideUnitCircle * wanderRadius;
            Vector3 c = home + new Vector3(r.x, 0f, r.y);
            if (!zoneOk || zone.Contains(c)) { target = c; return; }
        }
        target = home;
    }

    void Update()
    {
        Vector3 cur = transform.position;
        Vector3 flat = new Vector3(target.x - cur.x, 0f, target.z - cur.z);
        if (flat.sqrMagnitude > 0.05f)
        {
            Vector3 dir = flat.normalized;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnDegPerSec * Time.deltaTime);
            Vector3 next = cur + dir * speed * Time.deltaTime;
            if (!zoneOk || zone.Contains(next)) cur = next;
        }
        else if (Random.value < 0.012f) Pick();      // arrived → mostly idle/bob, occasionally amble somewhere new

        if (hopY <= 0f && Random.value < hopChance) hopVel = Mathf.Sqrt(2f * 9.81f * hopHeight);
        if (hopVel != 0f || hopY > 0f) { hopY += hopVel * Time.deltaTime; hopVel -= 9.81f * Time.deltaTime; if (hopY <= 0f) { hopY = 0f; hopVel = 0f; } }

        float groundY = terrain != null ? terrain.SampleHeight(cur) + terrain.transform.position.y : cur.y;
        float bob = Mathf.Abs(Mathf.Sin(Time.time * bobSpeed + phase)) * bobHeight;
        cur.y = groundY + bob + hopY;
        transform.position = cur;
    }
}
