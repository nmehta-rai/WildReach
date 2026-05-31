using UnityEngine;

// Surface-anchored climbing. The body is never "pushed off" the wall (which lets it drift in) — instead,
// every physics step we find the real surface point under the grip and PLACE the body at
// surfacePoint + normal * bodyOffset. Positioning outward from the surface makes penetration impossible.
// Movement slides the grip anchor along the surface tangent and re-projects it onto the rock each step.
[RequireComponent(typeof(PlayerMovement), typeof(StaminaSystem))]
public class ClimbingSystem : MonoBehaviour
{
    [SerializeField] float climbSpeed   = 3f;
    [SerializeField] float grabDistance = 2.5f;   // reach to grab a wall
    [SerializeField] float bodyOffset   = 0.45f;  // body centre sits this far off the surface (≈ capsule radius + gap)
    [SerializeField] float probeOut     = 0.6f;   // re-projection ray starts this far out from the surface
    [SerializeField] float probeDepth   = 1.2f;   // ...and searches this far back in
    [SerializeField] LayerMask climbMask = ~0;

    PlayerMovement movement;
    StaminaSystem stamina;
    Rigidbody rb;

    bool isClimbing;
    Vector3 anchor;      // current grip point ON the surface
    Vector3 surfNormal;  // smoothed surface normal at the anchor
    int missFrames;

    public bool IsClimbing => isClimbing;
    public Vector3 WallNormal => surfNormal;
    public float ClimbVertical { get; private set; }   // drives the up/down climb animation

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stamina  = GetComponent<StaminaSystem>();
        rb       = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (!isClimbing)
        {
            if (Input.GetKeyDown(KeyCode.E) && DetectWall(out var hit))
            {
                Debug.Log($"E pressed — wall found: True normal={hit.normal}");
                StartClimb(hit);
            }
            else if (Input.GetKeyDown(KeyCode.E))
                Debug.Log("E pressed — wall found: False");
        }
        else if (Input.GetKeyDown(KeyCode.Space) || stamina.IsEmpty)
        {
            StopClimb(vault: true);
        }
    }

    void FixedUpdate()
    {
        if (!isClimbing) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        ClimbVertical = v;
        bool moving = Mathf.Abs(h) > 0.05f || Mathf.Abs(v) > 0.05f;

        // tangent basis on the surface (world-up reference keeps "up" intuitive)
        Vector3 right  = Vector3.Cross(surfNormal, Vector3.up);
        if (right.sqrMagnitude < 0.001f) right = transform.right;   // surface nearly horizontal — degenerate guard
        right.Normalize();
        Vector3 wallUp = Vector3.Cross(right, surfNormal).normalized;

        Vector3 move = (right * h + wallUp * v) * climbSpeed * Time.fixedDeltaTime;

        // slide the anchor, then RE-PROJECT it onto the actual surface (follows curves, finds fresh normal)
        Vector3 probeStart = anchor + move + surfNormal * probeOut;
        if (Physics.Raycast(probeStart, -surfNormal, out var hit, probeOut + probeDepth, climbMask, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(transform))
        {
            anchor = hit.point;
            surfNormal = Vector3.Slerp(surfNormal, hit.normal, 0.25f).normalized;
            missFrames = 0;
        }
        else if (++missFrames > 10)        // ran off the edge / top of the climbable surface
        {
            StopClimb(vault: true);
            return;
        }

        // PLACE the body off the surface — can't penetrate, because we build the position FROM the surface out
        rb.MovePosition(anchor + surfNormal * bodyOffset);

        // upright, facing the wall (yaw only — the hand/foot IK reaches the limbs to the rock)
        Vector3 face = new Vector3(-surfNormal.x, 0f, -surfNormal.z).normalized;
        if (face.sqrMagnitude > 0.001f)
            rb.MoveRotation(Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face, Vector3.up), 0.2f));

        if (moving) stamina.DrainClimb(Time.fixedDeltaTime);
    }

    bool DetectWall(out RaycastHit hit)
    {
        hit = default;
        Vector3 c = transform.position + Vector3.up;
        for (int i = -2; i <= 2; i++)   // forward fan ±40°
        {
            Vector3 dir = Quaternion.Euler(0, i * 20f, 0) * transform.forward;
            if (Physics.Raycast(c, dir, out hit, grabDistance, climbMask, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform) && IsClimbable(hit))
                return true;
        }
        return false;
    }

    static bool IsClimbable(RaycastHit h) => Mathf.Abs(h.normal.y) < 0.7f;   // steep enough (~45°+)

    void StartClimb(RaycastHit hit)
    {
        isClimbing = true;
        missFrames = 0;
        anchor = hit.point;
        surfNormal = hit.normal;
        movement.Enabled = false;
        rb.isKinematic = true;
        rb.position = anchor + surfNormal * bodyOffset;   // snap straight onto the wall, no drift-in
    }

    void StopClimb(bool vault)
    {
        isClimbing = false;
        ClimbVertical = 0f;
        movement.Enabled = true;
        rb.isKinematic = false;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);   // un-tilt
        rb.linearVelocity = vault ? (surfNormal + Vector3.up * 2f).normalized * 5f : Vector3.zero;
    }
}
