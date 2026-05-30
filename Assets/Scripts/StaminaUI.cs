using UnityEngine;
using UnityEngine.UI;

public class StaminaUI : MonoBehaviour
{
    [SerializeField] Image staminaRing;
    [SerializeField] StaminaSystem staminaSystem;
    [SerializeField] Color fullColor = new Color(0.35f, 0.85f, 0.25f);
    [SerializeField] Color emptyColor = new Color(0.9f, 0.3f, 0.1f);

    void Start()
    {
        if (staminaRing == null) return;
        staminaRing.sprite = CreateCircleSprite(128);
        staminaRing.type = Image.Type.Filled;
        staminaRing.fillMethod = Image.FillMethod.Radial360;
        staminaRing.fillOrigin = (int)Image.Origin360.Top;
        staminaRing.fillClockwise = true;
        staminaRing.color = fullColor;
    }

    void Update()
    {
        if (staminaSystem == null || staminaRing == null) return;
        float ratio = staminaSystem.Stamina / staminaSystem.MaxStamina;
        staminaRing.fillAmount = ratio;
        staminaRing.color = Color.Lerp(emptyColor, fullColor, ratio);
    }

    Sprite CreateCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size);
        float center = size / 2f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                pixels[y * size + x] = dist <= center ? Color.white : Color.clear;
            }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f);
    }
}
