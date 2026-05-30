using UnityEngine;
using UnityEditor;

// Tree accents: white blossom clusters (canopy cards) and hanging glowing wisp drapes.
// Both spawn at origin as objects you position on the tree, then re-save the prefab.
public static class BuildAccents
{
    const string Dir = "Assets/Environment/MotherTree/Canopy/";

    [MenuItem("WildReach/Build Blossom Cluster")]
    static void Blossoms()
    {
        var mats = new[]
        {
            Cutout(Dir + "Blossom_1.png", "Blossom_Mat_1", false),
            Cutout(Dir + "Blossom_2.png", "Blossom_Mat_2", false),
        };
        if (mats[0] == null) return;

        var root = new GameObject("Blossoms");
        Undo.RegisterCreatedObjectUndo(root, "Build Blossoms");
        for (int i = 0; i < 8; i++)
        {
            var p = Random.insideUnitSphere;
            Card(root.transform, mats[Random.Range(0, mats.Length)],
                 new Vector3(p.x * 2f, p.y * 1.5f, p.z * 2f),
                 Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(0f, 360f), Random.Range(-15f, 15f)),
                 Random.Range(1.5f, 3f));
        }
        Selection.activeGameObject = root;
        Debug.Log("[Accents] Blossom cluster built. Nestle it into the crown; duplicate for more.");
    }

    [MenuItem("WildReach/Build Wisp Drape")]
    static void Wisps()
    {
        var mat = Cutout(Dir + "Wisp_1.png", "Wisp_Mat", emissive: true);
        if (mat == null) return;

        var root = new GameObject("Wisps");
        Undo.RegisterCreatedObjectUndo(root, "Build Wisps");
        for (int i = 0; i < 6; i++)
        {
            // hang vertically (texture drapes from the top), only spin around Y so they stay hanging
            Card(root.transform, mat,
                 new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f)),
                 Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                 Random.Range(2.5f, 4f));
        }
        Selection.activeGameObject = root;
        Debug.Log("[Accents] Wisp drape built (emissive). Tuck it under branches / over the hollow; its top is the anchor.");
    }

    static void Card(Transform parent, Material mat, Vector3 pos, Quaternion rot, float scale)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(q.GetComponent<MeshCollider>());
        q.GetComponent<MeshRenderer>().sharedMaterial = mat;
        q.transform.SetParent(parent, false);
        q.transform.localPosition = pos;
        q.transform.localRotation = rot;
        q.transform.localScale = Vector3.one * scale;
    }

    static Material Cutout(string texPath, string matName, bool emissive)
    {
        var imp = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (imp != null) { imp.alphaIsTransparency = true; imp.SaveAndReimport(); }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null) { Debug.LogError($"[Accents] Missing {texPath}"); return null; }

        string matPath = Dir + matName + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
        mat.SetTexture("_BaseMap", tex);
        mat.SetFloat("_AlphaClip", 1); mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetFloat("_Cutoff", 0.3f);
        mat.SetFloat("_Cull", 0);
        mat.SetFloat("_Smoothness", 0.1f);
        mat.renderQueue = 2450;
        if (emissive)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", tex);
            mat.SetColor("_EmissionColor", new Color(0.3f, 0.85f, 1f) * 2f); // cyan glow
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
