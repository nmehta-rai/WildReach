using UnityEngine;
using UnityEditor;

// Drops a placeholder predator (a capsule) + a respawn point and wires the combat loop, so the
// chase/attack/death/respawn flow can be tested today — before the snow leopard is rigged. When the
// rigged creature lands, swap the placeholder's mesh for it and add an Animator with Speed/Attack.
public static class SetupPredator
{
    [MenuItem("WildReach/Combat/Setup Predator (placeholder)")]
    static void Setup()
    {
        var pm = Object.FindAnyObjectByType<PlayerMovement>();
        if (pm == null) { Debug.LogError("[Predator] No PlayerMovement in scene."); return; }
        if (pm.GetComponent<HealthSystem>() == null) pm.gameObject.AddComponent<HealthSystem>();
        var terrain = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();

        // respawn point at the player's current spot
        var rp = GameObject.Find("RespawnPoint") ?? new GameObject("RespawnPoint");
        rp.transform.position = pm.transform.position;
        var rc = pm.GetComponent<RespawnController>() ?? pm.gameObject.AddComponent<RespawnController>();
        var so = new SerializedObject(rc);
        so.FindProperty("respawnPoint").objectReferenceValue = rp.transform;
        so.ApplyModifiedProperties();

        // placeholder predator ~15 m in front of the player
        var old = GameObject.Find("Predator"); if (old != null) Object.DestroyImmediate(old);
        var pred = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        pred.name = "Predator";
        pred.transform.localScale = new Vector3(1.2f, 1.2f, 2.0f);
        Vector3 pos = pm.transform.position + pm.transform.forward * 60f;   // well beyond its detect range from spawn
        if (terrain != null) pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y + 1.2f;
        pred.transform.position = pos;
        pred.AddComponent<Predator>();

        Selection.activeGameObject = pred;
        Debug.Log("[Predator] Placeholder predator + respawn point wired. Press Play, walk near the capsule: " +
                  "it prowls, chases, attacks you to death, then you fade out and respawn at the start. " +
                  "Move 'Predator' to the north lair and 'RespawnPoint' wherever you want once it feels right.");
    }
}
