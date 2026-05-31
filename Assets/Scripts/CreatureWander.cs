using UnityEngine;

// Slow, peaceful roaming for the Ember Shell Tortoise (and future land creatures).
// Picks a random reachable land point, ambles toward it on the Walk clip, pauses on Idle, repeats.
// Ground-conforms to the terrain each frame and never wanders into the sea or up steep slopes.
// Transform-driven (no physics) — it's a calm wildlife mob, not a physics body.
public class CreatureWander : MonoBehaviour
{
    [SerializeField] float speed       = 0.6f;   // m/s — a giant tortoise plods
    [SerializeField] float turnSpeed   = 70f;    // deg/s
    [SerializeField] float roamRadius  = 35f;    // how far it picks new targets
    [SerializeField] float arriveDist  = 1.2f;
    [SerializeField] Vector2 pauseRange = new Vector2(3f, 8f);
    [SerializeField] float seaLevel    = 0.33f;  // set by Setup Creature from the Water object
    [SerializeField] float maxSlope    = 30f;    // degrees — won't target steeper ground
    [SerializeField] float probeRadius = 0.45f;  // obstacle avoidance — how fat the look-ahead is
    [SerializeField] float probeDist   = 1.8f;   // how far ahead it checks for trees/player/walls
    [SerializeField] float bodyMidY    = 0.35f;  // height to cast the probe from

    Animator anim;
    Terrain terrain;
    Vector3 target;
    float pauseTimer;
    bool paused;

    public void SetSeaLevel(float y) => seaLevel = y;

    void Start()
    {
        anim = GetComponent<Animator>();
        terrain = Terrain.activeTerrain;
        Conform();
        PickTarget();
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (paused)
        {
            if (anim) anim.SetFloat("Speed", 0f, 0.2f, dt);
            pauseTimer -= dt;
            if (pauseTimer <= 0f) { paused = false; PickTarget(); }
            Conform();
            return;
        }

        Vector3 to = target - transform.position; to.y = 0f;
        if (to.magnitude < arriveDist)
        {
            paused = true;
            pauseTimer = Random.Range(pauseRange.x, pauseRange.y);
            return;
        }

        // obstacle ahead (tree/player/wall/ship) → turn toward a fresh target, don't push through it
        bool blocked = Blocked();
        if (blocked) { PickTarget(); to = target - transform.position; to.y = 0f; }

        if (to.sqrMagnitude > 0.001f)
        {
            Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * dt);
        }
        if (!blocked) transform.position += transform.forward * speed * dt;   // only advance when the path's clear
        if (anim) anim.SetFloat("Speed", 1f, 0.25f, dt);
        Conform();
    }

    // True if something solid is right in front (ignores itself and the terrain floor).
    bool Blocked()
    {
        Vector3 o = transform.position + Vector3.up * bodyMidY;
        foreach (var h in Physics.SphereCastAll(o, probeRadius, transform.forward, probeDist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;   // itself
            if (h.collider is TerrainCollider) continue;               // the ground, not an obstacle
            return true;
        }
        return false;
    }

    // Snap to the terrain surface (model origin is at the feet), never below the waterline.
    void Conform()
    {
        if (terrain == null) return;
        float h = terrain.SampleHeight(transform.position) + terrain.transform.position.y;
        var p = transform.position;
        p.y = Mathf.Max(h, seaLevel);
        transform.position = p;
    }

    // Find a nearby spot that's on dry land and not too steep.
    void PickTarget()
    {
        if (terrain == null) { target = transform.position; return; }
        for (int i = 0; i < 24; i++)
        {
            Vector2 r = Random.insideUnitCircle * roamRadius;
            Vector3 c = new Vector3(transform.position.x + r.x, 0f, transform.position.z + r.y);
            float h = terrain.SampleHeight(c) + terrain.transform.position.y;
            if (h < seaLevel + 0.5f) continue;                          // in the sea — skip

            var td = terrain.terrainData;
            float nx = Mathf.InverseLerp(terrain.transform.position.x, terrain.transform.position.x + td.size.x, c.x);
            float nz = Mathf.InverseLerp(terrain.transform.position.z, terrain.transform.position.z + td.size.z, c.z);
            Vector3 n = td.GetInterpolatedNormal(nx, nz);
            if (Vector3.Angle(n, Vector3.up) > maxSlope) continue;       // too steep — skip

            target = new Vector3(c.x, h, c.z);
            return;
        }
        target = transform.position;   // give up this round, idle in place
    }
}
