using UnityEngine;
using UnityEditor;

// Adds a (non-convex, static) MeshCollider to every mesh under the selected object
// so the player can't walk through it — and so the climb raycasts have a surface.
public static class AddTreeCollider
{
    [MenuItem("WildReach/Add Tree Collider")]
    static void Add()
    {
        var go = Selection.activeGameObject;
        if (go == null) { Debug.LogError("[Tree] Select the tree first."); return; }

        int added = 0;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.GetComponent<MeshCollider>() != null) continue;
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;   // static prop — concave collider is fine and accurate for climbing
            added++;
        }
        Debug.Log($"[Tree] Added {added} MeshCollider(s) to '{go.name}'. Player will collide + can climb it.");
    }
}
