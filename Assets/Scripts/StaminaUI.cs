using UnityEngine;
using UnityEngine.UI;

// Slim BotW-style stamina ring. The Fill image is a radial-filled ring sprite that depletes
// as stamina drains; a dark Track ring sits behind it. The whole widget fades out when stamina
// is full (so the clean HUD stays clean) and fades in while draining or recharging.
public class StaminaUI : MonoBehaviour
{
    [SerializeField] Image fillRing;
    [SerializeField] CanvasGroup group;
    [SerializeField] StaminaSystem staminaSystem;
    [SerializeField] Color fullColor = new Color(0.50f, 0.86f, 0.42f);
    [SerializeField] Color lowColor  = new Color(0.95f, 0.55f, 0.20f);

    float alpha;

    void Update()
    {
        if (staminaSystem == null || fillRing == null) return;
        float ratio = staminaSystem.MaxStamina > 0f ? staminaSystem.Stamina / staminaSystem.MaxStamina : 0f;
        fillRing.fillAmount = ratio;
        fillRing.color = Color.Lerp(lowColor, fullColor, ratio);

        float target = ratio >= 0.999f ? 0f : 1f;          // hide when full
        alpha = Mathf.MoveTowards(alpha, target, Time.deltaTime * 6f);
        if (group != null) group.alpha = alpha;
    }
}
