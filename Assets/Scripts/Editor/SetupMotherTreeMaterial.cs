using System.Linq;
using UnityEngine;
using UnityEditor;

// Builds a URP Lit bark material from the Meshy texture maps and assigns it to the
// selected tree (or one whose name contains "Hollowroot"). Also logs its real height.
public static class SetupMotherTreeMaterial
{
    const string Dir  = "Assets/Environment/MotherTree/";
    const string Base = Dir + "Meshy_AI_The_Hollowroot_Tree_0529211539_texture.png";
    const string Norm = Dir + "Meshy_AI_The_Hollowroot_Tree_0529211539_texture_normal.png";
    const string MatPath = Dir + "MotherTree_Mat.mat";

    [MenuItem("WildReach/Setup MotherTree Material")]
    static void Setup()
    {
        var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Base);
        if (baseTex == null) { Debug.LogError($"[Tree] Base colour not found at {Base}"); return; }

        // Normal map must be imported AS a normal map.
        var nrmImp = AssetImporter.GetAtPath(Norm) as TextureImporter;
        if (nrmImp != null && nrmImp.textureType != TextureImporterType.NormalMap)
        {
            nrmImp.textureType = TextureImporterType.NormalMap;
            nrmImp.SaveAndReimport();
        }
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Norm);

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        mat.SetTexture("_BaseMap", baseTex);
        if (normal != null) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.15f);   // matte bark
        EditorUtility.SetDirty(mat);

        // Target: selected object, else one named like the tree.
        var go = Selection.activeGameObject;
        if (go == null)
            go = Object.FindObjectsByType<MeshRenderer>()
                       .Select(r => r.gameObject)
                       .FirstOrDefault(g => g.name.Contains("Hollowroot"));
        if (go == null) { Debug.LogWarning("[Tree] Material built, but no tree selected/found to assign it to."); }
        else
        {
            var rends = go.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in rends)
                r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();

            // Report real height.
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                Debug.Log($"[Tree] '{go.name}' rendered HEIGHT = {b.size.y:F2}m (scale {go.transform.lossyScale}). " +
                          $"Bark material assigned to {rends.Length} renderer(s).");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
