using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

// Turns the bare "Ancient Sentinel" Meshy tree into dressed, climbable forest giants — and builds TWO whole-tree
// variants so the woods read naturally: GREEN trees and AUTUMN trees, mixed through the forest (not every tree a
// green/orange speckle). For each variant it:
//  - builds the bark material (base + normal) and bakes it flat onto the trunk,
//  - samples the upper/outer mesh vertices (branch tips) and scatters foliage CLUMPS there — green-leaf cards for
//    the green tree, autumn-leaf cards for the autumn tree — combined per-material so the canopy is a few draw
//    calls, not thousands of quads,
//  - adds a (non-convex) MeshCollider for a hand-placed hero tree,
//  - saves a flat prefab and REGISTERS it in the Temperate Forest scatter palette, so scattering the Forrest zone
//    plants the trees AS PART of the forest (height-normalised, windowed-instanced, auto trunk-collider).
// Leaf placement is bounds-relative, so it looks right at whatever scale the scatter normalises the tree to.
public static class SetupAncientTree
{
    const string Dir       = "Assets/Environment/Trees/AncientSentinel";
    const string Fbx     = Dir + "/Meshy_AI_Ancient_Sentinel_0606030025_texture.fbx";
    const string BaseTex = Dir + "/Meshy_AI_Ancient_Sentinel_0606030025_texture.png";
    const string NrmTex  = Dir + "/Meshy_AI_Ancient_Sentinel_0606030025_texture_normal.png";
    const string BarkMat = Dir + "/AncientSentinel_Bark.mat";
    const string ForestPalette = "Assets/World/TemperateForest.asset";

    static readonly string[] GreenMats =
    {
        "Assets/Environment/ForestTrees/Leaves/ForestLeaf_Green1_Mat.mat",
        "Assets/Environment/ForestTrees/Leaves/ForestLeaf_Green2_Mat.mat",
        "Assets/Environment/ForestTrees/Leaves/ForestLeaf_Green3_Mat.mat",
    };
    static readonly string[] AutumnMats = { "Assets/Environment/ForestTrees/Leaves/AutumnLeaf_Mat.mat" };

    const float TreeHeight   = 45f;    // ~45 m climbing giant (scatter normalises every tree to this, any FBX scale)
    const float CanopyStart  = 0.30f;  // ignore the bottom 30% of the trunk (no leaves on the base)
    const float CardSizeFrac = 0.11f;  // each foliage clump ≈ 11% of tree height
    const float SpacingFrac  = 0.045f; // min gap between clumps, as a fraction of tree size
    const int   MaxClumps    = 420;    // perf cap on foliage clumps

    [MenuItem("WildReach/World/Build Ancient Trees (green + autumn, climbable)")]
    static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Fbx) == null)
        { Debug.LogError($"[Tree] FBX not found at {Fbx}"); return; }

        // make the normal map a real normal map, and the mesh readable so we can scan its verts for branch tips
        if (AssetImporter.GetAtPath(NrmTex) is TextureImporter ni && ni.textureType != TextureImporterType.NormalMap)
        { ni.textureType = TextureImporterType.NormalMap; ni.SaveAndReimport(); }
        if (AssetImporter.GetAtPath(Fbx) is ModelImporter mi && !mi.isReadable)
        { mi.isReadable = true; mi.SaveAndReimport(); }

        var bark = BuildBark();
        BuildOne("Green",  LoadLeafMats(GreenMats),  bark);
        BuildOne("Autumn", LoadLeafMats(AutumnMats), bark);

        Debug.Log("[Tree] Built green + autumn Ancient Sentinels and added both to the Temperate Forest palette. " +
                  "Scatter the 'Forrest' zone — you'll get a natural mix of green and autumn giants you can climb.");
    }

    static void BuildOne(string season, Material[] leafMats, Material bark)
    {
        string prefabPath = $"Assets/Environment/Trees/AncientSentinel_{season}.prefab";

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        var root  = (GameObject)PrefabUtility.InstantiatePrefab(model);
        root.name = "AncientSentinel_" + season;
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = bark;
            r.sharedMaterials = mats;
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                // explicit null check: `GetComponent ?? AddComponent` is a trap — GetComponent returns Unity's
                // *fake-null* which `??` won't treat as null, so the collider never gets added and .sharedMesh throws.
                var mc = r.gameObject.GetComponent<MeshCollider>();
                if (mc == null) mc = r.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;   // non-convex bark surface for a hand-placed hero tree
            }
        }

        var pts = SampleBranchTips(root);
        var canopyMeshes = BuildCanopy(root, pts, leafMats, season);   // in-memory meshes, embedded below

        if (!AssetDatabase.IsValidFolder("Assets/Environment/Trees"))
            AssetDatabase.CreateFolder("Assets/Environment", "Trees");

        // EMBED the canopy meshes as SUB-ASSETS of the prefab. A separate .mesh asset keeps ghosting (file present,
        // loads null) when anything near it reimports; a sub-asset lives inside the .prefab file itself and cannot
        // orphan. Save once, then add each in-memory mesh into the returned prefab asset and re-point its MeshFilter
        // — editing the asset in place (NOT a second SaveAsPrefabAsset, which would drop the freshly-added sub-asset).
        var prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        int embedded = 0;
        var cn = prefabAsset.transform.Find("Canopy");
        if (cn != null)
            foreach (Transform leaf in cn)
                if (canopyMeshes.TryGetValue(leaf.name, out var mesh) && mesh != null)
                {
                    mesh.name = $"Canopy_{season}_{leaf.name}";
                    AssetDatabase.AddObjectToAsset(mesh, prefabAsset);
                    var mf = leaf.GetComponent<MeshFilter>(); if (mf != null) mf.sharedMesh = mesh;
                    embedded++;
                }
        EditorUtility.SetDirty(prefabAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(prefabPath);

        RegisterInForest(prefabAsset);
        Debug.Log($"[Tree] {season}: {pts.Count} canopy points, embedded {embedded} canopy mesh(es) in {prefabPath} (sub-assets, can't ghost).");
    }

    static Material BuildBark()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(BarkMat)
               ?? new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (!AssetDatabase.Contains(mat)) AssetDatabase.CreateAsset(mat, BarkMat);
        var bt = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseTex);
        if (bt != null) { mat.SetTexture("_BaseMap", bt); mat.SetColor("_BaseColor", Color.white); }
        var nt = AssetDatabase.LoadAssetAtPath<Texture2D>(NrmTex);
        if (nt != null) { mat.SetTexture("_BumpMap", nt); mat.EnableKeyword("_NORMALMAP"); }
        mat.SetFloat("_Smoothness", 0.12f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // load leaf cluster materials and force them to alpha-cutout + double-sided (same as the autumn clump tool)
    static Material[] LoadLeafMats(string[] paths)
    {
        var list = new List<Material>();
        foreach (var p in paths)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { Debug.LogWarning($"[Tree] leaf mat missing: {p}"); continue; }
            m.SetFloat("_AlphaClip", 1); m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cutoff", 0.4f); m.SetFloat("_Cull", 0); m.SetFloat("_Smoothness", 0.1f);
            m.renderQueue = 2450; EditorUtility.SetDirty(m);
            list.Add(m);
        }
        return list.ToArray();
    }

    // Pick branch-tip points: upper + outer mesh verts, spaced apart so the canopy is even, not blobby.
    static List<Vector3> SampleBranchTips(GameObject root)
    {
        var rootInv = root.transform.worldToLocalMatrix;
        var raw = new List<Vector3>();
        var b = new Bounds();
        bool first = true;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = mf.sharedMesh; if (mesh == null || !mesh.isReadable) continue;
            var l2l = rootInv * mf.transform.localToWorldMatrix;
            var vs = mesh.vertices;
            int stride = Mathf.Max(1, vs.Length / 20000);  // cap the scan on dense meshes
            for (int i = 0; i < vs.Length; i += stride)
            {
                var p = l2l.MultiplyPoint3x4(vs[i]);
                raw.Add(p);
                if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
            }
        }
        if (raw.Count == 0) return raw;

        float yMin = b.min.y, H = Mathf.Max(0.001f, b.size.y);
        var axis = new Vector2(b.center.x, b.center.z);
        float maxR = 0.001f;
        foreach (var p in raw) maxR = Mathf.Max(maxR, Vector2.Distance(new Vector2(p.x, p.z), axis));

        var cand = new List<Vector3>();
        foreach (var p in raw)
        {
            float hf = (p.y - yMin) / H;
            if (hf < CanopyStart) continue;
            float rf = Vector2.Distance(new Vector2(p.x, p.z), axis) / maxR;
            float keep = 0.25f + 0.55f * rf + 0.35f * Mathf.InverseLerp(CanopyStart, 1f, hf);
            float h = Mathf.Abs(Mathf.Sin(p.x * 12.9898f + p.y * 78.233f + p.z * 37.719f) * 43758.5f) % 1f;
            if (h < keep) cand.Add(p);
        }

        for (int i = cand.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (cand[i], cand[j]) = (cand[j], cand[i]); }
        float md2 = Mathf.Pow(SpacingFrac * b.size.magnitude, 2f);
        var pts = new List<Vector3>();
        foreach (var c in cand)
        {
            bool ok = true;
            foreach (var q in pts) if ((q - c).sqrMagnitude < md2) { ok = false; break; }
            if (ok) pts.Add(c);
            if (pts.Count >= MaxClumps) break;
        }
        return pts;
    }

    // Build the foliage as combined meshes (one child per leaf material) of crossed quads facing outward.
    // Returns leafChildName -> in-memory mesh; the caller embeds them into the prefab as sub-assets.
    static Dictionary<string, Mesh> BuildCanopy(GameObject root, List<Vector3> pts, Material[] leafMats, string season)
    {
        var result = new Dictionary<string, Mesh>();
        if (pts.Count == 0) { Debug.LogWarning("[Tree] no canopy points — is the mesh readable?"); return result; }
        if (leafMats.Length == 0) { Debug.LogWarning("[Tree] no leaf materials loaded."); return result; }
        var b = new Bounds(pts[0], Vector3.zero);
        foreach (var p in pts) b.Encapsulate(p);
        float card = CardSizeFrac * Mathf.Max(b.size.y, b.size.x);
        var axis = new Vector2(b.center.x, b.center.z);

        var quad = Quad();
        var byMat = new Dictionary<int, List<CombineInstance>>();
        foreach (var p in pts)
        {
            var outward = new Vector3(p.x - axis.x, 0f, p.z - axis.y);
            if (outward.sqrMagnitude < 1e-4f) outward = Vector3.forward;
            outward.Normalize();
            var face = Quaternion.LookRotation(outward, Vector3.up)
                     * Quaternion.Euler(Random.Range(-22f, 22f), 0f, Random.Range(-18f, 18f));
            float s = card * Random.Range(0.75f, 1.3f);

            int mi = Random.Range(0, leafMats.Length);   // vary across this season's leaf textures (green1/2/3)
            if (!byMat.TryGetValue(mi, out var list)) { list = new List<CombineInstance>(); byMat[mi] = list; }

            var lift = Vector3.up * s * 0.25f;   // sit the clump so the branch enters its lower third
            list.Add(new CombineInstance { mesh = quad, transform = Matrix4x4.TRS(p + lift, face, Vector3.one * s) });
            list.Add(new CombineInstance { mesh = quad, transform = Matrix4x4.TRS(p + lift, face * Quaternion.Euler(0, 90, 0), Vector3.one * s) });
        }

        var canopy = new GameObject("Canopy");
        canopy.transform.SetParent(root.transform, false);
        foreach (var kv in byMat)
        {
            var combined = new Mesh { name = $"CanopyLeaves_{season}_{kv.Key}" };
            combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combined.CombineMeshes(kv.Value.ToArray(), true, true);
            var go = new GameObject("Leaves_" + kv.Key);
            go.transform.SetParent(canopy.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = combined;          // in-memory now; embedded into the prefab later
            go.AddComponent<MeshRenderer>().sharedMaterial = leafMats[kv.Key];
            result["Leaves_" + kv.Key] = combined;
        }
        return result;
    }

    // Register the tree in the Forrest zone's scatter palette as a sparse, height-normalised giant. Both the green
    // and autumn variants go in (each at ~half density) → the forest plants a natural blend of the two.
    static void RegisterInForest(GameObject prefab)
    {
        var palette = AssetDatabase.LoadAssetAtPath<ScatterPalette>(ForestPalette);
        if (palette == null) { Debug.LogWarning($"[Tree] No forest palette at {ForestPalette} — assign manually."); return; }
        var e = palette.entries.FirstOrDefault(x => x.prefab == prefab);
        if (e == null) { e = new ScatterEntry(); palette.entries.Add(e); }
        e.prefab          = prefab;
        e.spacing         = 48f;     // taller giants → space them out more so they don't crowd
        e.density         = 0.3f;
        e.minScale        = 0.85f;
        e.maxScale        = 1.25f;
        e.slopeLimit      = 30f;
        e.groundSit       = true;
        e.footprint       = 2.5f;    // sit on the lowest ground across a 2.5m base radius (no floating on bumps/slopes)
        e.sinkDepth       = 1.2f;    // bury the base ~1.2m so the roots go into the ground
        e.normalizeHeight = TreeHeight;   // every tree normalised to this height regardless of the FBX import scale
        e.nearProps       = false;
        EditorUtility.SetDirty(palette);
        AssetDatabase.SaveAssets();
    }

    static Mesh Quad()
    {
        var m = new Mesh { name = "leafquad" };
        m.vertices  = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
        m.uv        = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        m.normals   = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
        return m;
    }
}
