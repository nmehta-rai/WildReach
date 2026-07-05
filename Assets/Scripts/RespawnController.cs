using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Watches the player's health; on death it fades to black, teleports to the respawn point with full
// health, and fades back in. One respawn point to start — extensible to BotW-style checkpoints later
// by reassigning respawnPoint when the player activates one. Sits on the player.
[RequireComponent(typeof(HealthSystem))]
public class RespawnController : MonoBehaviour
{
    [SerializeField] Transform respawnPoint;
    [SerializeField] float fadeTime  = 0.6f;
    [SerializeField] float blackHold = 0.6f;

    HealthSystem hp; PlayerMovement move; Rigidbody rb; CanvasGroup overlay; bool busy;

    void Awake()
    {
        hp = GetComponent<HealthSystem>();
        move = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody>();
        overlay = BuildOverlay();
    }

    void Update()
    {
        if (!busy && hp.IsDead) StartCoroutine(Respawn());
    }

    IEnumerator Respawn()
    {
        busy = true;
        if (move != null) move.Enabled = false;
        yield return Fade(0f, 1f);
        yield return new WaitForSeconds(blackHold);

        ToSpawn();

        yield return Fade(1f, 0f);
        if (move != null) move.Enabled = true;
        busy = false;
    }

    // Snap the player to the spawn point at full health, mobs sent home. Used by death-respawn AND the pause
    // menu's "Restart to Spawn".
    public void ToSpawn()
    {
        if (respawnPoint != null) transform.position = respawnPoint.position;
        if (rb != null) rb.linearVelocity = Vector3.zero;
        while (hp.Hearts < hp.MaxHearts) hp.Heal();   // full heal
        foreach (var p in FindObjectsByType<Predator>()) p.GoHome();   // send mobs back to their lairs
    }

    IEnumerator Fade(float a, float b)
    {
        for (float t = 0f; t < fadeTime; t += Time.deltaTime) { overlay.alpha = Mathf.Lerp(a, b, t / fadeTime); yield return null; }
        overlay.alpha = b;
    }

    static CanvasGroup BuildOverlay()
    {
        var go = new GameObject("RespawnFade");
        var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
        var cg = go.AddComponent<CanvasGroup>(); cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;
        var img = new GameObject("Black").AddComponent<Image>();
        img.transform.SetParent(go.transform, false);
        img.color = Color.black; img.raycastTarget = false;
        var rt = img.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return cg;
    }
}
