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

    // ---------------------------------------------------------------- vegetation / scatter
    // onlyZone == -1 builds (and wipes) all zones; >=0 rebuilds just that zone, leaving others alone.
    public static void BuildVegetation(IslandDefinition def, int onlyZone = -1)
    {
        var terrain = GetTerrain(def);
        if (terrain == null) { Debug.LogError("[Island] No terrain '" + (def ? def.terrainName : "?") + "'."); return; }
        var px = ReadZones(def, out int res);
        if (px == null) { Debug.LogError("[Island] No zone map — create + paint zones first."); return; }

        var td = terrain.terrainData; var tpos = terrain.transform.position;
        var hero = GameObject.Find("HomeTree");
        Vector3 heroPos = hero != null ? hero.transform.position : new Vector3(1e9f, 0, 1e9f);

        // keep the small scattered trees clear of the big hand-placed climbing giants so nothing grows through them
        var giants = new List<Vector2>();
        var giantRoot = GameObject.Find("ClimbingTrees_" + def.name);
        if (giantRoot != null) foreach (Transform g in giantRoot.transform) giants.Add(new Vector2(g.position.x, g.position.z));
        const float giantClear = 9f;

        string rootName = "IslandScatter_" + def.name;
        if (onlyZone < 0)   // full build also clears legacy containers
            foreach (var name in new[] { "IslandBuild_" + def.name, "ForestSlice", rootName })
            { var g = GameObject.Find(name); if (g != null) Object.DestroyImmediate(g); }

        var root = GameObject.Find(rootName);
        if (root == null) { root = new GameObject(rootName); Undo.RegisterCreatedObjectUndo(root, "Build Scatter"); }

        int from = onlyZone < 0 ? 0 : onlyZone;
        int to   = onlyZone < 0 ? def.zones.Count : onlyZone + 1;
        Random.InitState(def.seed + 1234 + from * 7);

        var cache = new Dictionary<GameObject, Combined>();
        int total = 0, instanced = 0;

        for (int zi = from; zi < to; zi++)
        {
            if (zi < 0 || zi >= def.zones.Count) continue;
            var z = def.zones[zi];
            var old = root.transform.Find(z.name);
            if (old != null) Object.DestroyImmediate(old.gameObject);   // rebuild just this zone
            if (z.palette == null || z.palette.entries.Count == 0) continue;

            var zoneRoot = new GameObject(z.name);
            zoneRoot.transform.SetParent(root.transform, false);
            var anchors = new List<Vector3>();                 // open-ground props -> mushrooms cluster near these
            var groups  = new List<ScatterField.Group>();      // instanced (windowed) layers for this zone

            // PASS 1: open-ground props (trees, logs) -> windowed INSTANCED, not GameObjects
            foreach (var e in z.palette.entries)
            {
                if (e.prefab == null || e.nearProps) continue;
                var cm = GetCombined(e.prefab, cache);
                if (cm.mesh == null) continue;
                float normScale = (e.normalizeHeight > 0f && cm.bounds.size.y > 0.01f) ? e.normalizeHeight / cm.bounds.size.y : 0f;
                float slopeCap = Mathf.Min(z.slopeLimit, e.slopeLimit);
                float sp = Mathf.Max(1f, e.spacing);
                var mats = new List<Matrix4x4>(); var posL = new List<Vector3>(); var sclL = new List<float>();

                for (float gz = tpos.z; gz < tpos.z + td.size.z; gz += sp)
                for (float gx = tpos.x; gx < tpos.x + td.size.x; gx += sp)
                {
                    float x = gx + Random.Range(-sp * 0.4f, sp * 0.4f);
                    float zc = gz + Random.Range(-sp * 0.4f, sp * 0.4f);
                    if (ZoneAt(px, res, terrain, x, zc) != zi) continue;
                    float nx = (x - tpos.x) / td.size.x, nz = (zc - tpos.z) / td.size.z;
                    if (nx < 0 || nx > 1 || nz < 0 || nz > 1) continue;
                    if (td.GetSteepness(nx, nz) > slopeCap) continue;
                    if (z.clearance > 0f && new Vector2(x - heroPos.x, zc - heroPos.z).magnitude < z.clearance) continue;
                    bool nearGiant = false;
                    foreach (var gp in giants) if ((new Vector2(x, zc) - gp).sqrMagnitude < giantClear * giantClear) { nearGiant = true; break; }
                    if (nearGiant) continue;
                    if (Random.value > e.density) continue;

                    float instS = (normScale > 0f ? normScale : 1f) * Random.Range(e.minScale, e.maxScale);
                    // sit on the LOWEST terrain across the base footprint so a wide prop never floats on the high side
                    // of a bump/slope; then sink the base a touch so roots embed instead of resting exactly on top.
                    float gy = e.footprint > 0.01f
                        ? LowestGround(terrain, tpos, x, zc, e.footprint)
                        : terrain.SampleHeight(new Vector3(x, 0, zc)) + tpos.y;
                    float y = e.groundSit ? gy - cm.bounds.min.y * instS - e.sinkDepth : gy - 0.1f;
                    var wp = new Vector3(x, y, zc);
                    mats.Add(Matrix4x4.TRS(wp, Quaternion.AngleAxis(Random.value * 360f, Vector3.up), Vector3.one * instS));
                    posL.Add(wp); sclL.Add(instS);
                    anchors.Add(new Vector3(x, gy, zc));
                    total++; instanced++;
                }

                if (mats.Count == 0) continue;
                foreach (var m in cm.mats) if (m != null) { m.enableInstancing = true; EditorUtility.SetDirty(m); }
                groups.Add(new ScatterField.Group
                {
                    name = e.prefab.name, mesh = cm.mesh, materials = cm.mats,
                    instances = mats.ToArray(), positions = posL.ToArray(), scales = sclL.ToArray(),
                    collide = cm.bounds.size.y > cm.bounds.size.x * 1.3f,            // tall = tree -> trunk capsule
                    colRadius = Mathf.Clamp(cm.bounds.size.x * 0.05f, 0.35f, 0.7f),
                    colHeight = Mathf.Max(2f, cm.bounds.size.y),
                    colCenterY = cm.bounds.center.y,
                });
            }

            // PASS 2: near-props (mushrooms, which carry glow lights) -> GameObjects around the anchors, upright
            foreach (var e in z.palette.entries)
            {
                if (e.prefab == null || !e.nearProps) continue;
                float baseScale = NormalizeScale(e);
                float slopeCap = Mathf.Min(z.slopeLimit, e.slopeLimit);
                foreach (var anchor in anchors)
                {
                    if (Random.value > e.density) continue;          // density = chance this prop gets a mushroom
                    float ang = Random.value * Mathf.PI * 2f, rr = Random.Range(0.8f, 2.6f);
                    float x = anchor.x + Mathf.Cos(ang) * rr, zc = anchor.z + Mathf.Sin(ang) * rr;
                    if (ZoneAt(px, res, terrain, x, zc) != zi) continue;
                    float nx = (x - tpos.x) / td.size.x, nz = (zc - tpos.z) / td.size.z;
                    if (nx < 0 || nx > 1 || nz < 0 || nz > 1) continue;
                    if (td.GetSteepness(nx, nz) > slopeCap) continue;
                    Place(e, x, zc, terrain, tpos, baseScale, zoneRoot.transform);
                    total++;
                }
            }

            if (groups.Count > 0)
            {
                var sf = zoneRoot.AddComponent<ScatterField>();
                sf.groups = groups.ToArray();
            }
        }

        Selection.activeGameObject = root;
        Debug.Log($"[Island] Scatter: {total} props ({instanced} instanced) {(onlyZone < 0 ? "across all zones" : "in '" + def.zones[onlyZone].name + "'")}. Trees/logs render windowed around the camera (no cap).");
    }

    // ---- per-prefab mesh merge (a tree's own ~30 parts -> one instanceable mesh; cached, reversible) ----
    struct Combined { public Mesh mesh; public Material[] mats; public Bounds bounds; }

    static Combined GetCombined(GameObject prefab, Dictionary<GameObject, Combined> cache)
    {
        if (cache.TryGetValue(prefab, out var c)) return c;
        var mesh = CombinePrefab(prefab, out var mats);
        c = new Combined { mesh = mesh, mats = mats, bounds = mesh != null ? mesh.bounds : new Bounds() };
        cache[prefab] = c;
        return c;
    }

    static Mesh CombinePrefab(GameObject prefab, out Material[] mats)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.transform.position = Vector3.zero;   // keep design rotation + scale, bake at world origin
        var byMat = new Dictionary<Material, List<CombineInstance>>();
        foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mf.sharedMesh == null || mr == null || !mr.enabled) continue;
            var sm = mr.sharedMaterials;
            for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
            {
                var mat = s < sm.Length ? sm[s] : (sm.Length > 0 ? sm[sm.Length - 1] : null);
                if (mat == null) continue;
                if (!byMat.TryGetValue(mat, out var list)) { list = new List<CombineInstance>(); byMat[mat] = list; }
                list.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = mf.transform.localToWorldMatrix });
            }
        }
        var perMat = new List<Mesh>(); var matList = new List<Material>();
        foreach (var kv in byMat)
        {
            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.CombineMeshes(kv.Value.ToArray(), true, true);   // merge all of this material's parts
            perMat.Add(m); matList.Add(kv.Key);
        }
        var fc = new CombineInstance[perMat.Count];
        for (int i = 0; i < perMat.Count; i++) fc[i] = new CombineInstance { mesh = perMat[i], transform = Matrix4x4.identity };
        var final = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = prefab.name + "_inst" };
        final.CombineMeshes(fc, false, false);    // one submesh per material
        final.RecalculateBounds();
        Object.DestroyImmediate(inst);
        mats = matList.ToArray();
        return final;
    }

    // Lowest terrain height across a small ring around (x,z) — so a wide-footed prop sits on the low point under
    // its base and doesn't float on the high side of a bump or slope. Returns world Y (terrain + base offset).
    static float LowestGround(Terrain terrain, Vector3 tpos, float x, float z, float radius)
    {
        float lo = terrain.SampleHeight(new Vector3(x, 0, z));
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 0.25f;
            float h = terrain.SampleHeight(new Vector3(x + Mathf.Cos(a) * radius, 0, z + Mathf.Sin(a) * radius));
            if (h < lo) lo = h;
        }
        return lo + tpos.y;
    }

    // Removes scattered props (current + legacy containers) without rebuilding.
    public static void ClearScatter(IslandDefinition def)
    {
        int n = 0;
        foreach (var name in new[] { "IslandScatter_" + def.name, "IslandBuild_" + def.name, "ForestSlice" })
        { var g = GameObject.Find(name); if (g != null) { Object.DestroyImmediate(g); n++; } }
        Debug.Log($"[Island] Cleared {n} scatter container(s).");
    }

    // ---------------------------------------------------------------- mobs
    // Place a zone's assigned mob across that zone and bind each to roam it. A scene object with count 1 is
    // bound in place; otherwise count copies are spawned at random in-zone spots under a "Mobs_<island>" root.
    public static void PlaceMobs(IslandDefinition def, int zoneIndex)
    {
        var z = def.zones[zoneIndex];
        if (z.mobs == null || z.mobs.All(e => e.prefab == null)) { Debug.LogWarning($"[Mobs] Zone '{z.name}' has no mobs assigned."); return; }
        var terrain = GetTerrain(def);
        if (terrain == null) { Debug.LogWarning("[Mobs] No terrain."); return; }
        var px = ReadZones(def, out int res);
        if (px == null) { Debug.LogWarning("[Mobs] No zone map painted yet."); return; }

        var tpos = terrain.transform.position; var size = terrain.terrainData.size;
        // collect EVERY painted-in-zone texel as a world position, then place from those — reliable for any zone
        // size (uniform random darts over the whole terrain almost never hit a tiny zone like a mountain plateau).
        var cells = new List<Vector3>();
        for (int yy = 0; yy < res; yy++)
        for (int xx = 0; xx < res; xx++)
            if (px[yy * res + xx].r - 1 == zoneIndex)
                cells.Add(new Vector3(tpos.x + (xx / (float)(res - 1)) * size.x, 0f, tpos.z + (yy / (float)(res - 1)) * size.z));
        if (cells.Count == 0) { Debug.LogWarning($"[Mobs] Zone '{z.name}' has no painted area — paint it before placing mobs."); return; }

        var root = GameObject.Find("Mobs_" + def.name) ?? new GameObject("Mobs_" + def.name);
        // clear THIS zone's previously-placed mobs first, so Place All replaces instead of stacking up
        int cleared = 0;
        for (int c = root.transform.childCount - 1; c >= 0; c--)
        {
            var child = root.transform.GetChild(c);
            if (child.name.Contains("_" + z.name)) { Object.DestroyImmediate(child.gameObject); cleared++; }
        }
        // half a texel, in world units — used to jitter each spawn off the texel grid so creatures don't snap
        // into visible rows/clusters (the "they spawn in their own sections" look at coarse zone-map resolution).
        float halfCellX = 0.5f * size.x / (res - 1);
        float halfCellZ = 0.5f * size.z / (res - 1);

        // Expand every (entry, count) into a flat placement list, then SHUFFLE it so the types interleave both
        // in the hierarchy and in placement order — beetles, frogs and hoppers end up mixed across the zone
        // instead of laid down one species-block at a time.
        var queue = new List<ZoneMob>();
        foreach (var entry in z.mobs)
            if (entry.prefab != null) for (int k = 0; k < Mathf.Max(1, entry.count); k++) queue.Add(entry);
        for (int i = queue.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (queue[i], queue[j]) = (queue[j], queue[i]); }

        var tally = new Dictionary<string, int>();
        int placed = 0;
        foreach (var entry in queue)
        {
            bool isAsset = EditorUtility.IsPersistent(entry.prefab);
            var cflat = cells[Random.Range(0, cells.Count)];
            float jx = cflat.x + Random.Range(-halfCellX, halfCellX);
            float jz = cflat.z + Random.Range(-halfCellZ, halfCellZ);
            var cj = new Vector3(jx, 0f, jz);
            Vector3 spot = new Vector3(jx, terrain.SampleHeight(cj) + tpos.y, jz);
            tally.TryGetValue(entry.prefab.name, out int idx); tally[entry.prefab.name] = idx + 1;
            var inst = isAsset ? (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab) : Object.Instantiate(entry.prefab);
            inst.name = entry.prefab.name + "_" + z.name + "_" + (idx + 1);
            inst.transform.SetParent(root.transform, true);
            inst.transform.position = spot;
            inst.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            BindMob(inst, def, z.name);
            placed++;
        }
        Debug.Log($"[Mobs] Placed {placed} creature(s) across '{z.name}' (cleared {cleared} old), mixed: {string.Join(", ", tally.Select(kv => $"{kv.Value}×{kv.Key}"))}.");
    }

    // Create/remove one zone-bound firefly emitter per zone, driven by each zone's `fireflies` on/off toggle.
    // The emitter only glows when the camera is inside that zone at night, so several zones can each have their own.
    public static void ApplyFireflies(IslandDefinition def)
    {
        var root = GameObject.Find("Fireflies_" + def.name) ?? new GameObject("Fireflies_" + def.name);
        int on = 0;
        foreach (var z in def.zones)
        {
            var existing = root.transform.Find(z.name);
            if (z.fireflies)
            {
                var go = existing != null ? existing.gameObject : new GameObject(z.name);
                go.transform.SetParent(root.transform, false);
                var ff = go.GetComponent<Fireflies>() ?? go.AddComponent<Fireflies>();
                ff.Configure(def, z.name, def.terrainName);
                on++;
            }
            else if (existing != null) Object.DestroyImmediate(existing.gameObject);
        }
        // also clear out any legacy single "Fireflies" emitters from the old (pre-zone) tool
        foreach (var stray in Object.FindObjectsByType<Fireflies>(FindObjectsSortMode.None))
            if (stray.transform.parent == null && stray.gameObject.name == "Fireflies") Object.DestroyImmediate(stray.gameObject);
        if (root.transform.childCount == 0) Object.DestroyImmediate(root);
        Debug.Log($"[Fireflies] {on} zone(s) now have fireflies (glow at night when you're inside them).");
    }

    // Despawn the current pet (the "Pet" object in the scene).
    public static void RemovePet()
    {
        var p = GameObject.Find("Pet");
        if (p != null) { Object.DestroyImmediate(p); Debug.Log("[Pet] Removed."); }
        else Debug.Log("[Pet] No pet in the scene to remove.");
    }

    // Spawn a creature beside the player as a follow-pet (strips its wild AI, adds PetFollow). One pet at a time.
    public static void SpawnPet(GameObject prefab)
    {
        if (prefab == null) return;
        var old = GameObject.Find("Pet");
        if (old != null) Object.DestroyImmediate(old);

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.name = "Pet";
        var pm = Object.FindFirstObjectByType<PlayerMovement>();
        if (pm != null) inst.transform.position = pm.transform.position + pm.transform.right * 1.5f - pm.transform.forward * 0.5f;

        // read the creature's OWN pace before stripping its AI, so the pet follows at its natural speed
        float petSpeed = 2.5f;
        var zr = inst.GetComponent<ZoneRoamer>(); if (zr != null) petSpeed = zr.RoamSpeed;
        var pr = inst.GetComponent<Predator>();   if (pr != null) petSpeed = pr.ChaseSpeed;

        foreach (var c in new Component[] { zr, pr, inst.GetComponent<CreatureWander>() })
            if (c != null) Object.DestroyImmediate(c);
        foreach (var col in inst.GetComponentsInChildren<Collider>(true)) col.enabled = false;   // a pet must never shove the player
        foreach (var rb in inst.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
        var pf = inst.GetComponent<PetFollow>() ?? inst.AddComponent<PetFollow>();
        pf.SetMaxSpeed(petSpeed);

        Selection.activeGameObject = inst;
        Debug.Log($"[Pet] '{prefab.name}' is now your pet. Press Play — walk/run/jump and it mirrors you. Tweak its clips, re-run its Setup, and Spawn as Pet again to re-check.");
    }

    static void BindMob(GameObject go, IslandDefinition def, string zoneName)
    {
        var pred = go.GetComponent<Predator>();
        if (pred != null) { pred.Configure(def, zoneName); EditorUtility.SetDirty(pred); return; }
        var filler = go.GetComponent<FillerCritter>();   // cheap ambient critter — leave it cheap, just bind the zone
        if (filler != null) { filler.Configure(def, zoneName, def.terrainName); EditorUtility.SetDirty(filler); return; }
        var cw = go.GetComponent<CreatureWander>();                 // old free-roam would fight the zone roamer
        if (cw != null) cw.enabled = false;
        var roamer = go.GetComponent<ZoneRoamer>() ?? Undo.AddComponent<ZoneRoamer>(go);
        roamer.Configure(def, zoneName, def.terrainName);
        EditorUtility.SetDirty(roamer);

        // self-diagnose the common mistake: assigning the raw imported model (grey, no controller) instead of
        // a configured creature. A textured + animated mob needs an Animator controller and built materials.
        var anim = go.GetComponentInChildren<Animator>();
        if (anim == null || anim.runtimeAnimatorController == null)
            Debug.LogWarning($"[Mobs] '{go.name}' has no Animator controller — it will be static. Assign a CONFIGURED " +
                             "creature to the zone's Mob slot (the textured, animated scene object or a saved prefab), NOT the raw imported FBX.");
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
    // Sets up the live, camera-windowed instanced grass field (no bake, no cap, per-zone live).
    public static void BuildGrass(IslandDefinition def)
    {
        var terrain = GetTerrain(def);
        if (terrain == null) { Debug.LogError("[Island] No terrain."); return; }
        if (def.zoneMap == null) { Debug.LogError("[Island] No zone map — paint zones first."); return; }
        var td = terrain.terrainData;

        // wipe any old terrain-detail billboard grass left on the terrain
        if (td.detailPrototypes.Length > 0) { td.detailPrototypes = new DetailPrototype[0]; EditorUtility.SetDirty(td); }

        // clump meshes + material, loaded directly by path (prefab mesh refs can dangle)
        var meshes = new List<Mesh>();
        for (int i = 1; i <= 3; i++)
        {
            var m = AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/Environment/Grass/GrassClump_{i}.mesh");
            if (m != null) meshes.Add(m);
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/Grass/GrassBlade.mat");
        if (meshes.Count == 0 || mat == null)
        { Debug.LogError($"[Island] grass assets missing (meshes={meshes.Count}, mat={(mat != null ? "ok" : "null")}) — run 'Build Grass Clumps' first."); return; }
        mat.enableInstancing = true; EditorUtility.SetDirty(mat);

        // per-zone on/off + density snapshot
        int n = def.zones.Count;
        var grassOn = new bool[n]; var dens = new float[n];
        for (int i = 0; i < n; i++) { grassOn[i] = def.zones[i].grass; dens[i] = def.zones[i].grassDensity; }

        // create/reuse the field object; clear any old baked batch children
        string goName = "IslandGrass_" + def.name;
        var existing = GameObject.Find(goName);
        var fieldGo = existing != null ? existing : new GameObject(goName);
        if (existing == null) Undo.RegisterCreatedObjectUndo(fieldGo, "Grass Field");
        else for (int c = fieldGo.transform.childCount - 1; c >= 0; c--) Object.DestroyImmediate(fieldGo.transform.GetChild(c).gameObject);

        var field = fieldGo.GetComponent<GrassField>() ?? fieldGo.AddComponent<GrassField>();
        field.terrainName = def.terrainName;
        field.zoneMap = def.zoneMap;
        field.meshes = meshes.ToArray();
        field.material = mat;
        field.zoneGrass = grassOn;
        field.zoneDensity = dens;
        field.seed = def.seed;
        field.Refresh();
        EditorUtility.SetDirty(fieldGo);

        Selection.activeGameObject = fieldGo;
        Debug.Log($"[Island] Grass: live instanced field set up ({meshes.Count} clump meshes). " +
                  "Windowed around the camera — any zone size, no cap, per-zone live. Re-run after editing zones/density.");
    }
}
