using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

// One-click setup for the first item (the stick): imports + materials the mesh, builds a ground-pickup prefab
// (collider + ItemPickup + sparkle), wires the player's WieldSystem with hand-grip + back-holster attach points
// on the skeleton, and drops a stick on the ground in front of the player. Re-runnable.
public static class SetupStickItem
{
    const string Fbx    = "Assets/Items/Stick/Stick.fbx";
    const string MatPath = "Assets/Items/Stick/Stick.mat";
    const string Prefab = "Assets/Items/Stick/Stick.prefab";
    const float  TargetLen = 1.0f;   // metres, longest axis

    [MenuItem("WildReach/Items/Setup Stick (pickup + wield)")]
    static void Go()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Fbx) == null) { Debug.LogError("[Stick] Stick.fbx missing."); return; }

        // 1. import the mesh (no rig) + build material
        var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        imp.animationType = ModelImporterAnimationType.None;
        imp.importCameras = imp.importLights = false;
        imp.SaveAndReimport();
        var mat = BuildMaterial();

        // 2. ground-pickup prefab: mesh + capsule collider + sparkle + ItemPickup
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx));
        var root = new GameObject("Stick");
        model.transform.SetParent(root.transform, false);
        foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;

        // scale so the longest axis ~ TargetLen
        var rend = root.GetComponentInChildren<Renderer>(true);
        if (rend != null)
        {
            var sz = rend.bounds.size; float longest = Mathf.Max(sz.x, sz.y, sz.z);
            if (longest > 0.0001f) root.transform.localScale *= TargetLen / longest;
        }
        // RE-PIVOT so the prefab origin sits at the GRIP POINT (12% from one end) — a held stick is then gripped
        // near the bottom, not the middle.
        var rb2 = root.GetComponentInChildren<Renderer>(true).bounds; var s2 = rb2.size;
        int ax = (s2.x >= s2.y && s2.x >= s2.z) ? 0 : (s2.y >= s2.z ? 1 : 2);
        Vector3 gripPt = rb2.center; gripPt[ax] = rb2.max[ax] - s2[ax] * 0.12f;   // grip near the FAR end -> bottom in hand
        model.transform.position += (root.transform.position - gripPt);
        var b = root.GetComponentInChildren<Renderer>(true).bounds;
        var col = root.AddComponent<CapsuleCollider>();
        col.direction = ax; col.height = s2[ax]; col.radius = 0.05f;
        col.center = root.transform.InverseTransformPoint(b.center);

        BuildSparkle(root.transform);
        root.AddComponent<ItemPickup>();

        if (File.Exists(Prefab)) AssetDatabase.DeleteAsset(Prefab);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        Object.DestroyImmediate(root);

        // 3. wire the player
        var move = Object.FindAnyObjectByType<PlayerMovement>();
        if (move == null) { Debug.LogError("[Stick] No player in scene."); return; }
        var player = move.gameObject;
        var wield = player.GetComponent<WieldSystem>() ?? player.AddComponent<WieldSystem>();

        var hand = FindBone(player.transform, "righthand");
        var back = FindBone(player.transform, "spine2") ?? FindBone(player.transform, "spine1") ?? FindBone(player.transform, "spine");
        var grip = GetOrMake(hand, "HandGrip", out bool gNew);
        var holster = GetOrMake(back, "BackHolster", out bool hNew);
        // only set the DEFAULT pose when newly created — never clobber a pose you've tuned by hand
        if (gNew && grip != null)    { grip.localPosition = Vector3.zero;                          grip.localRotation = Quaternion.Euler(-70f, 0f, 0f); }
        if (hNew && holster != null) { holster.localPosition = new Vector3(-0.13f, 0.16f, -0.14f); holster.localRotation = Quaternion.Euler(15f, 0f, 40f); }
        var so = new SerializedObject(wield);
        so.FindProperty("handGrip").objectReferenceValue = grip;
        so.FindProperty("backHolster").objectReferenceValue = holster;
        so.FindProperty("heldScale").vector3Value = prefab.transform.localScale;
        so.ApplyModifiedProperties();

        // edit-mode previews so the hold/holster can be tuned BY EYE (stripped at runtime by WieldSystem)
        MakePreview(grip, prefab); MakePreview(holster, prefab);
        if (player.GetComponentInChildren<CrosshairUI>() == null) player.AddComponent<CrosshairUI>();

        // 4. drop a stick on the ground in front of the player — clear any existing ground sticks first (no dupes)
        foreach (var oldPk in Object.FindObjectsByType<ItemPickup>())
            Object.DestroyImmediate(oldPk.gameObject);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Vector3 p = player.transform.position + player.transform.forward * 2f + Vector3.up * 2f;
        if (Physics.Raycast(p, Vector3.down, out var hit, 12f)) p = hit.point;
        inst.transform.position = p;
        inst.name = "Stick (Ground)";

        Selection.activeGameObject = inst; EditorUtility.SetDirty(player);
        Debug.Log("[Stick] Done. A preview stick is now in the hand + on the back (in edit mode). Select " +
                  "AzurePlayer/.../RightHand/HandGrip and BackHolster and nudge their ROTATION/position until the " +
                  "previews look right — that IS the held/sheathed pose. Then Play: E pickup, R sheathe, click swing, G drop.");
    }

    static Material BuildMaterial()
    {
        var dir = Path.GetDirectoryName(Fbx).Replace('\\', '/');
        string Find(System.Func<string, bool> ok) => Directory.GetFiles(dir, "*.png")
            .Select(p => p.Replace('\\', '/')).FirstOrDefault(p => ok(Path.GetFileName(p).ToLower()));
        var basePng = Find(n => !n.Contains("normal") && !n.Contains("metallic") && !n.Contains("rough") && !n.Contains("emit"));
        var normPng = Find(n => n.Contains("normal"));
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath) ?? new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (AssetDatabase.GetAssetPath(mat) == "") AssetDatabase.CreateAsset(mat, MatPath);
        if (basePng != null) mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePng));
        if (normPng != null)
        {
            if (AssetImporter.GetAtPath(normPng) is TextureImporter ni && ni.textureType != TextureImporterType.NormalMap)
            { ni.textureType = TextureImporterType.NormalMap; ni.SaveAndReimport(); }
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normPng)); mat.EnableKeyword("_NORMALMAP");
        }
        mat.SetFloat("_Smoothness", 0.15f); EditorUtility.SetDirty(mat);
        return mat;
    }

    static void BuildSparkle(Transform parent)
    {
        var go = new GameObject("Sparkle"); go.transform.SetParent(parent, false); go.transform.localPosition = Vector3.up * 0.1f;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.startLifetime = 1.1f; main.startSpeed = 0.25f; main.startSize = 0.045f;
        main.startColor = new Color(1f, 0.95f, 0.6f); main.maxParticles = 24; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 10f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.18f;
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.y = new ParticleSystem.MinMaxCurve(0.3f);
        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.9f, 0.5f), 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        var psr = ps.GetComponent<ParticleSystemRenderer>();
        psr.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { };
        psr.material.SetFloat("_Surface", 1f); psr.material.SetFloat("_Blend", 1f);
    }

    // A mesh-only stick parented to an attach point, correct size — so the hold/holster pose can be nudged in the
    // scene and seen live. Named "__Preview" so WieldSystem destroys it at runtime (no duplicate at play).
    static void MakePreview(Transform point, GameObject prefab)
    {
        if (point == null) return;
        for (int i = point.childCount - 1; i >= 0; i--)
            if (point.GetChild(i).name.StartsWith("__Preview")) Object.DestroyImmediate(point.GetChild(i).gameObject);
        var pv = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(pv, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        pv.name = "__Preview";
        var sp = pv.transform.Find("Sparkle"); if (sp != null) Object.DestroyImmediate(sp.gameObject);
        foreach (var m in pv.GetComponents<MonoBehaviour>()) Object.DestroyImmediate(m);
        foreach (var c in pv.GetComponents<Collider>())     Object.DestroyImmediate(c);
        pv.transform.SetParent(point, false);
        pv.transform.localPosition = Vector3.zero; pv.transform.localRotation = Quaternion.identity;
        var ls = point.lossyScale; var hs = prefab.transform.localScale;
        pv.transform.localScale = new Vector3(ls.x != 0 ? hs.x / ls.x : hs.x, ls.y != 0 ? hs.y / ls.y : hs.y, ls.z != 0 ? hs.z / ls.z : hs.z);
    }

    static Transform FindBone(Transform t, string contains)
    {
        if (t.name.ToLower().Contains(contains)) return t;
        foreach (Transform c in t) { var r = FindBone(c, contains); if (r != null) return r; }
        return null;
    }

    static Transform GetOrMake(Transform parent, string name, out bool created)
    {
        created = false;
        if (parent == null) return null;
        var existing = parent.Find(name);
        if (existing != null) return existing;
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        created = true;
        return go.transform;
    }

    // No-rebuild pickup speed-up: bumps the Pickup state's clip speed + matches the attach timing. Does NOT touch
    // the player visual, so your tuned HandGrip/BackHolster + the hold/swing stay exactly as they are.
    [MenuItem("WildReach/Items/Speed Up Pickup (no rebuild)")]
    static void SpeedUpPickup()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Characters/AzurePlayer/AzurePlayer.controller");
        if (ctrl != null)
        {
            foreach (var st in ctrl.layers[0].stateMachine.states)
                if (st.state.name == "Pickup") { st.state.speed = 1.55f; EditorUtility.SetDirty(ctrl); }
            AssetDatabase.SaveAssets();
        }
        var move = Object.FindAnyObjectByType<PlayerMovement>();
        var wield = move != null ? move.GetComponent<WieldSystem>() : null;
        if (wield != null)
        {
            var so = new SerializedObject(wield);
            var p = so.FindProperty("pickupAttachTime");   // field may be gone in the weapon-list WieldSystem
            if (p != null) { p.floatValue = 0.36f; so.ApplyModifiedProperties(); }
        }
        Debug.Log("[Stick] Pickup sped up (clip 1.55x) — no rebuild, your grip/swing untouched.");
    }

    // No-rebuild swing speed-up: the swing clip plays much faster so the hit connects quickly.
    [MenuItem("WildReach/Items/Speed Up Swing (no rebuild)")]
    static void SpeedUpSwing()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Characters/AzurePlayer/AzurePlayer.controller");
        if (ctrl == null) { Debug.LogError("[Swing] AzurePlayer.controller not found."); return; }
        bool done = false;
        foreach (var st in ctrl.layers[0].stateMachine.states)
            if (st.state.name == "Swing") { st.state.speed = 2.0f; EditorUtility.SetDirty(ctrl); done = true; }
        AssetDatabase.SaveAssets();
        Debug.Log(done ? "[Swing] Swing sped up to 2.0x (no rebuild)." : "[Swing] No 'Swing' state found in the controller.");
    }
}
