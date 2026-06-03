using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;

// Animation test harness: plays the currently-selected AnimationClip on the live Azure player so
// we can judge a new (e.g. mocap-retargeted) clip instantly. Because the player's avatar is
// Humanoid, any Humanoid clip auto-retargets onto it. Restores the real controller afterwards.
//
//   1. Import the mocap FBX as Humanoid.
//   2. Select its AnimationClip (or the FBX) in the Project window.
//   3. WildReach > Test Selected Clip On Player, then enter Play.
//   4. WildReach > Restore Player Animator to put the gameplay controller back.
public static class AnimTestHarness
{
    const string TestCtrl = "Assets/Characters/Player/_AnimTest.controller";
    const string RealCtrl  = "Assets/Characters/Player/PlayerAnimator.controller";
    const string PrefKey   = "WR_AnimTest_OrigCtrl";

    [MenuItem("WildReach/Test Selected Clip On Player")]
    static void Test()
    {
        var clip = SelectedClip();
        if (clip == null) { Debug.LogError("[AnimTest] Select a Humanoid AnimationClip (or its FBX) in the Project window first."); return; }

        var animator = HumanoidAnimator();
        if (animator == null) { Debug.LogError("[AnimTest] No Humanoid Animator found in the scene (is the Azure player present?)."); return; }

        // remember the current controller so we can restore it
        var cur = animator.runtimeAnimatorController;
        if (cur != null && AssetDatabase.GetAssetPath(cur) != TestCtrl)
            SessionState.SetString(PrefKey, AssetDatabase.GetAssetPath(cur));

        var ctrl = AnimatorController.CreateAnimatorControllerAtPathWithClip(TestCtrl, clip);
        animator.runtimeAnimatorController = ctrl;
        EditorUtility.SetDirty(animator);
        Debug.Log($"[AnimTest] Playing '{clip.name}' on '{animator.name}'. Enter Play to view; isHuman={animator.avatar?.isHuman}. Restore when done.");
    }

    [MenuItem("WildReach/Restore Player Animator")]
    static void Restore()
    {
        var animator = HumanoidAnimator();
        if (animator == null) return;
        string path = SessionState.GetString(PrefKey, RealCtrl);
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path)
                   ?? AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(RealCtrl);
        animator.runtimeAnimatorController = ctrl;
        EditorUtility.SetDirty(animator);
        Debug.Log($"[AnimTest] Restored controller: {(ctrl != null ? ctrl.name : "none found")}");
    }

    static Animator HumanoidAnimator()
    {
        return Object.FindObjectsByType<Animator>(FindObjectsSortMode.None)
            .FirstOrDefault(a => a.avatar != null && a.avatar.isHuman)
            ?? Object.FindFirstObjectByType<Animator>();
    }

    static AnimationClip SelectedClip()
    {
        if (Selection.activeObject is AnimationClip c) return c;
        // selected the FBX: grab its first real clip
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(a => !a.name.StartsWith("__preview"));
    }
}
