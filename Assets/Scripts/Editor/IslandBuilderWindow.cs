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
    }

    void SetDef(IslandDefinition d)
    {
        def = d;
        so = d != null ? new SerializedObject(d) : null;
        cache = null;
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
        if (paintZone >= 0)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"PAINTING: {def.zones[paintZone].name}" + (erase ? "  (erasing)" : ""), EditorStyles.boldLabel);
            brushRadius = EditorGUILayout.Slider("Brush radius (m)", brushRadius, 1f, 40f);
            erase = EditorGUILayout.ToggleLeft("Erase", erase);
            if (GUILayout.Button("Stop painting")) paintZone = -1;
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
                z.name = EditorGUILayout.TextField(z.name);
                bool on = paintZone == i;
                bool now = GUILayout.Toggle(on, on ? "Painting" : "Paint", "Button", GUILayout.Width(70));
                if (now != on) { paintZone = now ? i : -1; erase = false; }
                if (GUILayout.Button("✕", GUILayout.Width(24))) { Undo.RecordObject(def, "remove zone"); def.zones.RemoveAt(i); EditorUtility.SetDirty(def); break; }
            }
            z.palette = (ScatterPalette)EditorGUILayout.ObjectField("Palette", z.palette, typeof(ScatterPalette), false);
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
        if (GUI.changed) EditorUtility.SetDirty(def);
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
        if (GUILayout.Button("Build Island (both)", GUILayout.Height(24)))
        { IslandBuilder.BuildVegetation(def); IslandBuilder.BuildGrass(def); }
        EditorGUILayout.HelpBox("Vegetation is deterministic (seed). Re-run any time; it wipes its own output first.", MessageType.None);
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

        if (showOverlay && def.zoneMap != null) DrawOverlay(terrain);

        if (paintZone < 0 || paintZone >= def.zones.Count || def.zoneMap == null) return;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        var e = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (Physics.Raycast(ray, out var hit, 1e6f) && hit.transform == terrain.transform)
        {
            Handles.color = erase ? Color.red : def.zones[paintZone].color;
            Handles.DrawWireDisc(hit.point, Vector3.up, brushRadius);
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && !e.alt)
            { Stamp(terrain, hit.point); e.Use(); }
            if (e.type == EventType.MouseUp && e.button == 0) Persist();
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

        if (cache == null || cacheRes != res) { cache = def.zoneMap.GetPixels32(); cacheRes = res; }
        for (int dy = -rad; dy <= rad; dy++)
        for (int dx = -rad; dx <= rad; dx++)
        {
            if (dx * dx + dy * dy > rad * rad) continue;
            int X = cx + dx, Y = cy + dy;
            if (X < 0 || X >= res || Y < 0 || Y >= res) continue;
            cache[Y * res + X] = new Color32(val, 0, 0, 255);
        }
        def.zoneMap.SetPixels32(cache); def.zoneMap.Apply();
    }

    void Persist()
    {
        if (def.zoneMap == null) return;
        EditorUtility.SetDirty(def.zoneMap);
        AssetDatabase.SaveAssets();
    }

    void DrawOverlay(Terrain terrain)
    {
        int res = def.zoneMap.width;
        if (cache == null || cacheRes != res) { cache = def.zoneMap.GetPixels32(); cacheRes = res; }
        var tpos = terrain.transform.position; var size = terrain.terrainData.size;
        int step = Mathf.Max(1, res / 56);              // ~56x56 samples
        float disc = size.x / res * step * 0.6f;
        for (int y = 0; y < res; y += step)
        for (int x = 0; x < res; x += step)
        {
            int zi = cache[y * res + x].r - 1;
            if (zi < 0 || zi >= def.zones.Count) continue;
            float wx = tpos.x + (x + 0.5f) / res * size.x;
            float wz = tpos.z + (y + 0.5f) / res * size.z;
            float wy = terrain.SampleHeight(new Vector3(wx, 0, wz)) + tpos.y + 0.15f;
            var c = def.zones[zi].color; c.a = 0.4f; Handles.color = c;
            Handles.DrawSolidDisc(new Vector3(wx, wy, wz), Vector3.up, disc);
        }
    }

    static void Header(string t)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(t, EditorStyles.boldLabel);
        EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color(1, 1, 1, 0.15f));
    }
}
