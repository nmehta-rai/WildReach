using System;
using UnityEngine;

// Surface-conforming climbing. Every physics step we sample the rock around the grip with a small fan of
// rays, average their normals (smooth — no single-bump jerk) and keep the real surface point under the grip
// (so the body can't sink in). The whole body is then oriented to the wall plane — facing into the rock with
// "up" running along the surface — so he hugs slabs, overhangs and curves like a climber instead of staying
// bolt-upright. Hands/feet are pinned to the rock by ClimbIK on top of this.
[RequireComponent(typeof(PlayerMovement), typeof(StaminaSystem))]
public class ClimbingSystem : MonoBehaviour
{
    [SerializeField] float rootMotionScale = 1f;   // scales the animation's own climb travel (movement = root motion, not a constant)
    [SerializeField] float grabDistance = 2.5f;   // reach to grab a wall
    [SerializeField] float bodyOffset   = 0.45f;  // body centre sits this far off the surface
    [SerializeField] float probeOut     = 0.6f;   // re-projection ray starts this far out from the surface
    [SerializeField] float probeDepth   = 1.2f;   // ...and searches this far back in
    [SerializeField] float sampleSpread = 0.38f;  // how far out the fan rays sit (smooths the normal)
    [SerializeField] float normalSmooth = 9f;     // surface-normal smoothing rate (higher = snappier)
    [SerializeField] float rotSmooth    = 11f;    // body-orientation smoothing rate
    [SerializeField] float posSmooth    = 22f;    // body-position smoothing rate
    [SerializeField] LayerMask climbMask = ~0;

    PlayerMovement movement;
    StaminaSystem stamina;
    Rigidbody rb;
    CapsuleCollider body;

    bool isClimbing;
    Vector3 anchor;      // current grip point ON the surface
    Vector3 surfNormal;  // smoothed surface normal at the anchor
    int missFrames;
    Vector3 pendingRootMotion;   // climb animation's root delta, accumulated by PlayerAnimator.OnAnimatorMove

    // Fed each frame from the climb clip's root motion (the body translating as the limbs pull). FixedUpdate
    // consumes it as the actual movement — so the body is driven BY the animation, not a disconnected constant.
    public void AddRootMotion(Vector3 worldDelta) { if (isClimbing) pendingRootMotion += worldDelta; }

    public bool IsClimbing => isClimbing;
    public Vector3 WallNormal => surfNormal;
    public float ClimbVertical { get; private set; }   // drives the up/down climb animation
    public float ClimbHorizontal { get; private set; } // drives the left/right shimmy animation

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stamina  = GetComponent<StaminaSystem>();
        rb       = GetComponent<Rigidbody>();
        body     = GetComponent<CapsuleCollider>() ?? GetComponentInChildren<CapsuleCollider>();
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
        float dt = Time.fixedDeltaTime;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        ClimbVertical = v;
        ClimbHorizontal = h;
        bool moving = Mathf.Abs(h) > 0.05f || Mathf.Abs(v) > 0.05f;

        // tangent basis on the surface (used by SampleSurface's fan)
        TangentBasis(surfNormal, out var right, out var wallUp);

        // MOVEMENT = the climb animation's root motion, projected onto the wall (so the body moves exactly as the
        // limbs pull — connected, no slide). Input (h/v) only chose WHICH clip plays + gates it via ClimbMag.
        Vector3 tangentMove = Vector3.ProjectOnPlane(pendingRootMotion, surfNormal) * rootMotionScale;
        pendingRootMotion = Vector3.zero;
        Vector3 desired = anchor + tangentMove;

        // sample the surface around the new grip: keep the real point, average the normals
        if (SampleSurface(desired, surfNormal, right, wallUp, out var pt, out var n))
        {
            anchor = pt;
            surfNormal = Vector3.Slerp(surfNormal, n, SmoothT(normalSmooth, dt)).normalized;
            missFrames = 0;
        }
        else if (++missFrames > 10)        // ran off the edge / top of the climbable surface
        {
            StopClimb(vault: true);
            return;
        }

        // conform the WHOLE body to the wall: face into the rock, "up" along the surface
        Vector3 fwd = -surfNormal;
        Vector3 up  = Vector3.ProjectOnPlane(Vector3.up, surfNormal);
        if (up.sqrMagnitude < 0.001f) up = Vector3.ProjectOnPlane(transform.forward, surfNormal);  // near-flat slab
        up.Normalize();
        Quaternion targetRot = Quaternion.LookRotation(fwd, up);
        rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRot, SmoothT(rotSmooth, dt)));

        // ease toward the rest spot, then catch only DEEP penetration (body core inside the rock).
        // A small core probe means normal grazing of the fat capsule doesn't trigger a push — so no
        // vibration and no flinging him off the wall, while "whole body inside" is still impossible.
        Vector3 targetPos = anchor + surfNormal * bodyOffset;
        Vector3 newPos = Vector3.Lerp(transform.position, targetPos, SmoothT(posSmooth, dt));
        newPos = PushOutOfSurface(newPos, targetRot);
        rb.MovePosition(newPos);

        if (moving) stamina.DrainClimb(dt);
    }

    // Sweep the body capsule; if it overlaps rock, step it back out along the surface normal until clear.
    Vector3 PushOutOfSurface(Vector3 pos, Quaternion rot)
    {
        if (body == null) return pos;
        float scale = transform.lossyScale.x;
        // SMALL core probe: only fires when the body's centre line is genuinely inside the rock,
        // not when the wide capsule just touches the surface (that over-pushed him off the wall).
        float r     = Mathf.Min(0.22f, body.radius * scale * 0.45f);
        float half  = Mathf.Max(0f, body.height * 0.5f * scale - body.radius * scale);
        Vector3 axis = rot * Vector3.up;

        for (int step = 0; step < 14; step++)
        {
            Vector3 c  = pos + rot * Vector3.Scale(body.center, transform.lossyScale);
            Vector3 p1 = c + axis * half, p2 = c - axis * half;
            bool inside = false;
            foreach (var col in Physics.OverlapCapsule(p1, p2, r, climbMask, QueryTriggerInteraction.Ignore))
                if (col != body && !col.transform.IsChildOf(transform)) { inside = true; break; }
            if (!inside) break;
            pos += surfNormal * 0.04f;                          // nudge out and re-test
        }
        return pos;
    }

    // fan of rays around the grip: position from the centre hit (accurate, no sink), normal = average (smooth)
    bool SampleSurface(Vector3 center, Vector3 n, Vector3 right, Vector3 up, out Vector3 pt, out Vector3 outN)
    {
        Span<Vector3> offsets = stackalloc Vector3[5]
        { Vector3.zero, right * sampleSpread, -right * sampleSpread, up * sampleSpread, -up * sampleSpread };

        Vector3 sumN = Vector3.zero; int hits = 0;
        Vector3 centerHit = center; bool gotCenter = false;
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 start = center + offsets[i] + n * probeOut;
            if (Physics.Raycast(start, -n, out var hit, probeOut + probeDepth, climbMask, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform))
            {
                sumN += hit.normal; hits++;
                if (i == 0) { centerHit = hit.point; gotCenter = true; }
            }
        }
        if (hits == 0) { pt = center; outN = n; return false; }
        pt   = gotCenter ? centerHit : center;          // never average the position — that's what causes sink/float
        outN = (sumN / hits).normalized;
        return true;
    }

    static void TangentBasis(Vector3 n, out Vector3 right, out Vector3 up)
    {
        right = Vector3.Cross(n, Vector3.up);
        if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(n, Vector3.forward);  // surface ~horizontal
        right.Normalize();
        up = Vector3.Cross(right, n).normalized;
    }

    // frame-rate-independent smoothing factor
    static float SmoothT(float rate, float dt) => 1f - Mathf.Exp(-rate * dt);

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
