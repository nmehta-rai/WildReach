using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

// The island-agnostic generator. Reads an IslandDefinition + its painted zone map and produces
// the world: scatters each zone's palette, fills each grass-enabled zone with 3D grass clumps.
// Deterministic (driven by def.seed) and fully re-runnable (wipes its own output first).
public static class IslandBuilder
{
    public static Terrain GetTerrain(IslandDefinition def)
    {
        if (def != null && !string.IsNullOrEmpty(def.terrainName))
        {
            var go = GameObject.Find(def.terrainName);
            if (go != null) { var t = go.GetComponent<Terrain>(); if (t != null) return t; }
        }
        return Terrain.activeTerrain ?? Object.FindFirstObjectByType<Terrain>();
    }

    // zone index per texel (-1 = unpainted), row-major (y*res + x), v along +Z
    static Color32[] ReadZones(IslandDefinition def, out int res)
    {
        res = 0;
        if (def == null || def.zoneMap == null) return null;
        res = def.zoneMap.width;
        return def.zoneMap.GetPixels32();
    }

    static int ZoneAt(Color32[] px, int res, Terrain t, float wx, float wz)
    {
        var tpos = t.transform.position; var size = t.terrainData.size;
        float u = (wx - tpos.x) / size.x, v = (wz - tpos.z) / size.z;
        if (u < 0 || u > 1 || v < 0 || v > 1) return -1;
        int x = Mathf.Clamp(Mathf.RoundToInt(u * (res - 1)), 0, res - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(v * (res - 1)), 0, res - 1);
        return px[y * res + x].r - 1;
    }

    // ---------------------------------------------------------------- vegetation
    public static void BuildVegetation(IslandDefinition def)
    {
        var terrain = GetTerrain(def);
        if (terrain == null) { Debug.LogError("[Island] No terrain '" + (def ? def.terrainName : "?") + "'."); return; }
        var px = ReadZones(def, out int res);
        if (px == null) { Debug.LogError("[Island] No zone map — create + paint zones first."); return; }

        var td = terrain.terrainData; var tpos = terrain.transform.position;
        var hero = GameObject.Find("HomeTree");
        Vector3 heroPos = hero != null ? hero.transform.position : new Vector3(1e9f, 0, 1e9f);

        string rootName = "IslandBuild_" + def.name;
        var existing = GameObject.Find(rootName);
        if (existing != null) Object.DestroyImmediate(existing);
        var root = new GameObject(rootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Island Vegetation");

        Random.InitState(def.seed);
        int total = 0;

        for (int zi = 0; zi < def.zones.Count; zi++)
        {
            var z = def.zones[zi];
            if (z.palette == null || z.palette.entries.Count == 0) continue;
            var zoneRoot = new GameObject(z.name);
            zoneRoot.transform.SetParent(root.transform, false);

            foreach (var e in z.palette.entries)
            {
                if (e.prefab == null) continue;
                float baseScale = NormalizeScale(e);
                float slopeCap = Mathf.Min(z.slopeLimit, e.slopeLimit);
                var mine = new List<Vector3>();
                int made = 0, tries = 0, cap = Mathf.Max(40, e.count * 40);

                while (made < e.count && tries < cap)
                {
                    tries++;
                    float x = tpos.x + Random.value * td.size.x;
                    float zc = tpos.z + Random.value * td.size.z;
                    if (ZoneAt(px, res, terrain, x, zc) != zi) continue;

                    float nx = (x - tpos.x) / td.size.x, nz = (zc - tpos.z) / td.size.z;
                    if (td.GetSteepness(nx, nz) > slopeCap) continue;
                    if (z.clearance > 0f && new Vector2(x - heroPos.x, zc - heroPos.z).magnitude < z.clearance) continue;
                    if (mine.Any(p => new Vector2(x - p.x, zc - p.z).magnitude < e.spacing)) continue;

                    Place(e, x, zc, terrain, tpos, baseScale, zoneRoot.transform);
                    mine.Add(new Vector3(x, 0, zc));
                    made++; total++;
                }
                Debug.Log($"[Island] {z.name}/{e.Label}: {made}/{e.count}");
            }
        }

        Selection.activeGameObject = root;
        Debug.Log($"[Island] '{def.name}': scattered {total} props across {def.zones.Count} zone(s). Deterministic (seed {def.seed}).");
    }

    static float NormalizeScale(ScatterEntry e)
    {
        if (e.normalizeHeight <= 0f) return 0f;        // 0 => use the prefab's own scale
        var probe = (GameObject)PrefabUtility.InstantiatePrefab(e.prefab);
        var rs = probe.GetComponentsInChildren<Renderer>();
        float h = 0f;
        if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); h = b.size.y; }
        Object.DestroyImmediate(probe);
        return h > 0.01f ? e.normalizeHeight / h : 1f;
    }

    static void Place(ScatterEntry e, float x, float z, Terrain terrain, Vector3 tpos, float baseScale, Transform parent)
    {
        float gy = terrain.SampleHeight(new Vector3(x, 0, z)) + tpos.y;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(e.prefab);
        inst.transform.SetParent(parent, true);

        float r = Random.Range(e.minScale, e.maxScale);
        inst.transform.localScale = e.normalizeHeight > 0f
            ? Vector3.one * (baseScale * r)
            : inst.transform.localScale * r;
        // keep the prefab's own rotation (some bake an upright correction), add random yaw on top
        inst.transform.rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * inst.transform.rotation;

        inst.transform.position = new Vector3(x, gy - (e.groundSit ? 0f : 0.1f), z);
        if (e.groundSit)
        {
            var rends = inst.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds; foreach (var rr in rends) b.Encapsulate(rr.bounds);
                inst.transform.position += Vector3.up * (gy - b.min.y - 0.03f);
            }
        }
    }

    // ---------------------------------------------------------------- grass
    public static void BuildGrass(IslandDefinition def)
    {
        var terrain = GetTerrain(def);
        if (terrain == null) { Debug.LogError("[Island] No terrain."); return; }
        var px = ReadZones(def, out int res);
        if (px == null) { Debug.LogError("[Island] No zone map — paint zones first."); return; }
        var td = terrain.terrainData;

        // 3D grass-clump prototypes (built by 'Build Grass Clumps')
        var protos = new List<DetailPrototype>();
        for (int i = 1; i <= 3; i++)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Environment/Grass/GrassClump_{i}.prefab");
            if (go == null) continue;
            protos.Add(new DetailPrototype
            {
                usePrototypeMesh = true, prototype = go, renderMode = DetailRenderMode.Grass,
                healthyColor = new Color(0.85f, 1f, 0.7f), dryColor = new Color(0.7f, 0.85f, 0.5f),
                minWidth = 0.8f, maxWidth = 1.5f, minHeight = 0.8f, maxHeight = 1.5f, noiseSpread = 0.5f,
            });
        }
        if (protos.Count == 0) { Debug.LogError("[Island] No grass clumps — run 'Build Grass Clumps' first."); return; }

        td.detailPrototypes = protos.ToArray();
        int dres = 512; td.SetDetailResolution(dres, 16);
        int w = td.detailWidth, h = td.detailHeight, gc = protos.Count;
        const int MaxGrass = 6;

        var maps = new int[gc][,];
        for (int k = 0; k < gc; k++) maps[k] = new int[h, w];
        long filled = 0;

        for (int dy = 0; dy < h; dy++)
        for (int dx = 0; dx < w; dx++)
        {
            float u = (float)dx / (w - 1), v = (float)dy / (h - 1);
            int zi = ZoneAt(px, res, terrain, terrain.transform.position.x + u * td.size.x,
                                              terrain.transform.position.z + v * td.size.z);
            if (zi < 0 || zi >= def.zones.Count) continue;
            var z = def.zones[zi];
            if (!z.grass || z.grassDensity <= 0f) continue;

            float slope = td.GetSteepness(u, v);
            float slopeF = 1f - Mathf.Clamp01((slope - 22f) / 23f);
            if (slopeF <= 0f) continue;
            float clump = Mathf.PerlinNoise(dx * 0.05f, dy * 0.05f);
            int blades = Mathf.RoundToInt(z.grassDensity * slopeF * (0.45f + 0.55f * clump) * MaxGrass);
            if (blades <= 0) continue;
            int g = Mathf.Clamp(Mathf.FloorToInt(Mathf.PerlinNoise(dx * 0.02f + 9f, dy * 0.02f + 4f) * gc), 0, gc - 1);
            maps[g][dy, dx] = blades; filled++;
        }

        for (int k = 0; k < gc; k++) td.SetDetailLayer(0, 0, k, maps[k]);
        td.wavingGrassStrength = 0.25f; td.wavingGrassAmount = 0.35f; td.wavingGrassSpeed = 0.4f;
        td.wavingGrassTint = new Color(0.75f, 0.85f, 0.55f);
        terrain.drawTreesAndFoliage = true; terrain.detailObjectDistance = 250f; terrain.detailObjectDensity = 1f;

        EditorUtility.SetDirty(td); AssetDatabase.SaveAssets();
        Debug.Log($"[Island] Grass: filled {filled:N0} cells across grass zones ({gc} clump types).");
    }
}
