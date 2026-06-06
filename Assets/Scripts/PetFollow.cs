using UnityEngine;

// A companion that trails the player and MIRRORS the player's locomotion onto its own Animator: stand still
// and it idles, walk and it walks, run and it runs, jump and it hops. Built mainly to preview + fine-tune a
// creature's animations without hunting one down in the wild — assign any creature prefab as the pet.
// Drives Animator "Speed" (float) + "Airborne" (bool) + "Jump" (trigger) if the controller has them.
[DisallowMultipleComponent]
public class PetFollow : MonoBehaviour
{
    [SerializeField] float followSmooth   = 0.18f;  // SmoothDamp time — smaller = snappier, larger = floatier
    [SerializeField] float maxSpeed       = 3f;     // the creature's OWN top pace (set at spawn); it can't exceed this
    [SerializeField] float turnDegPerSec  = 240f;
    [SerializeField] float hopHeight      = 1.2f;   // physical hop when the player jumps (clip has no root motion)

    public void SetMaxSpeed(float s) { if (s > 0.01f) maxSpeed = s; }

    Transform player; PlayerMovement move; Animator anim; Terrain terrain;
    float groundY, hopVel, hopY;                    // hopY = current extra height above the ground

    void Start()
    {
        var pm = Object.FindFirstObjectByType<PlayerMovement>();
        if (pm != null) { player = pm.transform; move = pm; move.Jumped += OnPlayerJump; }
        anim = GetComponentInChildren<Animator>(true);
        terrain = Terrain.activeTerrain ?? Object.FindFirstObjectByType<Terrain>();
        if (anim != null) anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // never freeze when off the SMR bounds

        // EVIDENCE: this one line says why a creature does or doesn't animate.
        string ctrl = anim != null && anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "NULL";
        string av   = anim == null ? "no-animator" : anim.avatar == null ? "NULL" : (anim.avatar.isValid ? anim.avatar.name : "INVALID");
        string prm  = anim != null ? string.Join(",", System.Array.ConvertAll(anim.parameters, p => p.name)) : "-";
        Debug.Log($"[Pet] '{name}': animator={(anim != null)} controller={ctrl} avatar={av} player={(move != null)} params=[{prm}]");
        if (anim != null && anim.avatar == null)
            Debug.LogWarning("[Pet] Animator has NO AVATAR — Generic clips silently won't play. The creature's FBX needs Rig=Generic + Avatar=Create From This Model, and the prefab must keep that avatar.");
    }

    void OnDestroy() { if (move != null) move.Jumped -= OnPlayerJump; }

    void OnPlayerJump()
    {
        if (HasAnim && HasParam("Jump")) anim.SetTrigger("Jump");   // creatures without a jump clip just hop
        hopVel = Mathf.Sqrt(2f * 9.81f * hopHeight);                // launch the physical hop
    }

    void Update()
    {
        if (player == null) return;

        // Trail a spot just beside-and-behind the player and ease toward it with SmoothDamp — no catch-up
        // bursts, so the body glides smoothly while the legs animate (the old proportional sprint jerked).
        Vector3 spot = player.position + player.right * 1.6f - player.forward * 0.3f;
        Vector3 cur  = transform.position;
        Vector3 flatTarget = new Vector3(spot.x, cur.y, spot.z);
        Vector3 newPos = Vector3.SmoothDamp(cur, flatTarget, ref followVel, followSmooth, maxSpeed);   // capped to ITS pace

        Vector3 moveDelta = Flat(newPos - cur);
        float actualSpeed = moveDelta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);   // its own ground speed this frame
        FaceDir(moveDelta.sqrMagnitude > 0.0004f ? moveDelta.normalized : Flat(player.forward));

        // ground clamp + the physical hop arc on top
        groundY = terrain != null ? terrain.SampleHeight(newPos) + terrain.transform.position.y : cur.y - hopY;
        if (hopVel != 0f || hopY > 0f)
        {
            hopY += hopVel * Time.deltaTime;
            hopVel -= 9.81f * Time.deltaTime;
            if (hopY <= 0f) { hopY = 0f; hopVel = 0f; }
        }
        newPos.y = groundY + hopY;
        transform.position = newPos;

        // animate from ITS OWN pace, normalised to its max so the existing walk/run thresholds fire:
        // ambling along = walk, moving near its top speed (catching up) = run, stopped = idle.
        if (HasAnim)
        {
            anim.SetFloat("Speed", maxSpeed > 0.01f ? (actualSpeed / maxSpeed) * 18f : 0f);
            if (HasParam("Airborne")) anim.SetBool("Airborne", move != null && !move.IsGrounded);
        }
    }
    Vector3 followVel;

    bool HasAnim => anim != null && anim.runtimeAnimatorController != null;
    bool HasParam(string n) { foreach (var pr in anim.parameters) if (pr.name == n) return true; return false; }

    void FaceDir(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnDegPerSec * Time.deltaTime);
    }
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
