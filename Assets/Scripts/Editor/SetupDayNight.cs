using UnityEngine;
using UnityEditor;

// Wires the DayNightCycle onto the scene's Directional Light, plus quick editor toggles
// so you can flip the scene between day and night while dressing (no Play needed).
public static class SetupDayNight
{
    [MenuItem("WildReach/Setup Day-Night Cycle")]
    static void Setup()
    {
        var sun = FindSun();
        if (sun == null) { Debug.LogError("[DayNight] No Directional Light in the scene."); return; }

        var dnc = sun.GetComponent<DayNightCycle>();
        if (dnc == null) dnc = sun.gameObject.AddComponent<DayNightCycle>();

        var moon = GetOrMakeMoonLight();
        var disc = GetOrMakeMoonDisc();

        var so = new SerializedObject(dnc);
        so.FindProperty("sun").objectReferenceValue = sun;
        so.FindProperty("moon").objectReferenceValue = moon;
        so.FindProperty("moonDisc").objectReferenceValue = disc.transform;
        so.FindProperty("dayLengthSeconds").floatValue = 1800f;        // 30-min cycle; 720 was too fast
        // Apply the tuned night values (re-running Setup re-applies these).
        so.FindProperty("moonIntensity").floatValue = 0.28f;
        so.FindProperty("nightAmbient").colorValue  = new Color(0.11f, 0.13f, 0.21f);
        so.FindProperty("nightFog").colorValue      = new Color(0.07f, 0.09f, 0.16f);
        so.FindProperty("dayFog").colorValue        = new Color(0.74f, 0.82f, 0.90f);  // cool haze, not orange
        so.ApplyModifiedProperties();

        // thinner fog so the ~1km island's mountains read clearly (the infinite ocean hides the edge now)
        RenderSettings.fog = true;
        RenderSettings.fogDensity = 0.0022f;

        Selection.activeGameObject = sun.gameObject;
        EditorUtility.SetDirty(dnc);
        Debug.Log("[DayNight] Cycle + Moonlight + Moon disc wired. " +
                  "Play: N = toggle day/night, P = pause/resume. Editor: WildReach/Time - Day or Night. " +
                  "Flip to Night to see moonlight + glowing moon.");
    }

    [MenuItem("WildReach/Time - Day")]
    static void Day()
    {
        var dnc = Object.FindAnyObjectByType<DayNightCycle>();
        if (dnc == null) { Debug.LogError("[DayNight] Run Setup Day-Night Cycle first."); return; }
        dnc.SetDay(); EditorUtility.SetDirty(dnc);
    }

    [MenuItem("WildReach/Time - Night")]
    static void Night()
    {
        var dnc = Object.FindAnyObjectByType<DayNightCycle>();
        if (dnc == null) { Debug.LogError("[DayNight] Run Setup Day-Night Cycle first."); return; }
        dnc.SetNight(); EditorUtility.SetDirty(dnc);
    }

    static Light FindSun()
    {
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l.gameObject.name != "Moonlight") return l;
        return null;
    }

    static Light GetOrMakeMoonLight()
    {
        var go = GameObject.Find("Moonlight");
        if (go == null)
        {
            go = new GameObject("Moonlight");
            var nl = go.AddComponent<Light>();
            nl.type = LightType.Directional;
            nl.color = new Color(0.6f, 0.72f, 1f);
            nl.intensity = 0f;                 // DayNightCycle drives this
            go.transform.rotation = Quaternion.Euler(55f, 150f, 0f);
        }
        var l = go.GetComponent<Light>();
        l.shadows = LightShadows.None;         // soft fill, no harsh moon shadow on the tree
        return l;
    }

    static Transform GetOrMakeMoonDisc()
    {
        // Prefer the textured billboard from "Setup Sky Bodies" if it exists — that's the real moon image. Only
        // fall back to the plain emissive sphere if no billboard has been built yet.
        var billboard = GameObject.Find("MoonDisc");
        if (billboard != null) return billboard.transform;

        var go = GameObject.Find("Moon");
        if (go == null)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Moon";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * 50f;

            const string matPath = "Assets/Environment/Moon.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetColor("_BaseColor", new Color(0.85f, 0.9f, 1f));
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.8f, 0.88f, 1f) * 2.5f);   // soft glow + bloom halo
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
        return go.transform;
    }
}
