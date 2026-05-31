using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// Imports our Blender-authored Azure_Jump.fbx (JumpUp/Falling/Land) as Humanoid clips retargeted to the
// Azure avatar, loops only the airborne clip, and wires a jump sub-machine into Azure.controller:
//   AnyState --Jump trigger--> JumpUp --VertVel<0--> Falling --IsGrounded--> Land --(exit)--> locomotion.
// PlayerAnimator already feeds Jump / VerticalVelocity / IsGrounded. Idempotent.
public static class SetupJumpAnim
{
    const string Dir      = "Assets/Characters/AzureRenegade/";
    const string CtrlPath = Dir + "Azure.controller";
    const string JumpFbx  = Dir + "Azure_Jump.fbx";
    const string RefClip  = Dir + "Azure_Walk.fbx";   // copy its avatar setup

    [MenuItem("WildReach/Setup Jump Anim")]
    static void Setup()
    {
        var refImp = AssetImporter.GetAtPath(RefClip) as ModelImporter;
        Avatar src = refImp != null ? refImp.sourceAvatar : null;
        if (src == null)
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Dir + "Azure_Tpose.fbx"))
                if (o is Avatar av) src = av;
        if (src == null) { Debug.LogError("[Jump] No Azure source avatar found — can't retarget."); return; }

        // import Humanoid + retarget; loop ONLY the airborne 'Falling' clip
        var imp = AssetImporter.GetAtPath(JumpFbx) as ModelImporter;
        if (imp == null) { Debug.LogError($"[Jump] {JumpFbx} not found."); return; }
        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup   = ModelImporterAvatarSetup.CopyFromOther;
        imp.sourceAvatar  = src;
        var clips = imp.defaultClipAnimations;
        for (int i = 0; i < clips.Length; i++)
            clips[i].loopTime = clips[i].name.ToLower().Contains("falling");
        imp.clipAnimations = clips;
        imp.SaveAndReimport();

        AnimationClip up = null, fall = null, land = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(JumpFbx))
            if (o is AnimationClip c && !c.name.StartsWith("__"))
            {
                string n = c.name.ToLower();
                if (n.Contains("jumpup")) up = c;
                else if (n.Contains("falling")) fall = c;
                else if (n.Contains("land")) land = c;
            }
        if (up == null || fall == null || land == null)
            Debug.LogWarning($"[Jump] clips — up:{up?.name} fall:{fall?.name} land:{land?.name}. Check the FBX Animation tab.");

        var c0 = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
        if (c0 == null) { Debug.LogError($"[Jump] {CtrlPath} not found."); return; }
        Param(c0, "Jump", AnimatorControllerParameterType.Trigger);
        Param(c0, "VerticalVelocity", AnimatorControllerParameterType.Float);
        Param(c0, "IsGrounded", AnimatorControllerParameterType.Bool);

        var sm = c0.layers[0].stateMachine;
        if (FindState(sm, "JumpUp") == null)
        {
            var sUp = sm.AddState("JumpUp");   sUp.motion = up;
            var sFa = sm.AddState("Falling");  sFa.motion = fall;
            var sLa = sm.AddState("Land");     sLa.motion = land;

            var toUp = sm.AddAnyStateTransition(sUp);
            toUp.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            toUp.duration = 0.06f; toUp.hasExitTime = false; toUp.canTransitionToSelf = false;

            var upFa = sUp.AddTransition(sFa);
            upFa.AddCondition(AnimatorConditionMode.Less, 0f, "VerticalVelocity");
            upFa.duration = 0.1f; upFa.hasExitTime = false;

            var faLa = sFa.AddTransition(sLa);
            faLa.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
            faLa.duration = 0.08f; faLa.hasExitTime = false;

            var laOut = sLa.AddTransition(sm.defaultState);
            laOut.hasExitTime = true; laOut.exitTime = 0.7f; laOut.duration = 0.15f;
        }
        EditorUtility.SetDirty(c0); AssetDatabase.SaveAssets();
        Debug.Log("[Jump] Authored jump wired into Azure.controller (JumpUp→Falling→Land). Press Space in Play.");
    }

    static void Param(AnimatorController c, string n, AnimatorControllerParameterType t)
    { foreach (var p in c.parameters) if (p.name == n) return; c.AddParameter(n, t); }
    static AnimatorState FindState(AnimatorStateMachine sm, string n)
    { foreach (var s in sm.states) if (s.state.name == n) return s.state; return null; }
}
