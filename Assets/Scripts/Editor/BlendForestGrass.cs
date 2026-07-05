using System.Linq;
using UnityEngine;
using UnityEditor;

// Blends the Ground_Dark grass texture into PATCHES of the forest ground (terrain splat), at a gentle opacity so
// it reads as variation over the base green rather than a hard repaint. Patches come from low-freq noise, only
// inside the painted "Forrest" zone. Re-runnable + idempotent: it only redistributes the light↔dark "grass-ground"
// portion of each cell, so any rock/path painting you've done is preserved.
public static class BlendForestGrass
{
    const string IslandPath = "Assets/World/Island1.asset";
    const string DarkLayer  = "Assets/Materials/Ground_Dark.terrainlayer";
    const string ZoneName   = "Forrest";
    const float  MaxOpacity = 0.5f;    // peak dark-grass weight in a patch (kept low so it blends)
    const float  PatchScale = 0.02f;   // lower = bigger patches

    [MenuItem("WildReach/World/Blend Grass Into Forest Ground")]
    static void Blend()
    {
        var terrain = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
        if (terrain == null || terrain.terrainData == null) { Debug.LogError("[GroundGrass] No terrain."); return; }
        var td = terrain.terrainData;

        // ensure the dark grass layer is on the terrain; find base (light) + dark indices
        var layers = td.terrainLayers.ToList();
        int darkIdx = layers.FindIndex(l => l != null && l.name.Contains("Ground_Dark"));
        if (darkIdx < 0)
        {
            var dl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(DarkLayer);
            if (dl == null) { Debug.LogError($"[GroundGrass] Missing {DarkLayer} — run 'Setup Ground' first."); return; }
            layers.Add(dl); td.terrainLayers = layers.ToArray(); darkIdx = layers.Count - 1;
        }
        int baseIdx = layers.FindIndex(l => l != null && l.name.Contains("Ground_Light"));
        if (baseIdx < 0) baseIdx = 0;
        if (baseIdx == darkIdx) { Debug.LogError("[GroundGrass] base and dark layer are the same."); return; }

        var def = AssetDatabase.LoadAssetAtPath<IslandDefinition>(IslandPath);
        if (def == null || def.zoneMap == null) { Debug.LogError($"[GroundGrass] No island/zoneMap at {IslandPath}."); return; }
        int forest = def.zones.FindIndex(z => z.name == ZoneName);
        if (forest < 0) { Debug.LogError($"[GroundGrass] No '{ZoneName}' zone."); return; }
        var zp = def.zoneMap.GetPixels32(); int zr = def.zoneMap.width;

        int aw = td.alphamapWidth, ah = td.alphamapHeight;
        var maps = td.GetAlphamaps(0, 0, aw, ah);
        var tpos = terrain.transform.position; var size = td.size;
        int touched = 0;

        for (int y = 0; y < ah; y++)
        for (int x = 0; x < aw; x++)
        {
            float u = x / (float)(aw - 1), v = y / (float)(ah - 1);
            int zx = Mathf.Clamp(Mathf.RoundToInt(u * (zr - 1)), 0, zr - 1);
            int zy = Mathf.Clamp(Mathf.RoundToInt(v * (zr - 1)), 0, zr - 1);
            if (zp[zy * zr + zx].r - 1 != forest) continue;                 // forest texels only

            float wx = tpos.x + u * size.x, wz = tpos.z + v * size.z;
            float n = Mathf.PerlinNoise(wx * PatchScale, wz * PatchScale) * 0.65f
                    + Mathf.PerlinNoise(wx * PatchScale * 3.7f + 50f, wz * PatchScale * 3.7f + 50f) * 0.35f;
            float patch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.78f, n));
            float frac = patch * MaxOpacity;                               // 0..MaxOpacity dark within the grass pool

            // redistribute ONLY the light+dark pool (leave rock/other layers as-is) → idempotent + non-destructive
            float pool = maps[y, x, baseIdx] + maps[y, x, darkIdx];
            if (pool <= 0.001f) continue;
            maps[y, x, darkIdx] = pool * frac;
            maps[y, x, baseIdx] = pool * (1f - frac);
            touched++;
        }

        td.SetAlphamaps(0, 0, maps);
        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();
        Debug.Log($"[GroundGrass] Blended Ground_Dark grass into {touched} forest texels (patches, max {MaxOpacity:P0} opacity). " +
                  "Re-run to refresh; tweak MaxOpacity/PatchScale in the script to taste.");
    }
}
