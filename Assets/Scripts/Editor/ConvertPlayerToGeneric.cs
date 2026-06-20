using System.Linq;
using UnityEngine;
using UnityEditor;

// Converts the player from HUMANOID to GENERIC playback. Humanoid re-solves every clip through muscle/hip/
// proportion processing — that's what mangled the run (lower body over-rotating) and slumped the shoulders.
// Generic plays each clip's bone rotations DIRECTLY (no avatar retarget, no muscle solve), so the mocap plays as
// authored. Flips the rigged player + its mocap clips to Generic, re-points the scene Animator at the new Generic
// avatar, drops root motion (PlayerMovement owns position), and keeps Azure.controller (clip refs are name-based,
// so they survive the reimport). Reversible: set the rigs back to Humanoid. Run with SampleScene open.
public static class ConvertPlayerToGeneric
{
    const string RiggedFbx = "Assets/Characters/AzureRigged/Azure_Rigged.fbx";
    static readonly string[] ClipFbxs =
    {
        "Assets/Characters/AzureRenegade/Azure_Idle.fbx",
        "Assets/Characters/AzureRenegade/Azure_Walk.fbx",
        "Assets/Characters/AzureRenegade/Azure_Run.fbx",
        "Assets/Characters/AzureRenegade/Azure_Jump.fbx",
        "Assets/Characters/AzureRenegade/Azure_ClimbUp.fbx",
        "Assets/Characters/AzureRenegade/Azure_ClimbDown.fbx",
        "Assets/Characters/AzureRenegade/Azure_Swim.fbx",
        "Assets/Characters/AzureRenegade/Azure_Tread.fbx",
        "Assets/Characters/AzureRenegade/Azure_SwimToEdge.fbx",
    };

    [MenuItem("WildReach/Convert Player To Generic")]
    static void Convert()
    {
        int converted = 0;
        foreach (var path in new[] { RiggedFbx }.Concat(ClipFbxs))
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter imp) { Debug.LogWarning($"[Generic] missing: {path}"); continue; }
            if (imp.animationType != ModelImporterAnimationType.Generic)
            {
                imp.animationType = ModelImporterAnimationType.Generic;
                imp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.SaveAndReimport();
                converted++;
            }
        }

        // re-point the player's Animator at the new Generic avatar + drop root motion (player code drives position)
        var avatar = AssetDatabase.LoadAllAssetsAtPath(RiggedFbx).OfType<Avatar>().FirstOrDefault();
        var move = Object.FindFirstObjectByType<PlayerMovement>();
        var anim = move != null ? move.GetComponentInChildren<Animator>(true) : null;
        if (anim == null)
            anim = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(a => a.runtimeAnimatorController != null && a.runtimeAnimatorController.name.Contains("Azure"));

        if (anim != null)
        {
            if (avatar != null) anim.avatar = avatar;
            else Debug.LogWarning("[Generic] No Generic avatar found on the rigged FBX — check its Rig tab is Generic + Create From This Model.");
            anim.applyRootMotion = false;
            EditorUtility.SetDirty(anim);

            // sanity: how many of the controller's motions still resolve to real clips?
            int live = 0, total = 0;
            if (anim.runtimeAnimatorController != null)
                foreach (var c in anim.runtimeAnimatorController.animationClips) { total++; if (c != null) live++; }
            Debug.Log($"[Generic] {converted} FBX(s) -> Generic. Animator '{anim.name}': avatar={(avatar!=null?"Generic":"NULL")}, " +
                      $"controller='{anim.runtimeAnimatorController?.name}', clips live {live}/{total}.");
        }
        else Debug.LogWarning("[Generic] No player Animator found — open SampleScene with the player, then re-run.");

        Debug.Log("[Generic] Done. Play & test run/jump — Humanoid solve is gone. If a clip shows NO motion, its bone paths " +
                  "don't match the player hierarchy (tell me and I'll align them).");
    }
}
