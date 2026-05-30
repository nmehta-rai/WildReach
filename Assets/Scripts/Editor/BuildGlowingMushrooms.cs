using System.Linq;
using UnityEngine;
using UnityEditor;

// Turns the mushroom models into glowing prefabs you place by hand:
//   - boosts the cap emission to HDR so it blooms (glows in the dark, washes out in daylight)
//   - parents a soft warm Point Light inside each so it casts a glow-pool on the ground at night
// Output: EmbercapCluster_Glow.prefab / EmbercapGrove_Glow.prefab — drag THESE into the scene.
// Re-runnable. Tune Glow* constants (or edit the GlowLight on a prefab) and re-run.
public static class BuildGlowingMushrooms
{
    const string Dir = "Assets/Environment/Mushrooms/";

    // Point-light glow (shadows OFF so many of them stay cheap).
    static readonly Color GlowColor = new Color(1f, 0.6f, 0.32f); // warm amber ember
    const float GlowIntensity = 4f;
    const float GlowRange     = 5f;
    const float GlowHeight    = 0.5f;   // up from the base, near the caps
    const float EmissionBoost = 0.7f;   // subtle self-glow; the dark + point light do the drama

    struct M { public string name, fbx, mat, baseTex; }
    static readonly M[] Mush =
    {
        new M{ name="EmbercapCluster", fbx=Dir+"EmbercapCluster/EmbercapCluster.fbx", mat=Dir+"EmbercapCluster/EmbercapCluster.mat", baseTex=Dir+"EmbercapCluster/Cluster_Base.png" },
        new M{ name="EmbercapGrove",   fbx=Dir+"EmbercapGrove/EmbercapGrove.fbx",     mat=Dir+"EmbercapGrove/EmbercapGrove.mat",     baseTex=Dir+"EmbercapGrove/Grove_Base.png" },
    };

    [MenuItem("WildReach/Build Glowing Mushrooms")]
    static void Build()
    {
        foreach (var m in Mush)
        {
            // Emit the ALBEDO (Meshy's emit map is empty/black, so it produced no glow).
            // Driving emission from the base texture makes the caps self-illuminate + bloom.
            var mat = AssetDatabase.LoadAssetAtPath<Material>(m.mat);
            if (mat != null)
            {
                var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(m.baseTex);
                mat.EnableKeyword("_EMISSION");
                if (baseTex) mat.SetTexture("_EmissionMap", baseTex);
                mat.SetColor("_EmissionColor", Color.white * EmissionBoost);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                EditorUtility.SetDirty(mat);
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(m.fbx);
            if (model == null) { Debug.LogError($"[Mush] Missing {m.fbx} — run Setup Mushrooms first."); continue; }

            var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // Soft warm glow light inside the cluster.
            var lightGO = new GameObject("GlowLight");
            lightGO.transform.SetParent(root.transform, false);
            lightGO.transform.localPosition = new Vector3(0f, GlowHeight, 0f);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = GlowColor;
            light.intensity = GlowIntensity;
            light.range = GlowRange;
            light.shadows = LightShadows.None;   // keep cheap when many are placed

            string prefabPath = Dir + m.name + "/" + m.name + "_Glow.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[Mush] Built {m.name}_Glow.prefab (emissive caps + warm point light).");
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Mush] Drag the *_Glow prefabs from Assets/Environment/Mushrooms into the scene. " +
                  "Glow only reads in shade/night — test by lowering the sun (WildReach/Setup Golden Hour rotates it).");
    }
}
