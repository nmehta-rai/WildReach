using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

// Builds the pouch: a dimmed overlay + the parchment panel holding a clean centered ROW of weapon cards
// (big icon + name + key badge + equipped highlight). Cards are built at runtime by InventoryUI from the
// player's weapons. Toggled with I. Re-runnable.
public static class SetupInventoryMenu
{
    const string UIDir = "Assets/UI/";
    const string IconDir = "Assets/UI/Inventory/";
    static readonly Vector2 RefRes = new Vector2(1920, 1080);

    [MenuItem("WildReach/Setup Inventory Menu")]
    static void Setup()
    {
        AssetDatabase.Refresh();
        var panelSprite = Sprite("Panel.png", UIDir);
        var slotSprite  = Sprite("Slot.png", IconDir);

        var canvas = EnsureCanvas();
        EnsureEventSystem();
        Kill(canvas.transform.Find("InventoryMenu"));

        var root = NewUI("InventoryMenu", canvas.transform); Stretch(root);

        var dim = AddImage(root, "Dim", null, 0); Stretch(dim.rectTransform);
        dim.color = new Color(0, 0, 0, 0.66f); dim.raycastTarget = true;

        float pw = 940f, ph = panelSprite != null ? pw * (panelSprite.rect.height / panelSprite.rect.width) : pw * 0.6f;
        var panel = AddImage(root, "Panel", panelSprite, 0); panel.preserveAspect = true;
        panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        panel.rectTransform.sizeDelta = new Vector2(pw, ph);

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var title = NewUI("Title", panel.transform);
        title.anchorMin = new Vector2(0, 1); title.anchorMax = new Vector2(1, 1); title.pivot = new Vector2(0.5f, 1);
        title.anchoredPosition = new Vector2(0, -44); title.sizeDelta = new Vector2(0, 48);
        var t = title.gameObject.AddComponent<Text>();
        t.font = font; t.text = "WEAPONS"; t.fontSize = 38; t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter; t.color = new Color(0.97f, 0.91f, 0.76f); t.raycastTarget = false;
        var ol = title.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0, 0, 0, 0.85f); ol.effectDistance = new Vector2(2, -2);

        // centered row container — InventoryUI drops weapon cards in here
        var row = NewUI("Cards", panel.transform);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f); row.pivot = new Vector2(0.5f, 0.5f);
        row.anchoredPosition = new Vector2(0, -6); row.sizeDelta = new Vector2(pw - 180, 240);
        var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 28; hl.childAlignment = TextAnchor.MiddleCenter;
        hl.childControlWidth = hl.childControlHeight = false; hl.childForceExpandWidth = hl.childForceExpandHeight = false;

        var menu = canvas.GetComponent<InventoryMenu>() ?? canvas.gameObject.AddComponent<InventoryMenu>();
        var mso = new SerializedObject(menu); mso.FindProperty("root").objectReferenceValue = root.gameObject; mso.ApplyModifiedProperties();

        var invUI = root.gameObject.GetComponent<InventoryUI>() ?? root.gameObject.AddComponent<InventoryUI>();
        var iso = new SerializedObject(invUI);
        iso.FindProperty("cardRow").objectReferenceValue = row;
        iso.FindProperty("slotSprite").objectReferenceValue = slotSprite;
        iso.ApplyModifiedProperties();

        EditorUtility.SetDirty(canvas.gameObject);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root.gameObject;
        Debug.Log("[InvMenu] Rebuilt the pouch as a clean weapon row. Press I in Play — click a card or press 1/2.");
    }

    // --- helpers -------------------------------------------------------------

    static void Kill(Transform tr) { if (tr != null) Object.DestroyImmediate(tr.gameObject); }

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    static Sprite Sprite(string file, string dir)
    {
        string path = dir + file;
        if (!File.Exists(path)) { Debug.LogWarning("[InvMenu] missing " + path); return null; }
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { AssetDatabase.ImportAsset(path); ti = AssetImporter.GetAtPath(path) as TextureImporter; }
        if (ti != null && (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single || ti.mipmapEnabled))
        {
            ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Canvas EnsureCanvas()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var go = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        }
        if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
        var sc = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = RefRes; sc.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static Image AddImage(Transform parent, string name, Sprite sprite, float size)
    {
        var rt = NewUI(name, parent);
        if (size > 0) rt.sizeDelta = new Vector2(size, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite; img.raycastTarget = false;
        if (sprite != null) img.preserveAspect = true;
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
