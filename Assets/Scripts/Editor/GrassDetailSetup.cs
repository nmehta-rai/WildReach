using UnityEngine;
using UnityEditor;

// Registers grass tufts + ground flowers as paintable terrain detail prototypes,
// with gentle wind. No auto-fill — you paint them with the Detail brush.
// Re-runnable; existing painted detail on unchanged indices is preserved.
public static class GrassDetailSetup
{
    // path, minHeight, maxHeight (metres)
    static readonly (string path, float minH, float maxH)[] Details =
    {
        ("Assets/Materials/Textures/Grass/GrassTuft_1.png",      0.5f, 1.0f),
        ("Assets/Materials/Textures/Grass/GrassTuft_2.png",      0.5f, 1.0f),
        ("Assets/Materials/Textures/Grass/GrassTuft_3.png",      0.5f, 1.0f),
        ("Assets/Materials/Textures/Flowers/GroundFlower_1.png", 0.7f, 1.3f),
        ("Assets/Materials/Textures/Flowers/GroundFlower_2.png", 0.7f, 1.3f),
    };

    const int DetailRes = 512;
    const int PerPatch  = 32;

    [MenuItem("WildReach/Setup Grass Details")]
    static void Setup()
    {
        var protos = new DetailPrototype[Details.Length];
        for (int i = 0; i < Details.Length; i++)
        {
            var d = Details[i];
            var imp = AssetImporter.GetAtPath(d.path) as TextureImporter;
            if (imp != null) { imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.SaveAndReimport(); }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(d.path);
            if (tex == null) { Debug.LogError($"[Grass] Missing detail: {d.path}"); return; }

            protos[i] = new DetailPrototype
            {
                usePrototypeMesh = false,
                prototypeTexture = tex,
                renderMode   = DetailRenderMode.GrassBillboard,
                healthyColor = new Color(0.95f, 1f, 0.85f),
                dryColor     = new Color(0.85f, 0.9f, 0.6f),
                minWidth  = d.minH * 0.8f, maxWidth  = d.maxH * 0.8f,
                minHeight = d.minH,        maxHeight = d.maxH,
                noiseSpread = 0.3f,
            };
        }

        var terrain = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
        if (terrain == null || terrain.terrainData == null)
        { Debug.LogError("[Grass] No terrain found. Run Reset Flat Terrain first."); return; }

        var td = terrain.terrainData;
        td.detailPrototypes = protos;   // existing painted layers 0-2 (grass) preserved; 3-4 (flowers) start empty
        td.SetDetailResolution(DetailRes, PerPatch);

        td.wavingGrassStrength = 0.15f;
        td.wavingGrassAmount   = 0.2f;
        td.wavingGrassSpeed    = 0.25f;
        td.wavingGrassTint     = new Color(0.7f, 0.8f, 0.5f);

        terrain.detailObjectDistance = 120f;
        terrain.detailObjectDensity  = 1f;

        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Grass] {protos.Length} details set (3 grass + 2 flowers). Paint flowers sparsely with the Detail brush.");
    }
}
