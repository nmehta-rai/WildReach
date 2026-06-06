using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

// Tools for the forest's background life.
//  - "Setup Filler Creature": turn the SELECTED low-poly FBX into a cheap filler prefab (textured + FillerCritter,
//    NO skeleton/Animator) saved to Assets/Creatures so it shows up in the zone Mob dropdown like a main actor.
//  - "Add Fireflies": drop a glowing-mote emitter into the scene.
public static class SetupFiller
{
    [MenuItem("WildReach/Creatures/Setup ALL Filler Creatures (Assets/Characters/Filler)")]
    static void SetupAllFillers()
    {
        var guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Characters/Filler" });
        int n = 0;
        foreach (var g in guids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (p.ToLower().EndsWith(".fbx")) { SetupOne(p); n++; }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Filler] Set up {n} filler creature prefab(s) in Assets/Creatures. Pick them from a zone's Mob dropdown.");
    }

    [MenuItem("WildReach/Creatures/Setup Filler Creature (from selected FBX)")]
    static void SetupFillerCreature()
    {
        var fbxPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(fbxPath) || !fbxPath.ToLower().EndsWith(".fbx"))
        { Debug.LogError("[Filler] Select the creature's .fbx in the Project window first."); return; }
        SetupOne(fbxPath);
    }

    static void SetupOne(string fbxPath)
    {
        var dir  = Path.GetDirectoryName(fbxPath).Replace('\\', '/');
        // clean name = the folder right under .../Filler/ (e.g. "ForestBeetle"), not the ugly Meshy filename
        var parts = fbxPath.Split('/');
        int fi = System.Array.IndexOf(parts, "Filler");
        var name = (fi >= 0 && fi + 1 < parts.Length) ? parts[fi + 1] : Path.GetFileNameWithoutExtension(fbxPath);

        // base texture = the png next to the FBX that isn't a normal/metallic/roughness/emit map
        var baseTex = Directory.GetFiles(dir, "*.png")
            .Select(p => p.Replace('\\', '/'))
            .FirstOrDefault(p => { var n = p.ToLower(); return !n.Contains("normal") && !n.Contains("metallic") && !n.Contains("roughness") && !n.Contains("emit"); });

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        var matPath = $"{dir}/{name}_Filler.mat";
        AssetDatabase.CreateAsset(mat, matPath);
        if (baseTex != null)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(baseTex);
            if (tex != null) { mat.SetTexture("_BaseMap", tex); mat.SetColor("_BaseColor", Color.white); }
        }
        mat.SetFloat("_Smoothness", 0.2f);
        EditorUtility.SetDirty(mat);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        go.name = name;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }
        // strip any Animator (filler has no skeletal animation) and add the cheap critter brain
        foreach (var a in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
        if (go.GetComponent<FillerCritter>() == null) go.AddComponent<FillerCritter>();

        if (!AssetDatabase.IsValidFolder("Assets/Creatures")) AssetDatabase.CreateFolder("Assets", "Creatures");
        PrefabUtility.SaveAsPrefabAsset(go, $"Assets/Creatures/{name}.prefab");
        Object.DestroyImmediate(go);
        Debug.Log($"[Filler] Saved Assets/Creatures/{name}.prefab — pick it from a zone's Mob dropdown and Place a few hundred. Cheap (no skeleton).");
    }

    [MenuItem("WildReach/World/Add Fireflies")]
    static void AddFireflies()
    {
        var go = new GameObject("Fireflies");
        go.AddComponent<Fireflies>();
        var t = GameObject.Find("HomeTree");                       // sit them over the forest if we can find a landmark
        go.transform.position = t != null ? t.transform.position : Vector3.zero;
        Selection.activeGameObject = go;
        SceneView.FrameLastActiveSceneView();
        Debug.Log("[Fireflies] Added — position it over the forest. They fade in at night (tune radius/count/glow on the component).");
    }
}
