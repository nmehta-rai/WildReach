using UnityEngine;

// Boarding + sailing. When the player is standing on the deck (inside the BoardZone trigger),
// a "Press F to Sail" prompt appears. Pressing F hides the player and hands camera + control to
// the ship; the ship then drives on the water with WASD. F again disembarks (player reappears on
// deck). Kinematic body — moved by transform, no buoyancy physics. Look/animation polish is later.
public class ShipController : MonoBehaviour
{
    // wired by Setup Sailing
    public Transform player;
    public CameraFollow cameraFollow;
    public Transform deckSpawn;
    public float waterLevel = 1.5f;

    [SerializeField] float draft         = 2.5f;   // keel sits this far below the surface (bigger = ship sits deeper)
    [SerializeField] float moveSpeed     = 14f;
    [SerializeField] float turnSpeed     = 45f;    // manual A/D override turn rate
    [SerializeField] float lookTurnSpeed = 60f;    // how fast the bow swings toward where you look

    [Header("Visual alignment — scrub until the bow points along the ship's forward (+Z) arrow")]
    [SerializeField] float modelYaw = 0f;          // rotates ONLY the visual model; live in the editor via OnValidate
    [SerializeField] float hullClearance = 6f;     // how far ahead to stop before ramming land
    [SerializeField] float hullRadius    = 2.5f;   // probe thickness for the land check
    [SerializeField] float camDistance   = 28f;    // third-person chase view for the ship (much bigger than the player's)
    [SerializeField] float camHeight     = 9f;

    Rigidbody rb;
    bool sailing;
    bool playerOnDeck;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;
        ApplyModelYaw();
    }

    void OnValidate() => ApplyModelYaw();   // live preview while you scrub modelYaw in the Inspector

    // Spins ONLY the visual ShipModel child around vertical, keeping its upright tilt, so its bow
    // lines up with the control root's forward (+Z, the direction it actually sails).
    void ApplyModelYaw()
    {
        var model = transform.Find("ShipModel");
        if (model != null)
            model.localRotation = Quaternion.Euler(0f, modelYaw, 0f) * Quaternion.Euler(-90f, 0f, 0f);
    }

    void Update()
    {
        if (!sailing)
        {
            if (playerOnDeck && Input.GetKeyDown(KeyCode.F)) BecomeShip();
        }
        else if (Input.GetKeyDown(KeyCode.F))
        {
            LeaveShip();
        }
    }

    void FixedUpdate()
    {
        if (!sailing) return;

        float throttle = Input.GetAxis("Vertical");
        float steer    = Input.GetAxis("Horizontal");

        if (Mathf.Abs(steer) > 0.05f)
        {
            // manual override — arrow keys take precedence, turn directly
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, steer * turnSpeed * Time.fixedDeltaTime, 0f));
        }
        else if (cameraFollow != null && Mathf.Abs(throttle) > 0.05f)
        {
            // otherwise steer toward where you're looking (mouse-aim), like the player
            Vector3 look = Vector3.ProjectOnPlane(cameraFollow.transform.forward, Vector3.up);
            if (look.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(look.normalized, Vector3.up);
                rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, target, lookTurnSpeed * Time.fixedDeltaTime));
            }
        }

        float step = throttle * moveSpeed * Time.fixedDeltaTime;
        if (Mathf.Abs(step) > 0.0001f && BlockedAhead(Mathf.Sign(step), Mathf.Abs(step)))
            step = 0f;                              // land ahead — don't sail through the island

        Vector3 pos = rb.position + transform.forward * step;
        pos.y = waterLevel - draft;                 // stay locked to the surface
        rb.MovePosition(pos);
    }

    // Sweeps a fat probe in the travel direction; true if something solid (land/props) is in the way.
    bool BlockedAhead(float sign, float step)
    {
        Vector3 origin = rb.position + Vector3.up * 1f;
        Vector3 dir    = transform.forward * sign;
        float dist     = step + hullClearance;
        foreach (var h in Physics.SphereCastAll(origin, hullRadius, dir, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;  // ignore the ship's own colliders
            return true;
        }
        return false;
    }

    void BecomeShip()
    {
        sailing = true;
        if (player != null) player.gameObject.SetActive(false);
        if (cameraFollow != null) { cameraFollow.SetTarget(transform); cameraFollow.SetView(camDistance, camHeight); }
    }

    void LeaveShip()
    {
        sailing = false;
        if (player != null)
        {
            player.gameObject.SetActive(true);
            player.position = deckSpawn != null ? deckSpawn.position : transform.position + Vector3.up * 4f;
            var prb = player.GetComponent<Rigidbody>();
            if (prb != null) prb.linearVelocity = Vector3.zero;
        }
        if (cameraFollow != null) { cameraFollow.SetTarget(player); cameraFollow.ResetView(); }
    }

    void OnTriggerStay(Collider other)
    {
        if (other.GetComponentInParent<PlayerMovement>() != null) playerOnDeck = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerMovement>() != null) playerOnDeck = false;
    }

    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.white;
        if (sailing)
            GUI.Label(new Rect(Screen.width / 2 - 200, Screen.height - 70, 400, 30), "Sailing  —  WASD to steer,  F to disembark", style);
        else if (playerOnDeck)
            GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height - 70, 300, 30), "Press  F  to Sail", style);
    }
}
