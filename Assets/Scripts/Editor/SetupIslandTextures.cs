using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

// Builds the Island 1 zone terrain layers from the generated cel textures and AUTO-PAINTS them
// by height + slope: sand at the shore, forest in the bowl, snowy rock on cliffs/tree-line, snow
// on the peaks. Re-runnable. After running, hand-paint refinements with Terrain > Paint Texture.
//
// Drop the textures in Assets/Materials/Textures/ named: Sand, Forest, SnowRock, Snow, (Path optional).
// Flat cel art -> NO normal maps (a normal map double-shades the pre-shaded textures).
public static class SetupIslandTextures
{
    const string TexDir = "Assets/Materials/Textures/";
    const string LayerDir = "Assets/Materials/";
    const float  Sea = 0.10f;   // sea level, normalized terrain height (matches the heightmap)

    // name, tile size (metres per repeat)
    static readonly (string name, float tile)[] Zones =
    {
        // flat-ground layers tile LARGE so the repeat is far apart and not an obvious grid;
        // slope layers stay small so the steep-face smear reads as fine scree.
        ("Sand",       16f),
        ("Forest",     24f),
        ("SnowRock",    9f),
        ("Snow",       14f),
        ("Path",        8f),   // hand-paint
        ("Dirt_Rocky",  16f),   // town ground — hand-paint, no auto weight
        ("Dirt_Sandy",  16f),
        ("Dirt_Grass",  16f),
        ("Ground_Rock", 10f),   // mossy rock — hand-paint, no auto weight
    };

    [MenuItem("WildReach/Setup Island Textures")]
    static void Setup()
    {
        var go = GameObject.Find("Island1_Terrain");
        if (go == null) { Debug.LogError("[IslandTex] No 'Island1_Terrain' in the scene. Run Build Island Terrain first."); return; }
        var terrain = go.GetComponent<Terrain>();
        var td = terrain != null ? terrain.terrainData : null;
        if (td == null) { Debug.LogError("[IslandTex] Island1_Terrain has no TerrainData."); return; }

        // --- build the terrain layers from whatever zone textures exist ---
        var layers = new List<TerrainLayer>();
        var index  = new Dictionary<string, int>();
        foreach (var (name, tile) in Zones)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + name + ".png");
            if (tex == null) { Debug.LogWarning($"[IslandTex] Missing {TexDir}{name}.png — skipping."); continue; }

            string path = LayerDir + "Island_" + name + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
            layer.diffuseTexture = tex;
            layer.normalMapTexture = null;          // flat cel — no fake relief
            layer.tileSize = new Vector2(tile, tile);
            EditorUtility.SetDirty(layer);
            index[name] = layers.Count;
            layers.Add(layer);
        }
        if (layers.Count == 0) { Debug.LogError("[IslandTex] No zone textures found in " + TexDir); return; }
        td.terrainLayers = layers.ToArray();

        // --- auto-paint the splatmap by height + slope ---
        int res = 1024;
        td.alphamapResolution = res;
        int L = layers.Count;
        var maps = new float[res, res, L];

        for (int y = 0; y < res; y++)        // y -> Z
            for (int x = 0; x < res; x++)    // x -> X
            {
                float u = (float)x / (res - 1);
                float v = (float)y / (res - 1);
                float h = td.GetInterpolatedHeight(u, v) / td.size.y;   // 0..1 normalized
                float steep = td.GetSteepness(u, v);                    // degrees

                float sand   = 1f - Smooth(0.13f, 0.18f, h);            // shore + just above water
                float snow   = Smooth(0.48f, 0.62f, h);                 // white caps over the high ground
                float cliff  = Smooth(44f, 58f, steep) * (1f - 0.65f * snow); // bare rock only on true cliffs; snow still caps the tops
                float tree   = Smooth(0.38f, 0.46f, h) * (1f - Smooth(0.52f, 0.62f, h)); // rocky tree-line band below the snow
                float rock   = Mathf.Max(cliff, tree);
                float forest = Mathf.Clamp01(1f - sand - snow - rock) + 0.02f;

                // write only the zones we actually have (fall back to forest if a zone is missing)
                var w = new float[L];
                Add(w, index, "Sand", sand);
                Add(w, index, "Snow", snow);
                Add(w, index, "SnowRock", rock);
                Add(w, index, "Forest", forest);

                float sum = 0f; for (int k = 0; k < L; k++) sum += w[k];
                if (sum <= 0f) { w[FallbackForest(index)] = 1f; sum = 1f; }
                for (int k = 0; k < L; k++) maps[y, x, k] = w[k] / sum;
            }

        td.SetAlphamaps(0, 0, maps);
        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();
        Debug.Log($"[IslandTex] {layers.Count} zone layers set + auto-painted on Island1_Terrain " +
                  $"(sand/forest/rock/snow by height+slope). Brush paths and tweak with Terrain > Paint Texture.");
    }

    static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
    static void  Add(float[] w, Dictionary<string,int> idx, string name, float val) { if (idx.TryGetValue(name, out int i)) w[i] += val; }
    static int   FallbackForest(Dictionary<string,int> idx) { return idx.TryGetValue("Forest", out int i) ? i : 0; }
}
