using UnityEngine;
using UnityEditor;

// One-click wiring for the swim + climb + sail loop:
//  - Player gets a SwimSystem (so it floats instead of falling through the world).
//  - Ship gets MeshColliders on every mesh (so the existing ClimbingSystem can climb the hull),
//    a kinematic Rigidbody, a ShipController, a deck BoardZone trigger, and a DeckSpawn point.
//  - References (player, camera, deck spawn, water level) are all hooked up.
// Re-runnable: reuses existing components/children instead of duplicating them.
public static class SetupSailing
{
    [MenuItem("WildReach/Setup Sailing")]
    static void Setup()
    {
        var pm = Object.FindObjectOfType<PlayerMovement>();
        if (pm == null) { Debug.LogError("[Sailing] No PlayerMovement in the scene — can't find the player."); return; }
        var player = pm.gameObject;

        var ship = GameObject.Find("Ship");
        if (ship == null) { Debug.LogError("[Sailing] No 'Ship' in the scene — run Setup Ship first."); return; }

        var cam = Object.FindObjectOfType<CameraFollow>();

        // Use the ACTUAL water surface Y, not the constant — the Water object may have been moved,
        // and a mismatch is what makes the swimmer float at the wrong height.
        var waterGo = GameObject.Find("Water");
        float sea = waterGo != null ? waterGo.transform.position.y : SetupWater.SeaLevel;
        Debug.Log($"[Sailing] Using water surface Y = {sea} (from the Water object).");

        // --- Player: swim ---
        var swim = player.GetComponent<SwimSystem>();
        if (swim == null) swim = Undo.AddComponent<SwimSystem>(player);
        swim.SetWaterLevel(sea);
        swim.SetFloatOffset(-0.3f);   // tread: chest-deep (pivot ≈ body centre)
        swim.SetSwimOffset(-0.1f);    // swim: half-submerged, head/back above (tune on the SwimSystem)

        // --- Player: strip the old trail-ribbon wake (looked bad — a different approach comes later) ---
        var pWake = player.GetComponent<WaterWake>();
        if (pWake != null) Undo.DestroyObjectImmediate(pWake);

        // --- Ship: climbable colliders ---
        int colliders = 0;
        foreach (var mf in ship.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var mc = mf.GetComponent<MeshCollider>();
            if (mc == null) mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;     // concave is fine for a kinematic body; lets you climb the hull shape
            colliders++;
        }

        // --- Ship: kinematic body ---
        var rb = ship.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(ship);
        rb.isKinematic = true;
        rb.useGravity  = false;

        // --- Ship: strip the old trail-ribbon wake (looked bad — a different approach comes later) ---
        var sWake = ship.GetComponent<WaterWake>();
        if (sWake != null) Undo.DestroyObjectImmediate(sWake);

        // --- Ship: local-space bounds (scale/rotation independent) for placing the zone + spawn ---
        Bounds lb = LocalBounds(ship);

        // --- Ship: deck board-zone trigger ---
        var zoneT = ship.transform.Find("BoardZone");
        GameObject zone = zoneT != null ? zoneT.gameObject : new GameObject("BoardZone");
        zone.transform.SetParent(ship.transform, false);
        zone.transform.localPosition = lb.center;          // cover the WHOLE ship, not just the deck
        zone.transform.localRotation = Quaternion.identity;
        zone.transform.localScale    = Vector3.one;
        var box = zone.GetComponent<BoxCollider>();
        if (box == null) box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = lb.size * 1.2f;                          // a bit oversized so swimming up to the hull counts as contact

        // --- Ship: deck spawn (where the player reappears on disembark) ---
        var spawnT = ship.transform.Find("DeckSpawn");
        GameObject spawn = spawnT != null ? spawnT.gameObject : new GameObject("DeckSpawn");
        spawn.transform.SetParent(ship.transform, false);
        spawn.transform.localPosition = lb.center + Vector3.up * (lb.size.y * 0.55f);

        // --- Ship: controller + references ---
        var ctrl = ship.GetComponent<ShipController>();
        if (ctrl == null) ctrl = Undo.AddComponent<ShipController>(ship);
        ctrl.player       = player.transform;
        ctrl.cameraFollow = cam;
        ctrl.deckSpawn    = spawn.transform;
        ctrl.waterLevel   = sea;

        EditorUtility.SetDirty(ship);
        EditorUtility.SetDirty(player);
        Debug.Log($"[Sailing] Wired. Player can swim; ship has {colliders} climb colliders, a board zone + deck spawn, " +
                  $"and a ShipController (waterLevel={sea}). Press Play: walk off the coast to swim, press E facing the " +
                  $"hull to climb aboard, then F on deck to Sail. F again to disembark." +
                  (cam == null ? "  NOTE: no CameraFollow found — camera won't retarget to the ship." : ""));
    }

    // Combined bounds of all child meshes expressed in the ROOT's local space (so it ignores the
    // ship's scale and rotation — a box built from this lines up with the deck regardless).
    static Bounds LocalBounds(GameObject root)
    {
        var w2l = root.transform.worldToLocalMatrix;
        bool has = false;
        Bounds b = new Bounds();
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var m  = w2l * mf.transform.localToWorldMatrix;
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = mb.center + Vector3.Scale(mb.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = m.MultiplyPoint3x4(corner);
                if (!has) { b = new Bounds(p, Vector3.zero); has = true; }
                else b.Encapsulate(p);
            }
        }
        return b;
    }
}
