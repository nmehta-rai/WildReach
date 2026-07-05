using UnityEngine;

// A pickup-able item lying on the ground: gentle bob + sparkle so you know to grab it, and an "[E] Pick up"
// prompt when the player is close. On E it hands itself to the player's WieldSystem. Goes quiet while held.
public class ItemPickup : MonoBehaviour
{
    [SerializeField] string itemName = "Stick";
    [SerializeField] Sprite icon;                 // inventory icon (rendered from the mesh)
    [SerializeField] float pickupRange = 2.4f;
    public Sprite Icon => icon;
    [SerializeField] float bobHeight = 0.10f;
    [SerializeField] float bobSpeed  = 1.6f;
    [SerializeField] float spinSpeed = 20f;

    public string ItemName => itemName;

    bool held;
    bool near;
    float phase;
    Vector3 groundPos;
    Quaternion groundRot;
    Transform player;

    void Start()
    {
        phase = Random.value * 10f;
        groundPos = transform.position; groundRot = transform.rotation;
        var pm = Object.FindFirstObjectByType<PlayerMovement>();
        if (pm != null) player = pm.transform;
    }

    // Called by WieldSystem when picked up / dropped — silences the bob/sparkle/collider while carried.
    public void SetHeld(bool h)
    {
        held = h; near = false;
        foreach (var ps in GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(!h);
        foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = !h;
        if (!h) { groundPos = transform.position; groundRot = transform.rotation; }
    }

    void Update()
    {
        if (held || player == null) return;

        // float + slow spin in place so it reads as "grab me"
        transform.position = groundPos + Vector3.up * (bobHeight + Mathf.Sin((Time.time + phase) * bobSpeed) * bobHeight);
        transform.rotation = groundRot * Quaternion.Euler(0f, (Time.time + phase) * spinSpeed, 0f);

        near = Vector3.Distance(player.position, groundPos) < pickupRange;
    }

    void OnGUI()
    {
        if (held || !near) return;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.white;
        var r = new Rect(0, Screen.height * 0.62f, Screen.width, 40);
        GUI.color = new Color(0, 0, 0, 0.5f); GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), $"[E]  Pick up {itemName}", style);
        GUI.color = Color.white; GUI.Label(r, $"[E]  Pick up {itemName}", style);
    }
}
