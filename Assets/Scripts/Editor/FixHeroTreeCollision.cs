using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// The hero trees (HomeTree = a ~45m Meshy "Ancient Sentinel") have an ARCHED buttress base with real holes in the
// mesh — a non-convex collider leaves those holes open (you walk in) and one primitive can't span both the wide
// base and the narrow trunk. Fix: measure the trunk profile in height-bands, then fit TWO capsules — a wide "base
// plug" that fills the arches (seated below ground) + a narrow trunk capsule for an accurate climb. Removes the
// mesh collider so there's nothing to leak through. Targets HomeTree/SpiralTree by name + anything selected.
public static class FixHeroTreeCollision
{
    const float BelowGround = 3f;

    [MenuItem("WildReach/World/Fix Hero Tree Collision")]
    static void Fix()
    {
        var targets = new List<GameObject>();
        foreach (var n in new[] { "HomeTree", "SpiralTree" }) { var g = GameObject.Find(n); if (g != null) targets.Add(g); }
        foreach (var g in Selection.gameObjects) if (!targets.Contains(g)) targets.Add(g);
        if (targets.Count == 0) { Debug.LogError("[Tree] No HomeTree/SpiralTree found and nothing selected."); return; }

        Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("Fix Hero Tree Collision");
        int done = 0;
        foreach (var t in targets) if (FixOne(t)) done++;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log($"[Tree] Fixed {done} hero tree(s): base-plug + trunk capsule. Ctrl+Z to undo. If a capsule's off, edit its Radius/Height on the tree.");
    }

    static bool FixOne(GameObject tree)
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
        if (trunk == null) { Debug.LogWarning($"[Tree] {tree.name}: no trunk mesh."); return false; }

        var mt = trunk.transform;
        var verts = trunk.GetComponent<MeshFilter>().sharedMesh.vertices;
        var w = new Vector3[verts.Length];
        float minY = 1e9f, maxY = -1e9f;
        for (int i = 0; i < verts.Length; i++) { var p = mt.TransformPoint(verts[i]); w[i] = p; if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y; }
        float H = Mathf.Max(0.01f, maxY - minY);

        // centre from the LOWER trunk only (branches would pull an all-vert centroid off-axis)
        double lx = 0, lz = 0; int lc = 0;
        foreach (var p in w) if (p.y <= minY + 0.30f * H) { lx += p.x; lz += p.z; lc++; }
        float cx = lc > 0 ? (float)(lx / lc) : 0f, cz = lc > 0 ? (float)(lz / lc) : 0f;

        // per-band 80th-percentile radius
        const int bands = 24;
        var bandDist = new List<float>[bands];
        for (int i = 0; i < bands; i++) bandDist[i] = new List<float>();
        foreach (var p in w)
        {
            int b = Mathf.Clamp((int)((p.y - minY) / H * bands), 0, bands - 1);
            float dx = p.x - cx, dz = p.z - cz;
            bandDist[b].Add(Mathf.Sqrt(dx * dx + dz * dz));
        }
        var bandR = new float[bands];
        for (int i = 0; i < bands; i++)
        {
            if (bandDist[i].Count == 0) { bandR[i] = -1f; continue; }
            bandDist[i].Sort();
            bandR[i] = bandDist[i][Mathf.Clamp((int)(bandDist[i].Count * 0.80f), 0, bandDist[i].Count - 1)];
        }

        // trunk radius = median of the upper-mid bands
        var upper = new List<float>();
        for (int i = (int)(bands * 0.35f); i <= (int)(bands * 0.7f); i++) if (bandR[i] > 0) upper.Add(bandR[i]);
        upper.Sort();
        float trunkR = upper.Count > 0 ? upper[upper.Count / 2] : Mathf.Max(0.1f, bandR[bands / 2]);

        // buttress top = first band (from the base) where the profile has narrowed to ~the trunk; base radius = widest below it
        int bt = 0; float baseR = trunkR;
        for (int i = 0; i < bands; i++)
        {
            if (bandR[i] < 0) continue;
            if (bandR[i] <= trunkR * 1.2f) { bt = i; break; }
            if (bandR[i] > baseR) baseR = bandR[i];
        }
        float buttressTopY = minY + (bt / (float)bands) * H;

        foreach (var c in tree.GetComponentsInChildren<Collider>(true)) Undo.DestroyObjectImmediate(c);
        var oldHost = GameObject.Find(tree.name + "_Collider"); if (oldHost != null) Undo.DestroyObjectImmediate(oldHost);

        // THE bug all along: the tree's transform is FBX-rotated (270,194,0) + 100x scaled, so a dir=Y capsule on it
        // (or on a child) is fighting that transform. This host is a STANDALONE top-level object — no parent, world
        // position, identity rotation, unit scale — so the vertical capsules are guaranteed upright + sized in metres.
        var host = new GameObject(tree.name + "_Collider");
        Undo.RegisterCreatedObjectUndo(host, "Tree Collider");
        host.transform.SetPositionAndRotation(new Vector3(cx, 0f, cz), Quaternion.identity);
        host.transform.localScale = Vector3.one;

        if (bt > 0 && baseR > trunkR * 1.05f)
            AddCapsule(host, minY - BelowGround, buttressTopY + 0.5f, baseR);          // wide base plug fills the arches
        AddCapsule(host, (bt > 0 ? buttressTopY : minY - BelowGround), maxY, trunkR);  // narrow trunk

        Debug.Log($"[Tree] {tree.name}: trunkR={trunkR:F2} baseR={baseR:F2} buttressTop={(buttressTopY - minY):F1}m of {H:F1}m — {(bt > 0 ? "base plug + trunk" : "single trunk")} on a world-upright host.");
        return true;
    }

    static void AddCapsule(GameObject host, float bottomWorldY, float topWorldY, float worldRadius)
    {
        var cap = host.AddComponent<CapsuleCollider>();
        cap.direction = 1;                                            // Y = world up (host is upright + unit scale)
        cap.center = new Vector3(0f, (bottomWorldY + topWorldY) * 0.5f, 0f);
        cap.height = topWorldY - bottomWorldY;
        cap.radius = worldRadius;
    }
}
