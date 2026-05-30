using UnityEngine;
using UnityEditor;

// Spawns a small cluster of autumn leaf cards (cutout) to nestle onto the Spiral Tree's
// branch tips so it doesn't look barren. Drop it on a clump of branches, scale to fit, then
// duplicate (Cmd+D) onto each bare branch. Re-runnable.
public static class BuildAutumnCanopy
{
    const string LeafPath = "Assets/Environment/ForestTrees/Leaves/ForestLeaf_Autumn.png";
    const string MatPath  = "Assets/Environment/ForestTrees/Leaves/AutumnLeaf_Mat.mat";

    const int   CardCount = 14;
    const float RadiusXZ  = 1.2f;   // small clump (metres) — scale to fit a branch tip
    const float RadiusY   = 0.8f;
    const float CardMin   = 1.5f, CardMax = 2.8f;

    [MenuItem("WildReach/Build Autumn Canopy")]
    static void Build()
    {
        var imp = AssetImporter.GetAtPath(LeafPath) as TextureImporter;
        if (imp != null) { imp.alphaIsTransparency = true; imp.SaveAndReimport(); }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(LeafPath);
        if (tex == null) { Debug.LogError($"[Autumn] Missing {LeafPath}"); return; }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, MatPath); }
        mat.SetTexture("_BaseMap", tex);
        mat.SetFloat("_AlphaClip", 1);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetFloat("_Cutoff", 0.35f);
        mat.SetFloat("_Cull", 0);            // double-sided
        mat.SetFloat("_Smoothness", 0.1f);
        mat.renderQueue = 2450;
        EditorUtility.SetDirty(mat);

        var root = new GameObject("AutumnCanopy");
        Undo.RegisterCreatedObjectUndo(root, "Build Autumn Canopy");
        for (int i = 0; i < CardCount; i++)
        {
            var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
            card.name = $"Leaf_{i}";
            Object.DestroyImmediate(card.GetComponent<MeshCollider>());
            card.GetComponent<MeshRenderer>().sharedMaterial = mat;
            card.transform.SetParent(root.transform, false);

            var p = Random.insideUnitSphere;
            card.transform.localPosition = new Vector3(p.x * RadiusXZ, p.y * RadiusY, p.z * RadiusXZ);
            card.transform.localRotation = Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
            float s = Random.Range(CardMin, CardMax);
            card.transform.localScale = new Vector3(s, s, s);
        }

        Selection.activeGameObject = root;
        Debug.Log("[Autumn] Built a 14-card autumn clump at origin. Drag it onto a branch tip of the " +
                  "Spiral Tree, scale to fit, then Cmd+D onto each bare branch to fill it out.");
    }
}
