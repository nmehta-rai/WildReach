using UnityEngine;
using UnityEditor;

// Registers the stylized ground textures as paintable TerrainLayers.
// Layer 0 (light) coats the whole terrain; layer 1 (dark) is yours to paint.
// Flat cel albedo, no normal maps on purpose (the art is pre-shaded).
public static class GroundSetup
{
    // name, texture path, tile size (metres per repeat)
    static readonly (string layer, string tex, float tile)[] Layers =
    {
        ("Ground_Light", "Assets/Materials/Textures/Ground_Light.png", 10f),
        ("Ground_Dark",  "Assets/Materials/Textures/Ground_Dark.png",  10f),
        ("Ground_Rock",  "Assets/Materials/Textures/Ground_Rock.png",  10f),
    };

    [MenuItem("WildReach/Setup Ground")]
    static void Setup()
    {
        var built = new System.Collections.Generic.List<TerrainLayer>();
        foreach (var (name, texPath, tile) in Layers)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) { Debug.LogError($"[Ground] Missing texture: {texPath}"); return; }

            string layerPath = $"Assets/Materials/{name}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }
            layer.diffuseTexture = tex;
            layer.normalMapTexture = null;        // flat cel — no fake relief
            layer.tileSize = new Vector2(tile, tile);
            EditorUtility.SetDirty(layer);
            built.Add(layer);
        }

        var terrain = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
        if (terrain == null || terrain.terrainData == null)
        { Debug.LogError("[Ground] No terrain found. Run Reset Flat Terrain first."); return; }

        terrain.terrainData.terrainLayers = built.ToArray();
        EditorUtility.SetDirty(terrain.terrainData);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Ground] {built.Count} ground layers set on '{terrain.name}'. " +
                  "Light = base; paint Dark via Terrain > Paint Texture.");
    }
}
