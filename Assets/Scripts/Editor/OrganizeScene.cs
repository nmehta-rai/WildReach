using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Tidies the working scene so Island 1 can be built clean without deleting anything.
// Sweeps every old test-island object under a single "TestIsland" parent and deactivates it,
// leaving the system objects (camera, lights, player, UI, water, ship) at the root.
// Reversible: re-activate "TestIsland" to get the old slice back; delete it once Island 1 is done.
public static class OrganizeScene
{
    // Everything that belongs to the throwaway test island (by Hierarchy name).
    static readonly string[] TestIslandObjects =
    {
        "SliceTerrain", "Island1",                 // old terrains
        "HomeTree",                                  // old hero tree
        "EmbercapCluster_Glow", "EmbercapGrove_Glow", "EmbercapGrove_Glow (1)",
        "MossLog", "SpiralTree", "AutumnCanopy", "AutumnCanopy (1)",
        "ForestTree", "ScatteredForest", "ScatteredBlossoms",
        "Tortoise",
    };

    [MenuItem("WildReach/Organize Scene (Group Test Island)")]
    static void Organize()
    {
        var parent = GameObject.Find("TestIsland");
        if (parent == null)
        {
            parent = new GameObject("TestIsland");
            Undo.RegisterCreatedObjectUndo(parent, "Create TestIsland");
        }

        int moved = 0;
        foreach (var name in TestIslandObjects)
        {
            // Find a ROOT object with this name (skip anything already under TestIsland).
            GameObject go = null;
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == name) { go = root; break; }

            if (go == null || go == parent) continue;
            Undo.SetTransformParent(go.transform, parent.transform, "Group Test Island");
            moved++;
        }

        Undo.RecordObject(parent, "Deactivate TestIsland");
        parent.SetActive(false);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[Organize] Swept {moved} object(s) under 'TestIsland' and deactivated it. " +
                  "System objects (camera/lights/player/UI/Water/Ship) left at root. " +
                  "Now run Build Island Terrain. Re-activate or delete 'TestIsland' later.");
    }
}
