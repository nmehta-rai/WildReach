using System.Linq;
using UnityEngine;
using UnityEditor;

// Wires the Azure Spiral Tree feature prop: stylized material (base + normal) and a mesh
// collider (so the player collides with / can climb the trunk). Drag SpiralTree in after.
public static class SetupSpiralTree
{
    const string Dir  = "Assets/Environment/SpiralTree/";
    const string Fbx  = Dir + "SpiralTree.fbx";
    const string Base = Dir + "SpiralTree_Base.png";
    const string Norm = Dir + "SpiralTree_Normal.png";
    const string Mat  = Dir + "SpiralTree.mat";

    [MenuItem("WildReach/Setup Spiral Tree")]
    static void Setup()
    {
        var imp = AssetImporter.GetAtPath(Fbx) as ModelImporter;
        if (imp == null) { Debug.LogError($"[Spiral] Missing {Fbx}"); return; }
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        imp.addCollider = true;
        imp.SaveAndReimport();

        AsNormalMap(Norm);
        var mat = BuildMat();

        foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Material>())
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(sub), mat);
        imp.SaveAndReimport();

        LogHeight();
        AssetDatabase.SaveAssets();
        Debug.Log("[Spiral] Spiral Tree wired (material + collider). Drag SpiralTree into the scene. " +
                  "If the player bumps the foliage, tell me and I'll make the collider trunk-only.");
    }

    static Material BuildMat()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Mat);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, Mat); }
        var b = AssetDatabase.LoadAssetAtPath<Texture2D>(Base);
        var n = AssetDatabase.LoadAssetAtPath<Texture2D>(Norm);
        if (b) mat.SetTexture("_BaseMap", b);
        if (n) { mat.SetTexture("_BumpMap", n); mat.EnableKeyword("_NORMALMAP"); }
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.12f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void AsNormalMap(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null && imp.textureType != TextureImporterType.NormalMap)
        { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
    }

    static void LogHeight()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        if (go == null) return;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(go);
        var rends = inst.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            var bnd = rends[0].bounds;
            foreach (var r in rends) bnd.Encapsulate(r.bounds);
            Debug.Log($"[Spiral] SpiralTree size ~ {bnd.size.x:0.0} x {bnd.size.y:0.0} x {bnd.size.z:0.0} m (target height ~5).");
        }
        Object.DestroyImmediate(inst);
    }
}
