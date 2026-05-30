using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// One-button asset wiring for the Azure Renegade character:
//   - rigged T-pose mesh -> Humanoid avatar (created from the model)
//   - Idle / Walk / Run clips -> Humanoid, avatar copied from the T-pose (so they retarget)
//   - builds the Azure body material from the Meshy maps and remaps it onto the mesh
//   - builds Azure.controller (1D blend tree on Speed: Idle->Walk->Run)
// Re-runnable. After this, run "Swap In Azure Player" to put him in the scene.
public static class AzureCharacterSetup
{
    const string Dir   = "Assets/Characters/AzureRenegade/";
    const string Tpose = Dir + "Azure_Tpose.fbx";
    static readonly string[] Clips =
    {
        Dir + "Azure_Idle.fbx", Dir + "Azure_Walk.fbx", Dir + "Azure_Run.fbx",
        Dir + "Azure_ClimbUp.fbx", Dir + "Azure_ClimbDown.fbx",
    };

    // Meshy map filenames staged in the folder.
    const string Base   = Dir + "Meshy_AI_Azure_Renegade_0529234507_texture.png";
    const string Normal = Dir + "Meshy_AI_Azure_Renegade_0529234507_texture_normal.png";
    const string Emit   = Dir + "Meshy_AI_Azure_Renegade_0529234507_texture_emit.png";

    const string MatPath  = Dir + "Azure_Body.mat";
    const string CtrlPath = Dir + "Azure.controller";

    [MenuItem("WildReach/Setup Azure Character")]
    static void Setup()
    {
        // 1. Body material from the Meshy maps (flat-ish, stylized).
        AsNormalMap(Normal);
        var mat = BuildMaterial();

        // 2. Rigged mesh -> Humanoid, avatar created from this model.
        var tImp = (ModelImporter)AssetImporter.GetAtPath(Tpose);
        if (tImp == null) { Debug.LogError($"[Azure] Missing {Tpose}"); return; }
        tImp.animationType = ModelImporterAnimationType.Human;
        tImp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
        tImp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        tImp.importLights  = false;   // strip the Blender scene's Light (was glowing green at his feet)
        tImp.importCameras = false;   // and its Camera
        tImp.SaveAndReimport();

        // Remap every generated material slot on the mesh to our Azure body material.
        foreach (var m in AssetDatabase.LoadAllAssetsAtPath(Tpose).OfType<Material>())
            tImp.AddRemap(new AssetImporter.SourceAssetIdentifier(m), mat);
        tImp.SaveAndReimport();

        var avatar = AssetDatabase.LoadAllAssetsAtPath(Tpose).OfType<Avatar>().FirstOrDefault();
        if (avatar == null) { Debug.LogError("[Azure] No avatar generated from T-pose — check the rig imported as Humanoid."); return; }

        // 3. Each clip -> Humanoid, avatar copied from the T-pose, looping.
        foreach (var path in Clips)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            if (imp == null) { Debug.LogError($"[Azure] Missing clip {path}"); return; }
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup   = ModelImporterAvatarSetup.CopyFromOther;
            imp.sourceAvatar  = avatar;
            imp.importLights  = false;
            imp.importCameras = false;

            var ci = imp.defaultClipAnimations;
            if (ci.Length > 0) { foreach (var c in ci) c.loopTime = true; imp.clipAnimations = ci; }
            imp.SaveAndReimport();
        }

        var idle      = LoadClip(Clips[0]);
        var walk      = LoadClip(Clips[1]);
        var run       = LoadClip(Clips[2]);
        var climbUp   = LoadClip(Clips[3]);
        var climbDown = LoadClip(Clips[4]);
        if (idle == null || walk == null || run == null)
        { Debug.LogError("[Azure] Missing a clip after import (idle/walk/run)."); return; }
        if (climbUp == null || climbDown == null)
        { Debug.LogError("[Azure] Missing a climb clip (Azure_ClimbUp / Azure_ClimbDown)."); return; }

        // 4. Animator controller: 1D blend tree on Speed.
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath) != null)
            AssetDatabase.DeleteAsset(CtrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        ctrl.AddParameter("Speed",            AnimatorControllerParameterType.Float);
        ctrl.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("IsGrounded",       AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Jump",             AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("IsClimbing",       AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("ClimbDir",         AnimatorControllerParameterType.Float);
        ctrl.AddParameter("ClimbMag",         AnimatorControllerParameterType.Float);

        var sm = ctrl.layers[0].stateMachine;

        // --- Locomotion: 1D blend tree on normalised Speed (0 idle / 0.5 walk / 1 run) ---
        var locoState = ctrl.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendType     = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, 0.5f);
        tree.AddChild(run,  1f);
        ForceThresholds(tree, 0f, 0.5f, 1f);
        sm.defaultState = locoState;

        // --- Climb: 1D blend tree on ClimbDir (down -1 / cling 0 / up +1) ---
        // State playback speed = ClimbMag, so at 0 input he freezes (clings) on the wall.
        var climbState = ctrl.CreateBlendTreeInController("Climb", out var climbTree, 0);
        climbTree.blendType     = BlendTreeType.Simple1D;
        climbTree.blendParameter = "ClimbDir";
        climbTree.useAutomaticThresholds = false;
        climbTree.AddChild(climbDown, -1f);
        climbTree.AddChild(climbUp,    0f);   // cling base (frozen when ClimbMag = 0)
        climbTree.AddChild(climbUp,    1f);
        ForceThresholds(climbTree, -1f, 0f, 1f);
        climbState.speedParameterActive = true;
        climbState.speedParameter = "ClimbMag";

        // AnyState -> Climb when IsClimbing; Climb -> Locomotion when not.
        var toClimb = sm.AddAnyStateTransition(climbState);
        toClimb.hasExitTime = false; toClimb.duration = 0.15f; toClimb.canTransitionToSelf = false;
        toClimb.AddCondition(AnimatorConditionMode.If, 0, "IsClimbing");

        var fromClimb = climbState.AddTransition(locoState);
        fromClimb.hasExitTime = false; fromClimb.duration = 0.2f;
        fromClimb.AddCondition(AnimatorConditionMode.IfNot, 0, "IsClimbing");

        AssetDatabase.SaveAssets();
        Debug.Log("[Azure] Character wired: avatar + 5 retargeted clips (idle/walk/run/climbup/climbdown) " +
                  "+ body material + Azure.controller (Locomotion + Climb). Now run WildReach/Swap In Azure Player.");
    }

    static Material BuildMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, MatPath); }

        var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Base);
        var normal  = AssetDatabase.LoadAssetAtPath<Texture2D>(Normal);
        var emit    = AssetDatabase.LoadAssetAtPath<Texture2D>(Emit);

        if (baseTex) mat.SetTexture("_BaseMap", baseTex);
        if (normal)  { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
        if (emit)
        {
            mat.SetTexture("_EmissionMap", emit);
            mat.SetColor("_EmissionColor", Color.white);   // emit map carries the rune colours
            mat.EnableKeyword("_EMISSION");
        }
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.25f);                // stylized, mostly matte
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void AsNormalMap(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null && imp.textureType != TextureImporterType.NormalMap)
        { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
    }

    // AddChild can recompute thresholds; pin them explicitly.
    static void ForceThresholds(BlendTree tree, params float[] thresholds)
    {
        var children = tree.children;
        for (int i = 0; i < children.Length && i < thresholds.Length; i++)
            children[i].threshold = thresholds[i];
        tree.children = children;
    }

    static AnimationClip LoadClip(string path)
    {
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
            if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;
        return null;
    }
}
