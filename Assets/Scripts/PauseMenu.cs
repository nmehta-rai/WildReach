using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Esc opens a pause menu: freezes the game (Time.timeScale = 0), frees the cursor, and offers Resume / Restart to
// Spawn / Quit. Builds its own canvas at runtime, so it just needs to live on one GameObject in the scene (use
// WildReach/Setup Pause Menu). "Restart to Spawn" reuses RespawnController.ToSpawn (teleport + full heal + mobs
// home). Won't pop over the inventory (which already pauses) — Esc there closes the inventory first.
[DisallowMultipleComponent]
public class PauseMenu : MonoBehaviour
{
    GameObject root; bool open;
    PlayerMovement move; RespawnController respawn;
    Font font;

    void Start()
    {
        if (!Application.isPlaying) return;
        move    = Object.FindFirstObjectByType<PlayerMovement>();
        respawn = Object.FindFirstObjectByType<RespawnController>();
        font    = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        EnsureEventSystem();
        Build();
        root.SetActive(false);
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (open) Resume();
            else if (Time.timeScale > 0f) Open();   // don't pop over another paused menu (e.g. inventory)
        }
    }

    void Open()
    {
        open = true; root.SetActive(true); Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }

    void Resume()
    {
        open = false; root.SetActive(false); Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }

    void Restart()
    {
        if (respawn != null) respawn.ToSpawn();
        Resume();
    }

    void Quit()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---- UI built in code so there's no prefab to wire ----
    void Build()
    {
        root = new GameObject("PauseMenu_Canvas");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 900;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        root.AddComponent<GraphicRaycaster>();

        // dim full-screen backdrop (blocks clicks/world behind)
        var bg = NewImage("Dim", root.transform, new Color(0.02f, 0.03f, 0.05f, 0.78f));
        Stretch(bg.rectTransform);

        // centered panel
        var panel = NewImage("Panel", root.transform, new Color(0.08f, 0.10f, 0.14f, 0.96f));
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(440, 460);

        Label("PAUSED", panel.transform, 46, new Vector2(0, 160), FontStyle.Bold, new Color(0.85f, 0.92f, 1f));

        Button("Resume", panel.transform, new Vector2(0, 60), Resume);
        Button("Restart to Spawn", panel.transform, new Vector2(0, -20), Restart);
        Button("Quit", panel.transform, new Vector2(0, -100), Quit);

        Label("Esc to resume", panel.transform, 18, new Vector2(0, -180), FontStyle.Italic, new Color(0.5f, 0.56f, 0.66f));
    }

    Image NewImage(string name, Transform parent, Color c)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>(); img.color = c; return img;
    }

    void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }

    Text Label(string text, Transform parent, int size, Vector2 pos, FontStyle style, Color col)
    {
        var go = new GameObject("Label"); go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = text; t.font = font; t.fontSize = size; t.fontStyle = style; t.color = col;
        t.alignment = TextAnchor.MiddleCenter; t.horizontalOverflow = HorizontalWrapMode.Overflow;
        var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(400, size + 16);
        return t;
    }

    void Button(string text, Transform parent, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(text + "Btn"); go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>(); img.color = new Color(0.16f, 0.20f, 0.27f, 1f);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var cb = btn.colors; cb.highlightedColor = new Color(0.24f, 0.46f, 0.62f, 1f);
        cb.pressedColor = new Color(0.30f, 0.56f, 0.74f, 1f); cb.fadeDuration = 0.08f; btn.colors = cb;
        btn.onClick.AddListener(onClick);
        var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(320, 64);
        var lbl = Label(text, go.transform, 26, Vector2.zero, FontStyle.Normal, new Color(0.92f, 0.95f, 1f));
        Stretch(lbl.rectTransform);
    }

    void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }
}
