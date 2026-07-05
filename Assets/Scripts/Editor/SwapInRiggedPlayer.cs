using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// Promotes the new hand-rigged Azure (Azure_Rigged.fbx) to the actual player:
//   - builds his skin material from the new Meshy maps and remaps it onto the mesh
//   - imports him as Humanoid so the existing Azure.controller (Humanoid clips) drives him
//   - instantiates him under the Player object, wires Animator + PlayerAnimator, scales to ~2 m
//   - disables the old visual (kept as an alt skin) and deletes the AnimationGallery instance
// Re-runnable.
public static class SwapInRiggedPlayer
{
    const string Fbx      = "Assets/Characters/AzureRigged/Azure_Rigged.fbx";
    const string TexDir   = "Assets/Characters/AzureRigged/Tex/";
    const string MatPath  = "Assets/Characters/AzureRigged/Azure_Rigged_Body.mat";
    const string CtrlPath = "Assets/Characters/AzureRenegade/Azure.controller";
    const float  TargetHeight = 2.0f;

    [MenuItem("WildReach/Swap In Rigged Player")]
    static void Swap()
    {
        // --- 1. skin material from the new maps ---
        AsNormalMap(TexDir + "Azure_Normal.png");
        var mat = BuildMaterial();

        // --- 2. import the rig as Humanoid + remap its material slots to ours ---
        var imp = AssetImporter.GetAtPath(Fbx) as ModelImporter;
        if (imp == null) { Debug.LogError("[RiggedSwap] Missing " + Fbx); return; }
        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        imp.importLights  = false;
        imp.importCameras = false;
        imp.SaveAndReimport();
        foreach (var m in AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Material>())
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(m), mat);
        imp.SaveAndReimport();

        var model  = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Avatar>().FirstOrDefault();
        var ctrl   = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
        if (avatar == null || !avatar.isValid) { Debug.LogError("[RiggedSwap] Rig's Humanoid avatar isn't valid — fix the bone mapping first."); return; }
        if (ctrl == null) { Debug.LogError("[RiggedSwap] Missing " + CtrlPath + " — run 'Setup Azure Character' once to build it."); return; }

        // make sure IK Pass is on so ClimbIK's hand/foot pinning fires
        var layers = ctrl.layers;
        if (layers.Length > 0 && !layers[0].iKPass) { layers[0].iKPass = true; ctrl.layers = layers; EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets(); }

        // --- 3. find the Player ---
        var movement = Object.FindAnyObjectByType<PlayerMovement>();
        if (movement == null) { Debug.LogError("[RiggedSwap] No PlayerMovement in scene."); return; }
        var player = movement.gameObject;

        // remove the gallery instance + any previous rigged visual
        var gallery = GameObject.Find("AnimGallery_Azure");
        if (gallery != null) Object.DestroyImmediate(gallery);
        var dup = player.transform.Cast<Transform>().FirstOrDefault(c => c.name == "Azure_Rigged");
        if (dup != null) Object.DestroyImmediate(dup.gameObject);

        // ALL existing visuals (every child whose subtree has a skinned mesh) — capture transform,
        // then disable every one of them so none render on top of the new rig.
        var oldVisuals = player.transform.Cast<Transform>()
            .Where(c => c.GetComponentInChildren<SkinnedMeshRenderer>(true) != null).ToList();
        var refVisual = oldVisuals.FirstOrDefault();

        // --- 4. instantiate the new rig under Player ---
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        inst.name = "Azure_Rigged";
        inst.transform.SetParent(player.transform, false);
        inst.transform.localPosition = refVisual ? refVisual.localPosition : Vector3.zero;
        inst.transform.localRotation = refVisual ? refVisual.localRotation : Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        foreach (var l in inst.GetComponentsInChildren<Light>(true))  Object.DestroyImmediate(l);
        foreach (var c in inst.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c);

        var animator = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
        animator.avatar = avatar;
        animator.runtimeAnimatorController = ctrl;
        animator.applyRootMotion = false;
        if (inst.GetComponent<PlayerAnimator>() == null) inst.AddComponent<PlayerAnimator>();
        if (inst.GetComponent<ClimbIK>() == null) inst.AddComponent<ClimbIK>();   // hand/foot pinning on the wall

        // --- 5. normalise height to ~2 m ---
        var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);
        float height = smr != null ? smr.bounds.size.y : 0f;
        if (height > 0.01f)
        {
            float f = TargetHeight / height;
            inst.transform.localScale *= f;
            Debug.Log($"[RiggedSwap] Height {height:0.00} m -> scaled x{f:0.000} to ~{TargetHeight} m.");
        }

        foreach (var ov in oldVisuals) Object.DestroyImmediate(ov.gameObject);   // remove old visuals — no mess

        Selection.activeGameObject = inst;
        EditorUtility.SetDirty(player);
        Debug.Log("[RiggedSwap] New rigged Azure is the player. Old visuals deleted, gallery removed, ClimbIK + IK Pass on. Hit Play.");
    }

    static Material BuildMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath)
                  ?? new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (AssetDatabase.GetAssetPath(mat) == "") AssetDatabase.CreateAsset(mat, MatPath);

        var b = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "Azure_Base.png");
        var n = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "Azure_Normal.png");
        var e = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "Azure_Emit.png");
        if (b) mat.SetTexture("_BaseMap", b);
        if (n) { mat.SetTexture("_BumpMap", n); mat.EnableKeyword("_NORMALMAP"); }
        if (e) { mat.SetTexture("_EmissionMap", e); mat.SetColor("_EmissionColor", Color.white); mat.EnableKeyword("_EMISSION"); }
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.25f);
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
