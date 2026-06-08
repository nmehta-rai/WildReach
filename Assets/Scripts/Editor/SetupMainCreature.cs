using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// General one-click setup for a MAIN (hand-rigged, CreatureForge) creature: select its FBX in the Project window
// and run. Auto-detects base/normal textures, sets Generic rig + avatar, loops the clips, builds an Idle (+Walk/Run
// IF those clips exist) controller driven by "Speed", adds a passive ZoneRoamer, and saves a prefab to
// Assets/Creatures so it shows in every zone's Mob dropdown AND the Pet picker. Re-runnable. One tool for all 12.
public static class SetupMainCreature
{
    // per-creature roam speed (m/s). Default if not listed. A bison is heavy/slow; a hare is quick.
    static readonly System.Collections.Generic.Dictionary<string, float> Speeds = new()
    {
        { "Thunderhoof", 1.0f }, { "Sloth", 0.5f }, { "Bear", 2.4f }, { "Fawn", 3.2f },
        { "Hare", 4.5f }, { "Mandrill", 2.6f }, { "Chameleon", 1.6f }, { "Leafwyrm", 2.0f },
    };

    [MenuItem("WildReach/Creatures/Setup Main Creature (from selected FBX)")]
    static void Setup()
    {
        var fbx = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(fbx) || !fbx.ToLower().EndsWith(".fbx"))
        { Debug.LogError("[MainCreature] Select the creature's .fbx in the Project window first."); return; }

        var dir  = Path.GetDirectoryName(fbx).Replace('\\', '/');
        var name = Path.GetFileNameWithoutExtension(fbx);

        var mat  = BuildMaterial(dir, name);

        var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.importLights  = false; imp.importCameras = false;
        imp.SaveAndReimport();
        imp = (ModelImporter)AssetImporter.GetAtPath(fbx);

        var clips = imp.defaultClipAnimations;                       // FBX clips import non-looping → loop them
        bool changed = false;
        for (int i = 0; i < clips.Length; i++) if (!clips[i].loopTime) { clips[i].loopTime = true; changed = true; }
        if (changed) { imp.clipAnimations = clips; imp.SaveAndReimport(); }

        var ctrl   = BuildController(fbx, dir, name);
        var model  = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();

        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        go.name = name;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }
        var anim = go.GetComponent<Animator>(); if (anim == null) anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        if (avatar != null) anim.avatar = avatar;
        else Debug.LogWarning("[MainCreature] No Generic avatar on the FBX — it will be static. Check the FBX Rig tab.");

        // solid body capsule so the player can't walk through the wild mob (the pet preview strips colliders by design)
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
            var cap = go.GetComponent<CapsuleCollider>();   // NOT `?? AddComponent` — GetComponent's fake-null defeats ??
            if (cap == null) cap = go.AddComponent<CapsuleCollider>();
            bool zLong = b.size.z >= b.size.x;                       // capsule runs along the body's length
            cap.direction = zLong ? 2 : 0;
            cap.height = (zLong ? b.size.z : b.size.x) * 0.85f;
            cap.radius = (zLong ? b.size.x : b.size.z) * 0.42f;
            cap.center = go.transform.InverseTransformPoint(b.center);
        }

        float speed = Speeds.TryGetValue(name, out var s) ? s : 2.0f;
        var roamer = go.GetComponent<ZoneRoamer>(); if (roamer == null) roamer = go.AddComponent<ZoneRoamer>();
        roamer.Configure(null, "Forrest", "Island1_Terrain", speed);

        if (!AssetDatabase.IsValidFolder("Assets/Creatures")) AssetDatabase.CreateFolder("Assets", "Creatures");
        var prefabPath = $"Assets/Creatures/{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Debug.Log($"[MainCreature] Saved {prefabPath} (roam {speed} m/s). Pick it from the Pet dropdown or a zone's Mob list.");
    }

    static Material BuildMaterial(string dir, string name)
    {
        var pngs = Directory.GetFiles(dir, "*.png").Select(p => p.Replace('\\', '/')).ToArray();
        string Find(System.Func<string, bool> ok) => pngs.FirstOrDefault(p => ok(Path.GetFileName(p).ToLower()));
        var basePng   = Find(n => !n.Contains("normal") && !n.Contains("metallic") && !n.Contains("rough") && !n.Contains("emit"));
        var normalPng = Find(n => n.Contains("normal"));

        var matPath = $"{dir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath) ?? new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (AssetDatabase.GetAssetPath(mat) == "") AssetDatabase.CreateAsset(mat, matPath);
        if (basePng != null) mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePng));
        if (normalPng != null)
        {
            if (AssetImporter.GetAtPath(normalPng) is TextureImporter ni && ni.textureType != TextureImporterType.NormalMap)
            { ni.textureType = TextureImporterType.NormalMap; ni.SaveAndReimport(); }
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPng)); mat.EnableKeyword("_NORMALMAP");
        }
        mat.SetFloat("_Metallic", 0f); mat.SetFloat("_Smoothness", 0.18f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static AnimatorController BuildController(string fbx, string dir, string name)
    {
        AnimationClip idle = null, walk = null, run = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbx))
            if (a is AnimationClip c && !c.name.StartsWith("__"))
            {
                var n = c.name.ToLower();
                if (n.Contains("idle")) idle = c;
                else if (n.Contains("walk")) walk = c;
                else if (n.Contains("run")) run = c;
            }
        idle ??= AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__"));

        var path = $"{dir}/{name}.controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
        if (!ctrl.parameters.Any(p => p.name == "Speed")) ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var sm = ctrl.layers[0].stateMachine;
        // clear stale states so re-runs are clean
        foreach (var st in sm.states.ToArray()) sm.RemoveState(st.state);

        var idleSt = sm.AddState("Idle"); idleSt.motion = idle; sm.defaultState = idleSt;
        // ONLY add Walk/Run if those clips exist — otherwise an idle-only creature would freeze in an empty Walk state
        AnimatorState walkSt = walk != null ? Add(sm, "Walk", walk) : null;
        AnimatorState runSt  = run  != null ? Add(sm, "Run",  run)  : null;
        if (walkSt != null)
        {
            Link(idleSt, walkSt, AnimatorConditionMode.Greater, 0.5f);
            Link(walkSt, idleSt, AnimatorConditionMode.Less,    0.5f);
            if (runSt != null) { Link(walkSt, runSt, AnimatorConditionMode.Greater, 14f); Link(runSt, walkSt, AnimatorConditionMode.Less, 14f); }
        }
        EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets();
        return ctrl;
    }

    static AnimatorState Add(AnimatorStateMachine sm, string name, AnimationClip clip)
    { var st = sm.AddState(name); st.motion = clip; return st; }

    static void Link(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold)
    {
        var t = from.AddTransition(to); t.AddCondition(mode, threshold, "Speed");
        t.hasExitTime = false; t.duration = 0.18f;
    }
}
