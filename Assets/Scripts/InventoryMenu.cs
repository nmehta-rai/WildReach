using UnityEngine;

// Toggles the full inventory overlay (the parchment panel + item grid) with the I key.
// Pauses the game while open; Esc or I closes it. The menu hierarchy is built by the
// SetupInventoryMenu editor tool, which assigns `root`.
public class InventoryMenu : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] KeyCode toggleKey = KeyCode.I;

    bool open;

    void Start()
    {
        open = false;
        if (root != null) root.SetActive(false);   // built active for edit-mode review; start closed in play
    }

    void Update()
    {
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
