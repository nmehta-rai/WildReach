using UnityEngine;
using UnityEditor;
using System.IO;

// Builds true 3D grass-clump prefabs from the flat GrassTuft cutouts: each clump is 3 crossed
// vertical quads (so it has volume from every angle, unlike a camera-facing billboard), with
// up-facing normals so it lights like soft stylized foliage instead of going dark on the sides.
// One prefab per tuft texture -> Assets/Environment/Grass/GrassClump_N.prefab. FillForestGrass
// scatters THESE across the terrain as waving mesh details. Re-runnable.
public static class BuildGrassClumps
{
    static readonly string[] Tufts =
    {
        "Assets/Materials/Textures/Grass/GrassTuft_1.png",
        "Assets/Materials/Textures/Grass/GrassTuft_2.png",
        "Assets/Materials/Textures/Grass/GrassTuft_3.png",
    };
    const string OutDir   = "Assets/Environment/Grass";
    const string MeshPath = OutDir + "/GrassClump.mesh";
    const float  HalfW    = 0.45f;  // clump half-width (metres)
    const float  Height   = 0.6f;   // clump height (metres)

    [MenuItem("WildReach/Build Grass Clumps")]
    static void Build()
    {
        Directory.CreateDirectory(OutDir);

        // --- shared crossed-quad mesh (pivot at the base, up normals) ---
        var mesh = BuildClumpMesh();
        AssetDatabase.DeleteAsset(MeshPath);
        AssetDatabase.CreateAsset(mesh, MeshPath);

        int made = 0;
        for (int i = 0; i < Tufts.Length; i++)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Tufts[i]);
            if (tex == null) { Debug.LogWarning("[GrassClump] Missing " + Tufts[i]); continue; }

            var mat = BuildMaterial(tex, i);
            var go  = new GameObject("GrassClump_" + (i + 1));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // cheap; grass doesn't self-shadow
            mr.receiveShadows = true;

            string prefab = OutDir + "/GrassClump_" + (i + 1) + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefab);
            Object.DestroyImmediate(go);
            made++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[GrassClump] Built {made} grass-clump prefab(s) in {OutDir}. Now run 'Fill Forest Grass (3D)'.");
    }

    static Mesh BuildClumpMesh()
    {
        var verts = new System.Collections.Generic.List<Vector3>();
        var uvs   = new System.Collections.Generic.List<Vector2>();
        var norms = new System.Collections.Generic.List<Vector3>();
        var tris  = new System.Collections.Generic.List<int>();

        // 3 quads fanned around Y so the clump has thickness from any view
        for (int q = 0; q < 3; q++)
        {
            float a = q * 60f * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * HalfW;
            int b = verts.Count;
            verts.Add(-d);                       uvs.Add(new Vector2(0, 0));
            verts.Add(+d);                       uvs.Add(new Vector2(1, 0));
            verts.Add(+d + Vector3.up * Height); uvs.Add(new Vector2(1, 1));
            verts.Add(-d + Vector3.up * Height); uvs.Add(new Vector2(0, 1));
            for (int k = 0; k < 4; k++) norms.Add(Vector3.up);   // soft top-lit shading
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
            tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
        }

        var m = new Mesh { name = "GrassClump" };
        m.SetVertices(verts); m.SetUVs(0, uvs); m.SetNormals(norms); m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    static Material BuildMaterial(Texture2D tex, int i)
    {
        string path = OutDir + "/GrassClump_" + (i + 1) + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path)
                  ?? new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (AssetDatabase.GetAssetPath(mat) == "") AssetDatabase.CreateAsset(mat, path);

        mat.SetTexture("_BaseMap", tex);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0f);
        // alpha-clip cutout so the blade silhouette shows, double-sided so back faces render
        mat.SetFloat("_Surface", 0f);            // opaque pipeline...
        mat.SetFloat("_AlphaClip", 1f);          // ...with alpha test
        mat.SetFloat("_Cutoff", 0.4f);
        mat.SetFloat("_Cull", 0f);               // double-sided
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        mat.doubleSidedGI = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
