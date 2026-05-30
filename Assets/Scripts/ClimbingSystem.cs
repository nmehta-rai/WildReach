using UnityEngine;

[RequireComponent(typeof(PlayerMovement), typeof(StaminaSystem))]
public class ClimbingSystem : MonoBehaviour
{
    [SerializeField] float climbSpeed = 4f;
    [SerializeField] float grabDistance = 20f;

    PlayerMovement movement;
    StaminaSystem stamina;
    Rigidbody rb;
    CapsuleCollider capsule;

    bool isClimbing;
    public bool IsClimbing => isClimbing;

    /// Vertical climb input this frame (-1 down … 0 cling … +1 up) — drives the climb animation.
    public float ClimbVertical { get; private set; }
    RaycastHit wallHit;
    Vector3 smoothNormal;
    Collider climbTarget;
    int wallMissFrames;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stamina = GetComponent<StaminaSystem>();
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
    }

    void Update()
    {
        if (!isClimbing)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                bool found = DetectWall(out wallHit);
                Debug.Log($"E pressed — wall found: {found}" + (found ? $" normal={wallHit.normal}" : ""));
                if (found) StartClimbing();
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Space) || stamina.IsEmpty)
                StopClimbing(vault: false);
        }
    }

    void FixedUpdate()
    {
        if (!isClimbing) return;

        // Coast through brief detection misses using the last known wall, so concave/curved
        // rock doesn't freeze him mid-climb. Only release after sustained loss of contact.
        bool detected = DetectCurrentWall(out var hit);
        if (detected)
        {
            wallHit = hit;
            wallMissFrames = 0;
            smoothNormal = Vector3.Lerp(smoothNormal, wallHit.normal, 0.2f);
        }
        else
        {
            wallMissFrames++;
            if (wallMissFrames >= 12) { Debug.Log("Lost wall contact — releasing"); StopClimbing(vault: true); return; }
        }

        float v = Input.GetAxis("Vertical");
        float h = Input.GetAxis("Horizontal");
        ClimbVertical = v;
        bool climbMoving = Mathf.Abs(v) > 0.05f || Mathf.Abs(h) > 0.05f;

        Vector3 wallUp    = Vector3.ProjectOnPlane(Vector3.up, smoothNormal).normalized;
        Vector3 wallRight = Vector3.Cross(smoothNormal, Vector3.up).normalized;  // = player's right when facing the wall
        Vector3 move = (wallUp * v + wallRight * h) * climbSpeed * Time.fixedDeltaTime;

        // Hold a fixed standoff off the rock (only against a fresh surface point), nudged
        // gently/clamped so the curved terrain normal can't snap him in and out of the wall.
        Vector3 wallCorrection = Vector3.zero;
        if (detected)
        {
            Vector3 normalXZ = new Vector3(smoothNormal.x, 0, smoothNormal.z).normalized;
            float standoff = capsule.radius + 0.45f;     // sit further off the rock (less clipping)
            float distToSurface = Vector3.Dot(rb.position - wallHit.point, normalXZ);
            float correction = Mathf.Clamp(standoff - distToSurface, -0.15f, 0.15f);  // push out faster
            wallCorrection = normalXZ * correction;
        }

        rb.MovePosition(rb.position + move + wallCorrection);

        // Drain only while actually climbing; clinging in place holds stamina steady (no recharge).
        if (climbMoving) stamina.DrainClimb(Time.fixedDeltaTime);

        // Tilt the body to lie flush along the wall plane. MUST go through rb.MoveRotation —
        // a direct `transform.rotation =` write cancels the pending MovePosition each frame,
        // which is exactly what was freezing him in place mid-climb.
        Vector3 intoWall = -smoothNormal;
        if (intoWall.sqrMagnitude > 0.001f)
            rb.MoveRotation(Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(intoWall, wallUp),
                0.15f));
    }

    bool DetectWall(out RaycastHit hit)
    {
        hit = default;
        Vector3 center = transform.position + Vector3.up;

        // Probe in a forward fan (±40°) for a steep, climbable face within reach.
        for (int i = -2; i <= 2; i++)
        {
            Vector3 dir = Quaternion.Euler(0, i * 20f, 0) * transform.forward;
            if (Physics.Raycast(center, dir, out hit, grabDistance)
                && hit.collider.gameObject != gameObject && IsClimbable(hit))
            {
                climbTarget = hit.collider;
                return true;
            }
        }
        return false;
    }

    // A surface is climbable if it's steep enough (~45°+); normal.y near 0 = vertical wall.
    static bool IsClimbable(RaycastHit h) => Mathf.Abs(h.normal.y) < 0.7f;

    bool DetectCurrentWall(out RaycastHit hit)
    {
        hit = default;
        Vector3 center = transform.position + Vector3.up;

        // Probe straight at the wall (toward -smoothNormal) from several heights and angles,
        // so concave/curved rock doesn't drop contact and freeze the climb.
        Vector3 toWall = new Vector3(-smoothNormal.x, 0, -smoothNormal.z).normalized;
        Vector3[] origins =
        {
            transform.position + Vector3.up * 1.0f,
            transform.position + Vector3.up * 1.6f,
            transform.position + Vector3.up * 0.5f,
        };
        Vector3[] dirs =
        {
            toWall,
            Quaternion.Euler(0,  30f, 0) * toWall,
            Quaternion.Euler(0, -30f, 0) * toWall,
            Quaternion.Euler(25f, 0, 0) * toWall,    // up-into-wall (catches overhangs)
        };
        foreach (var o in origins)
            foreach (var d in dirs)
                if (Physics.Raycast(o, d, out hit, 3f) && hit.collider.gameObject != gameObject && IsClimbable(hit))
                    return true;
        return false;
    }

    void StartClimbing()
    {
        isClimbing = true;
        wallMissFrames = 0;
        movement.Enabled = false;
        rb.isKinematic = true;
        smoothNormal = wallHit.normal;
    }

    void StopClimbing(bool vault)
    {
        isClimbing = false;
        ClimbVertical = 0f;
        movement.Enabled = true;
        rb.isKinematic = false;
        transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);  // un-tilt after wall lean
        rb.linearVelocity = vault
            ? (-wallHit.normal + Vector3.up * 2f).normalized * 5f
            : Vector3.zero;
    }
}
