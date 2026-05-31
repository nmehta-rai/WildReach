using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;

// One-time architectural pass: the world's trees were placed as visual meshes with NO colliders, so the
// player and creatures phase through them (and can't stand/jump on them). This adds a trunk MeshCollider to
// every tree, skipping leaf-card / canopy meshes (so you don't collide with invisible foliage quads).
// Re-runnable: skips anything that already has a collider.
public static class SetupWorldColliders
{
    static readonly string[] SkipFoliage =
        { "leaf", "canopy", "blossom", "wisp", "crown", "frond", "petal" };

    static readonly string[] TreeParents = { "ScatteredForest", "ScatteredBlossoms" };
    static readonly string[] SoloTrees   = { "HomeTree", "SpiralTree", "ForestTree", "BlossomTree" };

    [MenuItem("WildReach/Setup World Colliders")]
    static void Setup()
    {
        var roots = new List<GameObject>();
        foreach (var p in TreeParents)
        {
            var parent = GameObject.Find(p);
            if (parent != null) foreach (Transform c in parent.transform) roots.Add(c.gameObject);
        }
        foreach (var n in SoloTrees) { var g = GameObject.Find(n); if (g != null) roots.Add(g); }

        int added = 0, trees = 0;
        foreach (var root in roots)
        {
            bool touched = false;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                string nm = mf.name.ToLower();
                if (SkipFoliage.Any(s => nm.Contains(s))) continue;          // foliage — no collision
                if (mf.sharedMesh == null) continue;
                if (mf.GetComponent<Collider>() != null) continue;           // already solid
                var mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;                                           // static, accurate
                added++; touched = true;
            }
            if (touched) trees++;
        }
        Debug.Log($"[Colliders] Added {added} trunk collider(s) across {trees} trees. " +
                  $"Player + tortoise can no longer walk through them, and you can stand/jump on them. " +
                  $"(Terrain already has its TerrainCollider; clipping INTO terrain is the climb-standoff pass, separate.)");
    }
}
