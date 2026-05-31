using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// One-click: take the rigged Tortoise.fbx and turn it into a textured, animated, roaming creature.
//  - sets the FBX rig to Generic (so the Walk/Idle clips play)
//  - builds a URP/Lit material from the Meshy base map (imports grey otherwise)
//  - builds an Idle↔Walk Animator driven by a "Speed" float
//  - drops one on the island (on dry land) with a CreatureWander
// Re-runnable: replaces any existing "Tortoise" in the scene.
public static class SetupCreature
{
    const string Dir      = "Assets/Environment/Creatures/Tortoise/";
    const string FbxPath  = Dir + "Tortoise.fbx";
    const string BaseTex  = Dir + "Meshy_AI_Ember_Shell_Tortoise_0531023907_texture.png";
    const string MatPath  = Dir + "TortoiseMat.mat";
    const string CtrlPath = Dir + "Tortoise.controller";

    [MenuItem("WildReach/Setup Creature")]
    static void Setup()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (prefab == null) { Debug.LogError($"[Creature] {FbxPath} not found."); return; }

        // 1. rig → Generic, and force every imported clip to LOOP (FBX clips import non-looping,
        //    so Walk/Idle would play once and freeze — which reads as "no animation").
        var imp = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (imp != null)
        {
            if (imp.animationType != ModelImporterAnimationType.Generic ||
                imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                imp.animationType = ModelImporterAnimationType.Generic;
                imp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;  // generate the avatar the clips need
                imp.SaveAndReimport();
                imp = AssetImporter.GetAtPath(FbxPath) as ModelImporter;   // refetch so takes are visible
            }
            var clips = imp.defaultClipAnimations;
            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
                if (!clips[i].loopTime) { clips[i].loopTime = true; changed = true; }
            if (changed) { imp.clipAnimations = clips; imp.SaveAndReimport(); }
            Debug.Log($"[Creature] FBX clips: {string.Join(", ", System.Array.ConvertAll(clips, c => c.name))} (set to loop).");
        }

        // 2. material from the base map
        var mat = BuildMaterial();

        // 3. Animator controller (Idle ↔ Walk by "Speed")
        var controller = BuildController();

        // 4. (re)place in the scene
        var existing = GameObject.Find("Tortoise");
        if (existing != null) Object.DestroyImmediate(existing);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = "Tortoise";

        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }

        var anim = go.GetComponent<Animator>();
        if (anim == null) anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;

        // assign the avatar the Generic clips retarget onto (this is what was missing → no playback)
        Avatar avatar = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            if (a is Avatar av) avatar = av;
        if (avatar != null) anim.avatar = avatar;
        else Debug.LogWarning("[Creature] No Avatar found in the FBX — animation won't play. Check the Rig tab.");

        // solid body (computed now, while rotation is still identity, so it fits) + kinematic Rigidbody
        // → the player can't walk through it, and other physics treats it as a real obstacle.
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(b.center);
            box.size   = b.size * 0.85f;   // identity rotation here, so world size == local size
        }
        var crb = go.GetComponent<Rigidbody>();
        if (crb == null) crb = go.AddComponent<Rigidbody>();
        crb.isKinematic = true; crb.useGravity = false;

        // place on dry land near the island centre
        float sea = SeaLevel();
        Vector3 spot = LandSpotNearCentre(sea);
        go.transform.position = spot;
        go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

        var wander = go.GetComponent<CreatureWander>();
        if (wander == null) wander = go.AddComponent<CreatureWander>();
        wander.SetSeaLevel(sea);

        Selection.activeGameObject = go;
        SceneView.FrameLastActiveSceneView();
        Debug.Log($"[Creature] Ember Shell Tortoise placed at {spot} on the island, textured + wandering (sea={sea}). " +
                  $"Press Play — it should amble and idle. If it walks backwards, tell me and I flip its forward axis.");
    }

    static Material BuildMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, MatPath); }
        else mat.shader = shader;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseTex);
        if (tex != null) { mat.SetTexture("_BaseMap", tex); mat.SetColor("_BaseColor", Color.white); }
        mat.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets();
        return mat;
    }

    static AnimatorController BuildController()
    {
        AnimationClip walk = null, idle = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            if (a is AnimationClip c && !c.name.StartsWith("__"))
            {
                if (c.name.ToLower().Contains("walk")) walk = c;
                else if (c.name.ToLower().Contains("idle")) idle = c;
            }
        if (walk == null || idle == null)
            Debug.LogWarning($"[Creature] clips found — walk:{walk?.name} idle:{idle?.name}. Check the FBX Animation tab.");

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);

        bool hasSpeed = false;
        foreach (var p in controller.parameters) if (p.name == "Speed") hasSpeed = true;
        if (!hasSpeed) controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        var sm = controller.layers[0].stateMachine;
        AnimatorState idleSt = FindState(sm, "Idle"), walkSt = FindState(sm, "Walk");
        if (idleSt == null) { idleSt = sm.AddState("Idle"); idleSt.motion = idle; sm.defaultState = idleSt; }
        else idleSt.motion = idle;
        if (walkSt == null) { walkSt = sm.AddState("Walk"); walkSt.motion = walk; }
        else walkSt.motion = walk;

        if (!HasTransition(idleSt, walkSt))
        {
            var t = idleSt.AddTransition(walkSt);
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t.hasExitTime = false; t.duration = 0.25f;
        }
        if (!HasTransition(walkSt, idleSt))
        {
            var t = walkSt.AddTransition(idleSt);
            t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            t.hasExitTime = false; t.duration = 0.25f;
        }
        EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
        return controller;
    }

    static AnimatorState FindState(AnimatorStateMachine sm, string n)
    {
        foreach (var s in sm.states) if (s.state.name == n) return s.state;
        return null;
    }
    static bool HasTransition(AnimatorState from, AnimatorState to)
    {
        foreach (var t in from.transitions) if (t.destinationState == to) return true;
        return false;
    }

    static float SeaLevel()
    {
        var w = GameObject.Find("Water");
        return w != null ? w.transform.position.y : SetupWater.SeaLevel;
    }

    static Vector3 LandSpotNearCentre(float sea)
    {
        var t = Object.FindObjectOfType<Terrain>();
        Vector3 c = SetupWater.IslandCenter();
        if (t == null) return c;
        for (int i = 0; i < 40; i++)
        {
            Vector3 p = i == 0 ? c : c + new Vector3(Random.Range(-30f, 30f), 0, Random.Range(-30f, 30f));
            float h = t.SampleHeight(p) + t.transform.position.y;
            if (h > sea + 0.5f) return new Vector3(p.x, h, p.z);
        }
        return new Vector3(c.x, sea + 0.5f, c.z);
    }
}
