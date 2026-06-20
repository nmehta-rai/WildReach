using UnityEngine;
using UnityEditor;

// Ensures a PauseMenu lives in the scene (Esc -> Resume / Restart to Spawn / Quit). The menu builds its own canvas
// at runtime, so this just guarantees the component exists on a GameManager object. Re-runnable.
public static class SetupPauseMenu
{
    [MenuItem("WildReach/Setup Game Systems (Pause + Save)")]
    static void Setup()
    {
        var go = GameObject.Find("GameManager") ?? new GameObject("GameManager");
        if (go.GetComponent<PauseMenu>() == null)  go.AddComponent<PauseMenu>();
        if (go.GetComponent<SaveSystem>() == null) go.AddComponent<SaveSystem>();
        Selection.activeGameObject = go;
        EditorUtility.SetDirty(go);
        Debug.Log("[GameSystems] PauseMenu + SaveSystem on '" + go.name + "'. Esc = menu; position/time auto-resumes on relaunch.");
    }
}
