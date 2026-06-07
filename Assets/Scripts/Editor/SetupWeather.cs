using System.IO;
using UnityEngine;
using UnityEditor;

// Atmosphere setup: a night star dome, and the global wind that sways the grass.
public static class SetupWeather
{
    const string SkyDir   = "Assets/Environment/Sky";
    const string StarsTex = SkyDir + "/Stars.png";
    const string StarsMat = SkyDir + "/Stars.mat";

    [MenuItem("WildReach/World/Setup Stars (night sky)")]
    static void SetupStars()
    {
        if (!AssetDatabase.IsValidFolder(SkyDir)) AssetDatabase.CreateFolder("Assets/Environment", "Sky");
        var tex = GenerateStarTexture();

        var mat = AssetDatabase.LoadAssetAtPath<Material>(StarsMat) ?? new Material(Shader.Find("WildReach/Stars"));
        if (!AssetDatabase.Contains(mat)) AssetDatabase.CreateAsset(mat, StarsMat);
        mat.shader = Shader.Find("WildReach/Stars");
        mat.SetTexture("_BaseMap", tex);
        mat.SetFloat("_Intensity", 2.5f);
        EditorUtility.SetDirty(mat);

        var dome = GameObject.Find("StarDome");
        if (dome == null)
        {
            dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "StarDome";
        }
        var col = dome.GetComponent<Collider>(); if (col != null) Object.DestroyImmediate(col);
        dome.transform.localScale = Vector3.one * 1000f;     // radius ~500m; follows the camera at runtime
        dome.GetComponent<MeshRenderer>().sharedMaterial = mat;
        dome.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (dome.GetComponent<Stars>() == null) dome.AddComponent<Stars>();

        Selection.activeGameObject = dome;
        AssetDatabase.SaveAssets();
        Debug.Log("[Weather] Star dome built. They fade in at night (driven by the sun) — scrub Time Of Day to dusk to see them. " +
                  "Tune brightness on the Stars material (_Intensity) and spin/fade on the Stars component.");
    }

    [MenuItem("WildReach/World/Setup Wind (grass sway)")]
    static void SetupWind()
    {
        var go = GameObject.Find("Wind") ?? new GameObject("Wind");
        if (go.GetComponent<WindManager>() == null) go.AddComponent<WindManager>();
        Selection.activeGameObject = go;
        Debug.Log("[Weather] Wind added — the grass now sways (tune direction/strength/gustiness on the WindManager). " +
                  "Any shader reading the global _WindParams responds to it.");
    }

    // a 2:1 equirect star field: mostly faint pinpricks, a few bright ones, slight colour variation, soft 1px glow
    static Texture2D GenerateStarTexture()
    {
        const int W = 2048, H = 1024;
        var px = new Color[W * H];
        for (int i = 0; i < px.Length; i++) px[i] = Color.black;

        Random.InitState(20260607);
        int count = 1900;
        for (int s = 0; s < count; s++)
        {
            int x = Random.Range(0, W), y = Random.Range(0, H);
            float b = Mathf.Pow(Random.value, 3.5f);                 // most dim, a few bright
            float bright = 0.25f + b * 1.4f;
            // subtle colour: mostly white, a few warm/cool
            var tint = Random.value < 0.2f ? new Color(0.8f, 0.85f, 1f)
                     : Random.value < 0.1f ? new Color(1f, 0.92f, 0.8f)
                     : Color.white;
            var c = tint * bright;
            Add(px, W, H, x, y, c);
            if (b > 0.6f)   // soft glow for the bright ones
            {
                var g = c * 0.35f;
                Add(px, W, H, x + 1, y, g); Add(px, W, H, x - 1, y, g);
                Add(px, W, H, x, y + 1, g); Add(px, W, H, x, y - 1, g);
            }
        }

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(StarsTex, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(StarsTex, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(StarsTex) is TextureImporter ti)
        { ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(StarsTex);
    }

    static void Add(Color[] px, int w, int h, int x, int y, Color c)
    {
        if (x < 0 || x >= w || y < 0 || y >= h) return;
        px[y * w + x] += c;
    }
}
