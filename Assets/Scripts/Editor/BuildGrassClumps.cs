using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

// Procedural 3D grass-blade clumps — NO textures. Each clump is a handful of curved, tapered ribbon
// meshes (real geometry, so it reads as blades from any angle), coloured by a dark-base-to-bright-tip
// vertex gradient via the WildReach/GrassBlade shader. Output: GrassClump_1..3.prefab, which the
// Island Builder's grass fill already scatters. 'Test Grass Patch' drops a patch in front of the
// camera as real GameObjects so the look can be judged directly. Re-runnable / deterministic.
public static class BuildGrassClumps
{
    const string OutDir   = "Assets/Environment/Grass";
    const int    Variants = 3;
    const int    Seg      = 4;     // height segments per blade (more = smoother arc)

    static readonly Color BaseCol = new Color(0.10f, 0.22f, 0.06f);  // deep shaded base (authored sRGB)
    static readonly Color TipCol  = new Color(0.34f, 0.52f, 0.15f);  // rich grass-green tip

    [MenuItem("WildReach/Build Grass Clumps")]
    static void Build()
    {
        Directory.CreateDirectory(OutDir);
        var mat = BuildMaterial();
        if (mat.shader == null) return;

        for (int v = 0; v < Variants; v++)
        {
            var mesh = BuildClumpMesh(1000 + v);
            string meshPath = $"{OutDir}/GrassClump_{v + 1}.mesh";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            AssetDatabase.ImportAsset(meshPath);                                  // finalise the import...
            var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) ?? mesh; // ...then reference the solid asset

            var go = new GameObject($"GrassClump_{v + 1}");
            go.AddComponent<MeshFilter>().sharedMesh = savedMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            PrefabUtility.SaveAsPrefabAsset(go, $"{OutDir}/GrassClump_{v + 1}.prefab");
            Object.DestroyImmediate(go);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[Grass] Built {Variants} procedural 3D blade clumps. Run 'Test Grass Patch' to judge them.");
    }

    static Material BuildMaterial()
    {
        var shader = Shader.Find("WildReach/GrassBlade");
        if (shader == null) { Debug.LogError("[Grass] Shader 'WildReach/GrassBlade' not imported yet — click into Unity to let it compile, then re-run."); return new Material(Shader.Find("Unlit/Color")); }
        string path = OutDir + "/GrassBlade.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        else mat.shader = shader;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Mesh BuildClumpMesh(int seed)
    {
        var rng = new System.Random(seed);
        var verts = new List<Vector3>(); var cols = new List<Color>();
        var norms = new List<Vector3>(); var tris = new List<int>();

        int blades = 22 + rng.Next(0, 10);   // 22-31 blades — fuller tuft
        for (int b = 0; b < blades; b++)
        {
            float a    = (float)(rng.NextDouble() * Mathf.PI * 2);
            float dist = (float)rng.NextDouble() * 0.20f;                       // wider spread from clump centre
            Vector3 root = new Vector3(Mathf.Cos(a) * dist, 0f, Mathf.Sin(a) * dist);

            float bladeYaw = (float)(rng.NextDouble() * Mathf.PI * 2);
            Vector3 face    = new Vector3(Mathf.Cos(bladeYaw), 0f, Mathf.Sin(bladeYaw));   // width axis
            Vector3 bendDir = new Vector3(-Mathf.Sin(bladeYaw), 0f, Mathf.Cos(bladeYaw));  // lean axis
            float height = 0.32f + (float)rng.NextDouble() * 0.30f;
            float bend   = 0.10f + (float)rng.NextDouble() * 0.24f;
            float baseW  = 0.020f + (float)rng.NextDouble() * 0.012f;
            float bright = 0.80f + (float)rng.NextDouble() * 0.30f;             // per-blade tip variation
            Color tip = new Color(TipCol.r * bright, TipCol.g * bright, TipCol.b * bright, 1f);

            AddBlade(verts, cols, norms, tris, root, face, bendDir, height, bend, baseW, tip);
        }

        var m = new Mesh { name = "GrassClump" };
        m.SetVertices(verts); m.SetColors(cols); m.SetNormals(norms); m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    static void AddBlade(List<Vector3> verts, List<Color> cols, List<Vector3> norms, List<int> tris,
                         Vector3 root, Vector3 face, Vector3 bendDir, float height, float bend, float baseW, Color tip)
    {
        int start = verts.Count;
        Vector3 normal = Vector3.Cross(face, Vector3.up).normalized;
        for (int s = 0; s <= Seg; s++)
        {
            float t = s / (float)Seg;                         // 0 base .. 1 tip
            float w = baseW * (1f - t);                       // taper to a point
            Vector3 spine = root + Vector3.up * (height * t) + bendDir * (bend * t * t * height); // quadratic arc
            Color c = Color.Lerp(BaseCol, tip, t).linear;   // author sRGB, store linear so it isn't washed out
            verts.Add(spine - face * w); cols.Add(c); norms.Add(normal);
            verts.Add(spine + face * w); cols.Add(c); norms.Add(normal);
        }
        for (int s = 0; s < Seg; s++)
        {
            int a = start + s * 2;
            tris.Add(a); tris.Add(a + 2); tris.Add(a + 1);
            tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 3);
        }
    }

    [MenuItem("WildReach/Test Grass Patch")]
    static void TestPatch()
    {
        var prefabs = new List<GameObject>();
        for (int v = 1; v <= Variants; v++)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{OutDir}/GrassClump_{v}.prefab");
            if (p != null) prefabs.Add(p);
        }
        if (prefabs.Count == 0) { Debug.LogError("[Grass] No clumps — run Build Grass Clumps first."); return; }

        var old = GameObject.Find("GrassTestPatch"); if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("GrassTestPatch");

        var sv = SceneView.lastActiveSceneView;
        Vector3 center = sv != null ? sv.camera.transform.position + sv.camera.transform.forward * 10f : Vector3.zero;
        var terrain = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();

        Random.InitState(7);
        int n = 0;
        for (float x = -2.5f; x <= 2.5f; x += 0.22f)
        for (float z = -2.5f; z <= 2.5f; z += 0.22f)
        {
            Vector3 pos = new Vector3(center.x + x + Random.Range(-0.08f, 0.08f), 0f, center.z + z + Random.Range(-0.08f, 0.08f));
            pos.y = terrain != null ? terrain.SampleHeight(pos) + terrain.transform.position.y : center.y;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[Random.Range(0, prefabs.Count)]);
            inst.transform.SetParent(root.transform, true);
            inst.transform.position = pos;
            inst.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            inst.transform.localScale = Vector3.one * Random.Range(0.8f, 1.3f);
            n++;
        }
        Selection.activeGameObject = root;
        sv?.FrameSelected();
        Debug.Log($"[Grass] Test patch: {n} real 3D clumps in front of the camera. Judge the blades; delete 'GrassTestPatch' when done.");
    }
}
