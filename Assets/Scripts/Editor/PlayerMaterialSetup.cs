using UnityEngine;
using UnityEditor;

public static class PlayerMaterialSetup
{
    [MenuItem("WildReach/Setup Player Material")]
    static void Setup()
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = "PlayerMaterial";

        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Characters/Player/Textures/albedo.png");
        var emit   = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Characters/Player/Textures/emit.png");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Characters/Player/Textures/normal.png");

        if (albedo) mat.SetTexture("_BaseMap", albedo);
        if (normal) mat.SetTexture("_BumpMap", normal);

        if (emit)
        {
            mat.SetTexture("_EmissionMap", emit);
            mat.SetColor("_EmissionColor", Color.white);
            mat.EnableKeyword("_EMISSION");
        }

        AssetDatabase.CreateAsset(mat, "Assets/Characters/Player/PlayerMaterial.mat");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Auto-assign to all SkinnedMeshRenderers inside the Player in the scene
        var playerRoot = GameObject.Find("Player");
        if (playerRoot != null)
        {
            foreach (var r in playerRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = mat;
                r.sharedMaterials = mats;
            }
            Debug.Log("PlayerMaterial assigned to all mesh renderers on Player.");
        }
        else
        {
            Debug.LogWarning("Player not found in scene — material created but not assigned.");
        }
    }
}
