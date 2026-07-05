using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Linq;
using System.Collections.Generic;

// Drops the newly-rigged Azure into the scene with an AnimationGallery so you can flip through
// every existing clip on the new rig (←/→ in Play). Ensures the FBX imports as Humanoid first.
public static class SetupAnimationGallery
{
    const string RiggedFbx = "Assets/Characters/AzureRigged/Azure_Rigged.fbx";
    const string AnimDir    = "Assets/Characters/AzureRenegade";

    [MenuItem("WildReach/Setup Animation Gallery")]
    static void Setup()
    {
        AssetDatabase.Refresh();
        var imp = AssetImporter.GetAtPath(RiggedFbx) as ModelImporter;
        if (imp == null) { Debug.LogError("[Gallery] " + RiggedFbx + " not found — export it from Blender first."); return; }

        // import the new rig as Humanoid (auto-builds the avatar from this model)
        if (imp.animationType != ModelImporterAnimationType.Human || imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.SaveAndReimport();
        }

        var model  = AssetDatabase.LoadAssetAtPath<GameObject>(RiggedFbx);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(RiggedFbx).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isValid)
            Debug.LogWarning("[Gallery] The new rig's Humanoid avatar isn't valid yet — some bones may be unmapped. " +
                             "Select " + RiggedFbx + " > Rig > Configure to check the mapping.");
        else if (!avatar.isHuman)
            Debug.LogWarning("[Gallery] Avatar built but not Humanoid.");

        // collect the existing clips (skip the base mesh and the T-pose source)
        var clips  = new List<AnimationClip>();
        var labels = new List<string>();
        foreach (var fbx in Directory.GetFiles(AnimDir, "Azure_*.fbx").OrderBy(p => p))
        {
            string name = Path.GetFileNameWithoutExtension(fbx);
            if (name.Contains("Tpose") || name == "AzureRenegade") continue;
            var clip = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));
            if (clip != null) { clips.Add(clip); labels.Add(name.Replace("Azure_", "")); }
        }
        if (clips.Count == 0) { Debug.LogError("[Gallery] No animation clips found in " + AnimDir); return; }

        // (re)create the gallery instance
        var existing = GameObject.Find("AnimGallery_Azure");
        if (existing != null) Object.DestroyImmediate(existing);

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        inst.name = "AnimGallery_Azure";

        // place it next to the current character if there is one, else origin
        var others = Object.FindObjectsByType<SkinnedMeshRenderer>()
                           .Where(s => s.transform.root != inst.transform).ToArray();
        Vector3 pos = others.Length > 0 ? others[0].transform.root.position + new Vector3(1.5f, 0, 0) : Vector3.zero;
        inst.transform.position = pos;

        var anim = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
        anim.avatar = avatar;
        anim.applyRootMotion = false;

        var gallery = inst.AddComponent<AnimationGallery>();
        gallery.clips = clips.ToArray();
        gallery.labels = labels.ToArray();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = inst;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log($"[Gallery] Placed AnimGallery_Azure with {clips.Count} clips: {string.Join(", ", labels)}. " +
                  $"Avatar valid={avatar != null && avatar.isValid}. Press Play, use ←/→ to cycle.");
    }
}
