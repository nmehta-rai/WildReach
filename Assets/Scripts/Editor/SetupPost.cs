using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;

// Cinematic post stack on the scene's global Volume: bloom, warm grade, vignette, tonemapping.
public static class SetupPost
{
    [MenuItem("WildReach/Setup Cinematic Post")]
    static void Setup()
    {
        var vol = Object.FindObjectsByType<Volume>()
            .OrderByDescending(v => v.isGlobal).FirstOrDefault();
        if (vol == null) { Debug.LogError("[Post] No Volume in scene (need a Global Volume)."); return; }

        var p = vol.sharedProfile;
        if (p == null) { Debug.LogError("[Post] Volume has no profile asset."); return; }

        var bloom = Get<Bloom>(p);
        bloom.intensity.Override(1.1f);
        bloom.threshold.Override(0.85f);
        bloom.scatter.Override(0.6f);
        bloom.highQualityFiltering.Override(true);

        var ca = Get<ColorAdjustments>(p);
        ca.postExposure.Override(0.15f);
        ca.contrast.Override(8f);
        ca.saturation.Override(12f);                              // keep WildReach vivid
        ca.colorFilter.Override(new Color(1.0f, 0.95f, 0.86f));   // warm

        var wb = Get<WhiteBalance>(p);
        wb.temperature.Override(12f);                             // warm push

        var vig = Get<Vignette>(p);
        vig.intensity.Override(0.28f);
        vig.smoothness.Override(0.45f);

        var tm = Get<Tonemapping>(p);
        tm.mode.Override(TonemappingMode.Neutral);                // filmic but keeps colour

        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        Debug.Log("[Post] Cinematic stack applied: bloom + warm grade + vignette + tonemapping. " +
                  "Emissive wisps will now glow.");
    }

    static T Get<T>(VolumeProfile p) where T : VolumeComponent
    {
        if (!p.TryGet<T>(out var c)) c = p.Add<T>(true);
        c.active = true;
        return c;
    }
}
