using UnityEngine;
using UnityEditor;

// Adds a (non-convex, static) MeshCollider to every mesh under the selected object
// so the player can't walk through it — and so the climb raycasts have a surface.
public static class AddTreeCollider
{
    [MenuItem("WildReach/Add Tree Collider")]
    static void Add()
    {
        var go = Selection.activeGameObject;
        if (go == null) { Debug.LogError("[Tree] Select the tree first."); return; }

        int added = 0;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.GetComponent<MeshCollider>() != null) continue;
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;   // static prop — concave collider is fine and accurate for climbing
            added++;
        }
        Debug.Log($"[Tree] Added {added} MeshCollider(s) to '{go.name}'. Player will collide + can climb it.");
    }

    // Convert the trunk's existing mesh colliders to CONVEX. Non-convex colliders are one-sided and keep their
    // concave folds, so the flared/buttress base of a giant tree lets the player slip through a back-face into the
    // hollow centre and then climb the inner wall. A convex hull fills those pockets and is solid from every side,
    // so you collide with the outer trunk and climb the outside — no walking in. Only touches existing colliders
    // (the trunk), never the canopy. Select the tree(s) or a parent group.
    [MenuItem("WildReach/Fix Tree Collision (Solid Trunk)")]
    static void Solidify()
    {
        // selection if given, else EVERY mesh collider in the scene (so you don't have to hunt down each tree)
        var cols = new System.Collections.Generic.List<MeshCollider>();
        var sel = Selection.gameObjects;
        if (sel != null && sel.Length > 0)
            foreach (var go in sel) cols.AddRange(go.GetComponentsInChildren<MeshCollider>(true));
        else
        {
            cols.AddRange(Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include));
            Debug.Log($"[Tree] Nothing selected — scanning the whole scene ({cols.Count} mesh colliders).");
        }

        int n = 0, removed = 0, reverted = 0;
        foreach (var mc in cols)
        {
            if (mc == null) continue;
            string nm = mc.name.ToLower();
            if (nm.Contains("quad") || nm.Contains("card") || nm.Contains("leaf")) { Object.DestroyImmediate(mc); removed++; continue; }  // leaves shouldn't collide
            if (nm.Contains("ship")) { if (mc.convex) { mc.convex = false; reverted++; } continue; }   // ship needs its concave hull
            if (nm.Contains("log")) continue;                                                          // a hollow log to walk through
            if (!mc.convex) { mc.convex = true; EditorUtility.SetDirty(mc); n++; }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log($"[Tree] Trunks→convex: {n}, leaf colliders removed: {removed}, ship reverted: {reverted}. " +
                  "For the GIANT climb trees use 'Fix Climb Tree (Tapered Solid)' instead — convex insets at their scale.");
    }

    // The giant climb trees are too big for a convex hull (it insets inside the bark). Instead, fit a STACK of
    // sphere colliders to the real trunk profile — wide at the flared base, tapering up. No cook, no inset, fills
    // the buttress gaps, and you collide with + climb the actual surface. Select the big tree(s).
    [MenuItem("WildReach/Fix Climb Tree (Tapered Solid)")]
    static void Tapered()
    {
        var sel = Selection.gameObjects;
        if (sel == null || sel.Length == 0) { Debug.LogError("[Tree] Select the big climbing tree(s) first."); return; }

        int done = 0;
        foreach (var go in sel)
        {
            MeshCollider trunk = null; float best = -1f;
            foreach (var mc in go.GetComponentsInChildren<MeshCollider>(true))
            {
                string nm = mc.name.ToLower();
                if (nm.Contains("quad") || nm.Contains("card") || nm.Contains("leaf")) { Object.DestroyImmediate(mc); continue; }
                if (mc.sharedMesh == null) continue;
                var s = mc.bounds.size; float vol = s.x * s.y * s.z;
                if (vol > best) { best = vol; trunk = mc; }
            }
            if (trunk == null) { Debug.LogWarning($"[Tree] {go.name}: no trunk mesh collider to fit."); continue; }
            BuildSphereStack(trunk);
            trunk.enabled = false;   // the sphere stack is the collision now
            done++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log($"[Tree] Built tapered solid collision on {done} tree(s) — sphere stack matching the trunk, so you can't get inside and you climb the real surface.");
    }

    static void BuildSphereStack(MeshCollider trunk)
    {
        var host = trunk.gameObject;
        foreach (var old in host.GetComponents<SphereCollider>()) Object.DestroyImmediate(old);

        var verts = trunk.sharedMesh.vertices;
        float minY = 1e9f, maxY = -1e9f;
        foreach (var v in verts) { if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y; }

        const int bands = 9;
        float bh = (maxY - minY) / bands;
        for (int i = 0; i < bands; i++)
        {
            float y0 = minY + i * bh, y1 = (i == bands - 1) ? maxY + 0.001f : y0 + bh;
            float sx = 0, sz = 0; int cnt = 0;
            foreach (var v in verts) if (v.y >= y0 && v.y < y1) { sx += v.x; sz += v.z; cnt++; }
            if (cnt == 0) continue;
            float cx = sx / cnt, cz = sz / cnt, maxR = 0;
            foreach (var v in verts) if (v.y >= y0 && v.y < y1) { float dx = v.x - cx, dz = v.z - cz; float d = Mathf.Sqrt(dx * dx + dz * dz); if (d > maxR) maxR = d; }
            if (maxR <= 0f) continue;
            var sph = host.AddComponent<SphereCollider>();
            sph.center = new Vector3(cx, (y0 + y1) * 0.5f, cz);
            sph.radius = maxR * 0.96f;   // hug just inside the bark
        }
    }
}
