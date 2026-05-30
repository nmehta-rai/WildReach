using System.Linq;
using UnityEngine;
using UnityEditor;

// Scatters BlossomTree.prefab SPARSELY across the terrain — far fewer and wider-spaced than
// the green forest, so the pink/purple blossoms punctuate the woods as rare accents.
public static class ScatterBlossoms
{
    const string PrefabPath = "Assets/Environment/ForestTrees/BlossomTree.prefab";
    const int   Count         = 9;     // rare
    const float MinScale      = 0.85f;
    const float MaxScale      = 1.3f;
    const float MaxSteepness  = 30f;
    const float TreeClearance = 12f;
    const float MinSpacing    = 22f;   // widely spaced

    [MenuItem("WildReach/Scatter Blossoms")]
    static void Scatter()
    {
        var terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null) { Debug.LogError("[Blossom] No terrain found."); return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("[Blossom] No BlossomTree.prefab — run Build Blossom Tree first."); return; }

        var old = GameObject.Find("ScatteredBlossoms");
        if (old != null) Object.DestroyImmediate(old);
        var container = new GameObject("ScatteredBlossoms");
        Undo.RegisterCreatedObjectUndo(container, "Scatter Blossoms");

        var td = terrain.terrainData;
        var tpos = terrain.transform.position;
        float w = td.size.x, d = td.size.z;
        var tree = GameObject.Find("HomeTree");
        Vector3 heroPos = tree != null ? tree.transform.position : new Vector3(1e9f, 0, 1e9f);

        var placed = new System.Collections.Generic.List<Vector3>();
        int tries = 0;
        while (placed.Count < Count && tries < Count * 40)
        {
            tries++;
            float x = tpos.x + Random.Range(0f, w);
            float z = tpos.z + Random.Range(0f, d);
            float nx = (x - tpos.x) / w, nz = (z - tpos.z) / d;
            if (td.GetSteepness(nx, nz) > MaxSteepness) continue;
            if (new Vector2(x - heroPos.x, z - heroPos.z).magnitude < TreeClearance) continue;
            if (placed.Any(p => new Vector2(x - p.x, z - p.z).magnitude < MinSpacing)) continue;

            float y = terrain.SampleHeight(new Vector3(x, 0, z)) + tpos.y - 0.1f;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.SetParent(container.transform, true);
            inst.transform.position = new Vector3(x, y, z);
            inst.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            inst.transform.localScale = Vector3.one * Random.Range(MinScale, MaxScale);
            placed.Add(new Vector3(x, y, z));
        }

        Selection.activeGameObject = container;
        Debug.Log($"[Blossom] Scattered {placed.Count} blossom trees (sparse). Re-run to reshuffle; " +
                  "tune Count/spacing in the script.");
    }
}
