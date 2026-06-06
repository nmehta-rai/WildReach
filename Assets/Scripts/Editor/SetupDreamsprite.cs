using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// One-click setup for the Lavender Dreamsprite: textured material (base + normal + glowing emit), Generic rig
// with looping Walk/Idle, an Idle<->Walk controller driven by "Speed", a passive ZoneRoamer, and a saved
// prefab in Assets/Creatures so it appears in every zone's Mob dropdown. Re-runnable.
public static class SetupDreamsprite
{
    const string Fbx      = "Assets/Characters/Dreamsprite/Dreamsprite_Rigged.fbx";
    const string TexDir   = "Assets/Characters/Dreamsprite/Tex/";
    const string MatPath  = "Assets/Characters/Dreamsprite/Dreamsprite_Body.mat";
    const string CtrlPath = "Assets/Characters/Dreamsprite/Dreamsprite.controller";
    const string Prefab   = "Assets/Creatures/Dreamsprite.prefab";

    [MenuItem("WildReach/Creatures/Setup Dreamsprite")]
    static void Setup()
    {
        AsNormalMap(TexDir + "Dreamsprite_Normal.png");
        var mat = BuildMaterial();

        var imp = AssetImporter.GetAtPath(Fbx) as ModelImporter;
        if (imp == null) { Debug.LogError("[Dreamsprite] Missing " + Fbx); return; }
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.importLights  = false; imp.importCameras = false;
        imp.SaveAndReimport();
        imp = AssetImporter.GetAtPath(Fbx) as ModelImporter;

        var clips = imp.defaultClipAnimations;                  // FBX clips import non-looping → loop them
        bool changed = false;
        for (int i = 0; i < clips.Length; i++) if (!clips[i].loopTime) { clips[i].loopTime = true; changed = true; }
        if (changed) { imp.clipAnimations = clips; imp.SaveAndReimport(); imp = AssetImporter.GetAtPath(Fbx) as ModelImporter; }

        var controller = BuildController();
        var model  = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Avatar>().FirstOrDefault();

        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        go.name = "Dreamsprite";
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }
        var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        if (avatar != null) anim.avatar = avatar;
        else Debug.LogWarning("[Dreamsprite] No Generic avatar found on the FBX — it will be static. Check the FBX Rig tab (Animation Type = Generic, Avatar = Create From This Model).");

        var roamer = go.GetComponent<ZoneRoamer>() ?? go.AddComponent<ZoneRoamer>();
        roamer.Configure(null, "Forrest", "Island1_Terrain", 3.5f);   // quick darty lizard pace (player walks ~3)

        if (!AssetDatabase.IsValidFolder("Assets/Creatures")) AssetDatabase.CreateFolder("Assets", "Creatures");
        PrefabUtility.SaveAsPrefabAsset(go, Prefab);
        Object.DestroyImmediate(go);                            // prefab is the deliverable; don't leave a loose one
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        Debug.Log($"[Dreamsprite] Saved {Prefab} — pick it from a zone's Mob dropdown in the Island Builder.");
    }

    static Material BuildMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath)
                  ?? new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (AssetDatabase.GetAssetPath(mat) == "") AssetDatabase.CreateAsset(mat, MatPath);
        var b = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "Dreamsprite_Base.png");
        var n = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "Dreamsprite_Normal.png");
        var e = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "Dreamsprite_Emit.png");
        if (b) mat.SetTexture("_BaseMap", b);
        if (n) { mat.SetTexture("_BumpMap", n); mat.EnableKeyword("_NORMALMAP"); }
        if (e) { mat.SetTexture("_EmissionMap", e); mat.SetColor("_EmissionColor", new Color(0.85f, 0.6f, 1f) * 1.1f); mat.EnableKeyword("_EMISSION"); mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
        mat.SetFloat("_Metallic", 0f); mat.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static AnimatorController BuildController()
    {
        AnimationClip walk = null, idle = null, run = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(Fbx))
            if (a is AnimationClip c && !c.name.StartsWith("__"))
            {
                var n = c.name.ToLower();
                if (n.Contains("walk")) walk = c;
                else if (n.Contains("idle")) idle = c;
                else if (n.Contains("run")) run = c;
            }
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath)
                   ?? AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        if (!ctrl.parameters.Any(p => p.name == "Speed")) ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var sm = ctrl.layers[0].stateMachine;
        var idleSt = State(sm, "Idle", idle); sm.defaultState = idleSt;
        var walkSt = State(sm, "Walk", walk);
        var runSt  = State(sm, "Run",  run);
        Link(idleSt, walkSt, AnimatorConditionMode.Greater, 0.5f);   // player walkSpeed=10, runSpeed=18
        Link(walkSt, idleSt, AnimatorConditionMode.Less,    0.5f);
        Link(walkSt, runSt,  AnimatorConditionMode.Greater, 14f);
        Link(runSt,  walkSt, AnimatorConditionMode.Less,    14f);
        EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets();
        return ctrl;
    }

    static AnimatorState State(AnimatorStateMachine sm, string name, AnimationClip clip)
    {
        var st = sm.states.FirstOrDefault(s => s.state.name == name).state ?? sm.AddState(name);
        st.motion = clip; return st;
    }
    static void Link(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold)
    {
        if (from.transitions.Any(t => t.destinationState == to)) return;
        var t = from.AddTransition(to); t.AddCondition(mode, threshold, "Speed");
        t.hasExitTime = false; t.duration = 0.18f;
    }

    static void AsNormalMap(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null && imp.textureType != TextureImporterType.NormalMap)
        { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
    }
}
