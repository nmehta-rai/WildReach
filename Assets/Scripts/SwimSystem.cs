using UnityEngine;

// Keeps the player from falling through the world once they leave the island.
// When the player drops to the water surface (and isn't standing on solid ground),
// gravity is cut and a buoyancy spring holds them at the waterline so they float and
// can swim around with normal WASD. No swim animation yet — pure mechanic.
// Runs AFTER PlayerMovement (exec order 100) so it has the final say on vertical velocity.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Rigidbody), typeof(PlayerMovement))]
public class SwimSystem : MonoBehaviour
{
    [SerializeField] float waterLevel  = 1.5f;   // set by Setup Sailing from SetupWater.SeaLevel
    [SerializeField] float floatOffset = -0.3f;  // TREADING: pivot (≈body centre) this far below surface → water at ~chest
    [SerializeField] float swimOffset  = -0.1f;  // SWIMMING: pivot near the surface → body half-submerged, head/back above
    [SerializeField] float buoyancy    = 6f;
    [SerializeField] float maxBob       = 3f;     // clamp so a fast fall doesn't launch the player

    Rigidbody rb;
    ClimbingSystem climbing;
    PlayerMovement movement;
    float curOffset;
    bool swimming;
    public bool IsSwimming => swimming;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        climbing = GetComponent<ClimbingSystem>();
        movement = GetComponent<PlayerMovement>();
        curOffset = floatOffset;
    }

    public void SetWaterLevel(float y) => waterLevel = y;
    public void SetFloatOffset(float o) => floatOffset = o;
    public void SetSwimOffset(float o) => swimOffset = o;

    void FixedUpdate()
    {
        // Climbing owns the body while it's active — don't fight it.
        if (climbing != null && climbing.IsClimbing) { if (swimming) Exit(); return; }

        // ride higher while actually swimming (prone stroke), deeper while treading
        bool moving = movement != null && movement.PlanarSpeed > 0.05f;
        curOffset = Mathf.Lerp(curOffset, moving ? swimOffset : floatOffset, 0.1f);
        float surface = waterLevel + curOffset;

        bool grounded = Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down,
                                        out var hit, 1.4f, ~0, QueryTriggerInteraction.Ignore)
                        && !hit.collider.isTrigger;

        bool inWater = transform.position.y <= surface + 0.4f && !grounded;

        if (inWater)
        {
            if (!swimming) Enter();
            var vel = rb.linearVelocity;
            vel.y = Mathf.Clamp((surface - rb.position.y) * buoyancy, -maxBob, maxBob);
            rb.linearVelocity = vel;
        }
        else if (swimming && (grounded || transform.position.y > surface + 0.5f))
        {
            Exit();
        }
    }

    void Enter() { swimming = true;  rb.useGravity = false; }
    void Exit()  { swimming = false; rb.useGravity = true;  }
}
