using UnityEngine;

// Plants the player's hands and feet on the actual climbing surface using Unity's built-in Humanoid IK.
// While ClimbingSystem.IsClimbing, each limb raycasts toward the wall (along the surface normal) and its
// IK goal is pinned to the hit point — so hands/feet stick to whatever rock shape you're on, instead of
// floating off or clipping in. Requires "IK Pass" enabled on the Animator's base layer (Setup Climb IK does that).
// Lives on the same GameObject as the Animator (OnAnimatorIK only fires there).
[RequireComponent(typeof(Animator))]
public class ClimbIK : MonoBehaviour
{
    [SerializeField] float reach        = 0.7f;    // how far a limb searches for the wall
    [SerializeField] float startOffset  = 0.3f;    // begin the ray this far OUT from the limb (avoid self-hits)
    [SerializeField] float surfaceOffset = 0.03f;  // sit the hand/foot just off the surface
    [SerializeField] float weightSpeed  = 8f;      // how fast IK fades in/out when (un)climbing

    Animator anim;
    ClimbingSystem climb;
    float ikWeight;

    static readonly (AvatarIKGoal goal, HumanBodyBones bone)[] LIMBS =
    {
        (AvatarIKGoal.LeftHand,  HumanBodyBones.LeftHand),
        (AvatarIKGoal.RightHand, HumanBodyBones.RightHand),
        (AvatarIKGoal.LeftFoot,  HumanBodyBones.LeftFoot),
        (AvatarIKGoal.RightFoot, HumanBodyBones.RightFoot),
    };

    void Awake()
    {
        anim = GetComponent<Animator>();
        climb = GetComponentInParent<ClimbingSystem>();
    }

    void OnAnimatorIK(int layer)
    {
        bool climbing = climb != null && climb.IsClimbing;
        ikWeight = Mathf.MoveTowards(ikWeight, climbing ? 1f : 0f, weightSpeed * Time.deltaTime);

        if (ikWeight <= 0.001f) { foreach (var l in LIMBS) anim.SetIKPositionWeight(l.goal, 0f); return; }

        Vector3 n = climb.WallNormal;
        if (n.sqrMagnitude < 0.01f) n = -transform.forward;   // fallback

        foreach (var (goal, bone) in LIMBS)
        {
            var t = anim.GetBoneTransform(bone);
            if (t == null) { anim.SetIKPositionWeight(goal, 0f); continue; }

            Vector3 origin = t.position + n * startOffset;     // start outside the body, cast back toward the wall
            if (Physics.Raycast(origin, -n, out var hit, startOffset + reach, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform.root))    // ignore the player's own colliders
            {
                anim.SetIKPositionWeight(goal, ikWeight);
                anim.SetIKPosition(goal, hit.point + hit.normal * surfaceOffset);
            }
            else anim.SetIKPositionWeight(goal, 0f);            // no rock found for this limb — leave it on the clip
        }
    }
}
