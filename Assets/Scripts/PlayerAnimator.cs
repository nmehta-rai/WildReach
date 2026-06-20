using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    Animator animator;
    Rigidbody rb;
    PlayerMovement movement;
    ClimbingSystem climbing;
    SwimSystem swim;

    [SerializeField] float hardLandDrop = 4.5f;   // only a fall taller than this plays Hard Landing (jumps reach ~3.2m)
    bool airborne;
    float peakY;

    void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponentInParent<Rigidbody>();
        movement = GetComponentInParent<PlayerMovement>();
        climbing = GetComponentInParent<ClimbingSystem>();
        swim = GetComponentInParent<SwimSystem>();
    }

    void OnEnable()  { if (movement != null) movement.Jumped += OnJump; }
    void OnDisable() { if (movement != null) movement.Jumped -= OnJump; }

    void OnJump() => animator.SetTrigger("Jump");

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
            if (drop > hardLandDrop) { animator.SetTrigger("HardLand"); movement.LockGround(0.5f); }   // pin during the heavy land
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
