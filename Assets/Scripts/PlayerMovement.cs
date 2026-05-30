using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] float walkSpeed = 10f;
    [SerializeField] float runSpeed = 18f;
    [SerializeField] float jumpForce = 10f;
    [SerializeField] float fallMultiplier = 90f;
    [SerializeField] Transform cameraTransform;

    Rigidbody rb;
    StaminaSystem stamina;

    bool jumpQueued;
    bool isGrounded = true;

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
    }

    void Update()
    {
        isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.15f);

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

        if (rb.linearVelocity.y < 0)
            rb.AddForce(Vector3.down * fallMultiplier, ForceMode.Acceleration);

        if (running)
            stamina.DrainRun(Time.fixedDeltaTime);
        else
            stamina.Tick(Time.fixedDeltaTime, false);

        if (moving)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(move), 0.15f);
    }

    public void SetVelocity(Vector3 v) => rb.linearVelocity = v;
    public void SetGravity(bool on) => rb.useGravity = on;
}
