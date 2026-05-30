using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public static class PlayerAnimatorSetup
{
    [MenuItem("WildReach/Setup Player Animator")]
    static void Setup()
    {
        var controller = AnimatorController.CreateAnimatorControllerAtPath(
            "Assets/Characters/Player/PlayerAnimator.controller"
        );

        controller.AddParameter("Speed",          AnimatorControllerParameterType.Float);
        controller.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded",     AnimatorControllerParameterType.Bool);

        var sm = controller.layers[0].stateMachine;

        var idle = sm.AddState("Idle");
        idle.motion = LoadClip("Assets/Characters/Player/Animations/Anim_Idle.fbx");
        sm.defaultState = idle;

        var walk = sm.AddState("Walk");
        walk.motion = LoadClip("Assets/Characters/Player/Animations/Anim_Walk.fbx");

        var run = sm.AddState("Run");
        run.motion = LoadClip("Assets/Characters/Player/Animations/Anim_Run.fbx");

        var rise = sm.AddState("Rise");
        rise.motion = LoadClip("Assets/Characters/Player/Animations/Anim_Jump.fbx");

        var fall = sm.AddState("Fall");
        fall.motion = LoadClip("Assets/Characters/Player/Animations/Anim_JumpDown.fbx");

        // Idle ↔ Walk
        FloatTransition(idle, walk, "Speed", AnimatorConditionMode.Greater, 1f,  0.05f);
        FloatTransition(walk, idle, "Speed", AnimatorConditionMode.Less,    1f,  0.05f);

        // Walk ↔ Run
        FloatTransition(walk, run,  "Speed", AnimatorConditionMode.Greater, 14f, 0.1f);
        FloatTransition(run,  walk, "Speed", AnimatorConditionMode.Less,    14f, 0.1f);

        // Ground states → Rise when not grounded
        BoolTransition(idle, rise, "IsGrounded", false, 0.05f);
        BoolTransition(walk, rise, "IsGrounded", false, 0.05f);
        BoolTransition(run,  rise, "IsGrounded", false, 0.05f);

        // Rise → Fall once velocity goes negative
        FloatTransition(rise, fall, "VerticalVelocity", AnimatorConditionMode.Less, 0f, 0.1f);

        // Both air states → Idle on landing
        BoolTransition(rise, idle, "IsGrounded", true,  0.15f);
        BoolTransition(fall, idle, "IsGrounded", true,  0.15f);

        AssetDatabase.SaveAssets();
        Debug.Log("PlayerAnimator controller created: Idle / Walk / Run / Rise / Fall");
    }

    static void FloatTransition(AnimatorState from, AnimatorState to, string param, AnimatorConditionMode mode, float threshold, float duration)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration    = duration;
        t.AddCondition(mode, threshold, param);
    }

    static void BoolTransition(AnimatorState from, AnimatorState to, string param, bool isTrue, float duration)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration    = duration;
        t.AddCondition(isTrue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, param);
    }

    static AnimationClip LoadClip(string path)
    {
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
            if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;
        return null;
    }
}
