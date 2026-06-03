using UnityEngine;
using UnityEditor;

// Toggles Island1_Terrain between its painted textures and a topographic GRID material for shaping.
// The grid (WildReach/SculptGrid) is unlit world-space contours, so the form reads regardless of
// scene lighting. Textures + paint are untouched underneath — toggling back restores them.
public static class SculptView
{
    const string MatPath = "Assets/Materials/SculptGrid.mat";

    [MenuItem("WildReach/Toggle Sculpt View")]
    static void Toggle()
    {
        var go = GameObject.Find("Island1_Terrain");
        if (go == null) { Debug.LogError("[Sculpt] No 'Island1_Terrain' in the scene."); return; }
        var terrain = go.GetComponent<Terrain>();
        if (terrain == null) { Debug.LogError("[Sculpt] Island1_Terrain has no Terrain component."); return; }

        var grid = GetGridMaterial();
        if (grid == null) return;

        if (terrain.materialTemplate == grid)
        {
            terrain.materialTemplate = null;   // back to the default TerrainLit -> painted textures return
            Debug.Log("[Sculpt] Sculpt view OFF — textures restored.");
        }
        else
        {
            terrain.materialTemplate = grid;   // topographic grid for shaping
            Debug.Log("[Sculpt] Sculpt view ON — topographic grid (contours every 8 m). " +
                      "Shape with the Terrain 'Raise or Lower Terrain' tool. Toggle again to see textures.");
        }
        EditorUtility.SetDirty(terrain);
    }

    static Material GetGridMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat != null) return mat;

        var shader = Shader.Find("WildReach/SculptGrid");
        if (shader == null) { Debug.LogError("[Sculpt] Shader 'WildReach/SculptGrid' not found — let it compile, then retry."); return null; }
        mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, MatPath);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
