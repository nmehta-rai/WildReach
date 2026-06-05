using System.Collections;
using UnityEngine;

// Hit reaction for non-player creatures: damage + a brief red flash of the mesh itself (so a hit reads even
// when it isn't the player getting hurt), plus a light knockback. Implements IHittable, so the same attacker
// code that flashes the player's screen instead tints a mob red. Drop it on any creature with renderers;
// add a HealthSystem too if it should be killable.
public class MobHurt : MonoBehaviour, IHittable
{
    [SerializeField] float flashTime = 0.25f;
    [SerializeField] Color hitTint   = new Color(1f, 0.25f, 0.2f);   // multiplied over the albedo while flashing
    [SerializeField] float knockback = 4f;

    HealthSystem hp; Rigidbody rb; Renderer[] rends; MaterialPropertyBlock mpb; Coroutine running;
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
        if (rb != null && !rb.isKinematic)
        {
            Vector3 away = transform.position - from; away.y = 0f;
            if (away.sqrMagnitude > 0.001f) rb.linearVelocity = away.normalized * knockback + Vector3.up * 1.5f;
        }
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Flash());
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
