using System.IO;
using UnityEngine;
using UnityEditor;

// The world-building cockpit. Pick an island, paint its zones (a dedicated brush, SEPARATE from
// texture painting), configure each zone's palette + grass in place, then Build. The zone map is
// its own paint channel, so you can still paint as many ground textures as you like per zone with
// Unity's native brush — the two never touch.
public class IslandBuilderWindow : EditorWindow
{
    const string PrefKey = "WildReach.IslandBuilder.LastDef";
    const string AssetDir = "Assets/World";

    IslandDefinition def;
    SerializedObject so;
    Vector2 scroll;

    int   paintZone = -1;     // -1 = not painting
    bool  erase;
    bool  showOverlay = true;
    float brushRadius = 8f;

    Color32[] cache;          // cached zone pixels for the overlay
    int       cacheRes;
    int       strokePainted, strokeRejected;   // diagnostics for the current paint stroke
    // overlay = a coarse terrain-conforming mesh textured with a full-res zone-colour map. The mesh
    // only hugs the ground; boundary smoothness comes from BILINEAR sampling of the texture, so edges
    // are smooth and colour-independent no matter how coarse the mesh is.
    Mesh overlayMesh; int gridRes = -1, gridStep;
    Texture2D zoneColTex; bool overlayDirty = true;
    static Material _ovMat;

    [MenuItem("WildReach/World/Island Builder")]
    static void Open()
    {
        var w = GetWindow<IslandBuilderWindow>("Island Builder");
        w.minSize = new Vector2(300, 480);
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnScene;
        string p = EditorPrefs.GetString(PrefKey, "");
        if (!string.IsNullOrEmpty(p)) SetDef(AssetDatabase.LoadAssetAtPath<IslandDefinition>(p));
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnScene;
        paintZone = -1;
        Save();
    }

    // persist zone names/colours/palettes to disk so a recompile can't drop them
    void OnLostFocus() => Save();
    void Save() { if (def != null) { EditorUtility.SetDirty(def); AssetDatabase.SaveAssets(); } }

    void SetDef(IslandDefinition d)
    {
        def = d;
        so = d != null ? new SerializedObject(d) : null;
        cache = null;
        gridRes = -1; overlayDirty = true;   // rebuild overlay geometry + colours for the new island
        paintZone = -1;
        if (d != null) EditorPrefs.SetString(PrefKey, AssetDatabase.GetAssetPath(d));
    }

    // ----------------------------------------------------------------- UI
    void OnGUI()
    {
        EditorGUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope())
        {
            var picked = (IslandDefinition)EditorGUILayout.ObjectField("Island", def, typeof(IslandDefinition), false);
            if (picked != def) SetDef(picked);
            if (GUILayout.Button("New", GUILayout.Width(50))) CreateIsland();
        }

        if (def == null)
        {
            EditorGUILayout.HelpBox("Pick an Island Definition or hit New.\n(Create -> WildReach -> Island Definition also works.)", MessageType.Info);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        // so-tracked fields get their own update/apply cycle so they can't clobber the direct
        // edits to zones / zoneMap that happen below.
        so.Update();
        EditorGUILayout.PropertyField(so.FindProperty("terrainName"));
        EditorGUILayout.PropertyField(so.FindProperty("seed"));
        so.ApplyModifiedProperties();

        ZoneMapSection();
        ZonesSection();
        TexturesSection();
        BuildSection();

        EditorGUILayout.EndScrollView();
    }

    void ZoneMapSection()
    {
        Header("Zone map");
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(def.zoneMap != null ? $"{def.zoneMap.width}px map" : "no map yet", GUILayout.Width(120));
            def.zoneMapRes = EditorGUILayout.IntPopup(def.zoneMapRes, new[] { "256", "512", "1024" }, new[] { 256, 512, 1024 });
            if (GUILayout.Button(def.zoneMap == null ? "Create" : "Resize")) CreateZoneMap();
            using (new EditorGUI.DisabledScope(def.zoneMap == null))
                if (GUILayout.Button("Clear", GUILayout.Width(54))) ClearZoneMap();
        }
        showOverlay = EditorGUILayout.ToggleLeft("Show zone overlay in Scene", showOverlay);
        if (paintZone >= 0 && paintZone < def.zones.Count)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"PAINTING: {def.zones[paintZone].name}" + (erase ? "  (erasing)" : ""), EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            brushRadius = EditorGUILayout.Slider("Brush radius (m)", brushRadius, 1f, 150f);
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();   // resize the brush gizmo live
            erase = EditorGUILayout.ToggleLeft("Erase", erase);
            if (GUILayout.Button("Stop painting")) { paintZone = -1; GUIUtility.ExitGUI(); }
            EditorGUILayout.EndVertical();
        }
    }

    void ZonesSection()
    {
        Header("Zones");
        for (int i = 0; i < def.zones.Count; i++)
        {
            var z = def.zones[i];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                z.color = EditorGUILayout.ColorField(GUIContent.none, z.color, false, false, false, GUILayout.Width(34));
                z.showOverlay = GUILayout.Toggle(z.showOverlay, z.showOverlay ? "show" : "hide", "Button", GUILayout.Width(44));
                z.name = EditorGUILayout.TextField(z.name);
                bool on = paintZone == i;
                bool now = GUILayout.Toggle(on, on ? "Painting" : "Paint", "Button", GUILayout.Width(70));
                if (now != on)
                {
                    paintZone = now ? i : -1; erase = false;
                    if (paintZone >= 0 && def.zoneMap == null) CreateZoneMap();   // no painting without a canvas
                    GUIUtility.ExitGUI();   // bail this GUI pass cleanly so Layout/Repaint stay in sync
                }
                if (GUILayout.Button("✕", GUILayout.Width(24)))
                { Undo.RecordObject(def, "remove zone"); def.zones.RemoveAt(i); paintZone = -1; EditorUtility.SetDirty(def); GUIUtility.ExitGUI(); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                z.palette = (ScatterPalette)EditorGUILayout.ObjectField("Palette", z.palette, typeof(ScatterPalette), false);
                using (new EditorGUI.DisabledScope(z.palette == null))
                    if (GUILayout.Button("Scatter", GUILayout.Width(70))) IslandBuilder.BuildVegetation(def, i);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                z.grass = EditorGUILayout.ToggleLeft("Grass", z.grass, GUILayout.Width(60));
                using (new EditorGUI.DisabledScope(!z.grass))
                    z.grassDensity = EditorGUILayout.Slider(z.grassDensity, 0f, 1f);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                z.slopeLimit = EditorGUILayout.FloatField("Slope limit", z.slopeLimit);
                z.clearance  = EditorGUILayout.FloatField("Hero clear", z.clearance);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                z.mob = (GameObject)EditorGUILayout.ObjectField("Mob", z.mob, typeof(GameObject), true);
                z.mobCount = Mathf.Max(1, EditorGUILayout.IntField(z.mobCount, GUILayout.Width(40)));
                using (new EditorGUI.DisabledScope(z.mob == null))
                    if (GUILayout.Button("Place", GUILayout.Width(70))) IslandBuilder.PlaceMobs(def, i);
            }
            EditorGUILayout.EndVertical();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Add zone"))
            {
                Undo.RecordObject(def, "add zone");
                def.zones.Add(new IslandZone { name = "Zone " + (def.zones.Count + 1), color = Random.ColorHSV(0, 1, 0.5f, 0.8f, 0.7f, 1f) });
                EditorUtility.SetDirty(def);
            }
        }
        if (GUI.changed) { EditorUtility.SetDirty(def); overlayDirty = true; }   // colour/visibility edits repaint the overlay
    }

    void TexturesSection()
    {
        Header("Ground textures (paint with Unity's brush — separate from zones)");
        so.Update();
        EditorGUILayout.PropertyField(so.FindProperty("groundTextures"), true);
        so.ApplyModifiedProperties();
        if (GUILayout.Button("Load textures onto terrain (ready to paint)")) LoadTextures();
    }

    void BuildSection()
    {
        Header("Build");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Build Vegetation", GUILayout.Height(26))) IslandBuilder.BuildVegetation(def);
            if (GUILayout.Button("Build Grass", GUILayout.Height(26)))      IslandBuilder.BuildGrass(def);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Build Island (both)", GUILayout.Height(24)))
            { IslandBuilder.BuildVegetation(def); IslandBuilder.BuildGrass(def); }
            if (GUILayout.Button("Clear Scatter", GUILayout.Width(110), GUILayout.Height(24))) IslandBuilder.ClearScatter(def);
        }
        EditorGUILayout.HelpBox("Per-zone: hit a zone's Scatter button. 'Clear Scatter' removes old props (incl. legacy ForestSlice). Deterministic by seed.", MessageType.None);
    }

    // ----------------------------------------------------------------- actions
    void CreateIsland()
    {
        Directory.CreateDirectory(AssetDir);
        string path = AssetDatabase.GenerateUniqueAssetPath(AssetDir + "/Island.asset");
        var d = CreateInstance<IslandDefinition>();
        AssetDatabase.CreateAsset(d, path);
        AssetDatabase.SaveAssets();
        SetDef(d);
    }

    void CreateZoneMap()
    {
        int res = def.zoneMapRes;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = def.name + "_ZoneMap" };
        var clear = new Color32[res * res];
        for (int i = 0; i < clear.Length; i++) clear[i] = new Color32(0, 0, 0, 255);
        // carry over old paint when resizing
        if (def.zoneMap != null)
        {
            var old = def.zoneMap.GetPixels32(); int ores = def.zoneMap.width;
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
                clear[y * res + x] = old[Mathf.Clamp(y * ores / res, 0, ores - 1) * ores + Mathf.Clamp(x * ores / res, 0, ores - 1)];
        }
        tex.SetPixels32(clear); tex.Apply();

        Directory.CreateDirectory(AssetDir);
        string path = AssetDatabase.GenerateUniqueAssetPath(AssetDir + "/" + def.name + "_ZoneMap.asset");
        if (def.zoneMap != null && AssetDatabase.Contains(def.zoneMap))
        { path = AssetDatabase.GetAssetPath(def.zoneMap); AssetDatabase.DeleteAsset(path); }
        AssetDatabase.CreateAsset(tex, path);
        Undo.RecordObject(def, "zone map"); def.zoneMap = tex; cache = null;
        EditorUtility.SetDirty(def); AssetDatabase.SaveAssets();
        Debug.Log($"[Island] zone map created: {res}px at {path}");
    }

    void ClearZoneMap()
    {
        if (def.zoneMap == null) return;
        int res = def.zoneMap.width; var px = new Color32[res * res];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 255);
        def.zoneMap.SetPixels32(px); def.zoneMap.Apply();
        cache = null; Persist();
    }

    void LoadTextures()
    {
        var terrain = IslandBuilder.GetTerrain(def);
        if (terrain == null || def.groundTextures.Count == 0) { Debug.LogWarning("[Island] No terrain or no textures listed."); return; }
        terrain.terrainData.terrainLayers = def.groundTextures.FindAll(l => l != null).ToArray();
        Debug.Log($"[Island] Loaded {def.groundTextures.Count} ground textures onto {terrain.name}. Use Terrain > Paint Texture.");
    }

    // ----------------------------------------------------------------- painting
    void OnScene(SceneView sv)
    {
        if (def == null) return;
        var terrain = IslandBuilder.GetTerrain(def);
        if (terrain == null) return;

        if (showOverlay && def.zoneMap != null && Event.current.type == EventType.Repaint) DrawOverlay(terrain);

        if (paintZone < 0 || paintZone >= def.zones.Count || def.zoneMap == null) return;

        // Correct scene-view input pattern: one consistent control id, claimed as the default in
        // Layout, with hotControl held for the whole stroke so Drag/Up reliably route back to us.
        var e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout) { HandleUtility.AddDefaultControl(id); return; }

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        bool over = Physics.Raycast(ray, out var hit, 1e6f) && hit.transform == terrain.transform;

        if (over)   // bold, high-contrast brush so it's obvious on any ground colour
        {
            var col = erase ? Color.red : def.zones[paintZone].color;
            var fill = col; fill.a = 0.25f;
            Handles.color = fill;        Handles.DrawSolidDisc(hit.point, hit.normal, brushRadius);
            Handles.color = Color.white; Handles.DrawWireDisc(hit.point, hit.normal, brushRadius, 3f);
            Handles.color = col;         Handles.DrawWireDisc(hit.point, hit.normal, brushRadius * 0.96f, 1.5f);
        }

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseDown:
                if (e.button == 0 && !e.alt && over)
                { GUIUtility.hotControl = id; strokePainted = strokeRejected = 0; Stamp(terrain, hit.point); e.Use(); }
                break;
            case EventType.MouseDrag:
                if (GUIUtility.hotControl == id && over) { Stamp(terrain, hit.point); e.Use(); }
                break;
            case EventType.MouseUp:
                if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; Persist(); e.Use(); }
                break;
        }
        sv.Repaint();
    }

    void Stamp(Terrain terrain, Vector3 wp)
    {
        var tpos = terrain.transform.position; var size = terrain.terrainData.size;
        int res = def.zoneMap.width;
        float u = (wp.x - tpos.x) / size.x, v = (wp.z - tpos.z) / size.z;
        if (u < 0 || u > 1 || v < 0 || v > 1) return;
        int cx = Mathf.RoundToInt(u * (res - 1)), cy = Mathf.RoundToInt(v * (res - 1));
        int rad = Mathf.Max(1, Mathf.CeilToInt(brushRadius / size.x * res));
        byte val = erase ? (byte)0 : (byte)(paintZone + 1);
        float seaY = SeaY();

        if (cache == null || cacheRes != res) { cache = def.zoneMap.GetPixels32(); cacheRes = res; }
        for (int dy = -rad; dy <= rad; dy++)
        for (int dx = -rad; dx <= rad; dx++)
        {
            if (dx * dx + dy * dy > rad * rad) continue;
            int X = cx + dx, Y = cy + dy;
            if (X < 0 || X >= res || Y < 0 || Y >= res) continue;
            if (!erase)   // only paint actual LAND — never the ocean
            {
                float wx = tpos.x + (X + 0.5f) / res * size.x;
                float wz = tpos.z + (Y + 0.5f) / res * size.z;
                if (terrain.SampleHeight(new Vector3(wx, 0, wz)) + tpos.y < seaY) { strokeRejected++; continue; }
            }
            cache[Y * res + X] = new Color32(val, 0, 0, 255);
            strokePainted++;
        }
        def.zoneMap.SetPixels32(cache); def.zoneMap.Apply();
        overlayDirty = true;
    }

    static float SeaY()
    {
        var w = GameObject.Find("Water");
        return w != null ? w.transform.position.y : 30f;   // matches the island's sea level
    }

    void Persist()
    {
        if (def.zoneMap == null) return;
        EditorUtility.SetDirty(def.zoneMap);
        AssetDatabase.SaveAssets();
        if (strokePainted + strokeRejected > 0)
        {
            Debug.Log($"[Island] stroke: painted {strokePainted} cells, rejected {strokeRejected} as ocean (sea Y={SeaY():0.#}). " +
                      (strokePainted == 0 ? "NOTHING painted — the land gate thinks this is below sea level." : ""));
            strokePainted = strokeRejected = 0;
        }
    }

    static Material OverlayMat
    {
        get
        {
            if (_ovMat == null)
                _ovMat = new Material(Shader.Find("Hidden/WildReach/ZoneOverlay")) { hideFlags = HideFlags.HideAndDontSave };
            return _ovMat;
        }
    }

    void DrawOverlay(Terrain terrain)
    {
        int res = def.zoneMap.width;
        if (cache == null || cacheRes != res) { cache = def.zoneMap.GetPixels32(); cacheRes = res; overlayDirty = true; }
        EnsureOverlayMesh(terrain, res);
        EnsureZoneTex(res);
        if (overlayMesh == null || zoneColTex == null) return;
        var m = OverlayMat; m.mainTexture = zoneColTex; m.SetPass(0);
        Graphics.DrawMeshNow(overlayMesh, Matrix4x4.identity);
    }

    // coarse mesh that just hugs the terrain, UV-mapped to the zone map. Rebuilt only when island/res changes.
    void EnsureOverlayMesh(Terrain terrain, int res)
    {
        int step = Mathf.Max(1, res / 64);
        if (gridRes == res && gridStep == step && overlayMesh != null) return;
        var tpos = terrain.transform.position; var size = terrain.terrainData.size;

        var xs = new System.Collections.Generic.List<int>();
        for (int x = 0; x < res; x += step) xs.Add(x);
        if (xs[xs.Count - 1] != res - 1) xs.Add(res - 1);
        var ys = new System.Collections.Generic.List<int>();
        for (int y = 0; y < res; y += step) ys.Add(y);
        if (ys[ys.Count - 1] != res - 1) ys.Add(res - 1);

        int gw = xs.Count, gh = ys.Count;
        var verts = new Vector3[gw * gh];
        var uvs = new Vector2[gw * gh];
        for (int j = 0; j < gh; j++)
        for (int i = 0; i < gw; i++)
        {
            float u = (xs[i] + 0.5f) / res, v = (ys[j] + 0.5f) / res;
            float wx = tpos.x + u * size.x, wz = tpos.z + v * size.z;
            verts[j * gw + i] = new Vector3(wx, terrain.SampleHeight(new Vector3(wx, 0, wz)) + tpos.y + 0.15f, wz);
            uvs[j * gw + i] = new Vector2(u, v);
        }
        var tris = new System.Collections.Generic.List<int>((gw - 1) * (gh - 1) * 6);
        for (int j = 0; j < gh - 1; j++)
        for (int i = 0; i < gw - 1; i++)
        {
            int a = j * gw + i, b = a + 1, c = a + gw, d = c + 1;
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(b); tris.Add(c); tris.Add(d);
        }
        if (overlayMesh == null) overlayMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
        overlayMesh.Clear();
        overlayMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        overlayMesh.vertices = verts; overlayMesh.uv = uvs; overlayMesh.triangles = tris.ToArray();
        overlayMesh.RecalculateBounds();
        gridRes = res; gridStep = step;
    }

    // full-res zone-colour texture (bilinear) — rebuilt only when the paint actually changes
    void EnsureZoneTex(int res)
    {
        if (zoneColTex == null || zoneColTex.width != res)
        {
            zoneColTex = new Texture2D(res, res, TextureFormat.RGBA32, false, true)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            overlayDirty = true;
        }
        if (!overlayDirty) return;
        var dst = new Color32[res * res];
        for (int i = 0; i < dst.Length; i++)
        {
            int zi = cache[i].r - 1;
            if (zi < 0 || zi >= def.zones.Count || !def.zones[zi].showOverlay) { dst[i] = new Color32(0, 0, 0, 0); continue; }
            var c = Color.Lerp(def.zones[zi].color, Color.white, 0.15f); c.a = 0.55f;
            dst[i] = c;
        }
        zoneColTex.SetPixels32(dst); zoneColTex.Apply();
        overlayDirty = false;
    }

    static void Header(string t)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(t, EditorStyles.boldLabel);
        EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color(1, 1, 1, 0.15f));
    }
}
