using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// Imports the rigged snow leopard (Generic rig), builds his skin material from the Meshy maps, and
// makes him the visual + body of the Predator (replacing the placeholder capsule). Animator is wired
// with his Generic avatar, ready for clips. Re-runnable.
public static class SetupLeopardPredator
{
    const string Fbx     = "Assets/Characters/Leopard/Leopard_Rigged.fbx";
    const string TexDir  = "Assets/Characters/Leopard/Tex/";
    const string MatPath  = "Assets/Characters/Leopard/Leopard_Body.mat";
    const string CtrlPath = "Assets/Characters/Leopard/Leopard.controller";
    const float  TargetHeight = 1.3f;

    // Consolidate the scene leopard into Assets/Creatures so it shows up in the zone Mob dropdown like the
    // tortoise. Saves the whole 'Predator' (AI + animated leopard) as a reusable prefab.
    [MenuItem("WildReach/Creatures/Save Leopard as Creature")]
    static void SaveLeopardCreature()
    {
        var pred = GameObject.Find("Predator");
        if (pred == null) { Debug.LogError("[Leopard] No 'Predator' in the scene to save."); return; }
        if (!AssetDatabase.IsValidFolder("Assets/Creatures")) AssetDatabase.CreateFolder("Assets", "Creatures");
        PrefabUtility.SaveAsPrefabAsset(pred, "Assets/Creatures/Leopard.prefab");
        Debug.Log("[Leopard] Saved Assets/Creatures/Leopard.prefab — pick it from a zone's Mob dropdown.");
    }

    // Animate the leopard that's ALREADY in the scene without re-instantiating or re-scaling him
    // (the full Setup re-runs auto-scale, which clobbered the manual size). Use this after re-baking clips.
    [MenuItem("WildReach/Combat/Wire Leopard Animation")]
    static void WireAnimation()
    {
        ImportRig();
        var controller = BuildController();
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Avatar>().FirstOrDefault();

        var pred = GameObject.Find("Predator");
        var vis  = pred != null ? pred.transform.Cast<Transform>().FirstOrDefault(t => t.name.Contains("Leopard")) : null;
        if (vis == null) { Debug.LogError("[Leopard] No 'Leopard' under a 'Predator' in the scene — run Setup Leopard Predator first."); return; }

        var animator = vis.GetComponent<Animator>() ?? vis.gameObject.AddComponent<Animator>();
        animator.avatar = avatar; animator.applyRootMotion = false;
        animator.runtimeAnimatorController = controller;
        EditorUtility.SetDirty(vis.gameObject);
        Debug.Log("[Leopard] Animation wired onto the existing leopard (transform untouched). Press Play — idle when still, walk/run when chasing, pounce on attack.");
    }

    // Shared FBX import: Generic rig, loop the cyclic clips (Pounce stays one-shot), remap to his material.
    static void ImportRig()
    {
        AsNormalMap(TexDir + "Leopard_Normal.png");
        var mat = BuildMaterial();
        var imp = AssetImporter.GetAtPath(Fbx) as ModelImporter;
        if (imp == null) { Debug.LogError("[Leopard] Missing " + Fbx); return; }
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        imp.importLights = false; imp.importCameras = false;
        imp.SaveAndReimport();
        imp = AssetImporter.GetAtPath(Fbx) as ModelImporter;

        var clips = imp.defaultClipAnimations;
        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            bool loop = !clips[i].name.ToLower().Contains("pounce");
            if (clips[i].loopTime != loop) { clips[i].loopTime = loop; changed = true; }
        }
        if (changed) { imp.clipAnimations = clips; imp.SaveAndReimport(); imp = AssetImporter.GetAtPath(Fbx) as ModelImporter; }
        Debug.Log($"[Leopard] clips: {string.Join(", ", clips.Select(c => c.name))}");
        foreach (var m in AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Material>())
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(m), mat);
        imp.SaveAndReimport();
    }

    [MenuItem("WildReach/Combat/Setup Leopard Predator")]
    static void Setup()
    {
        AsNormalMap(TexDir + "Leopard_Normal.png");
        var mat = BuildMaterial();

        var imp = AssetImporter.GetAtPath(Fbx) as ModelImporter;
        if (imp == null) { Debug.LogError("[Leopard] Missing " + Fbx); return; }
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        imp.importLights = false; imp.importCameras = false;
        imp.SaveAndReimport();
        imp = AssetImporter.GetAtPath(Fbx) as ModelImporter;   // refetch so the baked takes are visible

        // FBX clips import non-looping (play once then freeze = "no animation"). Loop the cyclic clips;
        // leave Pounce one-shot so the attack lunge plays once and recovers.
        var clips = imp.defaultClipAnimations;
        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            bool loop = !clips[i].name.ToLower().Contains("pounce");
            if (clips[i].loopTime != loop) { clips[i].loopTime = loop; changed = true; }
        }
        if (changed) { imp.clipAnimations = clips; imp.SaveAndReimport(); imp = AssetImporter.GetAtPath(Fbx) as ModelImporter; }
        Debug.Log($"[Leopard] clips: {string.Join(", ", clips.Select(c => c.name))}");

        foreach (var m in AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Material>())
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(m), mat);
        imp.SaveAndReimport();

        var controller = BuildController();
        var model  = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Avatar>().FirstOrDefault();

        // find or make the Predator root
        var pred = GameObject.Find("Predator");
        if (pred == null)
        {
            pred = new GameObject("Predator");
            var pm = Object.FindAnyObjectByType<PlayerMovement>();
            if (pm != null) pred.transform.position = pm.transform.position + pm.transform.forward * 60f;
        }
        foreach (var c in new Component[] { pred.GetComponent<MeshFilter>(), pred.GetComponent<MeshRenderer>() })
            if (c != null) Object.DestroyImmediate(c);
        var oldVis = pred.transform.Cast<Transform>().FirstOrDefault(t => t.name.Contains("Leopard"));
        if (oldVis != null) Object.DestroyImmediate(oldVis.gameObject);

        // leopard visual under the predator
        var vis = (GameObject)PrefabUtility.InstantiatePrefab(model);
        vis.name = "Leopard";
        vis.transform.SetParent(pred.transform, false);
        vis.transform.localPosition = Vector3.zero;
        vis.transform.localRotation = Quaternion.identity;
        vis.transform.localScale = Vector3.one;
        foreach (var l in vis.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);

        // normalise to ~1.3 m tall using the STABLE mesh bounds x current world scale (renderer.bounds
        // can be stale right after instantiate, which left him tiny last time)
        var skin = vis.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (skin != null && skin.sharedMesh != null)
        {
            float meshH  = skin.sharedMesh.bounds.size.y;
            float worldH = meshH * skin.transform.lossyScale.y;
            float f = worldH > 0.001f ? TargetHeight / worldH : 1f;
            vis.transform.localScale *= f;
            Debug.Log($"[Leopard] scale -> meshH={meshH:0.###} lossyY={skin.transform.lossyScale.y:0.####} worldH={worldH:0.###} factor={f:0.##} finalScale={vis.transform.localScale.x:0.##}");
        }
        else Debug.LogWarning("[Leopard] no SkinnedMeshRenderer found to scale.");

        var animator = vis.GetComponent<Animator>() ?? vis.AddComponent<Animator>();
        animator.avatar = avatar; animator.applyRootMotion = false;
        animator.runtimeAnimatorController = controller;   // Generic clips retarget onto the avatar above

        if (pred.GetComponent<Predator>() == null) pred.AddComponent<Predator>();
        var col = pred.GetComponent<CapsuleCollider>() ?? pred.AddComponent<CapsuleCollider>();
        if (skin != null && skin.sharedMesh != null)
        {
            var ls  = skin.transform.lossyScale;                 // reflects the new scale immediately
            var sz  = Vector3.Scale(skin.sharedMesh.bounds.size, ls);
            var ctr = skin.transform.TransformPoint(skin.sharedMesh.bounds.center);
            col.direction = sz.x >= sz.z ? 0 : 2;                // capsule along the longer horizontal axis
            col.height = Mathf.Max(sz.x, sz.z, 1f);
            col.radius = Mathf.Clamp(Mathf.Min(sz.x, sz.z) * 0.45f, 0.3f, 0.9f);
            col.center = pred.transform.InverseTransformPoint(ctr);
        }

        Selection.activeGameObject = pred;
        Debug.Log("[Leopard] Snow leopard is now the Predator. Press Play and approach. " +
                  "If he chases facing the wrong way, rotate the 'Leopard' child on Y until he faces forward — tell me the angle and I'll bake it in.");
    }

    // Idle ↔ Walk ↔ Run driven by the Predator's "Speed" float; Pounce fired from Any State by "Attack".
    static AnimatorController BuildController()
    {
        AnimationClip idle = null, walk = null, run = null, pounce = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(Fbx))
            if (a is AnimationClip c && !c.name.StartsWith("__"))
            {
                var n = c.name.ToLower();
                if (n.Contains("idle")) idle = c;
                else if (n.Contains("walk")) walk = c;
                else if (n.Contains("run")) run = c;
                else if (n.Contains("pounce")) pounce = c;
            }
        if (idle == null || walk == null || run == null)
            Debug.LogWarning($"[Leopard] clips — idle:{idle?.name} walk:{walk?.name} run:{run?.name} pounce:{pounce?.name}");

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath)
                   ?? AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        if (!ctrl.parameters.Any(p => p.name == "Speed"))  ctrl.AddParameter("Speed",  AnimatorControllerParameterType.Float);
        if (!ctrl.parameters.Any(p => p.name == "Attack")) ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        var sm = ctrl.layers[0].stateMachine;
        var idleSt = State(sm, "Idle", idle); sm.defaultState = idleSt;
        var walkSt = State(sm, "Walk", walk);
        var runSt  = State(sm, "Run",  run);
        var pounceSt = State(sm, "Pounce", pounce);

        Link(idleSt, walkSt, AnimatorConditionMode.Greater, 0.1f);
        Link(walkSt, idleSt, AnimatorConditionMode.Less,    0.1f);
        Link(walkSt, runSt,  AnimatorConditionMode.Greater, 3.5f);
        Link(runSt,  walkSt, AnimatorConditionMode.Less,    3.5f);

        // Attack: interrupt anything into Pounce, then auto-return when the one-shot finishes.
        if (!sm.anyStateTransitions.Any(t => t.destinationState == pounceSt))
        {
            var t = sm.AddAnyStateTransition(pounceSt);
            t.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            t.hasExitTime = false; t.duration = 0.05f; t.canTransitionToSelf = false;
        }
        if (!pounceSt.transitions.Any())
        {
            var t = pounceSt.AddTransition(idleSt);
            t.hasExitTime = true; t.exitTime = 0.85f; t.duration = 0.15f;
        }
        EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets();
        return ctrl;
    }

    static AnimatorState State(AnimatorStateMachine sm, string name, AnimationClip clip)
    {
        var st = sm.states.FirstOrDefault(s => s.state.name == name).state ?? sm.AddState(name);
        st.motion = clip;
        return st;
    }
    static void Link(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold)
    {
        if (from.transitions.Any(t => t.destinationState == to)) return;
        var t = from.AddTransition(to);
        t.AddCondition(mode, threshold, "Speed");
        t.hasExitTime = false; t.duration = 0.2f;
    }

    static Material BuildMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath)
                  ?? new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (AssetDatabase.GetAssetPath(mat) == "") AssetDatabase.CreateAsset(mat, MatPath);
        var b = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "Leopard_Base.png");
        var n = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "Leopard_Normal.png");
        var e = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "Leopard_Emit.png");
        if (b) mat.SetTexture("_BaseMap", b);
        if (n) { mat.SetTexture("_BumpMap", n); mat.EnableKeyword("_NORMALMAP"); }
        if (e) { mat.SetTexture("_EmissionMap", e); mat.SetColor("_EmissionColor", new Color(0.4f, 0.9f, 1f) * 1.3f); mat.EnableKeyword("_EMISSION"); mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
        mat.SetFloat("_Metallic", 0f); mat.SetFloat("_Smoothness", 0.2f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void AsNormalMap(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null && imp.textureType != TextureImporterType.NormalMap)
        { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
    }
}
