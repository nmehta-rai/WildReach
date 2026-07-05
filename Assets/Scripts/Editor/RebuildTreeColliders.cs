using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Clean-slate tree collision. For each tree it TEARS DOWN every collider and fits ONE vertical capsule to the
// actual trunk body (measured from the mesh, ignoring the wide buttress flare + the leaf canopy), seated a couple
// of metres BELOW the base so it sits right on curved ground. A single capsule is smooth (no snagging), solid (no
// leaking through root-gaps), has no convex inset (you climb the real bark), and is tunable afterwards (just edit
// the CapsuleCollider). Uses the selection if there is one, otherwise auto-finds every tree in the scene.
public static class RebuildTreeColliders
{
    const float BandLo = 0.15f, BandHi = 0.55f;   // height slice used to measure the trunk (above flare, below branches)
    const float Pct = 0.60f;                       // percentile of that slice's radius = trunk radius (ignores buttress tips)
    const float BelowGround = 2.5f;                // how far the capsule extends under the base

    [MenuItem("WildReach/Rebuild Tree Colliders (Clean)")]
    static void Rebuild()
    {
        var targets = new List<GameObject>();
        var sel = Selection.gameObjects;
        if (sel != null && sel.Length > 0) targets.AddRange(sel);
        else { targets.AddRange(AutoFindTrees()); Debug.Log($"[Tree] Nothing selected — auto-found {targets.Count} tree(s) in the scene."); }

        int done = 0;
        Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("Rebuild Tree Colliders");
        foreach (var go in targets) if (RebuildOne(go)) done++;
        if (done > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log($"[Tree] Rebuilt clean collision on {done} tree(s): one trunk capsule each, seated below ground. Ctrl+Z to undo; edit any CapsuleCollider's Radius to fine-tune.");
    }

    // A "tree" = a tall, non-leaf trunk mesh; its root is the top-level object (or the child directly under a
    // scatter/forest/island container).
    static List<GameObject> AutoFindTrees()
    {
        var roots = new HashSet<GameObject>();
        foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude))
        {
            if (mf.sharedMesh == null) continue;
            string nm = mf.name.ToLower();
            if (nm.Contains("quad") || nm.Contains("card") || nm.Contains("leaf") || nm.Contains("canopy") ||
                nm.Contains("grass") || nm.Contains("mushroom") || nm.Contains("rock") || nm.Contains("moss") || nm.Contains("flower")) continue;
            var r = mf.GetComponent<Renderer>();
            if (r == null || r.bounds.size.y < 3f) continue;   // trees are tall; skip props/foliage
            roots.Add(TreeRoot(mf.transform));
        }
        return new List<GameObject>(roots);
    }

    static GameObject TreeRoot(Transform t)
    {
        var cur = t;
        while (cur.parent != null)
        {
            string pn = cur.parent.name.ToLower();
            if (pn.Contains("scatter") || pn.Contains("forest") || pn.Contains("island") || pn.Contains("group")) return cur.gameObject;
            cur = cur.parent;
        }
        return cur.gameObject;
    }

    static bool RebuildOne(GameObject tree)
    {
        Renderer trunk = null; float best = -1f;
        foreach (var r in tree.GetComponentsInChildren<Renderer>(true))
        {
            string nm = r.name.ToLower();
            if (nm.Contains("quad") || nm.Contains("card") || nm.Contains("leaf") || nm.Contains("canopy") || nm.Contains("crown")) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var s = r.bounds.size; float vol = s.x * s.y * s.z;
            if (vol > best) { best = vol; trunk = r; }
        }
        if (trunk == null) { Debug.LogWarning($"[Tree] {tree.name}: no trunk mesh — skipped."); return false; }

        var mt = trunk.transform;
        var verts = trunk.GetComponent<MeshFilter>().sharedMesh.vertices;
        var w = new Vector3[verts.Length];
        float minY = 1e9f, maxY = -1e9f; double sx = 0, sz = 0;
        for (int i = 0; i < verts.Length; i++)
        {
            var p = mt.TransformPoint(verts[i]); w[i] = p;
            if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y; sx += p.x; sz += p.z;
        }
        float cx = (float)(sx / verts.Length), cz = (float)(sz / verts.Length), H = maxY - minY;

        float lo = minY + BandLo * H, hi = minY + BandHi * H;
        var dists = new List<float>();
        foreach (var p in w) if (p.y >= lo && p.y <= hi) { float dx = p.x - cx, dz = p.z - cz; dists.Add(Mathf.Sqrt(dx * dx + dz * dz)); }
        if (dists.Count == 0) foreach (var p in w) { float dx = p.x - cx, dz = p.z - cz; dists.Add(Mathf.Sqrt(dx * dx + dz * dz)); }
        dists.Sort();
        float trunkR = dists[Mathf.Clamp(Mathf.RoundToInt(dists.Count * Pct), 0, dists.Count - 1)];

        foreach (var c in tree.GetComponentsInChildren<Collider>(true)) Undo.DestroyObjectImmediate(c);

        float bottomY = minY - BelowGround, topY = maxY;
        var ls = tree.transform.lossyScale;
        float sxScale = Mathf.Abs(ls.x) < 1e-4f ? 1f : Mathf.Abs(ls.x);
        float syScale = Mathf.Abs(ls.y) < 1e-4f ? 1f : Mathf.Abs(ls.y);

        var cap = Undo.AddComponent<CapsuleCollider>(tree);
        cap.direction = 1;
        cap.center = tree.transform.InverseTransformPoint(new Vector3(cx, (bottomY + topY) * 0.5f, cz));
        cap.height = (topY - bottomY) / syScale;
        cap.radius = trunkR / sxScale;
        return true;
    }
}
