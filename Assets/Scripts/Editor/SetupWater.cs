using UnityEngine;
using UnityEditor;

// Builds a subdivided water plane (so the StylizedWater vertex waves show) + its material,
// and drops a "Water" object in the scene. Move it into the valley and lower its Y until it
// fills the dip. Re-runnable (reuses the mesh/material assets).
public static class SetupWater
{
    const string Dir      = "Assets/Environment/Water/";
    const string MeshPath = Dir + "WaterPlane.mesh";
    const string MatPath  = Dir + "Water.mat";
    const float  Size = 8000f; // metres across — a world-spanning ocean; islands drop into this one sheet
    const int    Res  = 500;   // grid subdivisions (501x501 verts) → ~16m quads so waves still sample near shore

    // Sea level (world Y). The ocean is the global sea plane at 0; islands are positioned so their
    // coastline crosses Y=0. Other tools (Setup Ship) read this so the ship floats at the same surface.
    public const float SeaLevel = 0f;

    // Centre of the island in X/Z, so the ocean (and the ship) sit on the slice rather than off in the void.
    public static Vector3 IslandCenter()
    {
        var t = Object.FindAnyObjectByType<Terrain>();
        if (t != null)
        {
            var s = t.terrainData.size;
            var p = t.transform.position;
            return new Vector3(p.x + s.x * 0.5f, 0f, p.z + s.z * 0.5f);
        }
        return Vector3.zero;
    }

    [MenuItem("WildReach/Setup Water")]
    static void Setup()
    {
        System.IO.Directory.CreateDirectory(Dir);

        // --- subdivided plane mesh ---
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (mesh == null) { mesh = new Mesh { name = "WaterPlane" }; AssetDatabase.CreateAsset(mesh, MeshPath); }
        mesh.Clear();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;  // >65k verts need 32-bit indices or the mesh truncates to a corner
        int n = Res + 1;
        var verts = new Vector3[n * n];
        var uvs   = new Vector2[n * n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                float fx = (float)x / Res, fz = (float)z / Res;
                verts[z * n + x] = new Vector3((fx - 0.5f) * Size, 0f, (fz - 0.5f) * Size);
                uvs[z * n + x]   = new Vector2(fx, fz);
            }
        var tris = new int[Res * Res * 6];
        int ti = 0;
        for (int z = 0; z < Res; z++)
            for (int x = 0; x < Res; x++)
            {
                int i = z * n + x;
                tris[ti++] = i;     tris[ti++] = i + n; tris[ti++] = i + 1;
                tris[ti++] = i + 1; tris[ti++] = i + n; tris[ti++] = i + n + 1;
            }
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);

        // --- material ---
        var shader = Shader.Find("WildReach/StylizedWater");
        if (shader == null) { Debug.LogError("[Water] Shader 'WildReach/StylizedWater' not found — check it compiled."); return; }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, MatPath); }
        else mat.shader = shader;
        EditorUtility.SetDirty(mat);

        // --- scene object ---
        var go = GameObject.Find("Water");
        if (go == null) go = new GameObject("Water");
        // NOTE: use explicit Unity == null checks, NOT ?? (?? bypasses Unity's null overload).
        var mf = go.GetComponent<MeshFilter>();
        if (mf == null) mf = go.AddComponent<MeshFilter>();
        var mr = go.GetComponent<MeshRenderer>();
        if (mr == null) mr = go.AddComponent<MeshRenderer>();
        mf.sharedMesh = mesh;
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // The sea is global and follows the camera, so it reads as infinite. Start at origin; WaterFollow snaps it.
        go.transform.position = new Vector3(0f, SeaLevel, 0f);
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var follow = go.GetComponent<WaterFollow>();
        if (follow == null) follow = go.AddComponent<WaterFollow>();
        follow.seaLevel = SeaLevel;

        AssetDatabase.SaveAssets();
        Selection.activeGameObject = go;
        Debug.Log($"[Water] Infinite ocean ready: {Size}m plane (mesh bounds {mesh.bounds.size.x:0}m), " +
                  $"32-bit indices, follows the camera at sea level Y={SeaLevel}. " +
                  $"Position islands anywhere so their coastline crosses Y=0. PINK = shader didn't compile.");
    }
}
