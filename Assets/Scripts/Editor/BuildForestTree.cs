using System.Linq;
using UnityEngine;
using UnityEditor;

// Assembles a scatterable forest tree: the Whispering trunk (material + collider) with a
// green leaf-card canopy clustered into its crown, saved as one ForestTree.prefab.
// Run once, drag the prefab in to check it, tune, then use Scatter Forest to populate.
public static class BuildForestTree
{
    const string Dir       = "Assets/Environment/ForestTrees/";
    const string TrunkFbx  = Dir + "Whispering/Whispering.fbx";
    const string TrunkBase = Dir + "Whispering/Whispering_Base.png";
    const string TrunkNorm = Dir + "Whispering/Whispering_Normal.png";
    const string TrunkMat   = Dir + "Whispering/Whispering.mat";
    static readonly string[] LeafCards = { Dir + "Leaves/ForestLeaf_Green1.png", Dir + "Leaves/ForestLeaf_Green2.png", Dir + "Leaves/ForestLeaf_Green3.png" };
    const string PrefabPath = Dir + "ForestTree.prefab";

    const int CanopyCards = 26;

    [MenuItem("WildReach/Build Forest Tree")]
    static void Build()
    {
        // 1. Trunk material + walkable/blocking collider.
        var imp = AssetImporter.GetAtPath(TrunkFbx) as ModelImporter;
        if (imp == null) { Debug.LogError($"[Forest] Missing {TrunkFbx}"); return; }
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        imp.addCollider = true;
        imp.SaveAndReimport();
        AsNormalMap(TrunkNorm);
        var trunkMat = BuildTrunkMat();
        foreach (var m in AssetDatabase.LoadAllAssetsAtPath(TrunkFbx).OfType<Material>())
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(m), trunkMat);
        imp.SaveAndReimport();

        // 2. Green leaf cutout materials.
        var leafMats = LeafCards.Select(BuildLeafMat).Where(m => m != null).ToArray();
        if (leafMats.Length == 0) { Debug.LogError("[Forest] No green leaf cards found."); return; }

        // 3. Assemble trunk + crown canopy under one root.
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(TrunkFbx);
        var root = new GameObject("ForestTree");
        var trunk = (GameObject)PrefabUtility.InstantiatePrefab(model);
        trunk.transform.SetParent(root.transform, false);
        PrefabUtility.UnpackPrefabInstance(trunk, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        var rends = trunk.GetComponentsInChildren<Renderer>();
        var b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        float H = b.size.y, W = Mathf.Max(b.size.x, b.size.z);

        var canopy = new GameObject("Canopy");
        canopy.transform.SetParent(root.transform, false);
        canopy.transform.position = new Vector3(b.center.x, b.min.y + H * 0.70f, b.center.z);  // crown
        float rXZ = W * 0.45f, rY = H * 0.22f;
        for (int i = 0; i < CanopyCards; i++)
        {
            var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
            card.name = $"Leaf_{i}";
            Object.DestroyImmediate(card.GetComponent<MeshCollider>());
            card.GetComponent<MeshRenderer>().sharedMaterial = leafMats[Random.Range(0, leafMats.Length)];
            card.transform.SetParent(canopy.transform, false);
            var p = Random.insideUnitSphere;
            card.transform.localPosition = new Vector3(p.x * rXZ, p.y * rY, p.z * rXZ);
            card.transform.localRotation = Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
            float s = Random.Range(W * 0.5f, W * 0.8f);
            card.transform.localScale = new Vector3(s, s, s);
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Selection.activeObject = prefab;
        Debug.Log($"[Forest] ForestTree.prefab built ({H:0.0}m trunk + {CanopyCards}-card green crown). " +
                  "Drag it into the scene to check it; tell me to tune the canopy, then run Scatter Forest.");
    }

    static Material BuildTrunkMat()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(TrunkMat);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, TrunkMat); }
        var b = AssetDatabase.LoadAssetAtPath<Texture2D>(TrunkBase);
        var n = AssetDatabase.LoadAssetAtPath<Texture2D>(TrunkNorm);
        if (b) mat.SetTexture("_BaseMap", b);
        if (n) { mat.SetTexture("_BumpMap", n); mat.EnableKeyword("_NORMALMAP"); }
        mat.SetFloat("_Metallic", 0f); mat.SetFloat("_Smoothness", 0.12f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material BuildLeafMat(string texPath)
    {
        var imp = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (imp != null) { imp.alphaIsTransparency = true; imp.SaveAndReimport(); }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null) { Debug.LogError($"[Forest] Missing {texPath}"); return null; }
        string matPath = texPath.Replace(".png", "_Mat.mat");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
        mat.SetTexture("_BaseMap", tex);
        mat.SetFloat("_AlphaClip", 1); mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetFloat("_Cutoff", 0.35f); mat.SetFloat("_Cull", 0);
        mat.SetFloat("_Smoothness", 0.1f); mat.renderQueue = 2450;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void AsNormalMap(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null && imp.textureType != TextureImporterType.NormalMap)
        { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
    }
}
