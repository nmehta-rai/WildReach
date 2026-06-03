using UnityEngine;
using UnityEditor;

// One-shot convenience: drops a ready-to-use Island1 + a "TemperateForest" scatter palette wired
// to the forest prefabs we already have, so the very first time you open the Island Builder you
// can just Create the zone map, paint the Forest zone, and Build. Re-runnable (overwrites).
public static class CreateStarterIsland
{
    const string Dir = "Assets/World";

    [MenuItem("WildReach/World/Create Starter Forest Island")]
    static void Create()
    {
        System.IO.Directory.CreateDirectory(Dir);

        // --- the reusable forest palette ---
        var palette = LoadOrCreate<ScatterPalette>(Dir + "/TemperateForest.asset");
        palette.entries.Clear();
        Add(palette, "Assets/Environment/ForestTrees/ForestTree.prefab",                          40, 0.8f, 1.5f, 5.5f, false, 0f);
        Add(palette, "Assets/Environment/ForestTrees/BlossomTree.prefab",                          8,  0.8f, 1.2f, 7f,   false, 0f);
        Add(palette, "Assets/Environment/Mushrooms/EmbercapCluster/EmbercapCluster_Glow.prefab",   14, 0.7f, 1.3f, 3.5f, true,  0f);
        Add(palette, "Assets/Environment/Mushrooms/EmbercapGrove/EmbercapGrove_Glow.prefab",       8,  0.7f, 1.2f, 4f,   true,  0f);
        Add(palette, "Assets/Environment/MossLog/MossLog.fbx",                                     5,  0.9f, 1.2f, 6f,   true,  1.2f);
        EditorUtility.SetDirty(palette);

        // --- the island with a single Forest zone ---
        var island = LoadOrCreate<IslandDefinition>(Dir + "/Island1.asset");
        island.terrainName = "Island1_Terrain";
        island.seed = 12345;
        island.zoneMapRes = 512;
        island.zones.Clear();
        island.zones.Add(new IslandZone
        {
            name = "Forest", color = new Color(0.30f, 0.75f, 0.35f),
            palette = palette, grass = true, grassDensity = 0.7f,
            slopeLimit = 32f, clearance = 13f,
        });
        EditorUtility.SetDirty(island);

        AssetDatabase.SaveAssets();
        Selection.activeObject = island;
        Debug.Log("[Island] Starter ready: Assets/World/Island1.asset + TemperateForest palette. " +
                  "Open WildReach > World > Island Builder, Create the zone map, paint Forest, Build.");
    }

    static void Add(ScatterPalette p, string prefabPath, int count, float minS, float maxS, float spacing, bool groundSit, float normH)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (go == null) { Debug.LogWarning("[Island] starter: missing " + prefabPath); return; }
        p.entries.Add(new ScatterEntry
        {
            prefab = go, count = count, minScale = minS, maxScale = maxS,
            spacing = spacing, slopeLimit = 35f, groundSit = groundSit, normalizeHeight = normH,
        });
    }

    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a == null) { a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, path); }
        return a;
    }
}
