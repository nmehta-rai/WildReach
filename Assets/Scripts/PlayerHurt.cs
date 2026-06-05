using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Hit feedback for the player: damage + a red screen flash + a knockback shove away from the attacker,
// with a brief hit-stun so the shove actually carries (PlayerMovement overwrites velocity every FixedUpdate
// unless it's disabled). Predators call Hit() on a landed strike. Self-builds its flash overlay.
[RequireComponent(typeof(HealthSystem))]
public class PlayerHurt : MonoBehaviour, IHittable
{
    [SerializeField] float knockback = 7f;     // horizontal shove on a hit
    [SerializeField] float knockUp   = 3f;     // a little pop so it reads as impact, not a slide
    [SerializeField] float stunTime  = 0.25f;  // control frozen so the knockback isn't instantly overwritten
    [SerializeField] float flashTime = 0.35f;  // red flash fade
    [SerializeField] float flashPeak = 0.4f;   // strongest red alpha

    HealthSystem hp; Rigidbody rb; PlayerMovement move; CanvasGroup flash; Coroutine running;

    void Awake()
    {
        hp = GetComponent<HealthSystem>();
        rb = GetComponent<Rigidbody>();
        move = GetComponent<PlayerMovement>();
        flash = BuildFlash();
    }

    // Take a hit from a world position: damage, red flash, and a knockback away from the source.
    public void Hit(int damage, Vector3 from)
    {
        if (hp == null || hp.IsDead) return;
        hp.Damage(damage);

        if (rb != null)
        {
            Vector3 away = transform.position - from; away.y = 0f;
            away = away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward;
            rb.linearVelocity = new Vector3(away.x * knockback, knockUp, away.z * knockback);
        }
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(React());
    }

    IEnumerator React()
    {
        if (move != null) move.Enabled = false;       // hit-stun: let the knockback carry
        float reEnable = stunTime;
        for (float t = 0f; t < flashTime; t += Time.deltaTime)
        {
            flash.alpha = flashPeak * (1f - t / flashTime);   // punch then fade
            if (move != null && !move.Enabled && t >= reEnable && !hp.IsDead) move.Enabled = true;
            yield return null;
        }
        flash.alpha = 0f;
        if (move != null && !hp.IsDead) move.Enabled = true;   // never re-enable into death (respawn owns that)
    }

    CanvasGroup BuildFlash()
    {
        var go = new GameObject("HurtFlash");
        var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 900;
        var cg = go.AddComponent<CanvasGroup>(); cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;
        var img = new GameObject("Red").AddComponent<Image>();
        img.transform.SetParent(go.transform, false);
        img.color = new Color(0.85f, 0.05f, 0.05f, 1f); img.raycastTarget = false;
        var rt = img.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return cg;
    }
}
