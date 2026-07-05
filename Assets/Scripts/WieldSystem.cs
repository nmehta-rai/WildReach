using System.Collections.Generic;
using UnityEngine;

// The player's weapons. You start with a list (stick, torch); switch with number keys (1/2) or by clicking in
// the pouch. The selected weapon spawns into the tuned hand grip; R slings it to the back and draws it again;
// left-click swings — during the swing the weapon's actual swept volume is tested each frame, so damage lands
// when the weapon really reaches the target (reliable, unlike animation-driven trigger callbacks).
public class WieldSystem : MonoBehaviour
{
    // grip/holster are PER-WEAPON tuned points (each mesh sits differently); fall back to the shared ones if unset.
    [System.Serializable] public class Weapon { public string name; public Sprite icon; public GameObject prefab; public Transform grip; public Transform holster; public int damage = 1; public GameObject fireVfx; }

    [SerializeField] Transform handGrip;
    [SerializeField] Transform backHolster;
    [SerializeField] Weapon[] weapons;
    [SerializeField] float contactStart = 0.35f;   // normalized swing time the blade starts connecting (skips wind-up)
    [SerializeField] float contactEnd   = 0.72f;   // ...and when the swing-through is past

    Animator anim;
    GameObject held;
    int equipped = -1;
    bool sheathed;
    bool swinging; float swingStart;

    Transform weaponMesh;                 // the held weapon's mesh transform (we sweep along it during a swing)
    Vector3 capA, capB; float capRadius;  // weapon capsule endpoints + radius, in mesh-local space
    readonly HashSet<IHittable> struck = new HashSet<IHittable>();

    public Weapon[] Weapons => weapons;
    public int EquippedIndex => equipped;
    public bool Drawn => held != null && !sheathed;

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("__Preview")) Destroy(t.gameObject);   // strip the edit-mode grip previews
        UpdateFire();   // all fire off until a weapon is drawn
    }

    // A weapon's fire VFX (e.g. the torch flame) is on only while that weapon is the one in hand AND drawn.
    void UpdateFire()
    {
        if (weapons == null) return;
        for (int i = 0; i < weapons.Length; i++)
            if (weapons[i].fireVfx != null) weapons[i].fireVfx.SetActive(i == equipped && !sheathed);
    }

    public void Equip(int index)
    {
        if (weapons == null || index < 0 || index >= weapons.Length || weapons[index].prefab == null) return;
        if (held != null) Destroy(held);
        var w = weapons[index];
        held = Instantiate(w.prefab);
        foreach (var c in held.GetComponentsInChildren<ItemPickup>(true)) Destroy(c);   // drop pickup logic, KEEP vfx (torch fire/light)
        foreach (var col in held.GetComponentsInChildren<Collider>(true)) Destroy(col);
        var sp = held.transform.Find("Sparkle"); if (sp != null) Destroy(sp.gameObject);
        equipped = index; sheathed = false;
        Attach(Grip());
        UpdateFire();
        MeasureWeapon();
    }

    // Record the weapon as a capsule (two endpoints along its long axis + a radius) in mesh-local space, so we can
    // place it in the world each frame and sweep-test where the blade actually is.
    void MeasureWeapon()
    {
        weaponMesh = null;
        if (held == null) return;
        var mf = held.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        weaponMesh = mf.transform;
        Bounds mb = mf.sharedMesh.bounds; Vector3 s = mb.size;
        int ax = (s.x >= s.y && s.x >= s.z) ? 0 : (s.y >= s.z ? 1 : 2);
        capA = mb.center; capA[ax] = mb.min[ax];
        capB = mb.center; capB[ax] = mb.max[ax];
        capRadius = Mathf.Max(s[(ax + 1) % 3], s[(ax + 2) % 3]) * 0.5f + s[ax] * 0.05f;
    }

    // Tested every frame the swing is live: where is the weapon RIGHT NOW, and did it just reach something?
    void SwingSweep()
    {
        if (weaponMesh == null) return;
        Vector3 p0 = weaponMesh.TransformPoint(capA);
        Vector3 p1 = weaponMesh.TransformPoint(capB);
        Vector3 ls = weaponMesh.lossyScale;
        float r = capRadius * Mathf.Max(ls.x, Mathf.Max(ls.y, ls.z));
        int dmg = equipped >= 0 ? Mathf.Max(1, weapons[equipped].damage) : 1;
        foreach (var col in Physics.OverlapCapsule(p0, p1, r, ~0, QueryTriggerInteraction.Collide))
        {
            if (col.transform.IsChildOf(transform)) continue;        // not ourselves
            var h = col.GetComponentInParent<IHittable>();
            if (h == null || h is PlayerHurt) continue;
            if (struck.Add(h)) h.Hit(dmg, transform.position);       // once per swing, at the real contact frame
        }
    }

    Transform Grip() { var w = weapons[equipped]; return w.grip != null ? w.grip : handGrip; }
    Transform Holster() { var w = weapons[equipped]; return w.holster != null ? w.holster : backHolster; }

    public void ToggleSheathe()
    {
        if (held == null) return;
        sheathed = !sheathed;
        Attach(sheathed ? Holster() : Grip());
        UpdateFire();
    }

    public void Swing()
    {
        if (!Drawn || anim == null) return;
        anim.SetTrigger("Swing");
        struck.Clear();
        swinging = true; swingStart = Time.time;
    }

    // Only sweep during the forward swing-THROUGH (not the wind-up / ready pose), so the hit lands when the blade
    // actually crosses the target. Driven by the swing's normalized time, so it adapts to the clip's speed.
    void UpdateSwingHit()
    {
        if (anim == null || Time.time - swingStart > 1f) { swinging = false; return; }
        var st = anim.GetCurrentAnimatorStateInfo(0);
        if (!st.IsName("Swing")) return;            // still transitioning into the swing
        float nt = st.normalizedTime;
        if (nt >= contactStart && nt <= contactEnd) SwingSweep();
        if (nt >= contactEnd) swinging = false;     // swing-through done
    }

    void Attach(Transform point)
    {
        if (held == null || point == null || equipped < 0) return;
        Vector3 intended = weapons[equipped].prefab.transform.localScale;
        held.transform.SetParent(point, false);
        held.transform.localPosition = Vector3.zero;
        held.transform.localRotation = Quaternion.identity;
        var ls = point.lossyScale;
        held.transform.localScale = new Vector3(
            ls.x != 0 ? intended.x / ls.x : intended.x,
            ls.y != 0 ? intended.y / ls.y : intended.y,
            ls.z != 0 ? intended.z / ls.z : intended.z);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) Equip(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Equip(1);
        if (swinging) UpdateSwingHit();
        if (held == null) return;
        if (Input.GetKeyDown(KeyCode.R)) ToggleSheathe();
        if (Input.GetMouseButtonDown(0)) Swing();
    }
}
