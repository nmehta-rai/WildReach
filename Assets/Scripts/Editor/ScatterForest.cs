using System.Linq;
using UnityEngine;
using UnityEditor;

// Scatters ForestTree.prefab across the terrain: conformed to the ground, random scale and
// rotation, skipping steep slopes and a clearance around the HomeTree. Re-runnable — it
// clears the previous scatter and reshuffles, so run it until you like the spread.
public static class ScatterForest
{
    const string PrefabPath = "Assets/Environment/ForestTrees/ForestTree.prefab";
    const int   Count         = 35;
    const float MinScale      = 0.7f;
    const float MaxScale      = 1.3f;
    const float MaxSteepness  = 30f;
    const float TreeClearance = 12f;   // keep clear of the Hollowroot hero
    const float MinSpacing    = 6f;    // don't stack trees on each other

    [MenuItem("WildReach/Scatter Forest")]
    static void Scatter()
    {
        var terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null) { Debug.LogError("[Forest] No terrain found."); return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("[Forest] No ForestTree.prefab — run Build Forest Tree first."); return; }

        var old = GameObject.Find("ScatteredForest");
        if (old != null) Object.DestroyImmediate(old);
        var container = new GameObject("ScatteredForest");
        Undo.RegisterCreatedObjectUndo(container, "Scatter Forest");

        var td = terrain.terrainData;
        var tpos = terrain.transform.position;
        float w = td.size.x, d = td.size.z;
        var tree = GameObject.Find("HomeTree");
        Vector3 heroPos = tree != null ? tree.transform.position : new Vector3(1e9f, 0, 1e9f);

        var placed = new System.Collections.Generic.List<Vector3>();
        int tries = 0;
        while (placed.Count < Count && tries < Count * 20)
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
        Debug.Log($"[Forest] Scattered {placed.Count} trees under 'ScatteredForest'. " +
                  "Re-run to reshuffle; tune Count/spacing/scale in the script; delete any awkward ones.");
    }
}
