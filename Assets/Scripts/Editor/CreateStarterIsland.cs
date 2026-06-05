using UnityEngine;
using UnityEditor;

// Builds the reusable "TemperateForest" scatter palette (and, optionally, a starter island).
// 'Setup Forest Palette' only touches the palette asset — safe to run on an island you've already
// zoned. Assign the palette to your Forest zone, then hit that zone's Scatter button.
public static class CreateStarterIsland
{
    const string Dir         = "Assets/World";
    const string PalettePath = Dir + "/TemperateForest.asset";

    [MenuItem("WildReach/World/Setup Forest Palette")]
    static void SetupForestPalette()
    {
        var p = BuildForestPalette();
        Selection.activeObject = p;
        Debug.Log("[Island] TemperateForest palette ready: ForestTree (common), BlossomTree (rare), " +
                  "Embercap mushrooms (cluster near trees/logs, upright), MossLog. Assign it to your Forest zone, then Scatter.");
    }

    [MenuItem("WildReach/World/Create Starter Forest Island")]
    static void CreateStarter()
    {
        var palette = BuildForestPalette();
        var island = LoadOrCreate<IslandDefinition>(Dir + "/Island1.asset");
        island.terrainName = "Island1_Terrain";
        island.seed = 12345;
        island.zoneMapRes = 512;
        island.zones.Clear();
        island.zones.Add(new IslandZone
        {
            name = "Forest", color = new Color(0.30f, 0.75f, 0.35f),
            palette = palette, grass = true, grassDensity = 0.7f, slopeLimit = 32f, clearance = 13f,
        });
        EditorUtility.SetDirty(island);
        AssetDatabase.SaveAssets();
        Selection.activeObject = island;
        Debug.Log("[Island] Starter Island1 + TemperateForest palette created.");
    }

    static ScatterPalette BuildForestPalette()
    {
        System.IO.Directory.CreateDirectory(Dir);
        var p = LoadOrCreate<ScatterPalette>(PalettePath);
        p.entries.Clear();
        //    prefab path                                                                     spacing density minS  maxS  groundSit normH nearProps
        Add(p, "Assets/Environment/ForestTrees/ForestTree.prefab",                            7f,   0.55f, 0.8f, 1.5f, false, 0f,   false);
        Add(p, "Assets/Environment/ForestTrees/BlossomTree.prefab",                          42f,   0.15f, 0.8f, 1.2f, false, 0f,   false);  // pink = rare
        Add(p, "Assets/Environment/MossLog/MossLog.fbx",                                     24f,   0.30f, 0.9f, 1.2f, true,  1.2f, false);
        Add(p, "Assets/Environment/Mushrooms/EmbercapCluster/EmbercapCluster_Glow.prefab",    0f,   0.25f, 0.7f, 1.3f, true,  0f,   true);   // at tree/log bases
        Add(p, "Assets/Environment/Mushrooms/EmbercapGrove/EmbercapGrove_Glow.prefab",        0f,   0.14f, 0.7f, 1.2f, true,  0f,   true);
        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        return p;
    }

    static void Add(ScatterPalette p, string prefabPath, float spacing, float density,
                    float minS, float maxS, bool groundSit, float normH, bool nearProps)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (go == null) { Debug.LogWarning("[Island] palette: missing " + prefabPath); return; }
        p.entries.Add(new ScatterEntry
        {
            prefab = go, spacing = spacing, density = density, minScale = minS, maxScale = maxS,
            slopeLimit = 35f, groundSit = groundSit, normalizeHeight = normH, nearProps = nearProps,
        });
    }

    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a == null) { a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, path); }
        return a;
    }
}
