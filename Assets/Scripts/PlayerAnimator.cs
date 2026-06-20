using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    Animator animator;
    Rigidbody rb;
    PlayerMovement movement;
    ClimbingSystem climbing;
    SwimSystem swim;
    PlayerHurt hurt;

    [SerializeField] float hardLandDrop = 4.5f;   // only a fall taller than this plays Hard Landing (jumps reach ~3.2m)
    bool airborne;
    float peakY;

    Transform hips;                 // the climb travel lives on this bone (Unity Generic won't lift it to deltaPosition)
    Vector3 hipsRest, hipsPrev;
    bool climbTracking;

    void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponentInParent<Rigidbody>();
        movement = GetComponentInParent<PlayerMovement>();
        climbing = GetComponentInParent<ClimbingSystem>();
        swim = GetComponentInParent<SwimSystem>();
        hurt = GetComponentInParent<PlayerHurt>();
        hips = FindDeep(transform, "hips");
    }

    static Transform FindDeep(Transform t, string contains)
    {
        if (t.name.ToLower().Contains(contains)) return t;
        foreach (Transform c in t) { var r = FindDeep(c, contains); if (r != null) return r; }
        return null;
    }

    void OnEnable()  { if (movement != null) movement.Jumped += OnJump; }
    void OnDisable() { if (movement != null) movement.Jumped -= OnJump; }

    void OnJump() => animator.SetTrigger("Jump");

    // MANUAL climb root motion. Unity Generic leaves the climb travel on the Hips BONE (195 position curves) and
    // refuses to lift it into deltaPosition (always 0). So each frame we read the Hips' own movement, hand the
    // delta to the ClimbingSystem to drive the CAPSULE, then pin the Hips back to its climb-start position so the
    // visual doesn't ALSO translate (no double-move). Result: the body climbs the wall exactly as the limbs pull.
    void LateUpdate()
    {
        if (climbing == null || !climbing.IsClimbing || hips == null) { climbTracking = false; return; }
        Vector3 now = hips.localPosition;
        if (!climbTracking) { hipsRest = now; hipsPrev = now; climbTracking = true; }   // first climb frame: anchor
        else
        {
            Vector3 delta = now - hipsPrev;
            if (delta.magnitude < 0.3f)   // reject the loop-wrap snap (~the full travel); per-frame climb is tiny
                climbing.AddRootMotion(hips.parent.TransformVector(delta));
            hipsPrev = now;
        }
        hips.localPosition = hipsRest;     // remove the climb (and bob) from the visual — the capsule carries it instead
    }

    void Update()
    {
        // Normalise the player's INTENDED planar speed to 0 (idle) / 0.5 (walk) / 1 (run).
        // Using intent (not measured rb velocity) keeps the walk smooth on uneven ground,
        // where collisions would otherwise jitter the measured speed and stutter the clip.
        float planar = movement.PlanarSpeed;
        float walk = Mathf.Max(0.01f, movement.WalkSpeed);
        float run  = Mathf.Max(walk + 0.01f, movement.RunSpeed);

        float norm;
        if (planar <= 0.05f)      norm = 0f;
        else if (planar <= walk)  norm = Mathf.Lerp(0f, 0.5f, planar / walk);
        else                      norm = Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(walk, run, planar));

        animator.SetFloat("Speed", norm, 0.1f, Time.deltaTime);   // small damp = smooth blend
        animator.SetFloat("VerticalVelocity", rb.linearVelocity.y);
        bool grounded = movement.IsGrounded;
        animator.SetBool("IsGrounded", grounded);

        // Hard-landing by fall DISTANCE (robust — landing-frame velocity is noisy because the ground-ray leads
        // the collider). Track the peak height while airborne; on touchdown, only a drop > hardLandDrop plays it.
        float y = rb.position.y;
        if (!grounded)
        {
            if (!airborne) { airborne = true; peakY = y; }
            else if (y > peakY) peakY = y;
        }
        else if (airborne)
        {
            airborne = false;
            float drop = peakY - y;
            if (drop > hardLandDrop) { animator.SetTrigger("HardLand"); movement.LockGround(0.5f); if (hurt != null) hurt.Damage(1); }   // pin + red flash + lose a heart
            else animator.SetTrigger("Landed");   // soft touchdown — exits the jump clip in sync with the physics
            Debug.Log($"[Land] drop {drop:0.0}m -> {(drop > hardLandDrop ? "HARD" : "soft")}");
        }

        // Swimming: tread (idle) ↔ swim (moving) by the same normalised speed.
        bool isSwimming = swim != null && swim.IsSwimming;
        animator.SetBool("IsSwimming", isSwimming);
        if (isSwimming) animator.SetFloat("SwimSpeed", norm, 0.1f, Time.deltaTime);

        // Climbing: ClimbDir picks down/cling/up; ClimbMag scales playback so he freezes
        // (clings) when not moving and animates only while actually climbing.
        bool isClimbing = climbing != null && climbing.IsClimbing;
        animator.SetBool("IsClimbing", isClimbing);
        if (isClimbing)
        {
            float dir = climbing.ClimbVertical;                   // -1 … 1
            float strafe = climbing.ClimbHorizontal;              // -1 (left) … 1 (right)
            animator.SetFloat("ClimbDir", dir, 0.08f, Time.deltaTime);
            animator.SetFloat("ClimbStrafe", strafe, 0.08f, Time.deltaTime);
            animator.SetFloat("ClimbMag", Mathf.Max(Mathf.Abs(dir), Mathf.Abs(strafe)));
        }
    }
}
