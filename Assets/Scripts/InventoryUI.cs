using UnityEngine;
using UnityEngine.UI;

// Builds a clean weapon card per weapon (big icon + name + key badge + equipped highlight) into the pouch row,
// clickable to equip. Reads the list off the player's WieldSystem and rebuilds on open / after a click.
public class InventoryUI : MonoBehaviour
{
    [SerializeField] RectTransform cardRow;
    [SerializeField] Sprite slotSprite;
    WieldSystem wield;
    Font font;

    void OnEnable() { Bind(); Build(); }

    void Bind()
    {
        if (wield != null) return;
        var pm = Object.FindFirstObjectByType<PlayerMovement>();
        if (pm != null) wield = pm.GetComponent<WieldSystem>();
    }

    void Build()
    {
        if (cardRow == null) return;
        Bind();
        for (int i = cardRow.childCount - 1; i >= 0; i--) Destroy(cardRow.GetChild(i).gameObject);
        var weapons = wield != null ? wield.Weapons : null;
        if (weapons == null) return;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (int i = 0; i < weapons.Length; i++)
            if (weapons[i].icon != null || weapons[i].prefab != null)
                MakeCard(weapons[i], i, wield.EquippedIndex == i);
    }

    void MakeCard(WieldSystem.Weapon w, int idx, bool equipped)
    {
        var card = new GameObject("Card_" + w.name, typeof(RectTransform), typeof(Image), typeof(Button));
        var crt = (RectTransform)card.transform; crt.SetParent(cardRow, false); crt.sizeDelta = new Vector2(168, 200);
        var cimg = card.GetComponent<Image>(); cimg.color = new Color(0, 0, 0, 0); cimg.raycastTarget = true;   // full-card click area

        // slot frame (square) on top
        var slot = new GameObject("Slot", typeof(RectTransform), typeof(Image));
        var srt = (RectTransform)slot.transform; srt.SetParent(card.transform, false);
        srt.anchorMin = new Vector2(0.5f, 1); srt.anchorMax = new Vector2(0.5f, 1); srt.pivot = new Vector2(0.5f, 1);
        srt.anchoredPosition = new Vector2(0, 0); srt.sizeDelta = new Vector2(164, 164);
        var simg = slot.GetComponent<Image>(); simg.sprite = slotSprite; simg.raycastTarget = false; simg.preserveAspect = true;
        simg.color = equipped ? new Color(1f, 0.96f, 0.72f) : new Color(0.92f, 0.92f, 0.92f);

        // icon
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var irt = (RectTransform)icon.transform; irt.SetParent(slot.transform, false);
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f); irt.anchoredPosition = Vector2.zero; irt.sizeDelta = new Vector2(124, 124);
        var iimg = icon.GetComponent<Image>(); iimg.sprite = w.icon; iimg.preserveAspect = true; iimg.raycastTarget = false; iimg.enabled = w.icon != null;

        // key number badge (top-left)
        Badge(slot.transform, (idx + 1).ToString(), new Vector2(0, 1), new Vector2(12, -10), 26, new Color(1f, 0.86f, 0.42f));

        // name under the slot
        var nm = new GameObject("Name", typeof(RectTransform), typeof(Text));
        var nrt = (RectTransform)nm.transform; nrt.SetParent(card.transform, false);
        nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(1, 0); nrt.pivot = new Vector2(0.5f, 0);
        nrt.anchoredPosition = new Vector2(0, 4); nrt.sizeDelta = new Vector2(0, 30);
        var nt = nm.GetComponent<Text>(); nt.font = font; nt.text = w.name; nt.fontSize = 23; nt.fontStyle = FontStyle.Bold;
        nt.alignment = TextAnchor.MiddleCenter; nt.color = equipped ? new Color(0.2f, 0.45f, 0.15f) : new Color(0.26f, 0.16f, 0.08f); nt.raycastTarget = false;

        var btn = card.GetComponent<Button>(); btn.targetGraphic = simg;
        var colors = btn.colors; colors.highlightedColor = new Color(1f, 1f, 1f, 1f); colors.pressedColor = new Color(0.8f, 0.8f, 0.8f); btn.colors = colors;
        int captured = idx;
        btn.onClick.AddListener(() => { if (wield != null) { wield.Equip(captured); Build(); } });
    }

    void Badge(Transform parent, string txt, Vector2 anchor, Vector2 pos, int size, Color color)
    {
        var go = new GameObject("Key", typeof(RectTransform), typeof(Text));
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(36, 36);
        var t = go.GetComponent<Text>(); t.font = font; t.text = txt; t.fontSize = size; t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter; t.color = color; t.raycastTarget = false;
        var o = go.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.9f); o.effectDistance = new Vector2(1.5f, -1.5f);
    }
}
