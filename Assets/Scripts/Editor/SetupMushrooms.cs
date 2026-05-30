using System.Linq;
using UnityEngine;
using UnityEditor;

// Builds stylized URP materials for the Embercap mushroom props (base + normal + glowing
// ember caps from the emit map), remaps them onto each FBX, and logs the imported height
// so we can confirm the 2.5 m export landed (not the Meshy 50x trap). Re-runnable.
// After running: drag EmbercapCluster / EmbercapGrove into the scene and sit them on the ground.
public static class SetupMushrooms
{
    const string Dir = "Assets/Environment/Mushrooms/";

    struct M { public string fbx, baseTex, normal, emit, mat; }
    static readonly M[] Mush =
    {
        new M{ fbx=Dir+"EmbercapCluster/EmbercapCluster.fbx", baseTex=Dir+"EmbercapCluster/Cluster_Base.png",
               normal=Dir+"EmbercapCluster/Cluster_Normal.png", emit=Dir+"EmbercapCluster/Cluster_Emit.png",
               mat=Dir+"EmbercapCluster/EmbercapCluster.mat" },
        new M{ fbx=Dir+"EmbercapGrove/EmbercapGrove.fbx", baseTex=Dir+"EmbercapGrove/Grove_Base.png",
               normal=Dir+"EmbercapGrove/Grove_Normal.png", emit=Dir+"EmbercapGrove/Grove_Emit.png",
               mat=Dir+"EmbercapGrove/EmbercapGrove.mat" },
    };

    [MenuItem("WildReach/Setup Mushrooms")]
    static void Setup()
    {
        foreach (var m in Mush)
        {
            var imp = AssetImporter.GetAtPath(m.fbx) as ModelImporter;
            if (imp == null) { Debug.LogError($"[Mush] Missing {m.fbx}"); continue; }
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.SaveAndReimport();

            AsNormalMap(m.normal);
            var mat = BuildMat(m);

            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(m.fbx).OfType<Material>())
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(sub), mat);
            imp.SaveAndReimport();

            LogHeight(m.fbx);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Mush] Mushrooms wired (materials + glowing ember caps). " +
                  "Drag EmbercapCluster / EmbercapGrove into the scene and sit them on the terrain.");
    }

    static Material BuildMat(M m)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(m.mat);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, m.mat); }

        var b = AssetDatabase.LoadAssetAtPath<Texture2D>(m.baseTex);
        var n = AssetDatabase.LoadAssetAtPath<Texture2D>(m.normal);
        var e = AssetDatabase.LoadAssetAtPath<Texture2D>(m.emit);

        if (b) mat.SetTexture("_BaseMap", b);
        if (n) { mat.SetTexture("_BumpMap", n); mat.EnableKeyword("_NORMALMAP"); }
        if (e)
        {
            mat.SetTexture("_EmissionMap", e);
            mat.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.45f) * 1.3f); // warm ember glow (bloom picks it up)
            mat.EnableKeyword("_EMISSION");
        }
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.2f);   // stylized, mostly matte
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void AsNormalMap(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null && imp.textureType != TextureImporterType.NormalMap)
        { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
    }

    static void LogHeight(string fbx)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (go == null) return;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(go);
        var rends = inst.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            var bnd = rends[0].bounds;
            foreach (var r in rends) bnd.Encapsulate(r.bounds);
            Debug.Log($"[Mush] {go.name} height ~ {bnd.size.y:0.00} m (target ~2.5). " +
                      "If it reads ~250, the export scale didn't apply — tell me and I'll set the FBX scale factor.");
        }
        Object.DestroyImmediate(inst);
    }
}
