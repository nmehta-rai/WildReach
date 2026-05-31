using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] float walkSpeed = 10f;
    [SerializeField] float runSpeed = 18f;
    [SerializeField] float jumpForce = 6.5f;            // ~2m apex (char is 2m) — was 10 = a slow 5m float
    [SerializeField] float fallMultiplier = 1.7f;       // extra gravity while falling — softened (2.5 slammed down)
    [SerializeField] float lowJumpMultiplier = 2f;      // extra gravity on the way up if Space is released → variable height
    [SerializeField] float maxFallSpeed = 14f;          // cap descent so the landing never slams
    [SerializeField] Transform cameraTransform;

    Rigidbody rb;
    StaminaSystem stamina;

    bool jumpQueued;
    bool isGrounded = true;

    Transform groundTf, lastGroundTf;   // moving-platform carry (e.g. riding the tortoise)
    Vector3 lastGroundPos;

    public bool Enabled { get; set; } = true;
    public bool IsGrounded => isGrounded;
    public float WalkSpeed => walkSpeed;
    public float RunSpeed  => runSpeed;

    /// Intended planar locomotion speed (0 / walk / run) — drives the animation
    /// without the jitter that measured rigidbody velocity picks up on uneven ground.
    public float PlanarSpeed { get; private set; }

    /// Fired the instant a jump impulse is applied (drives the Launch animation).
    public event System.Action Jumped;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        stamina = GetComponent<StaminaSystem>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;                    // smooth visual motion (no landing jitter)
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;              // ContinuousDynamic snagged the capsule on terrain seams → mid-stride stops; the softened fall makes Discrete safe
    }

    void Update()
    {
        isGrounded = Physics.Raycast(transform.position, Vector3.down, out var groundHit, 1.15f);
        groundTf = isGrounded ? groundHit.transform : null;

        if (!Enabled) return;

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
            jumpQueued = true;
    }

    void FixedUpdate()
    {
        if (!Enabled) { PlanarSpeed = 0f; return; }

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        bool moving = Mathf.Abs(h) > 0.05f || Mathf.Abs(v) > 0.05f;
        bool running = Input.GetKey(KeyCode.LeftShift) && moving && !stamina.IsEmpty;

        float speed = running ? runSpeed : walkSpeed;
        PlanarSpeed = moving ? speed : 0f;

        Vector3 camForward = Vector3.Scale(cameraTransform.forward, new Vector3(1, 0, 1)).normalized;
        Vector3 camRight = Vector3.Scale(cameraTransform.right, new Vector3(1, 0, 1)).normalized;
        Vector3 move = (camForward * v + camRight * h).normalized * speed;

        rb.linearVelocity = new Vector3(move.x, rb.linearVelocity.y, move.z);

        if (jumpQueued)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            jumpQueued = false;
            Jumped?.Invoke();
        }

        // "Better jumping": extra gravity on descent for a snappy fall, and extra gravity on the rise
        // when Space is let go, so a tap is a short hop and a hold is a full jump. Physics.gravity.y is negative.
        // Shape the jump arc only in the AIR — applying extra gravity while grounded jams the capsule
        // down into uneven terrain and contributed to the mid-walk stalls.
        float dt = Time.fixedDeltaTime;
        if (!isGrounded)
        {
            if (rb.linearVelocity.y < 0)
                rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1f) * dt;
            else if (rb.linearVelocity.y > 0 && !Input.GetKey(KeyCode.Space))
                rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1f) * dt;

            if (rb.linearVelocity.y < -maxFallSpeed)
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, -maxFallSpeed, rb.linearVelocity.z);
        }

        if (running)
            stamina.DrainRun(Time.fixedDeltaTime);
        else
            stamina.Tick(Time.fixedDeltaTime, false);

        if (moving)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(move), 0.15f);

        // Moving-platform carry: if the ground under us moved this step, move with it (horizontal only).
        if (groundTf != null && groundTf == lastGroundTf)
        {
            Vector3 d = groundTf.position - lastGroundPos; d.y = 0f;
            if (d.sqrMagnitude < 4f) rb.position += d;     // ignore teleports
        }
        lastGroundTf = groundTf;
        if (groundTf != null) lastGroundPos = groundTf.position;
    }

    public void SetVelocity(Vector3 v) => rb.linearVelocity = v;
    public void SetGravity(bool on) => rb.useGravity = on;
}
