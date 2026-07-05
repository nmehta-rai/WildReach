using UnityEngine;

// While a weapon is drawn, pitches the upper body toward where you're aiming (the crosshair = camera pitch), so a
// swing lands at the height you're LOOKING — letting you hit small creatures low to the ground instead of only a
// fixed mid-body height. Runs in LateUpdate, layered on top of the swing animation.
public class SpineAim : MonoBehaviour
{
    [SerializeField] float weight = 0.9f;     // how much of the look pitch the body takes
    [SerializeField] float maxDown = 58f;     // clamp so it can reach low without folding over
    [SerializeField] float maxUp = 40f;
    [SerializeField] float smooth = 12f;

    Transform spine; WieldSystem wield; Camera cam; float cur;

    void Start()
    {
        spine = FindDeep(transform, "mixamorig:Spine1") ?? FindDeep(transform, "mixamorig:Spine2") ?? FindDeep(transform, "mixamorig:Spine");
        wield = GetComponent<WieldSystem>();
    }

    void LateUpdate()
    {
        if (spine == null) return;
        if (cam == null) cam = Camera.main;

        float target = 0f;
        if (cam != null && wield != null && wield.Drawn)
        {
            float pitch = -Mathf.Asin(Mathf.Clamp(cam.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;   // +down, -up
            target = Mathf.Clamp(pitch * weight, -maxUp, maxDown);
        }
        cur = Mathf.Lerp(cur, target, 1f - Mathf.Exp(-smooth * Time.deltaTime));
        if (Mathf.Abs(cur) > 0.05f)
            spine.rotation = Quaternion.AngleAxis(cur, transform.right) * spine.rotation;   // pitch the chest + arms + weapon
    }

    static Transform FindDeep(Transform t, string n)
    {
        if (t.name == n) return t;
        foreach (Transform c in t) { var r = FindDeep(c, n); if (r != null) return r; }
        return null;
    }
}
