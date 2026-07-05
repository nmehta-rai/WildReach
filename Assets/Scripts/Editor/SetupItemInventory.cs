using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

// Builds the torch held-mesh, renders clean icons for the stick + torch (from the actual meshes), and loads BOTH
// weapons into the player's WieldSystem so you start with both and switch (1/2 keys or click them in the pouch).
// Run after Setup Stick. Re-runnable. Does NOT touch the tuned HandGrip/BackHolster.
public static class SetupItemInventory
{
    const string IconDir = "Assets/UI/Inventory/";

    [MenuItem("WildReach/Items/Setup Weapons (start with both)")]
    static void Go()
    {
        // no more ground pickups — you start with both weapons; clear any leftover ground sticks
        foreach (var pk in Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None)) Object.DestroyImmediate(pk.gameObject);

        var stickPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Items/Stick/Stick.prefab");
        var torchPrefab = BuildMeshPrefab("Assets/Items/Torch/Torch.fbx", "Assets/Items/Torch/Torch.mat", "Assets/Items/Torch/Torch.prefab", 0.82f);

        var stickIcon = EnsureSprite(IconDir + "Stick.png");   // icons are pre-rendered (Blender) — just import as Sprite
        var torchIcon = EnsureSprite(IconDir + "Torch.png");

        var move = Object.FindFirstObjectByType<PlayerMovement>();
        var wield = move != null ? move.GetComponent<WieldSystem>() : null;
        if (wield == null) { Debug.LogError("[Weapons] No WieldSystem on the player — run 'Setup Stick' first."); return; }
        if (move.GetComponent<SpineAim>() == null) move.gameObject.AddComponent<SpineAim>();   // swing aims at the crosshair height

        // the stick's tuned grip/holster; give the torch its OWN copies to fine-tune (each mesh sits differently)
        var handGrip = FindDeep(move.transform, "HandGrip");
        var backHolster = FindDeep(move.transform, "BackHolster");
        var torchGrip = handGrip != null ? ClonePoint(handGrip, "TorchGrip") : null;
        var torchHolster = backHolster != null ? ClonePoint(backHolster, "TorchHolster") : null;
        MakePreview(torchGrip, torchPrefab); MakePreview(torchHolster, torchPrefab);
        var torchFire = CreateTorchFire(torchGrip);   // the lit flame — a scene object you can nudge by eye

        var so = new SerializedObject(wield);
        var arr = so.FindProperty("weapons");
        arr.arraySize = 2;
        SetWeapon(arr.GetArrayElementAtIndex(0), "Stick", stickIcon, stickPrefab, handGrip, backHolster, 1, null);
        SetWeapon(arr.GetArrayElementAtIndex(1), "Torch", torchIcon, torchPrefab, torchGrip, torchHolster, 1, torchFire);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(move.gameObject);

        Debug.Log("[Weapons] Stick + Torch loaded. To position the flame: search 'TorchFire' in the Hierarchy, select it, and drag it onto the torch's head — THIS is the real in-game fire now (it lights up only when the torch is drawn).");
    }

    static void SetWeapon(SerializedProperty p, string name, Sprite icon, GameObject prefab, Transform grip, Transform holster, int damage, GameObject fireVfx)
    {
        p.FindPropertyRelative("name").stringValue = name;
        p.FindPropertyRelative("icon").objectReferenceValue = icon;
        p.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        p.FindPropertyRelative("grip").objectReferenceValue = grip;
        p.FindPropertyRelative("holster").objectReferenceValue = holster;
        p.FindPropertyRelative("damage").intValue = damage;
        p.FindPropertyRelative("fireVfx").objectReferenceValue = fireVfx;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root) { var r = FindDeep(c, name); if (r != null) return r; }
        return null;
    }

    // a sibling point that starts as a copy of the stick's grip pose; if it already exists, keep its tuned pose
    static Transform ClonePoint(Transform src, string name)
    {
        var existing = src.parent.Find(name);
        if (existing != null) return existing;
        var t = new GameObject(name).transform; t.SetParent(src.parent, false);
        t.localPosition = src.localPosition; t.localRotation = src.localRotation; t.localScale = src.localScale;
        return t;
    }

    static void MakePreview(Transform point, GameObject prefab)
    {
        if (point == null) return;
        for (int i = point.childCount - 1; i >= 0; i--)
            if (point.GetChild(i).name.StartsWith("__Preview")) Object.DestroyImmediate(point.GetChild(i).gameObject);
        if (prefab == null) return;
        var pv = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(pv, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        pv.name = "__Preview";
        foreach (var m in pv.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(m);
        foreach (var c in pv.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        pv.transform.SetParent(point, false); pv.transform.localPosition = Vector3.zero; pv.transform.localRotation = Quaternion.identity;
        var ls = point.lossyScale; var hs = prefab.transform.localScale;
        pv.transform.localScale = new Vector3(ls.x != 0 ? hs.x / ls.x : hs.x, ls.y != 0 ? hs.y / ls.y : hs.y, ls.z != 0 ? hs.z / ls.z : hs.z);
    }

    // ---- lit torch: a flame + warm flickering light, as a TUNABLE scene object under the torch's hand grip.
    // The WieldSystem turns it on only while the torch is drawn. Re-run keeps the position you nudged.
    static GameObject CreateTorchFire(Transform torchGrip)
    {
        if (torchGrip == null) return null;
        for (int i = torchGrip.childCount - 1; i >= 0; i--)   // clear leftovers from the old baked-fire approach
        {
            var c = torchGrip.GetChild(i);
            if (c.name == "Fire" || c.name == "Torch") Object.DestroyImmediate(c.gameObject);
        }
        var existing = torchGrip.Find("TorchFire");
        if (existing != null) return existing.gameObject;   // keep the position you tuned

        var flameTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/VFX/Fire/FlameParticle1_I.jpg");
        var fire = new GameObject("TorchFire"); fire.transform.SetParent(torchGrip, false);
        var gl = torchGrip.lossyScale;                       // counter the bone scale so the flame is real-world sized
        fire.transform.localScale = new Vector3(gl.x != 0 ? 1f / gl.x : 1f, gl.y != 0 ? 1f / gl.y : 1f, gl.z != 0 ? 1f / gl.z : 1f);
        ConfigureFlame(fire, flameTex);
        var lightGO = new GameObject("Light"); lightGO.transform.SetParent(fire.transform, false);
        var lt = lightGO.AddComponent<Light>();
        lt.type = LightType.Point; lt.color = new Color(1f, 0.62f, 0.26f); lt.range = 7f; lt.intensity = 2.6f; lt.shadows = LightShadows.None;
        lightGO.AddComponent<TorchLight>();

        // seed it at the torch head (the preview torch's far end from the hand) so it starts close
        var prev = torchGrip.Find("__Preview");
        var rend = prev != null ? prev.GetComponentInChildren<Renderer>(true) : null;
        if (rend != null)
        {
            var b = rend.bounds; var s = b.size;
            int ax = (s.x >= s.y && s.x >= s.z) ? 0 : (s.y >= s.z ? 1 : 2);
            Vector3 emin = b.center; emin[ax] = b.min[ax];
            Vector3 emax = b.center; emax[ax] = b.max[ax];
            Vector3 gp = torchGrip.position;
            Vector3 head = Vector3.Distance(emin, gp) > Vector3.Distance(emax, gp) ? emin : emax;
            fire.transform.position = Vector3.Lerp(head, gp, 0.06f);
        }
        return fire;
    }

    static void ConfigureFlame(GameObject go, Texture2D tex)
    {
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 1.05f; main.startSpeed = 0.08f; main.startSize = 0.3f;   // long-lived, barely rising = a slow flame
        main.startColor = new Color(1f, 0.65f, 0.22f); main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = 40;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // respect the scale-counter on TorchFire
        var em = ps.emission; em.rateOverTime = 20f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.035f;   // tight to the tip
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local; vel.y = new ParticleSystem.MinMaxCurve(0.18f);
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.88f, 0.45f), 0f), new GradientColorKey(new Color(1f, 0.42f, 0.08f), 0.5f), new GradientColorKey(new Color(0.4f, 0.08f, 0f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        var col = ps.colorOverLifetime; col.enabled = true; col.color = new ParticleSystem.MinMaxGradient(grad);
        var sol = ps.sizeOverLifetime; sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.15f)));
        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard; psr.material = BuildFlameMaterial(tex);
    }

    static Material BuildFlameMaterial(Texture2D tex)
    {
        const string path = "Assets/VFX/Fire/Flame.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(mat, path); }
        if (tex != null) mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", new Color(1f, 0.72f, 0.32f, 1f));
        mat.SetFloat("_Surface", 1f);                              // transparent
        mat.SetFloat("_Blend", 2f);                                // additive
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.renderQueue = 3000;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Imports a pre-rendered icon PNG as a UI sprite and returns it.
    static Sprite EnsureSprite(string path)
    {
        if (!System.IO.File.Exists(path)) { Debug.LogWarning("[Weapons] icon missing: " + path); return null; }
        AssetDatabase.ImportAsset(path);
        if (AssetImporter.GetAtPath(path) is TextureImporter ti &&
            (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single || ti.mipmapEnabled))
        {
            ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---- a held-mesh prefab (material + scaled + re-pivoted to grip near one end), no pickup/collider ----
    static GameObject BuildMeshPrefab(string fbx, string matPath, string prefabPath, float targetLen)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null) { Debug.LogWarning("[Weapons] missing " + fbx); return null; }
        var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
        imp.animationType = ModelImporterAnimationType.None; imp.importCameras = imp.importLights = false; imp.SaveAndReimport();
        var mat = BuildMaterial(Path.GetDirectoryName(fbx).Replace('\\', '/'), matPath);

        var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
        model.transform.SetParent(root.transform, false);
        foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;

        var rend = root.GetComponentInChildren<Renderer>(true);
        if (rend != null) { var sz = rend.bounds.size; float longest = Mathf.Max(sz.x, sz.y, sz.z); if (longest > 0.0001f) root.transform.localScale *= targetLen / longest; }
        var rb = root.GetComponentInChildren<Renderer>(true).bounds; var s = rb.size;
        int ax = (s.x >= s.y && s.x >= s.z) ? 0 : (s.y >= s.z ? 1 : 2);
        Vector3 grip = rb.center; grip[ax] = rb.max[ax] - s[ax] * 0.12f;
        model.transform.position += (root.transform.position - grip);

        if (File.Exists(prefabPath)) AssetDatabase.DeleteAsset(prefabPath);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static Material BuildMaterial(string dir, string matPath)
    {
        string Find(System.Func<string, bool> ok) => Directory.GetFiles(dir, "*.png").Select(p => p.Replace('\\', '/'))
            .FirstOrDefault(p => ok(Path.GetFileName(p).ToLower()));
        var basePng = Find(n => !n.Contains("normal") && !n.Contains("metallic") && !n.Contains("rough") && !n.Contains("emit"));
        var normPng = Find(n => n.Contains("normal"));
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath) ?? new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (AssetDatabase.GetAssetPath(mat) == "") AssetDatabase.CreateAsset(mat, matPath);
        if (basePng != null) mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePng));
        if (normPng != null)
        {
            if (AssetImporter.GetAtPath(normPng) is TextureImporter ni && ni.textureType != TextureImporterType.NormalMap) { ni.textureType = TextureImporterType.NormalMap; ni.SaveAndReimport(); }
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normPng)); mat.EnableKeyword("_NORMALMAP");
        }
        mat.SetFloat("_Smoothness", 0.15f); EditorUtility.SetDirty(mat);
        return mat;
    }
}
