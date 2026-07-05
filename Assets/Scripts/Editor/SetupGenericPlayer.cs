using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// Sets up + swaps in the HAND-RIGGED Generic player (PlayerGen.fbx, our own rig — no mixamorig/Humanoid).
// Generic clips play DIRECTLY on the matching skeleton, so there is no avatar retarget to mismatch (the whole
// reason we left Humanoid). Steps: configure the FBX as Generic + own avatar, loop clips, build a Speed-driven
// locomotion blend tree (Idle@0 / Walk@0.5 / Run@1 — only the clips that exist), build the material, then swap the
// model into the Player object (keeping PlayerMovement/collider/camera) and auto-scale to ~2 m. Re-runnable: re-run
// after exporting new clips (walk/run) to wire them in.
public static class SetupGenericPlayer
{
    const string Fbx         = "Assets/Characters/PlayerGen/PlayerGen.fbx";
    const string CtrlPath    = "Assets/Characters/PlayerGen/PlayerGen.controller";
    const float  TargetHeight = 2.0f;   // the WildReach scale law

    [MenuItem("WildReach/Swap In Generic Player (hand-rigged)")]
    static void Go()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Fbx) == null)
        { Debug.LogError($"[GenPlayer] {Fbx} not found."); return; }

        // --- 1. import as Generic with its own avatar; loop the locomotion clips ---
        var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.importLights  = false; imp.importCameras = false;
        var clips = imp.defaultClipAnimations;
        bool changed = false;
        for (int i = 0; i < clips.Length; i++) if (!clips[i].loopTime) { clips[i].loopTime = true; changed = true; }
        if (changed) imp.clipAnimations = clips;
        imp.SaveAndReimport();

        var ctrl = BuildController();
        var material = BuildMaterial();

        // --- 2. swap into the Player object ---
        var movement = Object.FindAnyObjectByType<PlayerMovement>();
        if (movement == null) { Debug.LogError("[GenPlayer] No PlayerMovement in scene — open SampleScene."); return; }
        var player = movement.gameObject;

        var existing = player.transform.Cast<Transform>().FirstOrDefault(c => c.name == "PlayerGen");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var oldVisual = player.transform.Cast<Transform>()
            .FirstOrDefault(c => c.GetComponentInChildren<SkinnedMeshRenderer>(true) != null);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        inst.name = "PlayerGen";
        inst.transform.SetParent(player.transform, false);
        inst.transform.localPosition = oldVisual != null ? oldVisual.localPosition : Vector3.zero;
        inst.transform.localRotation = oldVisual != null ? oldVisual.localRotation : Quaternion.identity;

        foreach (var l in inst.GetComponentsInChildren<Light>(true))  Object.DestroyImmediate(l);
        foreach (var c in inst.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c);
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterials = r.sharedMaterials.Select(_ => material).ToArray();

        var animator = inst.GetComponent<Animator>(); if (animator == null) animator = inst.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        animator.applyRootMotion = false;
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Avatar>().FirstOrDefault();
        if (avatar != null) animator.avatar = avatar;

        if (inst.GetComponent<PlayerAnimator>() == null) inst.AddComponent<PlayerAnimator>();

        var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);
        float height = smr != null ? smr.bounds.size.y : 0f;
        if (height > 0.01f)
        {
            float factor = TargetHeight / height;
            inst.transform.localScale *= factor;
            Debug.Log($"[GenPlayer] Height {height:0.00} m -> scaled by {factor:0.000} to ~{TargetHeight} m.");
        }

        // align his FEET to the bottom of the player's collider so he doesn't float or sink (his pivot is at the
        // feet, but the old visual's Y offset assumed a different pivot)
        var col = player.GetComponent<Collider>();
        var smr3 = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (col != null && smr3 != null)
        {
            float dy = col.bounds.min.y - smr3.bounds.min.y;
            inst.transform.position += Vector3.up * dy;
            Debug.Log($"[GenPlayer] Foot-aligned to collider bottom (dy {dy:0.000}).");
        }

        if (oldVisual != null) { oldVisual.gameObject.SetActive(false); Debug.Log($"[GenPlayer] Old visual '{oldVisual.name}' disabled (kept as alt skin)."); }

        Selection.activeGameObject = inst;
        EditorUtility.SetDirty(player);
        Debug.Log("[GenPlayer] Hand-rigged Generic player swapped in. Hit Play — idle should drive off movement. (Walk/Run wire in once authored.)");
    }

    static AnimatorController BuildController()
    {
        AnimationClip idle = null, walk = null, run = null, jump = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(Fbx))
            if (a is AnimationClip c && !c.name.StartsWith("__"))
            {
                var n = c.name.ToLower();
                if (n.Contains("idle")) idle = c; else if (n.Contains("walk")) walk = c;
                else if (n.Contains("run")) run = c; else if (n.Contains("jump")) jump = c;
            }
        idle ??= AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__"));

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath)
                   ?? AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        // params the existing PlayerAnimator/climb/swim drive (present so nothing throws)
        // params the PlayerAnimator + Climbing/Swim systems drive (present so SetFloat/SetBool never warn,
        // even though climb/swim states aren't authored yet)
        foreach (var (n, t) in new[] {("Speed", AnimatorControllerParameterType.Float),
                                      ("VerticalVelocity", AnimatorControllerParameterType.Float),
                                      ("IsGrounded", AnimatorControllerParameterType.Bool),
                                      ("Jump", AnimatorControllerParameterType.Trigger),
                                      ("IsClimbing", AnimatorControllerParameterType.Bool),
                                      ("ClimbDir", AnimatorControllerParameterType.Float),
                                      ("ClimbMag", AnimatorControllerParameterType.Float),
                                      ("IsSwimming", AnimatorControllerParameterType.Bool),
                                      ("SwimSpeed", AnimatorControllerParameterType.Float)})
            if (!ctrl.parameters.Any(p => p.name == n)) ctrl.AddParameter(n, t);

        var sm = ctrl.layers[0].stateMachine;
        foreach (var st in sm.states.ToArray()) sm.RemoveState(st.state);

        // 1D blend tree on Speed: idle@0, walk@0.5, run@1 (PlayerAnimator already normalises Speed to those)
        var tree = new BlendTree { name = "Locomotion", blendType = BlendTreeType.Simple1D, blendParameter = "Speed" };
        AssetDatabase.AddObjectToAsset(tree, ctrl);
        tree.AddChild(idle, 0f);
        if (walk != null) tree.AddChild(walk, 0.5f);
        if (run  != null) tree.AddChild(run, 1f);
        var loco = sm.AddState("Locomotion"); loco.motion = tree; sm.defaultState = loco;

        // Jump: Any-State -> Jump on the trigger, exits back to locomotion after the clip
        if (jump != null)
        {
            var js = sm.AddState("Jump"); js.motion = jump;
            var toJ = sm.AddAnyStateTransition(js); toJ.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            toJ.duration = 0.06f; toJ.canTransitionToSelf = false;
            var outJ = js.AddTransition(loco); outJ.hasExitTime = true; outJ.exitTime = 0.7f; outJ.duration = 0.15f;
        }

        EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets();
        Debug.Log($"[GenPlayer] Controller: idle{(walk!=null?" + walk":"")}{(run!=null?" + run":"")}{(jump!=null?" + jump":"")}.");
        return ctrl;
    }

    static Material BuildMaterial()
    {
        var dir = Path.GetDirectoryName(Fbx).Replace('\\', '/');
        var pngs = Directory.GetFiles(dir, "*.png").Select(p => p.Replace('\\', '/')).ToArray();
        string Find(System.Func<string, bool> ok) => pngs.FirstOrDefault(p => ok(Path.GetFileName(p).ToLower()));
        var basePng   = Find(n => !n.Contains("normal") && !n.Contains("metallic") && !n.Contains("rough") && !n.Contains("emit"));
        var normalPng = Find(n => n.Contains("normal"));
        var matPath = $"{dir}/PlayerGen.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath) ?? new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (AssetDatabase.GetAssetPath(mat) == "") AssetDatabase.CreateAsset(mat, matPath);
        if (basePng != null) mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePng));
        if (normalPng != null)
        {
            if (AssetImporter.GetAtPath(normalPng) is TextureImporter ni && ni.textureType != TextureImporterType.NormalMap)
            { ni.textureType = TextureImporterType.NormalMap; ni.SaveAndReimport(); }
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPng)); mat.EnableKeyword("_NORMALMAP");
        }
        mat.SetFloat("_Smoothness", 0.2f); EditorUtility.SetDirty(mat);
        return mat;
    }
}
