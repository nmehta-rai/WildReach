using System.Linq;
using UnityEngine;
using UnityEditor;

// Parents every loose Canopy* cluster under the HomeTree trunk and saves the whole
// thing as one prefab. After this, HomeTree is a single reusable object.
public static class SaveTreePrefab
{
    const string PrefabPath = "Assets/Environment/MotherTree/HomeTree.prefab";

    [MenuItem("WildReach/Save Tree Prefab")]
    static void Save()
    {
        var trunk = GameObject.Find("HomeTree");
        if (trunk == null) { Debug.LogError("[Tree] No GameObject named 'HomeTree' in the scene."); return; }

        // Loose canopy clusters sitting at scene root.
        string[] accentPrefixes = { "Canopy", "Blossoms", "Wisps" };
        var canopies = Object.FindObjectsByType<GameObject>()
            .Where(g => g.transform.parent == null && accentPrefixes.Any(p => g.name.StartsWith(p)))
            .ToArray();

        foreach (var c in canopies)
            c.transform.SetParent(trunk.transform, worldPositionStays: true);

        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(trunk, PrefabPath, InteractionMode.UserAction);
        Selection.activeGameObject = trunk;
        Debug.Log($"[Tree] Saved '{prefab.name}' prefab with trunk + {canopies.Length} canopy clusters → {PrefabPath}. " +
                  "It's one object now; drag the prefab in to reuse it.");
    }
}
