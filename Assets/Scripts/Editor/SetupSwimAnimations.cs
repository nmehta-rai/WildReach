using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// Imports the three swim FBXs as Humanoid clips retargeted to the Azure avatar (looped), then adds a
// Swim state to Azure.controller: a 1D blend (Treading Water @0 → Swimming @1) driven by "SwimSpeed",
// entered from AnyState when "IsSwimming" is true and exited back to the default state when it's false.
// Idempotent — re-running won't duplicate the state or params.
public static class SetupSwimAnimations
{
    const string Dir        = "Assets/Characters/AzureRenegade/";
    const string CtrlPath   = Dir + "Azure.controller";
    const string TreadPath  = Dir + "Azure_Tread.fbx";
    const string SwimPath   = Dir + "Azure_Swim.fbx";
    const string RefClip    = Dir + "Azure_Walk.fbx";   // copy its avatar setup so retargeting matches

    [MenuItem("WildReach/Setup Swim Anim")]
    static void Setup()
    {
        // --- 1. match the avatar source the existing clips use ---
        var refImp = AssetImporter.GetAtPath(RefClip) as ModelImporter;
        Avatar src = refImp != null ? refImp.sourceAvatar : null;
        if (src == null)
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(Dir + "Azure_Tpose.fbx"))
                if (a is Avatar av) src = av;
        if (src == null) { Debug.LogError("[Swim] Couldn't find the Azure source avatar — clips won't retarget."); return; }

        ImportHumanoidLooped(TreadPath, src);
        ImportHumanoidLooped(SwimPath, src);
        ImportHumanoidLooped(Dir + "Azure_SwimToEdge.fbx", src);   // imported for later (climb-out); unused for now

        var tread = LoadClip(TreadPath);
        var swim  = LoadClip(SwimPath);
        if (tread == null || swim == null) { Debug.LogError("[Swim] Missing tread/swim clip after import."); return; }

        // --- 2. controller params ---
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
        if (controller == null) { Debug.LogError($"[Swim] {CtrlPath} not found."); return; }
        AddParam(controller, "IsSwimming", AnimatorControllerParameterType.Bool);
        AddParam(controller, "SwimSpeed",  AnimatorControllerParameterType.Float);

        var sm = controller.layers[0].stateMachine;

        // --- 3. Swim state (skip if it already exists) ---
        if (FindState(sm, "Swim") == null)
        {
            var bt = new BlendTree { name = "SwimBlend", blendType = BlendTreeType.Simple1D, blendParameter = "SwimSpeed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(bt, controller);
            bt.AddChild(tread, 0f);
            bt.AddChild(swim,  1f);

            var swimState = sm.AddState("Swim");
            swimState.motion = bt;

            var toSwim = sm.AddAnyStateTransition(swimState);
            toSwim.AddCondition(AnimatorConditionMode.If, 0, "IsSwimming");
            toSwim.duration = 0.15f; toSwim.hasExitTime = false; toSwim.canTransitionToSelf = false;

            var back = swimState.AddTransition(sm.defaultState);
            back.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSwimming");
            back.duration = 0.15f; back.hasExitTime = false;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[Swim] Swim anims imported + wired into Azure.controller (Treading Water ↔ Swimming by SwimSpeed). " +
                  "PlayerAnimator drives IsSwimming/SwimSpeed from SwimSystem.");
    }

    static void ImportHumanoidLooped(string path, Avatar src)
    {
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) { Debug.LogWarning($"[Swim] No importer for {path}"); return; }
        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup   = ModelImporterAvatarSetup.CopyFromOther;
        imp.sourceAvatar  = src;
        var clips = imp.defaultClipAnimations;
        for (int i = 0; i < clips.Length; i++) clips[i].loopTime = true;
        imp.clipAnimations = clips;
        imp.SaveAndReimport();
    }

    static AnimationClip LoadClip(string path)
    {
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
            if (a is AnimationClip c && !c.name.StartsWith("__")) return c;
        return null;
    }

    static void AddParam(AnimatorController c, string name, AnimatorControllerParameterType type)
    {
        foreach (var p in c.parameters) if (p.name == name) return;
        c.AddParameter(name, type);
    }

    static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        foreach (var s in sm.states) if (s.state.name == name) return s.state;
        return null;
    }
}
