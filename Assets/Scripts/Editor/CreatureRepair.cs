using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Repairs the "purple critters" — the filler materials turned out to be GHOST ASSETS: the .mat files were created
// INSIDE each FBX's "..._texture_fbx" subfolder, and when those folders got re-imported the materials orphaned out
// of Unity's live asset database. The file is on disk, the GUID resolves, but LoadAssetAtPath returns null, so every
// renderer pointing at it draws a null (magenta) material — confirmed: 1215 critters on NULL-MATERIAL.
//
// Fix: for each filler prefab, rebuild a fresh URP/Lit material in a CLEAN folder (Assets/Creatures/Materials),
// force-import it, VERIFY it loads non-null, then reassign it to the prefab AND every live scene instance.
public static class CreatureRepair
{
    const string MatDir = "Assets/Creatures/Materials";

    [MenuItem("WildReach/Creatures/Repair Filler Materials (fix purple critters)")]
    static void RepairFiller()
    {
        if (Application.isPlaying)
        { Debug.LogWarning("[Repair] Stop Play first — edits in Play mode revert on exit."); return; }

        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Creatures", "Materials");

        // name -> freshly-rebuilt, verified material
        var fixedMat = new Dictionary<string, Material>();

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Creatures" }))
        {
            var path   = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponentInChildren<FillerCritter>(true) == null) continue;  // filler only

            // find the base texture from the prefab's mesh -> its source FBX folder
            var mf = prefab.GetComponentInChildren<MeshFilter>(true);
            if (mf == null || mf.sharedMesh == null) { Debug.LogWarning($"[Repair] {prefab.name}: no mesh."); continue; }
            var fbxDir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(mf.sharedMesh))?.Replace('\\', '/');
            var baseTex = Directory.GetFiles(fbxDir, "*.png")
                .Select(p => p.Replace('\\', '/'))
                .FirstOrDefault(p => { var n = p.ToLower(); return !n.Contains("normal") && !n.Contains("metallic") && !n.Contains("roughness") && !n.Contains("emit"); });

            // fresh material in a CLEAN folder (away from the churny FBX subfolder)
            var matPath = $"{MatDir}/{prefab.name}_Filler.mat";
            AssetDatabase.DeleteAsset(matPath);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = prefab.name + "_Filler" };
            AssetDatabase.CreateAsset(mat, matPath);
            if (baseTex != null)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(baseTex);
                if (tex != null) { mat.SetTexture("_BaseMap", tex); mat.SetColor("_BaseColor", Color.white); }
            }
            mat.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.ImportAsset(matPath, ImportAssetOptions.ForceUpdate);

            // VERIFY it actually loads now (the whole point — a ghost asset would still be null here)
            var verified = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (verified == null) { Debug.LogError($"[Repair] {prefab.name}: rebuilt material STILL won't load — deeper issue."); continue; }
            fixedMat[prefab.name] = verified;

            // assign it onto the prefab asset (edit contents safely, then save)
            var contents = PrefabUtility.LoadPrefabContents(path);
            foreach (var r in contents.GetComponentsInChildren<Renderer>(true))
            { var a = new Material[r.sharedMaterials.Length]; for (int i = 0; i < a.Length; i++) a[i] = verified; r.sharedMaterials = a; }
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
            Debug.Log($"[Repair] {prefab.name}: rebuilt material → {matPath} (loads OK, shader {verified.shader.name}, baseMap={(baseTex != null)}).");
        }

        // now fix every live scene instance (match by the name before the first '_': "ForestBeetle_Forrest_12" -> "ForestBeetle")
        int sceneFixed = 0;
        foreach (var fc in Object.FindObjectsByType<FillerCritter>(FindObjectsInactive.Include))
        {
            var key = fc.gameObject.name.Split('_')[0];
            if (!fixedMat.TryGetValue(key, out var m)) continue;
            foreach (var r in fc.GetComponentsInChildren<Renderer>(true))
            { var a = new Material[r.sharedMaterials.Length]; for (int i = 0; i < a.Length; i++) a[i] = m; r.sharedMaterials = a; EditorUtility.SetDirty(r); sceneFixed++; }
        }

        AssetDatabase.SaveAssets();
        if (sceneFixed > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[Repair] Rebuilt {fixedMat.Count} filler material(s); reassigned {sceneFixed} scene renderer(s). " +
                  "Critters should be textured now. (New mats live in Assets/Creatures/Materials.)");
    }
}
