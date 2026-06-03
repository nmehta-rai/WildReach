using UnityEngine;
using UnityEngine.UI;

// Drives the row of heart images from HealthSystem: full sprite for filled hearts, empty for the
// rest. The heart Images themselves are built + assigned by the SetupHUD editor tool.
[ExecuteAlways]
public class HeartsUI : MonoBehaviour
{
    [SerializeField] HealthSystem health;
    [SerializeField] Image[] hearts;
    [SerializeField] Sprite fullSprite;
    [SerializeField] Sprite emptySprite;

    void Update()
    {
        if (hearts == null) return;
        int filled = health != null ? health.Hearts : hearts.Length;   // show all full in edit mode w/o health
        for (int i = 0; i < hearts.Length; i++)
        {
            if (hearts[i] == null) continue;
            hearts[i].sprite = i < filled ? fullSprite : emptySprite;
        }
    }
}
