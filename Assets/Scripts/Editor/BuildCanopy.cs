using System.Linq;
using UnityEngine;
using UnityEditor;

// Builds leaf-card cutout materials from the blue foliage textures, then spawns a
// clustered volumetric canopy (random cards in an ellipsoid) you drop onto the crown.
public static class BuildCanopy
{
    const string Dir = "Assets/Environment/MotherTree/Canopy/";
    static readonly string[] Leaves = { "Leaf_1.png", "Leaf_2.png", "Leaf_3.png", "Leaf_4.png" };

    const int   CardCount = 18;
    const float RadiusXZ  = 3f;   // cluster half-width (metres) — scale the Canopy object to fit your tree
    const float RadiusY   = 2f;
    const float CardMin   = 2.5f, CardMax = 4.5f;

    [MenuItem("WildReach/Build Canopy")]
    static void Build()
    {
        // Cutout materials (one per leaf variant), double-sided.
        var mats = new Material[Leaves.Length];
        for (int i = 0; i < Leaves.Length; i++)
        {
            string texPath = Dir + Leaves[i];
            var imp = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (imp != null) { imp.alphaIsTransparency = true; imp.SaveAndReimport(); }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) { Debug.LogError($"[Canopy] Missing {texPath}"); return; }

            string matPath = Dir + $"Leaf_Mat_{i + 1}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetFloat("_AlphaClip", 1);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Cutoff", 0.35f);     // low-ish keeps the soft painterly edge
            mat.SetFloat("_Cull", 0);           // double-sided
            mat.SetFloat("_Smoothness", 0.1f);
            mat.renderQueue = 2450;             // AlphaTest
            EditorUtility.SetDirty(mat);
            mats[i] = mat;
        }

        // Cluster of cards.
        var root = new GameObject("Canopy");
        Undo.RegisterCreatedObjectUndo(root, "Build Canopy");
        for (int i = 0; i < CardCount; i++)
        {
            var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
            card.name = $"Leaf_{i}";
            Object.DestroyImmediate(card.GetComponent<MeshCollider>());
            card.GetComponent<MeshRenderer>().sharedMaterial = mats[Random.Range(0, mats.Length)];
            card.transform.SetParent(root.transform, false);

            // random point in an ellipsoid
            var p = Random.insideUnitSphere;
            card.transform.localPosition = new Vector3(p.x * RadiusXZ, p.y * RadiusY, p.z * RadiusXZ);
            card.transform.localRotation = Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
            float s = Random.Range(CardMin, CardMax);
            card.transform.localScale = new Vector3(s, s, s);
        }

        Selection.activeGameObject = root;
        Debug.Log($"[Canopy] Built a {CardCount}-card canopy cluster at origin. " +
                  "Drag it onto the tree crown, scale to fit, duplicate 2-3 for fullness, then we'll prefab the whole tree.");
    }
}
