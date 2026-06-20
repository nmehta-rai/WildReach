using UnityEngine;

// Plants the character's feet on the ground at RUNTIME, in the real idle pose. Edit-time mesh sampling is
// unreliable with an Animator (the clip often doesn't pose the mesh), which left him floating. This waits until
// he's resting on the ground, then offsets the visual once so its lowest vertex (the boot sole) sits on the
// collider bottom. One-time constant offset — it doesn't fight the walk/run foot motion layered on top.
public class FootPlanter : MonoBehaviour
{
    Collider body;
    SkinnedMeshRenderer smr;
    PlayerMovement move;
    bool planted;

    void Awake()
    {
        smr  = GetComponentInChildren<SkinnedMeshRenderer>(true);
        body = GetComponentInParent<Collider>();
        move = GetComponentInParent<PlayerMovement>();
        if (smr != null) smr.updateWhenOffscreen = true;   // keep world bounds accurate to the current pose
    }

    void LateUpdate()
    {
        if (planted || smr == null || body == null) return;
        if (move != null && !move.IsGrounded) return;      // wait until settled on the ground

        float delta = body.bounds.min.y - smr.bounds.min.y;
        if (Mathf.Abs(delta) > 0.0005f) transform.position += Vector3.up * delta;
        planted = true;
    }
}
