using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

// Builds the full inventory menu: a dimmed overlay + the parchment Panel with a grid of slots
// holding the cut item icons. Toggled with I (see InventoryMenu). Re-runnable. Built active so
// you can review it in the Game view immediately; InventoryMenu starts it closed in play.
public static class SetupInventoryMenu
{
    const string UIDir = "Assets/UI/";
    const string IconDir = "Assets/UI/Inventory/";
    static readonly Vector2 RefRes = new Vector2(1920, 1080);

    // items shown in the grid (Slot/Pouch are frames, not items)
    static readonly string[] Items = { "Axe", "Flint", "Log", "Rope", "Cloth", "Hide", "Meat", "Berries", "Sap" };
    const int Cols = 6, Rows = 3;                 // 18 slots; items fill the first 9
    const float Cell = 130f, IconInset = 96f, Spacing = 14f;

    [MenuItem("WildReach/Setup Inventory Menu")]
    static void Setup()
    {
        AssetDatabase.Refresh();
        var panelSprite = Sprite("Panel.png", UIDir);
        var slotSprite  = Sprite("Slot.png", IconDir);
        if (panelSprite == null || slotSprite == null) { Debug.LogError("[InvMenu] Missing Panel.png or Inventory/Slot.png"); return; }

        var canvas = EnsureCanvas();
        Kill(canvas.transform.Find("InventoryMenu"));

        // root (full screen), built active for review
        var root = NewUI("InventoryMenu", canvas.transform);
        Stretch(root);

        // dim backdrop that also blocks clicks behind
        var dim = AddImage(root, "Dim", null, 0);
        Stretch(dim.rectTransform);
        dim.color = new Color(0, 0, 0, 0.55f);
        dim.raycastTarget = true;

        // parchment panel, centred
        float pw = 1200f, ph = pw * (panelSprite.rect.height / panelSprite.rect.width);
        var panel = AddImage(root, "Panel", panelSprite, 0);
        panel.preserveAspect = true;
        panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        panel.rectTransform.sizeDelta = new Vector2(pw, ph);

        // title
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            var title = NewUI("Title", panel.transform);
            title.anchorMin = new Vector2(0, 1); title.anchorMax = new Vector2(1, 1); title.pivot = new Vector2(0.5f, 1);
            title.anchoredPosition = new Vector2(0, -54); title.sizeDelta = new Vector2(0, 50);
            var t = title.gameObject.AddComponent<Text>();
            t.font = font; t.text = "POUCH"; t.fontSize = 40; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.color = new Color(0.36f, 0.22f, 0.12f); t.raycastTarget = false;
        }

        // grid container inset into the parchment area
        var content = NewUI("Grid", panel.transform);
        content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(1, 1);
        content.offsetMin = new Vector2(130, 90); content.offsetMax = new Vector2(-130, -150);
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(Cell, Cell);
        grid.spacing = new Vector2(Spacing, Spacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Cols;
        grid.childAlignment = TextAnchor.UpperCenter;

        for (int i = 0; i < Cols * Rows; i++)
        {
            var slot = AddImage(content, "Slot" + i, slotSprite, 0);   // size driven by grid
            if (i < Items.Length)
            {
                var icon = Sprite(Items[i] + ".png", IconDir);
                if (icon != null)
                {
                    var im = AddImage(slot.rectTransform, Items[i], icon, IconInset);
                    im.rectTransform.anchorMin = im.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    im.rectTransform.anchoredPosition = Vector2.zero;
                }
            }
        }

        // wire the toggle onto the canvas
        var menu = canvas.GetComponent<InventoryMenu>() ?? canvas.gameObject.AddComponent<InventoryMenu>();
        var so = new SerializedObject(menu);
        so.FindProperty("root").objectReferenceValue = root.gameObject;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(canvas.gameObject);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root.gameObject;
        Debug.Log("[InvMenu] Built inventory menu (9 items in an 18-slot grid). Press I in Play to toggle.");
    }

    // --- helpers -------------------------------------------------------------

    static void Kill(Transform t) { if (t != null) Object.DestroyImmediate(t.gameObject); }

    static Sprite Sprite(string file, string dir)
    {
        string path = dir + file;
        if (!File.Exists(path)) { Debug.LogWarning("[InvMenu] missing " + path); return null; }
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { AssetDatabase.ImportAsset(path); ti = AssetImporter.GetAtPath(path) as TextureImporter; }
        if (ti != null && (ti.textureType != TextureImporterType.Sprite
                           || ti.spriteImportMode != SpriteImportMode.Single || ti.mipmapEnabled))
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
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
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
        }
        var sc = canvas.GetComponent<CanvasScaler>();
        if (sc != null) { sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = RefRes; sc.matchWidthOrHeight = 0.5f; }
        return canvas;
    }

    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static Image AddImage(Transform parent, string name, Sprite sprite, float size)
    {
        var rt = NewUI(name, parent);
        if (size > 0) rt.sizeDelta = new Vector2(size, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        if (sprite != null) img.preserveAspect = true;
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
