using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Places the Ancient Sentinel giants as REAL GameObjects (not ScatterField instances) across the forest, so they
// have their full MeshCollider and you climb them exactly like HomeTree — and they're seated on the ground from
// their actual renderer bounds, so they don't float. Also removes them from the scatter palette so the thin-capsule
// instanced version stops being generated. Run, then re-Scatter the Forrest zone once to clear any old floating ones.
public static class PlaceClimbingTrees
{
    const string IslandPath = "Assets/World/Island1.asset";
    const string ZoneName   = "Forrest";
    static readonly string[] Prefabs =
    {
        "Assets/Environment/Trees/AncientSentinel_Green.prefab",
        "Assets/Environment/Trees/AncientSentinel_Autumn.prefab",
    };
    const float TargetHeight = 38f;   // metres tall
    const int   Count        = 16;
    const float MinSpacing   = 34f;   // keep the giants well apart (their canopies are wide)
    const float PropClear    = 12f;   // keep clear of hero props (HomeTree, MossLog, SpiralTree)
    const float Sink         = 1.5f;  // bury the base a touch
    const float SlopeLimit   = 28f;

    [MenuItem("WildReach/World/Diagnose Climbing Trees")]
    static void Diagnose()
    {
        var def = AssetDatabase.LoadAssetAtPath<IslandDefinition>(IslandPath);
        var root = GameObject.Find("ClimbingTrees_" + (def != null ? def.name : "Island1"));
        if (root == null) { Debug.LogWarning("[ClimbTrees] No ClimbingTrees root in scene."); return; }
        int bare = 0;
        foreach (Transform t in root.transform)
        {
            var canopy = t.Find("Canopy");
            int leafRenderers = 0, leafMeshOk = 0;
            if (canopy != null)
                foreach (var mf in canopy.GetComponentsInChildren<MeshFilter>(true))
                { leafRenderers++; if (mf.sharedMesh != null) leafMeshOk++; }
            bool ok = canopy != null && leafMeshOk > 0;
            if (!ok) bare++;
            Debug.Log($"[ClimbTrees] {t.name}: canopy={(canopy != null)} leafRenderers={leafRenderers} leafMeshOk={leafMeshOk} scale={t.localScale.x:F2} {(ok ? "" : "  <-- BARE")}");
        }
        Debug.Log($"[ClimbTrees] {root.transform.childCount} giants, {bare} bare. If bare>0, those instances lost their canopy mesh (re-run Place; if it persists the prefab's canopy mesh asset is missing).");
    }

    // Re-seat the ALREADY-PLACED giants where they stand (no re-roll of positions) — for when the seating
    // logic improves, like the slope-footprint fix. Handles BOTH homes the sentinels can live in:
    // real GameObjects under a ClimbingTrees root, AND baked instance matrices inside ScatterField groups
    // (where they actually are since the palette drop — the group data survives with the old seating).
    [MenuItem("WildReach/World/Reseat Climbing Trees")]
    static void Reseat()
    {
        var def = AssetDatabase.LoadAssetAtPath<IslandDefinition>(IslandPath);
        var terrain = (def != null ? GameObject.Find(def.terrainName)?.GetComponent<Terrain>() : null) ?? Terrain.activeTerrain;
        if (terrain == null) { Debug.LogError("[ClimbTrees] No terrain."); return; }

        int moved = 0;

        // 1) real placed GameObjects, if any
        var root = GameObject.Find("ClimbingTrees_" + (def != null ? def.name : "Island1"));
        if (root != null)
            foreach (Transform tr in root.transform)
            {
                float oldY = tr.position.y;
                Seat(tr.gameObject, terrain, tr.position);
                if (Mathf.Abs(tr.position.y - oldY) > 0.01f) moved++;
                Debug.Log($"[ClimbTrees] {tr.name}: y {oldY:F2} -> {tr.position.y:F2} (moved {tr.position.y - oldY:+0.00;-0.00}m)");
            }

        // 2) instanced sentinels baked into ScatterField groups
        foreach (var sf in Object.FindObjectsByType<ScatterField>(FindObjectsInactive.Include))
        {
            if (sf.groups == null) continue;
            bool dirty = false;
            foreach (var g in sf.groups)
            {
                if (g == null || g.mesh == null || g.instances == null || !g.name.Contains("AncientSentinel")) continue;
                var mb = g.mesh.bounds;   // combined mesh, unit scale; instance rotation is yaw-only so Y maths hold
                for (int i = 0; i < g.instances.Length; i++)
                {
                    var m = g.instances[i];
                    var pos = new Vector3(m.m03, m.m13, m.m23);
                    float sc = (g.scales != null && i < g.scales.Length) ? g.scales[i] : 1f;
                    float footprint = Mathf.Clamp(Mathf.Max(mb.size.x, mb.size.z) * sc * 0.25f, 2.5f, 9f);
                    float gy = GroundMinY(terrain, pos, footprint);
                    float newY = gy - mb.min.y * sc - Sink;
                    if (Mathf.Abs(newY - pos.y) < 0.01f) continue;
                    Debug.Log($"[ClimbTrees] {g.name}[{i}]: y {pos.y:F2} -> {newY:F2} (moved {newY - pos.y:+0.00;-0.00}m)");
                    m.m13 = newY; g.instances[i] = m;
                    pos.y = newY; g.positions[i] = pos;
                    dirty = true; moved++;
                }
            }
            if (dirty)
            {
                EditorUtility.SetDirty(sf);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sf.gameObject.scene);
            }
        }

        Debug.Log($"[ClimbTrees] Reseat complete — {moved} giant(s) moved (footprint-lowest seating). Save the scene to keep it.");
    }

    [MenuItem("WildReach/World/Place Climbing Trees in Forest")]
    static void Place()
    {
        var def = AssetDatabase.LoadAssetAtPath<IslandDefinition>(IslandPath);
        if (def == null || def.zoneMap == null) { Debug.LogError($"[ClimbTrees] No island/zoneMap at {IslandPath}."); return; }
        var terrain = GameObject.Find(def.terrainName)?.GetComponent<Terrain>() ?? Terrain.activeTerrain;
        if (terrain == null) { Debug.LogError("[ClimbTrees] No terrain."); return; }
        int forest = def.zones.FindIndex(z => z.name == ZoneName);
        if (forest < 0) { Debug.LogError($"[ClimbTrees] No '{ZoneName}' zone."); return; }

        var prefabs = Prefabs.Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(p => p != null).ToArray();
        if (prefabs.Length == 0) { Debug.LogError("[ClimbTrees] Build the trees first (Build Ancient Trees)."); return; }

        // stop the scatter palette from making the floating thin-capsule instanced version
        DropFromForestPalette();

        // collect forest texels as world cells
        var zp = def.zoneMap.GetPixels32(); int zr = def.zoneMap.width;
        var tpos = terrain.transform.position; var size = terrain.terrainData.size; var td = terrain.terrainData;
        var cells = new List<Vector3>();
        for (int y = 0; y < zr; y++)
        for (int x = 0; x < zr; x++)
            if (zp[y * zr + x].r - 1 == forest)
                cells.Add(new Vector3(tpos.x + (x / (float)(zr - 1)) * size.x, 0f, tpos.z + (y / (float)(zr - 1)) * size.z));
        if (cells.Count == 0) { Debug.LogError("[ClimbTrees] Forest zone isn't painted."); return; }

        var root = GameObject.Find("ClimbingTrees_" + def.name);
        if (root != null) Object.DestroyImmediate(root);
        root = new GameObject("ClimbingTrees_" + def.name);

        // hero props the giants must not grow into
        var avoid = new[] { "HomeTree", "MossLog", "SpiralTree" }
            .Select(n => GameObject.Find(n)).Where(g => g != null).Select(g => g.transform.position).ToList();

        var placed = new List<Vector3>();
        int tries = 0, made = 0;
        while (made < Count && tries < Count * 80)
        {
            tries++;
            var c = cells[Random.Range(0, cells.Count)];
            float u = (c.x - tpos.x) / size.x, v = (c.z - tpos.z) / size.z;
            if (td.GetSteepness(u, v) > SlopeLimit) continue;
            if (placed.Any(p => (p - c).sqrMagnitude < MinSpacing * MinSpacing)) continue;
            if (avoid.Any(p => (p - c).sqrMagnitude < PropClear * PropClear)) continue;

            var prefab = prefabs[Random.Range(0, prefabs.Length)];
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.SetParent(root.transform, true);

            // scale to target height from the full bounds, then seat using the TRUNK only.
            var b = Bounds(inst);
            float h = Mathf.Max(0.01f, b.size.y);
            float s = TargetHeight / h * Random.Range(0.85f, 1.2f);
            inst.transform.localScale *= s;
            inst.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            Seat(inst, terrain, c);
            inst.name = prefab.name + "_" + (made + 1);

            if (made < 4)   // instrument the first few so we can see what's actually happening
            {
                int leafOk = 0, leafTot = 0;
                var cn = inst.transform.Find("Canopy");
                if (cn != null) foreach (var mf in cn.GetComponentsInChildren<MeshFilter>(true)) { leafTot++; if (mf.sharedMesh != null) leafOk++; }
                Debug.Log($"[ClimbTrees] {inst.name}: y={inst.transform.position.y:F2} scale={inst.transform.localScale.x:F2} leaves={leafOk}/{leafTot}");
            }
            placed.Add(c);
            made++;
        }

        Selection.activeGameObject = root;
        Debug.Log($"[ClimbTrees] Placed {made} real climbing giants (mesh colliders, ground-seated) under '{root.name}'. " +
                  "Now re-Scatter the Forrest zone ONCE to clear any old floating instanced trees.");
    }

    // Seat the tree: trunk base on the LOWEST ground across its footprint. A single center height sample seats
    // the trunk at mid-slope on a hillside and leaves the downhill half of the root flare hanging in the air —
    // same lesson as ScatterField's `footprint` fix. Better a buried uphill root than a floating downhill one.
    static void Seat(GameObject inst, Terrain terrain, Vector3 c)
    {
        float trunkMinY = TrunkBase(inst, out float footprint);
        float gy = GroundMinY(terrain, c, footprint);
        float baseOffset = trunkMinY - inst.transform.position.y;
        inst.transform.position = new Vector3(c.x, gy - baseOffset - Sink, c.z);
    }

    // Lowest terrain height at the center + a ring of samples across the footprint radius.
    static float GroundMinY(Terrain t, Vector3 c, float radius)
    {
        float tpy = t.transform.position.y;
        float min = t.SampleHeight(c) + tpy;
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 2f / 8f;
            var p = new Vector3(c.x + Mathf.Cos(a) * radius, 0f, c.z + Mathf.Sin(a) * radius);
            min = Mathf.Min(min, t.SampleHeight(p) + tpy);
        }
        return min;
    }

    // The trunk's lowest world Y + its footprint radius (world XZ half-extent of the root flare). The trunk is
    // the renderer(s) that carry the MeshCollider (the canopy has none); measuring off this ignores leaves + any
    // bare-tree null-mesh renderer so the trunk base sits on the ground.
    static float TrunkBase(GameObject go, out float footprint)
    {
        // transform the trunk mesh's LOCAL bounds corners by its world matrix (always current — no reliance on
        // Renderer.bounds, which can lag a frame behind a transform change in the editor and float the tree).
        float minY = float.MaxValue;
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.GetComponent<MeshCollider>() == null || mf.sharedMesh == null) continue;   // trunk = has a collider
            var b = mf.sharedMesh.bounds; var m = mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + new Vector3(
                    (i & 1) == 0 ? -b.extents.x : b.extents.x,
                    (i & 2) == 0 ? -b.extents.y : b.extents.y,
                    (i & 4) == 0 ? -b.extents.z : b.extents.z);
                var w = m.MultiplyPoint3x4(corner);
                minY = Mathf.Min(minY, w.y);
                minX = Mathf.Min(minX, w.x); maxX = Mathf.Max(maxX, w.x);
                minZ = Mathf.Min(minZ, w.z); maxZ = Mathf.Max(maxZ, w.z);
            }
        }
        if (minY == float.MaxValue) { footprint = 4f; return Bounds(go).min.y; }
        // trunk bounds include high branches, which overstate the base flare — half the extent, kept sane
        footprint = Mathf.Clamp(Mathf.Max(maxX - minX, maxZ - minZ) * 0.25f, 2.5f, 9f);
        return minY;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static void DropFromForestPalette()
    {
        var palette = AssetDatabase.LoadAssetAtPath<ScatterPalette>("Assets/World/TemperateForest.asset");
        if (palette == null) return;
        int before = palette.entries.Count;
        palette.entries.RemoveAll(e => e.prefab != null && e.prefab.name.Contains("AncientSentinel"));
        if (palette.entries.Count != before)
        {
            EditorUtility.SetDirty(palette); AssetDatabase.SaveAssets();
            Debug.Log("[ClimbTrees] Removed the Ancient Sentinels from the Temperate Forest scatter palette (now placed as real trees).");
        }
    }
}
