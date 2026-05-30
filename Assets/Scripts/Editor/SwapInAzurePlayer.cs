using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// Swaps the Azure Renegade rig into the existing Player object in the scene:
//   - instantiates the rigged T-pose model under Player (keeps the prefab link)
//   - wires Azure.controller + the generated avatar onto its Animator
//   - re-adds PlayerAnimator so Speed/Jump drive the new rig
//   - auto-scales to ~2 m so we never hit the Meshy 50x scale trap again
//   - disables the old visual (kept, not deleted, so it stays as an alt skin)
// Run "Setup Azure Character" first. Re-runnable.
public static class SwapInAzurePlayer
{
    const string Tpose    = "Assets/Characters/AzureRenegade/Azure_Tpose.fbx";
    const string CtrlPath = "Assets/Characters/AzureRenegade/Azure.controller";
    const float  TargetHeight = 2.0f;   // metres — the WildReach scale law

    [MenuItem("WildReach/Swap In Azure Player")]
    static void Swap()
    {
        var movement = Object.FindFirstObjectByType<PlayerMovement>();
        if (movement == null) { Debug.LogError("[Azure] No PlayerMovement in the scene — open SampleScene with the Player."); return; }
        var player = movement.gameObject;

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Tpose);
        var ctrl  = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
        if (model == null || ctrl == null) { Debug.LogError("[Azure] Run 'Setup Azure Character' first (missing model or controller)."); return; }

        // Don't double-add if it's already there.
        var existing = player.transform.Cast<Transform>().FirstOrDefault(c => c.name == "AzureRenegade");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        // The current visual = first child whose subtree has a SkinnedMeshRenderer.
        var oldVisual = player.transform.Cast<Transform>()
            .FirstOrDefault(c => c.GetComponentInChildren<SkinnedMeshRenderer>(true) != null);

        // Instantiate Azure under Player, at the old visual's local transform (or origin).
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        inst.name = "AzureRenegade";
        inst.transform.SetParent(player.transform, false);
        if (oldVisual != null)
        {
            inst.transform.localPosition = oldVisual.localPosition;
            inst.transform.localRotation = oldVisual.localRotation;
        }
        else
        {
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
        }

        // Belt-and-suspenders: kill any Light/Camera the FBX carried in (Blender scene leftovers).
        foreach (var l in inst.GetComponentsInChildren<Light>(true))  Object.DestroyImmediate(l);
        foreach (var c in inst.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c);

        // Animator: assign avatar (already on the model) + our controller.
        var animator = inst.GetComponent<Animator>();
        if (animator == null) animator = inst.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        animator.applyRootMotion = false;

        // PlayerAnimator drives Speed/VerticalVelocity/IsGrounded/Jump on this object.
        if (inst.GetComponent<PlayerAnimator>() == null) inst.AddComponent<PlayerAnimator>();

        // Auto-correct scale to ~2 m using the rendered bounds.
        var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);
        float height = smr != null ? smr.bounds.size.y : 0f;
        if (height > 0.01f)
        {
            float factor = TargetHeight / height;
            inst.transform.localScale = inst.transform.localScale * factor;
            Debug.Log($"[Azure] Measured height {height:0.00} m -> scaled by {factor:0.0000} to ~{TargetHeight} m.");
        }
        else Debug.LogWarning("[Azure] Could not measure height — check scale manually.");

        // Keep the old character as a disabled alt skin.
        if (oldVisual != null)
        {
            oldVisual.gameObject.SetActive(false);
            Debug.Log($"[Azure] Old visual '{oldVisual.name}' disabled (kept as alt skin).");
        }

        Selection.activeGameObject = inst;
        EditorUtility.SetDirty(player);
        Debug.Log("[Azure] Azure Renegade swapped into Player. Hit Play — Idle/Walk/Run should drive off movement.");
    }
}
