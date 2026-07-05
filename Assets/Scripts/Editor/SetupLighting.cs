using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

// Sets a golden-hour / sunset mood: warm low sun, long shadows, warm ambient + fog.
// Tweak the two knobs noted below in the Directional Light after, to taste.
public static class SetupLighting
{
    [MenuItem("WildReach/Setup Golden Hour")]
    static void Setup()
    {
        var light = Object.FindObjectsByType<Light>()
            .FirstOrDefault(l => l.type == LightType.Directional);
        if (light == null) { Debug.LogError("[Light] No Directional Light found."); return; }

        // KNOB 1: sun warmth (colour).  KNOB 2: sun height (X rotation — lower = later/golden).
        light.color = new Color(1.0f, 0.78f, 0.52f);          // warm gold
        light.intensity = 1.15f;
        light.transform.rotation = Quaternion.Euler(16f, -35f, 0f); // low sun → long shadows
        light.shadows = LightShadows.Soft;
        light.colorTemperature = 4200f;

        // Warm ambient so shadows aren't cold blue.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.48f, 0.42f);

        // Cool atmospheric distance haze (aerial perspective), thin so the mountains read.
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.74f, 0.82f, 0.90f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0022f;

        EditorUtility.SetDirty(light);
        Debug.Log("[Light] Golden hour set. Tweak the Directional Light's rotation X (sun height) " +
                  "and colour (warmth) to taste — lower X + warmer = deeper sunset.");
    }
}
