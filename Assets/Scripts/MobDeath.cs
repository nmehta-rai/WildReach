using System.Collections;
using UnityEngine;

// Watches a creature's HealthSystem and, when it hits zero, kills it cleanly: stop the AI + physics, play a
// death animation if the rig has one, then topple + sink + shrink away and destroy. Pairs with MobHurt
// (damage + flash) and HealthSystem (hearts). Drop it on any killable creature.
[RequireComponent(typeof(HealthSystem))]
public class MobDeath : MonoBehaviour
{
    [SerializeField] float settle = 1.2f;      // pause on the death pose before it sinks away
    [SerializeField] float sinkTime = 0.8f;

    HealthSystem hp;
    bool dying;

    void Awake() => hp = GetComponent<HealthSystem>();

    void Update()
    {
        if (!dying && hp != null && hp.IsDead) { dying = true; StartCoroutine(Die()); }
    }

    IEnumerator Die()
    {
        // freeze the AI + let it stop colliding/being shoved
        foreach (var b in GetComponentsInChildren<MonoBehaviour>())
            if (b != this && !(b is MobHurt)) b.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        var rb = GetComponent<Rigidbody>(); if (rb != null) rb.isKinematic = true;

        var anim = GetComponentInChildren<Animator>();
        bool deathAnim = anim != null && TryTrigger(anim, "Die", "Death", "Dead", "Dying");

        // no death clip? keel over SIDEWAYS (not face-first into the dirt) so it reads as a kill
        if (!deathAnim)
        {
            Quaternion a = transform.rotation;
            Quaternion b = Quaternion.AngleAxis(85f, transform.forward) * transform.rotation;   // roll onto its side
            for (float t = 0f; t < 0.35f; t += Time.deltaTime) { transform.rotation = Quaternion.Slerp(a, b, t / 0.35f); yield return null; }
        }

        yield return new WaitForSeconds(settle);

        // shrink away in place — no sinking through the ground
        Vector3 s0 = transform.localScale;
        for (float t = 0f; t < sinkTime; t += Time.deltaTime)
        {
            transform.localScale = s0 * (1f - t / sinkTime);
            yield return null;
        }
        Destroy(gameObject);
    }

    static bool TryTrigger(Animator a, params string[] names)
    {
        foreach (var p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger)
                foreach (var n in names) if (p.name == n) { a.SetTrigger(n); return true; }
        return false;
    }
}
