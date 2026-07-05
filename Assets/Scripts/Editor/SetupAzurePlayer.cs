using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// Sets up + swaps in the consolidated GENERIC player (AzurePlayer.fbx — mesh + every mocap clip on ONE skeleton, so
// Generic binds by matching paths; no Humanoid solve to mangle the run, no avatar to orphan). Imports Generic,
// builds a controller (Speed locomotion blend + Jump + Climb + Swim, driven by the same params PlayerAnimator/
// Climbing/Swim set), and swaps it into the Player. Re-runnable.
public static class SetupAzurePlayer
{
    const string Fbx          = "Assets/Characters/AzurePlayer/AzurePlayer.fbx";
    const string CtrlPath     = "Assets/Characters/AzurePlayer/AzurePlayer.controller";
    const float  TargetHeight = 2.0f;

    [MenuItem("WildReach/Swap In Azure Player (Generic)")]
    static void Go()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Fbx) == null) { Debug.LogError($"[Azure] {Fbx} not found — run the Blender consolidation first."); return; }

        // import Generic + own avatar; loop the cyclic clips (not jump/climb)
        var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.motionNodeName = "mixamorig:Hips";   // Generic root-motion node — WITHOUT this, deltaPosition is always 0 (climb root motion never extracted)
        imp.importLights  = false; imp.importCameras = false;
        var clips = imp.defaultClipAnimations;
        // continuous actions must loop; one-shots (jump, land, roll, punch, hit, death, sit-down, pickup) must not
        var loopers = new[] { "idle", "walk", "run", "sprint", "strafe", "turn",
                              "climb", "shimmy", "swim", "tread", "float", "sleep", "fall" };
        for (int i = 0; i < clips.Length; i++)
        {
            var ln = clips[i].name.ToLower();
            clips[i].loopTime = loopers.Any(l => ln.Contains(l));
            if (ln.Contains("climb") || ln.Contains("shimmy"))   // root-motion-driven: DON'T bake the travel into the pose
            {
                clips[i].keepOriginalPositionY  = true; clips[i].lockRootHeightY    = false;
                clips[i].keepOriginalPositionXZ = true; clips[i].lockRootPositionXZ = false;
                clips[i].keepOriginalOrientation = true; clips[i].lockRootRotation  = false;
            }
            else   // everything else is in-place: bake the Hips motion (incl. the bob) into the pose -> no root motion
            {
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].lockRootRotation = true;
            }
        }
        imp.clipAnimations = clips;
        imp.SaveAndReimport();

        var ctrl = BuildController();
        var material = BuildMaterial();

        var move = Object.FindFirstObjectByType<PlayerMovement>();
        if (move == null) { Debug.LogError("[Azure] No PlayerMovement in scene."); return; }
        var player = move.gameObject;

        var existing = player.transform.Cast<Transform>().FirstOrDefault(c => c.name == "AzurePlayer");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        var oldVisual = player.transform.Cast<Transform>().FirstOrDefault(c => c.GetComponentInChildren<SkinnedMeshRenderer>(true) != null);

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx));
        inst.name = "AzurePlayer";
        inst.transform.SetParent(player.transform, false);
        inst.transform.localPosition = oldVisual != null ? oldVisual.localPosition : Vector3.zero;
        inst.transform.localRotation = oldVisual != null ? oldVisual.localRotation : Quaternion.identity;

        foreach (var l in inst.GetComponentsInChildren<Light>(true))  Object.DestroyImmediate(l);
        foreach (var c in inst.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c);
        if (material != null)
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials = r.sharedMaterials.Select(_ => material).ToArray();

        var animator = inst.GetComponent<Animator>(); if (animator == null) animator = inst.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        animator.applyRootMotion = true;   // climb is root-motion-driven; PlayerAnimator.OnAnimatorMove routes it (in-place clips are no-ops)
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Avatar>().FirstOrDefault();
        if (avatar != null) animator.avatar = avatar;
        if (inst.GetComponent<PlayerAnimator>() == null) inst.AddComponent<PlayerAnimator>();

        var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);
        float h = smr != null ? smr.bounds.size.y : 0f;
        if (h > 0.01f) { inst.transform.localScale *= TargetHeight / h; Debug.Log($"[Azure] Height {h:0.00} -> ~{TargetHeight} m."); }

        // feet are planted at RUNTIME by FootPlanter — edit-time mesh sampling is unreliable with an Animator
        // (the idle clip often doesn't pose the mesh), which is what kept leaving him floating.
        if (inst.GetComponent<FootPlanter>() == null) inst.AddComponent<FootPlanter>();

        if (oldVisual != null) oldVisual.gameObject.SetActive(false);
        Selection.activeGameObject = inst; EditorUtility.SetDirty(player);
        Debug.Log("[Azure] Generic player swapped in. Play: idle/walk/run/jump/climb/swim all play DIRECTLY (no Humanoid solve).");
    }

    static AnimatorController BuildController()
    {
        AnimationClip Clip(string key) => AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>()
            .FirstOrDefault(c => c.name.ToLower() == key.ToLower() || c.name.ToLower().EndsWith("|" + key.ToLower()));
        var idle = Clip("Idle"); var walk = Clip("Walk"); var run = Clip("Run");
        var fall = Clip("Fall"); var land = Clip("Land");
        var jump = Clip("Jump"); var runJump = Clip("RunJump"); var jumpUp = Clip("JumpUp");
        var climbUp = Clip("ClimbUp"); var climbDown = Clip("ClimbDown");
        var shimmyL = Clip("ShimmyL"); var shimmyR = Clip("ShimmyR");
        var swim = Clip("Swim"); var tread = Clip("Tread");
        var pickup = Clip("Pickup"); var draw = Clip("Draw"); var sheathe = Clip("Sheathe"); var swing = Clip("Swing");

        if (File.Exists(CtrlPath)) AssetDatabase.DeleteAsset(CtrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        foreach (var (n, t) in new[] {
            ("Speed", AnimatorControllerParameterType.Float), ("VerticalVelocity", AnimatorControllerParameterType.Float),
            ("IsGrounded", AnimatorControllerParameterType.Bool), ("Jump", AnimatorControllerParameterType.Trigger),
            ("HardLand", AnimatorControllerParameterType.Trigger), ("Landed", AnimatorControllerParameterType.Trigger),
            ("IsClimbing", AnimatorControllerParameterType.Bool), ("ClimbDir", AnimatorControllerParameterType.Float),
            ("ClimbMag", AnimatorControllerParameterType.Float), ("ClimbStrafe", AnimatorControllerParameterType.Float),
            ("IsSwimming", AnimatorControllerParameterType.Bool),
            ("SwimSpeed", AnimatorControllerParameterType.Float),
            ("Pickup", AnimatorControllerParameterType.Trigger), ("Draw", AnimatorControllerParameterType.Trigger),
            ("Sheathe", AnimatorControllerParameterType.Trigger), ("Swing", AnimatorControllerParameterType.Trigger) })
            ctrl.AddParameter(n, t);
        var sm = ctrl.layers[0].stateMachine;

        // Locomotion blend (Speed: idle 0 / walk .5 / run 1)
        var locoTree = new BlendTree { name = "Loco", blendType = BlendTreeType.Simple1D, blendParameter = "Speed" };
        AssetDatabase.AddObjectToAsset(locoTree, ctrl);
        locoTree.AddChild(idle, 0f); if (walk) locoTree.AddChild(walk, 0.5f); if (run) locoTree.AddChild(run, 1f);
        var loco = sm.AddState("Locomotion"); loco.motion = locoTree; sm.defaultState = loco;

        // Jump (clip-based): use the RUNNING JUMP clip for all jumps — at 0.9s, launching at 0.3s, it's the only
        // clip that fits the ~1.1s airtime (the standing clips are 2s, which is what lagged AND jittered when I
        // time-scaled them). Played at NORMAL speed (no scaling = no jitter) and exits on the PHYSICS landing
        // (Landed/HardLand), so it stays synced. Stepping off a ledge (no jump) uses the held Air pose.
        // clean symmetric arms-up launch (jumping up); the Running Jump's right-arm swing resists clean surgical
        // fixes in mocap. For a custom dynamic jump later, hand-pose one in PoseForge.
        var jumpClip = jumpUp != null ? jumpUp : (runJump != null ? runJump : jump);

        AnimatorState landSt = null;
        if (land)
        {
            landSt = sm.AddState("Land"); landSt.motion = land;
            var ol = landSt.AddTransition(loco); ol.hasExitTime = true; ol.exitTime = 0.35f; ol.duration = 0.12f;
        }
        void ExitOnLanding(AnimatorState s)
        {
            if (landSt != null) { var h = s.AddTransition(landSt); h.AddCondition(AnimatorConditionMode.If, 0, "HardLand"); h.hasExitTime = false; h.duration = 0.05f; }
            var so = s.AddTransition(loco); so.AddCondition(AnimatorConditionMode.If, 0, "Landed"); so.hasExitTime = false; so.duration = 0.10f;
        }

        // Air pose (looping Falling Idle) — the airborne HOLD. Both the leap-finished-but-still-falling case and
        // stepping off a ledge route here, so we never freeze on a clip's last frame.
        AnimatorState airSt = null;
        if (fall) { airSt = sm.AddState("Air"); airSt.motion = fall; ExitOnLanding(airSt); }

        if (jumpClip)
        {
            var js = sm.AddState("Jump"); js.motion = jumpClip;   // normal speed, no time-scaling
            ExitOnLanding(js);                                    // landed mid-leap -> straight out
            if (airSt != null)                                    // leap done but still airborne -> fall (loop)
            {
                var toAir = js.AddTransition(airSt);
                toAir.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
                toAir.hasExitTime = true; toAir.exitTime = 0.6f; toAir.duration = 0.15f;   // short launch pop -> fall
            }
            var toJ = sm.AddAnyStateTransition(js); toJ.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            toJ.duration = 0.04f; toJ.canTransitionToSelf = false;
        }
        if (airSt != null)   // stepped off a ledge with no jump
        {
            var toAir = loco.AddTransition(airSt);
            toAir.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
            toAir.AddCondition(AnimatorConditionMode.IfNot, 0, "IsClimbing");
            toAir.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSwimming");
            toAir.hasExitTime = false; toAir.duration = 0.12f;
        }

        // Climb: 2D blend by (ClimbStrafe x ClimbDir) -> up/down + shimmy L/R; ClimbMag scales playback (clings when still)
        if (climbUp && climbDown)
        {
            var climbTree = new BlendTree { name = "Climb", blendType = BlendTreeType.SimpleDirectional2D,
                                            blendParameter = "ClimbStrafe", blendParameterY = "ClimbDir" };
            AssetDatabase.AddObjectToAsset(climbTree, ctrl);
            climbTree.AddChild(climbUp, new Vector2(0f, 0f));     // center = cling (ClimbMag freezes it)
            climbTree.AddChild(climbUp, new Vector2(0f, 1f));
            climbTree.AddChild(climbDown, new Vector2(0f, -1f));
            if (shimmyL) climbTree.AddChild(shimmyL, new Vector2(-1f, 0f));
            if (shimmyR) climbTree.AddChild(shimmyR, new Vector2(1f, 0f));
            var cs = sm.AddState("Climb"); cs.motion = climbTree; cs.speedParameter = "ClimbMag"; cs.speedParameterActive = true;
            var toC = sm.AddAnyStateTransition(cs); toC.AddCondition(AnimatorConditionMode.If, 0, "IsClimbing");
            toC.duration = 0.12f; toC.canTransitionToSelf = false;
            var outC = cs.AddTransition(loco); outC.AddCondition(AnimatorConditionMode.IfNot, 0, "IsClimbing");
            outC.hasExitTime = false; outC.duration = 0.12f;
        }

        // Swim (Tread 0 / Swim 1 by SwimSpeed)
        if (tread && swim)
        {
            var swimTree = new BlendTree { name = "Swim", blendType = BlendTreeType.Simple1D, blendParameter = "SwimSpeed" };
            AssetDatabase.AddObjectToAsset(swimTree, ctrl);
            swimTree.AddChild(tread, 0f); swimTree.AddChild(swim, 1f);
            var ss = sm.AddState("Swim"); ss.motion = swimTree;
            var toS = sm.AddAnyStateTransition(ss); toS.AddCondition(AnimatorConditionMode.If, 0, "IsSwimming");
            toS.duration = 0.12f; toS.canTransitionToSelf = false;
            var outS = ss.AddTransition(loco); outS.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSwimming");
            outS.hasExitTime = false; outS.duration = 0.12f;
        }

        // One-shot wield actions: each fires from its trigger and returns to locomotion. The WieldSystem drives
        // the triggers and re-parents the held item mid-clip so it matches the hand reaching the ground/back.
        void OneShot(string trig, AnimationClip clip)
        {
            if (!clip) return;
            var s = sm.AddState(trig); s.motion = clip;
            var to = sm.AddAnyStateTransition(s); to.AddCondition(AnimatorConditionMode.If, 0, trig);
            to.duration = 0.07f; to.canTransitionToSelf = false;
            var outt = s.AddTransition(loco); outt.hasExitTime = true; outt.exitTime = 0.85f; outt.duration = 0.12f;
        }
        OneShot("Pickup", pickup); OneShot("Draw", draw); OneShot("Sheathe", sheathe); OneShot("Swing", swing);

        EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets();
        Debug.Log($"[Azure] Controller built (locomotion + jump/climb/swim + pickup/draw/sheathe/swing where clips exist).");
        return ctrl;
    }

    static Material BuildMaterial()
    {
        var dir = Path.GetDirectoryName(Fbx).Replace('\\', '/');
        var pngs = Directory.GetFiles(dir, "*.png").Select(p => p.Replace('\\', '/')).ToArray();
        string Find(System.Func<string, bool> ok) => pngs.FirstOrDefault(p => ok(Path.GetFileName(p).ToLower()));
        var basePng   = Find(n => !n.Contains("normal") && !n.Contains("metallic") && !n.Contains("rough") && !n.Contains("emit") && !n.Contains("emission"));
        var normalPng = Find(n => n.Contains("normal"));
        var matPath = $"{dir}/AzurePlayer.mat";
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
