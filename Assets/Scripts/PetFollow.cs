using UnityEngine;

// Preview companion for tuning animations. It MIRRORS THE PLAYER'S STATE onto its own Animator — idle when you
// idle, walk when you walk, run when you run, jump when you jump — but it MOVES at the CREATURE'S OWN gait pace
// (not yours), so you can read the true pacing of each clip without foot-skating or catch-up jitter. When you idle
// it ambles back to your side and settles. (Later, wild creatures do their own thing; this is the test harness.)
[DisallowMultipleComponent]
public class PetFollow : MonoBehaviour
{
    [SerializeField] float walkPace      = 1.0f;   // the creature's own walking speed (set at spawn) — match the
                                                   // walk clip's stride so the feet lock instead of skating
    [SerializeField] float runMultiplier = 2.5f;   // run pace = walkPace * this (match the run clip's stride)
    [SerializeField] float turnDegPerSec = 260f;
    [SerializeField] float hopHeight     = 1.2f;   // physical hop when you jump (until a jump clip exists)
    [SerializeField] float sideOffset    = 2.0f;   // how far to your side it trails
    [SerializeField] float backOffset    = 0.5f;
    [SerializeField] float leashKeep     = 2f;     // within this it moves at its true gait pace (feet lock)
    [SerializeField] float leashGain     = 8f;     // beyond it, speed up to catch back into view

    public void SetMaxSpeed(float s) { if (s > 0.01f) walkPace = s; }

    Transform player; PlayerMovement move; Animator anim; Terrain terrain;
    float hopVel, hopY;

    void Start()
    {
        var pm = Object.FindAnyObjectByType<PlayerMovement>();
        if (pm != null) { player = pm.transform; move = pm; move.Jumped += OnPlayerJump; }
        anim = GetComponentInChildren<Animator>(true);
        terrain = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
        if (anim != null) anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    void OnDestroy() { if (move != null) move.Jumped -= OnPlayerJump; }

    void OnPlayerJump()
    {
        if (HasAnim && HasParam("Jump")) anim.SetTrigger("Jump");   // plays the jump clip once we have one
        hopVel = Mathf.Sqrt(2f * 9.81f * hopHeight);                // physical hop in the meantime
    }

    void Update()
    {
        if (player == null || move == null) return;

        // --- decide STATE from the player, and PACE from the creature ---
        float ps = move.PlanarSpeed;                       // player's intended speed: 0 / walk / run
        bool playerMoving  = ps > 0.5f;
        bool playerRunning = ps >= move.RunSpeed - 0.5f;
        float runPace = walkPace * runMultiplier;

        Vector3 spot = player.position + player.right * sideOffset - player.forward * backOffset;
        Vector3 cur  = transform.position;
        Vector3 toSpot = new Vector3(spot.x - cur.x, 0f, spot.z - cur.z);
        float dist = toSpot.magnitude;

        float pace, animSpeed;
        if (playerMoving)                                  // mirror the player's gait + travel at the creature's pace
        {
            pace = playerRunning ? runPace : walkPace;
            animSpeed = ps;                                // 0/walk/run → pet's controller picks the matching state
        }
        else if (dist > 1.5f)                              // player idle but we're behind → amble back at walk
        {
            pace = walkPace; animSpeed = move.WalkSpeed;
        }
        else { pace = 0f; animSpeed = 0f; }                // settled beside you → idle

        // LEASH: you move much faster than these creatures, so without this the pet vanishes behind you and you
        // never see the run. If it falls behind, speed it up to catch back into view (a little foot-slide during
        // the catch-up is the trade for being able to watch the gait at all).
        if (pace > 0.01f && dist > leashKeep)
            pace = Mathf.Max(pace, pace + (dist - leashKeep) * leashGain);

        // --- move at exactly `pace` while travelling (constant speed = the gait reads true) ---
        Vector3 newPos = cur;
        if (pace > 0.01f && dist > 0.05f)
        {
            Vector3 step = toSpot.normalized * Mathf.Min(pace * Time.deltaTime, dist);
            newPos += step;
            FaceDir(step);
        }
        else FaceDir(Flat(player.forward));                // face the way you face when settled

        // ground clamp + the physical hop arc
        float groundY = terrain != null ? terrain.SampleHeight(newPos) + terrain.transform.position.y : cur.y;
        if (hopVel != 0f || hopY > 0f)
        {
            hopY += hopVel * Time.deltaTime; hopVel -= 9.81f * Time.deltaTime;
            if (hopY <= 0f) { hopY = 0f; hopVel = 0f; }
        }
        newPos.y = groundY + hopY;
        transform.position = newPos;

        if (HasAnim)
        {
            anim.SetFloat("Speed", animSpeed, 0.08f, Time.deltaTime);   // mirror state, lightly damped
            if (HasParam("Airborne")) anim.SetBool("Airborne", !move.IsGrounded);
        }
    }

    bool HasAnim => anim != null && anim.runtimeAnimatorController != null;
    bool HasParam(string n) { foreach (var pr in anim.parameters) if (pr.name == n) return true; return false; }

    void FaceDir(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnDegPerSec * Time.deltaTime);
    }
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
