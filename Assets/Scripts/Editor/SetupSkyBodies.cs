using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

// Builds the 2D sun + moon billboards from textures and wires them into the DayNightCycle. Drop your two MJ
// textures at Assets/Environment/Sky/Sun.png and Moon.png (black background — additive blend makes the black
// vanish and bloom haloes the bright disc), then run this. Re-runnable.
public static class SetupSkyBodies
{
    const string Dir = "Assets/Environment/Sky";
    const string SunTex = Dir + "/Sun.png";
    const string MoonTex = Dir + "/Moon.png";

    [MenuItem("WildReach/World/Setup Sky Bodies (moon)")]
    static void Setup()
    {
        var dnc = Object.FindAnyObjectByType<DayNightCycle>();
        if (dnc == null) { Debug.LogError("[Sky] Run 'Setup Day-Night Cycle' first."); return; }

        var moon = GameObject.Find("Moonlight");

        // NO sun billboard — the default lit sky reads better for daytime. Remove any existing SunDisc + unwire it.
        var oldSun = GameObject.Find("SunDisc");
        if (oldSun != null) Object.DestroyImmediate(oldSun);

        var moonDisc = moon != null ? BuildBody("MoonDisc", MoonTex, new Color(0.7f, 0.8f, 1f), 120f, moon.transform) : null;

        var so = new SerializedObject(dnc);
        so.FindProperty("sunDisc").objectReferenceValue = null;        // sun uses the default lighting, no disc
        if (moonDisc != null) so.FindProperty("moonDisc").objectReferenceValue = moonDisc.transform;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(dnc);

        Debug.Log("[Sky] Moon billboard built; sun uses the default sky (no disc). Scrub Time Of Day or Play: " +
                  "the moon rides the night sky. Tune size by scaling MoonDisc; tune glow via its material _BaseColor (HDR).");
    }

    static GameObject BuildBody(string name, string texPath, Color tint, float size, Transform light)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null)
            Debug.LogWarning($"[Sky] No texture at {texPath} — building '{name}' as a plain disc for now. " +
                             "Drop the MJ texture there (black background) and re-run for the real look.");

        var go = GameObject.Find(name) ?? GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        var col = go.GetComponent<Collider>(); if (col != null) Object.DestroyImmediate(col);
        go.transform.localScale = Vector3.one * size;

        // dedicated sky-body shader: alpha from the texture's brightness, so the black bg is fully transparent
        // (no square) and the disc stays solid; HDR tint so bloom haloes it. No URP surface/blend fiddling.
        var sky = Shader.Find("WildReach/SkyBody");
        var mat = new Material(sky != null ? sky : Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", Color.white);           // keep the texture's own colour; _Gain does the modest lift
        if (tex != null) mat.SetTexture("_BaseMap", tex);
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Environment", "Sky");
        var matPath = $"{Dir}/{name}.mat";
        AssetDatabase.DeleteAsset(matPath);
        AssetDatabase.CreateAsset(mat, matPath);
        AssetDatabase.ImportAsset(matPath, ImportAssetOptions.ForceUpdate);
        go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        var bb = go.GetComponent<SkyBillboard>() ?? go.AddComponent<SkyBillboard>();
        bb.sourceLight = light;
        bb.distance = 600f;
        return go;
    }
}
