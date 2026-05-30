using System.Linq;
using UnityEngine;
using UnityEditor;

// Wires the Enchanted Moss Log prop: stylized material (base + normal), a mesh collider so
// the player can walk/stand on it, and a height check. Drag MossLog into the scene after.
public static class SetupMossLog
{
    const string Dir  = "Assets/Environment/MossLog/";
    const string Fbx  = Dir + "MossLog.fbx";
    const string Base = Dir + "MossLog_Base.png";
    const string Norm = Dir + "MossLog_Normal.png";
    const string Emit = Dir + "MossLog_EmitMask.png";   // only the orange shelf fungi
    const string Mat  = Dir + "MossLog.mat";

    [MenuItem("WildReach/Setup Moss Log")]
    static void Setup()
    {
        var imp = AssetImporter.GetAtPath(Fbx) as ModelImporter;
        if (imp == null) { Debug.LogError($"[Log] Missing {Fbx}"); return; }
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        imp.addCollider = true;                 // walkable mesh collider
        imp.SaveAndReimport();

        AsNormalMap(Norm);
        var mat = BuildMat();

        foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Material>())
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(sub), mat);
        imp.SaveAndReimport();

        LogHeight();
        AssetDatabase.SaveAssets();
        Debug.Log("[Log] Moss Log wired (material + walkable collider). Drag MossLog into the scene; " +
                  "it already has a collider so the player can stand on it.");
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
        mat.SetFloat("_Smoothness", 0.15f);     // mossy, matte

        // Faint warm glow on only the orange shelf fungi (mask isolates them; moss/ferns stay dark).
        var e = AssetDatabase.LoadAssetAtPath<Texture2D>(Emit);
        if (e)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", e);
            mat.SetColor("_EmissionColor", Color.white * 1.0f);   // faint; reads at night
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
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
            Debug.Log($"[Log] MossLog size ~ {bnd.size.x:0.0} x {bnd.size.y:0.0} x {bnd.size.z:0.0} m (target height ~1.2).");
        }
        Object.DestroyImmediate(inst);
    }
}
