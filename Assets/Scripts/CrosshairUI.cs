using UnityEngine;

// Fixed dead-center crosshair (Minecraft-style): it never moves; you aim by turning the camera, and whatever's
// under the center is what you're pointing at. Shown while a weapon is drawn.
public class CrosshairUI : MonoBehaviour
{
    [SerializeField] float size = 8f, thickness = 2f, gap = 4f;
    [SerializeField] Color color = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] bool onlyWhenDrawn = true;

    WieldSystem wield;
    void Awake() => wield = GetComponentInParent<WieldSystem>() ?? GetComponentInChildren<WieldSystem>();

    void OnGUI()
    {
        if (onlyWhenDrawn && (wield == null || !wield.Drawn)) return;
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        var tex = Texture2D.whiteTexture;
        void Bar(Rect r) { GUI.color = new Color(0, 0, 0, color.a * 0.6f); GUI.DrawTexture(new Rect(r.x + 1, r.y + 1, r.width, r.height), tex); GUI.color = color; GUI.DrawTexture(r, tex); }
        Bar(new Rect(cx - gap - size, cy - thickness * 0.5f, size, thickness));
        Bar(new Rect(cx + gap,        cy - thickness * 0.5f, size, thickness));
        Bar(new Rect(cx - thickness * 0.5f, cy - gap - size, thickness, size));
        Bar(new Rect(cx - thickness * 0.5f, cy + gap,        thickness, size));
        GUI.color = Color.white;
    }
}
