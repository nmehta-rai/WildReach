using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Hit reaction for non-player creatures: damage + a brief red flash of the mesh itself (so a hit reads even
// when it isn't the player getting hurt), plus a knockback. Most creatures are kinematic (AI-driven), so the
// shove is applied to the transform with the AI movers paused briefly, which actually reads. Implements
// IHittable, so the same attacker code that flashes the player's screen instead tints a mob red.
public class MobHurt : MonoBehaviour, IHittable
{
    [SerializeField] float flashTime = 0.25f;
    [SerializeField] Color hitTint   = new Color(1f, 0.25f, 0.2f);   // multiplied over the albedo while flashing
    [SerializeField] float knockback = 10f;
    [SerializeField] float knockTime = 0.25f;

    HealthSystem hp; Rigidbody rb; Renderer[] rends; MaterialPropertyBlock mpb; Coroutine running;
    readonly List<Behaviour> paused = new List<Behaviour>();   // AI movers paused during a knockback
    Vector3 knockVel; float knockUntil;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        hp = GetComponent<HealthSystem>();
        rb = GetComponent<Rigidbody>();
        rends = GetComponentsInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
    }

    public void Hit(int damage, Vector3 from)
    {
        if (hp != null) { if (hp.IsDead) return; hp.Damage(damage); }
        Vector3 away = transform.position - from; away.y = 0f;
        if (away.sqrMagnitude > 0.001f)
        {
            away.Normalize();
            if (rb != null && !rb.isKinematic) rb.linearVelocity = away * knockback + Vector3.up * 1.5f;
            else
            {
                knockVel = away * knockback; knockUntil = Time.time + knockTime;
                paused.Clear();   // gather the AI NOW (it may have been added after Awake) and pause it so the shove reads
                foreach (var b in GetComponents<MonoBehaviour>())
                    if ((b is Predator || b is ZoneRoamer || b is CreatureWander) && b.enabled) { b.enabled = false; paused.Add(b); }
            }
        }
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Flash());
    }

    void LateUpdate()
    {
        if (knockUntil <= 0f) return;
        if (Time.time < knockUntil)
        {
            transform.position += knockVel * Time.deltaTime;
            knockVel = Vector3.Lerp(knockVel, Vector3.zero, 5f * Time.deltaTime);   // slower decay = carries further
        }
        else
        {
            knockUntil = 0f; knockVel = Vector3.zero;
            if (hp == null || !hp.IsDead)
                foreach (var b in paused) if (b != null) b.enabled = true;   // resume (unless it died)
            paused.Clear();
        }
    }

    IEnumerator Flash()
    {
        for (float t = 0f; t < flashTime; t += Time.deltaTime)
        {
            var c = Color.Lerp(Color.white, hitTint, 1f - t / flashTime);   // red at impact, fades to no-tint
            foreach (var r in rends) { r.GetPropertyBlock(mpb); mpb.SetColor(BaseColor, c); r.SetPropertyBlock(mpb); }
            yield return null;
        }
        foreach (var r in rends) r.SetPropertyBlock(null);                  // clear override → exact original look
    }
}
