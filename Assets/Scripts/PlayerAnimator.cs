using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    Animator animator;
    Rigidbody rb;
    PlayerMovement movement;
    ClimbingSystem climbing;
    SwimSystem swim;

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
        animator.SetBool("IsGrounded", movement.IsGrounded);

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
            animator.SetFloat("ClimbDir", dir, 0.08f, Time.deltaTime);
            animator.SetFloat("ClimbMag", Mathf.Abs(dir));
        }
    }
}
