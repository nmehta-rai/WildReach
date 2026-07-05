using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;

// The fix for "the ground looks completely 2D": registers the grass-tuft + flower detail
// prototypes (waving 3D billboards) AND auto-fills them across the terrain so a dense carpet
// of swaying grass covers wherever the ground is green — no hand-painting. Grass thins out on
// slopes and vanishes on sand/rock/snow. Flowers are scattered sparsely on top in clumps.
// Re-runnable: tweak Density / the clump scales and run again until it reads right.
public static class FillForestGrass
{
    // grass blade cutouts -> these get the heavy carpet
    static readonly string[] Grass =
    {
        "Assets/Materials/Textures/Grass/GrassTuft_1.png",
        "Assets/Materials/Textures/Grass/GrassTuft_2.png",
        "Assets/Materials/Textures/Grass/GrassTuft_3.png",
    };
    // flowers -> sparse colour accents
    static readonly string[] Flowers =
    {
        "Assets/Materials/Textures/Flowers/GroundFlower_1.png",
        "Assets/Materials/Textures/Flowers/GroundFlower_2.png",
    };

    const int   PerPatch     = 16;
    const int   MaxGrassMesh = 6;   // peak 3D clumps per cell (bushy — fewer needed)
    const int   MaxGrassBill = 14;  // peak billboards per cell (fallback path)
    const float SlopeFull   = 22f;  // grass at full density below this slope (deg)
    const float SlopeNone   = 45f;  // grass gone above this
    const float ClumpScale  = 0.045f; // perlin frequency for grass density patches
    const float FlowerScale = 0.02f;  // perlin frequency for flower patches
    const float FlowerGate  = 0.74f;  // only the tops of flower-noise peaks get flowers

    [MenuItem("WildReach/Fill Forest Grass (3D)")]
    static void Fill()
    {
        // EVERY terrain in the scene (covers the case where the rendered terrain isn't activeTerrain)
        var terrains = Object.FindObjectsByType<Terrain>();
        if (terrains.Length == 0) { Debug.LogError("[Grass] No terrain in the scene."); return; }
        Debug.Log($"[Grass] Found {terrains.Length} terrain(s): {string.Join(", ", terrains.Select(t => t.name))}");
        foreach (var terrain in terrains) FillOne(terrain);
    }

    static void FillOne(Terrain terrain)
    {
        if (terrain == null || terrain.terrainData == null) return;
        var td = terrain.terrainData;

        // --- 1. register prototypes: prefer the 3D mesh clumps; fall back to flat billboards ---
        var protos = new List<DetailPrototype>();
        int grassCount = 0;
        bool meshMode = false;
        var clumps = new List<string>();
        for (int i = 1; i <= 3; i++)
        {
            string pp = $"Assets/Environment/Grass/GrassClump_{i}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(pp) != null) clumps.Add(pp);
        }
        if (clumps.Count > 0)
        {
            meshMode = true;
            foreach (var cp in clumps) { if (TryMeshProto(cp, out var dp)) { protos.Add(dp); grassCount++; } }
        }
        else
        {
            foreach (var p in Grass) { if (TryProto(p, true, out var dp)) { protos.Add(dp); grassCount++; } }
        }
        foreach (var p in Flowers) { if (TryProto(p, false, out var dp)) protos.Add(dp); }
        if (grassCount == 0) { Debug.LogError("[Grass] No grass clumps/tufts found — run 'Build Grass Clumps' first."); return; }

        int detailRes = meshMode ? 512 : 1024;   // meshes are heavier, so a coarser grid
        int maxGrass  = meshMode ? MaxGrassMesh : MaxGrassBill;
        td.detailPrototypes = protos.ToArray();
        td.SetDetailResolution(detailRes, PerPatch);

        // --- 2. find which terrain layer is the green ground (grass lives only there) ---
        int greenIdx = -1;
        var layers = td.terrainLayers;
        for (int i = 0; i < layers.Length; i++)
        {
            string n = layers[i] != null ? layers[i].name.ToLower() : "";
            if (n.Contains("forest") || n.Contains("grass")) { greenIdx = i; break; }
        }
        int aw = td.alphamapWidth, ah = td.alphamapHeight;
        float[,,] alpha = layers.Length > 0 ? td.GetAlphamaps(0, 0, aw, ah) : null;

        // --- 3. build the density maps ---
        long nonZero = 0, totalBlades = 0;
        int w = td.detailWidth, h = td.detailHeight;
        var maps = new int[protos.Count][,];
        for (int k = 0; k < protos.Count; k++) maps[k] = new int[h, w];

        for (int dy = 0; dy < h; dy++)
        for (int dx = 0; dx < w; dx++)
        {
            float u = (float)dx / (w - 1);
            float v = (float)dy / (h - 1);

            // green-ness from the splatmap (1 everywhere if we couldn't find a layer)
            float green = 1f;
            if (alpha != null && greenIdx >= 0)
            {
                int ax = Mathf.Clamp(Mathf.RoundToInt(u * (aw - 1)), 0, aw - 1);
                int ay = Mathf.Clamp(Mathf.RoundToInt(v * (ah - 1)), 0, ah - 1);
                green = alpha[ay, ax, greenIdx];
            }
            if (green < 0.2f) continue;

            float steep = td.GetSteepness(u, v);
            float slope = 1f - Mathf.Clamp01((steep - SlopeFull) / (SlopeNone - SlopeFull));
            if (slope <= 0f) continue;

            // clumpy density so it's not a flat uniform lawn
            float clump = Mathf.PerlinNoise(dx * ClumpScale, dy * ClumpScale);       // 0..1
            float dens  = green * slope * (0.45f + 0.55f * clump);
            int   blades = Mathf.RoundToInt(dens * maxGrass);
            if (blades > 0)
            {
                // pick a grass variant per region so neighbouring tufts match (natural patches)
                int g = Mathf.Clamp(Mathf.FloorToInt(Mathf.PerlinNoise(dx * 0.02f + 31f, dy * 0.02f + 17f) * grassCount), 0, grassCount - 1);
                maps[g][dy, dx] = blades;
                nonZero++; totalBlades += blades;
            }

            // sparse flower clumps on the peaks of a second noise field
            if (protos.Count > grassCount && green > 0.45f && slope > 0.6f)
            {
                float fn = Mathf.PerlinNoise(dx * FlowerScale + 99f, dy * FlowerScale + 71f);
                if (fn > FlowerGate)
                {
                    int f = grassCount + (((dx >> 1) ^ (dy >> 1)) & 1) % (protos.Count - grassCount);
                    maps[f][dy, dx] = 1;
                }
            }
        }

        for (int k = 0; k < protos.Count; k++) td.SetDetailLayer(0, 0, k, maps[k]);

        // --- 4. wind + draw settings ---
        td.wavingGrassStrength = 0.25f;
        td.wavingGrassAmount   = 0.35f;
        td.wavingGrassSpeed    = 0.4f;
        td.wavingGrassTint     = new Color(0.75f, 0.85f, 0.55f);
        terrain.drawTreesAndFoliage   = true;
        terrain.detailObjectDistance  = 250f;   // Unity clamps to 250 max
        terrain.detailObjectDensity   = 1f;

        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Grass] '{terrain.name}': detailGrid {w}x{h}, greenLayer={greenIdx} ({(greenIdx>=0?layers[greenIdx].name:"NONE→grass-everywhere")}), " +
                  $"alpha={(alpha!=null?$"{aw}x{ah}x{layers.Length}":"none")}. Filled {nonZero:N0} cells / {totalBlades:N0} blades " +
                  $"across {grassCount} grass + {protos.Count - grassCount} flower layers.");
    }

    // 3D grass-clump mesh prototype — lit, sits in world space, waves with the terrain wind.
    static bool TryMeshProto(string prefabPath, out DetailPrototype dp)
    {
        dp = default;
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (go == null) return false;
        dp = new DetailPrototype
        {
            usePrototypeMesh = true,
            prototype        = go,
            renderMode       = DetailRenderMode.Grass,  // mesh + wind sway
            healthyColor     = new Color(0.85f, 1f, 0.7f),
            dryColor         = new Color(0.7f, 0.85f, 0.5f),
            minWidth  = 0.8f, maxWidth  = 1.5f,
            minHeight = 0.8f, maxHeight = 1.5f,
            noiseSpread = 0.5f,
        };
        return true;
    }

    static bool TryProto(string path, bool grass, out DetailPrototype dp)
    {
        dp = default;
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null) { imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.SaveAndReimport(); }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) { Debug.LogWarning("[Grass] Missing detail texture: " + path); return false; }

        dp = new DetailPrototype
        {
            usePrototypeMesh = false,
            prototypeTexture = tex,
            renderMode   = DetailRenderMode.GrassBillboard, // sways with the wind
            healthyColor = grass ? new Color(0.78f, 0.95f, 0.55f) : new Color(1f, 1f, 1f),
            dryColor     = grass ? new Color(0.62f, 0.78f, 0.40f) : new Color(0.95f, 0.95f, 0.85f),
            minWidth  = grass ? 0.55f : 0.35f, maxWidth  = grass ? 0.95f : 0.55f,
            minHeight = grass ? 0.45f : 0.30f, maxHeight = grass ? 0.85f : 0.55f,
            noiseSpread = 0.4f,
        };
        return true;
    }
}
