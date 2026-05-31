using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// Wires the hand/foot climbing IK: enables "IK Pass" on Azure.controller's base layer (so OnAnimatorIK fires)
// and adds the ClimbIK component to the player's Animator object. Re-runnable.
public static class SetupClimbIK
{
    const string CtrlPath = "Assets/Characters/AzureRenegade/Azure.controller";

    [MenuItem("WildReach/Setup Climb IK")]
    static void Setup()
    {
        // 1. enable IK Pass on the controller's base layer (must reassign the layers array — Unity quirk)
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
        if (ctrl == null) { Debug.LogError($"[ClimbIK] {CtrlPath} not found."); return; }
        var layers = ctrl.layers;
        if (!layers[0].iKPass) { layers[0].iKPass = true; ctrl.layers = layers; EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets(); }

        // 2. add ClimbIK to the player's Animator GameObject
        var pm = Object.FindObjectOfType<PlayerMovement>();
        if (pm == null) { Debug.LogError("[ClimbIK] No PlayerMovement in the scene."); return; }
        var animator = pm.GetComponentInChildren<Animator>();
        if (animator == null) { Debug.LogError("[ClimbIK] No Animator under the player."); return; }
        if (animator.GetComponent<ClimbIK>() == null) Undo.AddComponent<ClimbIK>(animator.gameObject);

        EditorUtility.SetDirty(animator.gameObject);
        Debug.Log("[ClimbIK] IK Pass enabled + ClimbIK added to '" + animator.name + "'. Climb a wall (E) — hands/feet should plant on the rock.");
    }
}
