using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// Slices Anim_Jump into beats and builds a jump-driven Animator controller.
// Re-runnable: tune the slice frames below and click WildReach/Setup Jump again.
public static class JumpSetup
{
    const string Dir      = "Assets/Characters/Player/Animations/";
    const string JumpFbx  = Dir + "Anim_Jump.fbx";
    const string CtrlPath = "Assets/Characters/Player/PlayerController.controller";

    // Source frames (clip spans 15-50). Feet leave @24, land @41 — tune & re-run if a beat reads wrong.
    // All non-looping: each beat plays in then HOLDS its last pose. Looping short
    // mocap slices spazzes, so we go pose-to-pose instead.
    static readonly (string name, int first, int last, bool loop)[] Slices =
    {
        ("Jump_Launch", 15, 24, false),
        ("Jump_Air",    26, 30, false),
        ("Jump_Fall",   34, 40, false),
        ("Jump_Land",   41, 50, false),
    };

    [MenuItem("WildReach/Setup Jump")]
    static void Setup()
    {
        // 1) Slice Anim_Jump into Launch / Air / Land.
        var imp = (ModelImporter)AssetImporter.GetAtPath(JumpFbx);
        imp.clipAnimations = Slices.Select(s => new ModelImporterClipAnimation
        {
            name = s.name, firstFrame = s.first, lastFrame = s.last,
            loopTime = s.loop, loopPose = s.loop,
            lockRootRotation = true, lockRootHeightY = true, lockRootPositionXZ = true,
            keepOriginalOrientation = true, keepOriginalPositionY = true, keepOriginalPositionXZ = true,
        }).ToArray();
        imp.SaveAndReimport();

        // 2) Gather clips.
        var launch = Clip(JumpFbx, "Jump_Launch");
        var air    = Clip(JumpFbx, "Jump_Air");
        var fall   = Clip(JumpFbx, "Jump_Fall");
        var land   = Clip(JumpFbx, "Jump_Land");
        var idle   = FirstClip(Dir + "Anim_Idle.fbx");
        var walk   = FirstClip(Dir + "Anim_Walk.fbx");
        var run    = FirstClip(Dir + "Anim_Run.fbx");
        if (new[] { launch, air, fall, land, idle, walk, run }.Any(c => c == null))
        { Debug.LogError("[Jump] A clip failed to load — aborting."); return; }

        // 3) Fresh controller.
        AssetDatabase.DeleteAsset(CtrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        ctrl.AddParameter("Speed",            AnimatorControllerParameterType.Float);
        ctrl.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("IsGrounded",       AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Jump",             AnimatorControllerParameterType.Trigger);

        var sm = ctrl.layers[0].stateMachine;
        var sIdle   = AddState(sm, "Idle",   idle,   new Vector3(300, 0, 0));
        var sWalk   = AddState(sm, "Walk",   walk,   new Vector3(300, 80, 0));
        var sRun    = AddState(sm, "Run",    run,    new Vector3(300, 160, 0));
        var sLaunch = AddState(sm, "Launch", launch, new Vector3(600, 0, 0));
        var sAir    = AddState(sm, "Air",    air,    new Vector3(600, 80, 0));
        var sFall   = AddState(sm, "Fall",   fall,   new Vector3(600, 160, 0));
        var sLand   = AddState(sm, "Land",   land,   new Vector3(600, 240, 0));
        sm.defaultState = sIdle;

        // Locomotion (kept simple — jump is the focus this turn).
        Float(sIdle.AddTransition(sWalk), AnimatorConditionMode.Greater, 0.1f, "Speed", 0.1f);
        Float(sWalk.AddTransition(sIdle), AnimatorConditionMode.Less,    0.1f, "Speed", 0.1f);
        Float(sWalk.AddTransition(sRun),  AnimatorConditionMode.Greater, 14f,  "Speed", 0.1f);
        Float(sRun.AddTransition(sWalk),  AnimatorConditionMode.Less,    14f,  "Speed", 0.1f);

        // Jump flow, driven by the Jump trigger + physics, not clip timing.
        var any = sm.AddAnyStateTransition(sLaunch);
        any.AddCondition(AnimatorConditionMode.If, 0, "Jump");
        any.hasExitTime = false; any.canTransitionToSelf = false;
        any.hasFixedDuration = true; any.duration = 0.03f;

        // Launch -> Air after the push-off plays out.
        var la = sLaunch.AddTransition(sAir);
        la.hasExitTime = true; la.exitTime = 0.8f; la.hasFixedDuration = true; la.duration = 0.05f;
        // ...or straight to Fall if we're already descending.
        Float(sLaunch.AddTransition(sFall), AnimatorConditionMode.Less, -0.1f, "VerticalVelocity", 0.05f);

        Float(sAir.AddTransition(sFall), AnimatorConditionMode.Less, -0.1f, "VerticalVelocity", 0.1f);
        Bool(sAir.AddTransition(sLand),  AnimatorConditionMode.If, "IsGrounded", 0.02f);   // short-hop safety
        Bool(sFall.AddTransition(sLand), AnimatorConditionMode.If, "IsGrounded", 0.02f);

        // Land -> Idle after the settle.
        var li = sLand.AddTransition(sIdle);
        li.hasExitTime = true; li.exitTime = 0.7f; li.hasFixedDuration = true; li.duration = 0.12f;

        // 4) Assign to the Player's Animator.
        var player = GameObject.Find("Player");
        var anim = player ? player.GetComponent<Animator>() : null;
        if (anim) { anim.runtimeAnimatorController = ctrl; EditorUtility.SetDirty(anim); }
        else Debug.LogWarning("[Jump] Controller built but no Player Animator found to assign it to.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Jump] Setup complete: Launch→Air→Fall→Land wired to physics. Assigned to Player.");
    }

    static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion m, Vector3 pos)
    { var s = sm.AddState(name, pos); s.motion = m; s.writeDefaultValues = true; return s; }

    static void Float(AnimatorStateTransition t, AnimatorConditionMode mode, float val, string p, float dur)
    { t.AddCondition(mode, val, p); t.hasExitTime = false; t.hasFixedDuration = true; t.duration = dur; }

    static void Bool(AnimatorStateTransition t, AnimatorConditionMode mode, string p, float dur)
    { t.AddCondition(mode, 0, p); t.hasExitTime = false; t.hasFixedDuration = true; t.duration = dur; }

    static AnimationClip Clip(string path, string name) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);

    static AnimationClip FirstClip(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
}
