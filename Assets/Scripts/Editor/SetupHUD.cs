using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

// Builds the clean BotW-style default HUD: a small top-left vitals cluster (3 warm hearts + a
// slim stamina ring that hides when full). Rips out the old screen-wide inventory bar and the
// giant green stamina disc. Re-runnable. The full inventory menu + quick-switch strip come later.
public static class SetupHUD
{
    const string UIDir = "Assets/UI/";
    static readonly Vector2 RefRes = new Vector2(1920, 1080);

    const float HeartSize = 40f;
    const float HeartGap  = 6f;
    const float RingSize  = 66f;

    [MenuItem("WildReach/Setup HUD (Vitals)")]
    static void Setup()
    {
        AssetDatabase.Refresh();                 // pick up PNGs written outside Unity
        var heartFull  = LoadSprite("Heart.png");
        var heartEmpty = LoadSprite("Heart_Empty.png");
        var ring       = EnsureRingSprite();
        if (heartFull == null) { Debug.LogError("[HUD] Missing Assets/UI/Heart.png — run _tools/make_heart.py"); return; }

        var canvas = EnsureCanvas();

        // --- clear the old stuff ---
        Kill(canvas.transform.Find("InventoryHUD"));
        Kill(canvas.transform.Find("StaminaRing"));
        Kill(canvas.transform.Find("Vitals"));
        foreach (var old in Object.FindObjectsByType<StaminaUI>()) Object.DestroyImmediate(old);

        // --- systems ---
        var stamSys = Object.FindAnyObjectByType<StaminaSystem>();
        var healthHost = stamSys != null ? stamSys.gameObject : canvas.gameObject;
        var health = healthHost.GetComponent<HealthSystem>() ?? healthHost.AddComponent<HealthSystem>();

        // --- vitals container, top-left ---
        var vitals = NewUI("Vitals", canvas.transform);
        vitals.anchorMin = vitals.anchorMax = new Vector2(0, 1);
        vitals.pivot = new Vector2(0, 1);
        vitals.anchoredPosition = new Vector2(36, -30);
        vitals.sizeDelta = new Vector2(360, 130);

        // hearts row
        var heartImgs = new Image[health.MaxHearts];
        for (int i = 0; i < health.MaxHearts; i++)
        {
            var h = AddImage(vitals, "Heart" + i, heartFull, HeartSize);
            h.rectTransform.pivot = new Vector2(0, 1);
            h.rectTransform.anchorMin = h.rectTransform.anchorMax = new Vector2(0, 1);
            h.rectTransform.anchoredPosition = new Vector2(i * (HeartSize + HeartGap), 0);
            heartImgs[i] = h;
        }
        var heartsUI = vitals.gameObject.AddComponent<HeartsUI>();
        var hso = new SerializedObject(heartsUI);
        hso.FindProperty("health").objectReferenceValue = health;
        hso.FindProperty("fullSprite").objectReferenceValue = heartFull;
        hso.FindProperty("emptySprite").objectReferenceValue = heartEmpty;
        var arr = hso.FindProperty("hearts");
        arr.arraySize = heartImgs.Length;
        for (int i = 0; i < heartImgs.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = heartImgs[i];
        hso.ApplyModifiedProperties();

        // stamina ring, just below the hearts
        var stam = NewUI("Stamina", vitals);
        stam.pivot = new Vector2(0, 1);
        stam.anchorMin = stam.anchorMax = new Vector2(0, 1);
        stam.anchoredPosition = new Vector2(2, -(HeartSize + 14));
        stam.sizeDelta = new Vector2(RingSize, RingSize);
        var group = stam.gameObject.AddComponent<CanvasGroup>();

        var track = AddImage(stam, "Track", ring, RingSize);
        track.color = new Color(0, 0, 0, 0.35f);
        var fill = AddImage(stam, "Fill", ring, RingSize);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = true;
        fill.fillAmount = 1f;
        fill.color = new Color(0.50f, 0.86f, 0.42f);

        var stamUI = vitals.gameObject.AddComponent<StaminaUI>();
        var sso = new SerializedObject(stamUI);
        sso.FindProperty("fillRing").objectReferenceValue = fill;
        sso.FindProperty("group").objectReferenceValue = group;
        sso.FindProperty("staminaSystem").objectReferenceValue = stamSys;
        sso.ApplyModifiedProperties();

        EditorUtility.SetDirty(canvas.gameObject);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = vitals.gameObject;
        Debug.Log($"[HUD] Clean vitals built: {health.MaxHearts} hearts + stamina ring (hides when full). " +
                  (stamSys == null ? "No StaminaSystem found — ring won't animate until one exists." : "Wired to StaminaSystem."));
    }

    // --- helpers -------------------------------------------------------------

    static void Kill(Transform t) { if (t != null) Object.DestroyImmediate(t.gameObject); }

    // Loads a PNG as a Sprite, forcing the importer to Sprite first (PNGs default to Texture,
    // which makes LoadAssetAtPath<Sprite> return null).
    static Sprite LoadSprite(string file)
    {
        string path = UIDir + file;
        if (!File.Exists(path)) return null;
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { AssetDatabase.ImportAsset(path); ti = AssetImporter.GetAtPath(path) as TextureImporter; }
        if (ti != null && (ti.textureType != TextureImporterType.Sprite
                           || ti.spriteImportMode != SpriteImportMode.Single || ti.mipmapEnabled))
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;   // not Multiple — else it's a sub-asset
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite EnsureRingSprite()
    {
        string path = UIDir + "Ring.png";
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float c = (size - 1) / 2f, R = c - 3f, ri = R * 0.70f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                float a = Mathf.Clamp01(Mathf.Min(R - d + 0.5f, d - ri + 0.5f));
                px[y * size + x] = new Color(1, 1, 1, a);
            }
        tex.SetPixels(px); tex.Apply();
        Directory.CreateDirectory(UIDir);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null) { ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Canvas EnsureCanvas()
    {
        var canvas = Object.FindAnyObjectByType<Canvas>();
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
        rt.sizeDelta = new Vector2(size, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = true;
        return img;
    }
}
