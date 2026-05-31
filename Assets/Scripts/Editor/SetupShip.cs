using UnityEngine;
using UnityEditor;

// Drops the Kraken's Galleon into the scene at the coast, floating at sea level, upright and textured.
//
// Structure (important for sailing): a LEVEL control root "Ship" (world-aligned axes) with the visual
// FBX as a child "ShipModel". The Meshy FBX needs a -90° upright tilt to stand up, which would tumble
// the control axes if baked onto the root — so the tilt lives ONLY on the visual child. The root steers
// and moves cleanly; the model just rides along.
//
// Re-runnable: replaces any existing "Ship". Static landmark until you board it (Setup Sailing wires that).
public static class SetupShip
{
    const string Dir          = "Assets/Environment/Ship/";
    const string FbxPath      = Dir + "Meshy_AI_Kraken_s_Galleon_0531001619_texture.fbx";
    const string BaseTexPath  = Dir + "Meshy_AI_Kraken_s_Galleon_0531001619_texture.png";
    const string MatPath      = Dir + "ShipMat.mat";
    const float  TargetHeight = 12f;   // metres, mast-tip to keel — a small ship next to the 2m character
    const float  Draft        = 2.5f;  // how far the ship sinks below the surface (sits IN the water, not on it)
    const float  SpawnRadius  = 82f;   // metres from island centre — just off the coast, short swim to reach

    // Meshy FBXs import on their side. This stands the MODEL upright (visual only — not on the control root).
    // If it's upside-down, flip X to +90; if still on its side, try Z = 90.
    static readonly Quaternion Upright = Quaternion.Euler(-90f, 0f, 0f);
    const float Yaw      = -90f;  // control-root facing (level, around world up)
    const float ModelYaw = 0f;    // base only — live nose alignment is the ShipController.modelYaw field (scrub it in the Inspector)

    [MenuItem("WildReach/Setup Ship")]
    static void Setup()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (prefab == null)
        {
            Debug.LogError($"[Ship] FBX not found at {FbxPath}. Click back into Unity so it imports the Ship folder, then re-run.");
            return;
        }

        var existing = GameObject.Find("Ship");
        if (existing != null) Object.DestroyImmediate(existing);

        // level control root
        var root = new GameObject("Ship");

        // visual model as a child, stood upright (tilt stays off the root)
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        model.name = "ShipModel";
        model.transform.SetParent(root.transform, false);
        model.transform.localRotation = Quaternion.Euler(0f, ModelYaw, 0f) * Upright;  // upright, nose spun by ModelYaw
        model.transform.localScale    = Vector3.one;

        // normalize size from the model's renderer bounds (taken AFTER the upright, robust to Meshy's 50x quirk)
        var rends = model.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            if (b.size.y > 0.0001f) model.transform.localScale *= TargetHeight / b.size.y;
        }
        else Debug.LogWarning("[Ship] No renderers found on the FBX — check the import.");

        // place + face the ROOT (clean, level)
        var c = SetupWater.IslandCenter();
        root.transform.position = new Vector3(c.x + 110f, SetupWater.SeaLevel - Draft, c.z);
        root.transform.rotation = Quaternion.Euler(0f, Yaw, 0f);

        AssignWoodMaterial(rends);

        Selection.activeGameObject = root;
        SceneView.FrameLastActiveSceneView();
        Debug.Log($"[Ship] Kraken's Galleon placed off the coast (root level + visual model upright), height ~{TargetHeight}m, " +
                  $"textured. Run Setup Sailing to make it climbable + sailable. If it later sails sideways, bump ModelYaw.");
    }

    // Builds (once) a URP/Lit material from Meshy's base colour map and assigns it to every submesh,
    // so the ship reads as wood instead of importing grey. Glow (emit) is a later PIL-mask pass.
    static void AssignWoodMaterial(Renderer[] rends)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) { Debug.LogWarning("[Ship] URP/Lit not found — left grey."); return; }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, MatPath); }
        else mat.shader = shader;

        var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseTexPath);
        if (baseTex != null) { mat.SetTexture("_BaseMap", baseTex); mat.SetColor("_BaseColor", Color.white); }
        else Debug.LogWarning($"[Ship] Base texture not found at {BaseTexPath}.");

        mat.SetFloat("_Smoothness", 0.2f);   // wood, not glossy
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        foreach (var r in rends)
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }
    }
}
