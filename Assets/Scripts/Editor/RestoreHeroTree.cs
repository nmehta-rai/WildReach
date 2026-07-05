using UnityEngine;
using UnityEditor;

// Puts HomeTree / SpiralTree back to the ORIGINAL collision they shipped with (and had working): a plain
// non-convex MeshCollider on the trunk renderer(s), exactly as SetupAncientTree built them. Removes every capsule
// / standalone blocker / convex conversion my later tools applied. Does NOT touch the forest/climbing trees —
// those are a separate problem.
public static class RestoreHeroTree
{
    [MenuItem("WildReach/World/RESTORE HomeTree Collider (undo my mess)")]
    static void Restore()
    {
        Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("Restore Hero Tree Collider");
        int trees = 0;
        foreach (var n in new[] { "HomeTree", "SpiralTree" })
        {
            var tree = GameObject.Find(n);
            if (tree == null) continue;

            // delete the standalone blocker object I created
            var blocker = GameObject.Find(n + "_Collider");
            if (blocker != null) Undo.DestroyObjectImmediate(blocker);

            // delete capsule/box colliders I stuck on the tree
            foreach (var c in tree.GetComponentsInChildren<Collider>(true))
                if (c is CapsuleCollider || c is BoxCollider || c is SphereCollider) Undo.DestroyObjectImmediate(c);

            // restore a non-convex MeshCollider on each TRUNK renderer, matching its mesh (the original working state)
            int cols = 0;
            foreach (var mr in tree.GetComponentsInChildren<MeshRenderer>(true))
            {
                string nm = mr.name.ToLower();
                if (nm.Contains("leaf") || nm.Contains("canopy") || nm.Contains("quad") || nm.Contains("crown")) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mc = mr.GetComponent<MeshCollider>();
                if (mc == null) mc = Undo.AddComponent<MeshCollider>(mr.gameObject);
                mc.convex = false;                 // back to the original non-convex bark surface
                mc.sharedMesh = mf.sharedMesh;
                cols++;
            }
            Debug.Log($"[Tree] Restored '{n}': {cols} non-convex trunk mesh collider(s), capsules/blockers removed.");
            trees++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log($"[Tree] Restored {trees} hero tree(s) to original collision. Forest/climbing trees untouched. Ctrl+Z to undo.");
    }
}
