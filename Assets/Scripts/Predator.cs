using UnityEngine;

// Zone-bound predator AI. Roams freely across a painted zone (his "territory"), senses the player within
// detectRadius, chases, and pounces — but never leaves the zone, so he can't walk off a cliff edge or
// follow you past his territory. Drives Animator params ("Speed" float, "Attack" trigger).
// Movement is terrain-clamped steering with turn-then-go so reversals pivot instead of snapping — no NavMesh.
public class Predator : MonoBehaviour
{
    [Header("Territory")]
    [SerializeField] IslandDefinition island;          // the island whose zone map defines his territory
    [SerializeField] string roamZone = "peak plateau"; // the painted zone he roams + chases within
    [SerializeField] float  roamRadius = 60f;          // how far from home to scatter prowl targets (zone-filtered)

    [Header("Senses / ranges (metres)")]
    [SerializeField] float detectRadius = 22f;
    [SerializeField] float leashRadius  = 45f;     // fallback bound only if the zone map can't be read

    [Header("Movement")]
    [SerializeField] float prowlSpeed = 2f;
    [SerializeField] float chaseSpeed = 7.5f;     // clearly faster than the player's run
    [SerializeField] float turnDegPerSec = 120f;   // capped turn rate — he arcs through turns instead of whipping

    [Header("Attack")]
    [SerializeField] int   attackDamage = 2;       // hearts per pounce — strong mob; 2 hits drops a fresh player
    [SerializeField] float pounceRange  = 3f;      // stop and wind up at this distance
    [SerializeField] float windUpTime   = 0.2f;    // telegraph before he springs (your dodge window)
    [SerializeField] float strikeDelay  = 0.18f;   // crouch portion of the clip — he holds, THEN leaps
    [SerializeField] float pounceSpeed  = 9f;      // lunge speed once he leaps
    [SerializeField] float pounceTime   = 0.55f;   // total strike length (must exceed strikeDelay)
    [SerializeField] float recoverTime  = 0.7f;    // back-off / cooldown after a pounce
    [SerializeField] float contactPad   = 0.2f;    // surface-to-surface gap that still counts as a hit

    enum State { Prowl, Chase, WindUp, Pounce, Recover, Return }
    State state = State.Prowl;

    Transform player; HealthSystem playerHp; Collider playerCol; PlayerHurt playerHurt; Animator anim; Terrain terrain;
    Collider body;
    Vector3 home, prowlTarget, lungeDir; float stateEnter, nextProwl; bool hitDone;
    readonly ZoneSampler zone = new ZoneSampler();

    public float ChaseSpeed => chaseSpeed;   // a pet reads this to move at the leopard's own pace

    void Enter(State s) { state = s; stateEnter = Time.time; }

    // Bind this predator to a territory (called by the Island Builder when assigning a mob to a zone).
    public void Configure(IslandDefinition def, string zone) { island = def; roamZone = zone; }

    void Start()
    {
        if (pounceRange <= 0.1f)  pounceRange = 3f;
        if (windUpTime  <= 0.01f) windUpTime  = 0.2f;
        if (strikeDelay <= 0.01f) strikeDelay = 0.18f;
        if (pounceSpeed <= 0.1f)  pounceSpeed = 9f;
        if (recoverTime <= 0.01f) recoverTime = 0.7f;
        float needed = strikeDelay + pounceRange / Mathf.Max(pounceSpeed, 1f) + 0.05f;
        if (pounceTime < needed) pounceTime = needed;

        home = transform.position;
        anim = GetComponentInChildren<Animator>();
        terrain = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null)
        {
            player = pm.transform;
            playerHp = pm.GetComponent<HealthSystem>();
            playerCol = pm.GetComponentInChildren<Collider>();
            playerHurt = pm.GetComponent<PlayerHurt>() ?? pm.gameObject.AddComponent<PlayerHurt>();
        }
        SetupZone();
        FitBody();
        PickProwl();
    }

    void SetupZone()
    {
        if (!zone.Bind(ref island, terrain, roamZone))
            Debug.LogWarning($"[Predator] Couldn't bind roam zone '{roamZone}' — falling back to a {leashRadius}m leash. " +
                             "Assign the Island asset + zone name on the Predator to bound him to the plateau.");
    }

    // Is this world position inside his territory? (Falls back to a home-radius test if the zone map is unreadable.)
    bool InZone(Vector3 w) => zone.Ready ? zone.Contains(w) : Flat(w - home).magnitude <= leashRadius;
    bool PlayerInZone => player != null && InZone(player.position);

    void FitBody()
    {
        body = GetComponent<CapsuleCollider>();
        var skin = GetComponentInChildren<SkinnedMeshRenderer>();
        if (skin == null || skin.sharedMesh == null) return;
        var cap = body as CapsuleCollider ?? gameObject.AddComponent<CapsuleCollider>();
        var ls  = skin.transform.lossyScale;
        var sz  = Vector3.Scale(skin.sharedMesh.bounds.size, ls);
        var ctr = skin.transform.TransformPoint(skin.sharedMesh.bounds.center);
        cap.direction = sz.z >= sz.x ? 2 : 0;
        cap.height = Mathf.Max(sz.x, sz.z);
        cap.radius = Mathf.Clamp(Mathf.Max(Mathf.Min(sz.x, sz.z), sz.y) * 0.45f, 0.2f, 1.2f);
        cap.center = transform.InverseTransformPoint(ctr);
        cap.isTrigger = true;
        body = cap;
    }

    bool Touching()
    {
        if (body == null || player == null) return false;
        Vector3 pc    = playerCol != null ? playerCol.bounds.center : player.position;
        Vector3 onLeo = body.ClosestPoint(pc);
        Vector3 onYou = playerCol != null ? playerCol.ClosestPoint(onLeo) : pc;
        return Vector3.Distance(onLeo, onYou) <= contactPad;
    }

    void Update()
    {
        if (player == null) return;
        float dist = Flat(player.position - transform.position).magnitude;
        float t = Time.time - stateEnter;
        Vector3 toPlayer = Flat(player.position - transform.position).normalized;

        switch (state)
        {
            case State.Prowl:
                if (dist < detectRadius && PlayerInZone) { Enter(State.Chase); break; }
                if (Time.time > nextProwl || Flat(prowlTarget - transform.position).magnitude < 1.2f) PickProwl();
                MoveToward(prowlTarget, prowlSpeed);
                break;

            case State.Chase:
                if (!PlayerInZone) { Enter(State.Return); break; }      // you left his territory — he gives up
                if (dist <= pounceRange) { Enter(State.WindUp); break; }
                MoveToward(player.position, chaseSpeed);                 // zone-bounded: he stops at the cliff edge, won't follow off
                break;

            case State.WindUp:
                SetSpeed(0f); FaceDir(toPlayer);
                if (!PlayerInZone || dist > pounceRange * 1.7f) { Enter(State.Chase); break; }
                if (t >= windUpTime)
                {
                    lungeDir = toPlayer; hitDone = false;
                    if (HasAnim) anim.SetTrigger("Attack");
                    Enter(State.Pounce);
                }
                break;

            case State.Pounce:
                FaceDir(lungeDir);
                SetSpeed(0f);
                if (t >= strikeDelay)
                {
                    Vector3 next = transform.position + lungeDir * pounceSpeed * Time.deltaTime;
                    if (!Touching() && InZone(next)) { transform.position = next; ClampToGround(); }  // never leap off the edge
                    if (!hitDone && Touching() && playerHp != null && !playerHp.IsDead)
                    {
                        if (playerHurt != null) playerHurt.Hit(attackDamage, transform.position);
                        else playerHp.Damage(attackDamage);
                        hitDone = true;
                    }
                }
                if (t >= pounceTime) Enter(State.Recover);
                break;

            case State.Recover:
                SetSpeed(0f); FaceDir(toPlayer);
                if (t < 0.4f && dist < pounceRange)
                {
                    Vector3 next = transform.position + Flat(transform.position - player.position).normalized * chaseSpeed * 0.7f * Time.deltaTime;
                    if (InZone(next)) { transform.position = next; ClampToGround(); }   // don't back off the cliff
                }
                if (t >= recoverTime)
                {
                    if (!PlayerInZone)            Enter(State.Return);
                    else if (dist <= pounceRange) Enter(State.WindUp);
                    else if (dist <  detectRadius) Enter(State.Chase);
                    else                          Enter(State.Prowl);
                }
                break;

            case State.Return:
                if (Flat(transform.position - home).magnitude < 1.5f) { Enter(State.Prowl); PickProwl(); break; }
                if (dist < detectRadius * 0.7f && PlayerInZone) { Enter(State.Chase); break; }
                MoveToward(home, chaseSpeed * 0.8f);
                break;
        }
    }

    // Snap back to the lair and disengage — called on player respawn so it can't camp the spawn.
    public void GoHome()
    {
        transform.position = home;
        Enter(State.Prowl);
        PickProwl();
    }

    // Wander the whole territory: scatter a target anywhere within roamRadius of home that lands in-zone.
    void PickProwl()
    {
        nextProwl = Time.time + Random.Range(3f, 6f);
        for (int i = 0; i < 24; i++)
        {
            Vector2 r = Random.insideUnitCircle * roamRadius;
            Vector3 cand = home + new Vector3(r.x, 0f, r.y);
            if (InZone(cand)) { prowlTarget = cand; return; }
        }
        prowlTarget = home;   // couldn't find an in-zone point — head back to the centre
    }

    // Turn-then-go: face the target, and only advance as far as we're aligned to it, so a reversal pivots
    // smoothly instead of snapping the body around. Zone-bounded — a step that would leave the zone is refused.
    void MoveToward(Vector3 target, float speed)
    {
        Vector3 dir = Flat(target - transform.position);
        if (dir.sqrMagnitude > 0.0025f)
        {
            dir.Normalize();
            FaceDir(dir);
            // keep moving while turning so he banks through an arc (a hard stop-and-pivot looks like skating)
            float scale = Mathf.Clamp(Vector3.Dot(transform.forward, dir) + 0.6f, 0.35f, 1f);
            Vector3 next = transform.position + dir * speed * scale * Time.deltaTime;
            if (InZone(next)) transform.position = next;
            SetSpeed(speed * scale);
        }
        else SetSpeed(0f);
        ClampToGround();
    }

    void FaceDir(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.001f) return;
        // capped angular speed (not an exponential snap) so he banks through turns at an animal-like rate
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnDegPerSec * Time.deltaTime);
    }

    bool HasAnim => anim != null && anim.runtimeAnimatorController != null;
    void SetSpeed(float s) { if (HasAnim) anim.SetFloat("Speed", s); }

    void ClampToGround()
    {
        if (terrain == null) return;
        var p = transform.position;
        p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
        transform.position = p;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0.3f, 0.3f, 1); Gizmos.DrawWireSphere(transform.position, detectRadius);
        Gizmos.color = new Color(1, 0.5f, 0.1f, 1); Gizmos.DrawWireSphere(transform.position, pounceRange);
    }
}
