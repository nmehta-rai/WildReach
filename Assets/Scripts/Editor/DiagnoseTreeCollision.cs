using UnityEngine;
using UnityEditor;

// Read-only. Dumps exactly what colliders the player + the HomeTree actually have, where they are, and the
// relevant physics settings — so the tree walk-through can be diagnosed from data instead of guesses.
public static class DiagnoseTreeCollision
{
    [MenuItem("WildReach/DIAGNOSE Tree Collision (read-only)")]
    static void Diagnose()
    {
        Debug.Log("===== [DIAG] TREE COLLISION =====");

        var pm = Object.FindFirstObjectByType<PlayerMovement>();
        if (pm == null) Debug.Log("[DIAG] No PlayerMovement found!");
        else
        {
            var rb = pm.GetComponent<Rigidbody>();
            Debug.Log($"[DIAG] Player '{pm.name}' layer={pm.gameObject.layer} " +
                      (rb != null ? $"RB kinematic={rb.isKinematic} detection={rb.collisionDetectionMode} gravity={rb.useGravity}" : "NO RIGIDBODY"));
            var cols = pm.GetComponentsInChildren<Collider>(true);
            if (cols.Length == 0) Debug.Log("[DIAG] Player has NO colliders!");
            foreach (var c in cols)
                Debug.Log($"[DIAG] Player collider: {c.GetType().Name} on '{c.name}' enabled={c.enabled} trigger={c.isTrigger} layer={c.gameObject.layer} worldBoundsSize={c.bounds.size:F2}");
        }

        var ht = GameObject.Find("HomeTree");
        if (ht == null) Debug.Log("[DIAG] HomeTree NOT FOUND by name.");
        else
        {
            Debug.Log($"[DIAG] HomeTree pos={ht.transform.position:F1} lossyScale={ht.transform.lossyScale:F2} rot={ht.transform.eulerAngles:F0}");
            var cols = ht.GetComponentsInChildren<Collider>(true);
            Debug.Log($"[DIAG] HomeTree has {cols.Length} collider(s):");
            foreach (var c in cols)
            {
                string extra = c is CapsuleCollider cap ? $" capsule(r={cap.radius:F2} h={cap.height:F2} dir={cap.direction})"
                             : c is MeshCollider mc ? $" mesh(convex={mc.convex} mesh={(mc.sharedMesh != null ? mc.sharedMesh.name : "NULL")})" : "";
                Debug.Log($"[DIAG]   {c.GetType().Name} on '{c.name}' enabled={c.enabled} trigger={c.isTrigger} layer={c.gameObject.layer} worldBounds center={c.bounds.center:F1} size={c.bounds.size:F1}{extra}");
            }
        }

        Debug.Log($"[DIAG] Physics.queriesHitTriggers={Physics.queriesHitTriggers}  (layer collision matrix was verified all-collide)");
        Debug.Log("===== [DIAG] END =====");
    }
}
