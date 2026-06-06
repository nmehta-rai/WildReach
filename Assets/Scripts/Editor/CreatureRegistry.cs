using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Our roster of usable creatures = the prefabs in Assets/Creatures. The Island Builder's per-zone Mob picker
// reads this so you only ever choose from OUR creatures, not every GameObject in the project.
// Add a creature by saving its configured prefab into Assets/Creatures (Setup Creature / Save Leopard do this).
public static class CreatureRegistry
{
    const string Folder = "Assets/Creatures";

    public static List<GameObject> Prefabs()
    {
        var list = new List<GameObject>();
        if (!AssetDatabase.IsValidFolder(Folder)) return list;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder }))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (go != null) list.Add(go);
        }
        list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        return list;
    }
}
