using UnityEngine;

// Toggles the full inventory overlay (the parchment panel + item grid) with the I key.
// Pauses the game while open; Esc or I closes it. The menu hierarchy is built by the
// SetupInventoryMenu editor tool, which assigns `root`. [ExecuteAlways] keeps the giant
// parchment hidden in edit mode so it doesn't block the Scene view.
[ExecuteAlways]
public class InventoryMenu : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] KeyCode toggleKey = KeyCode.I;

    bool open;

    void OnEnable()
    {
        // keep it closed in the editor; it only opens on I during play
        if (!Application.isPlaying && root != null) root.SetActive(false);
    }

    void Start()
    {
        if (!Application.isPlaying) return;
        open = false;
        if (root != null) root.SetActive(false);   // start closed in play; opens on I
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (Input.GetKeyDown(toggleKey) || (open && Input.GetKeyDown(KeyCode.Escape)))
            Toggle();
    }

    void Toggle()
    {
        open = !open;
        if (root != null) root.SetActive(open);
        Time.timeScale = open ? 0f : 1f;
    }
}
