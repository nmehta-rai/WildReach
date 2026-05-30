using UnityEngine;
using UnityEditor;

// A dockable control panel for all the WildReach setup tools, grouped by category, so you
// don't have to hunt the long flat menu. Open with WildReach > Control Panel (Cmd+Shift+W),
// then dock it next to the Inspector. Each button just runs the matching menu item, so the
// underlying tools are unchanged.
public class WildReachPanel : EditorWindow
{
    Vector2 scroll;

    [MenuItem("WildReach/Control Panel %#w", false, -100)]   // Cmd+Shift+W
    static void Open()
    {
        var w = GetWindow<WildReachPanel>("WildReach");
        w.minSize = new Vector2(220, 360);
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        Section("World / Terrain");
        Btn("Reset Flat Terrain");
        Btn("Setup Ground");
        Btn("Setup Grass Details");

        Section("Lighting & Time");
        Btn("Setup Golden Hour");
        Btn("Setup Cinematic Post");
        Btn("Setup Day-Night Cycle");
        Row("Time - Day", "Time - Night");

        Section("Character");
        Btn("Setup Azure Character");
        Btn("Swap In Azure Player");
        Btn("Debug Scale");

        Section("Hero Tree (Hollowroot)");
        Btn("Setup MotherTree Material");
        Btn("Build Canopy");
        Row("Build Blossom Cluster", "Build Wisp Drape");
        Btn("Glow Tree Leaves");
        Row("Add Tree Collider", "Save Tree Prefab");

        Section("Environment Props");
        Row("Setup Mushrooms", "Build Glowing Mushrooms");
        Btn("Setup Moss Log");
        Btn("Setup Spiral Tree");
        Btn("Build Autumn Canopy");

        Section("Forest");
        Btn("Build Forest Tree");
        Btn("Scatter Forest");
        Btn("Build Blossom Tree");
        Btn("Scatter Blossoms");

        Section("Water");
        Btn("Setup Water");

        GUILayout.Space(10);
        EditorGUILayout.HelpBox("Buttons run the matching WildReach menu item. Dock me next to the Inspector.", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    static void Section(string title)
    {
        GUILayout.Space(8);
        var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
        EditorGUILayout.LabelField(title, style);
        var r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(1, 1, 1, 0.15f));
        GUILayout.Space(2);
    }

    static void Btn(string menu, string label = null)
    {
        if (GUILayout.Button(label ?? menu, GUILayout.Height(22)))
            if (!EditorApplication.ExecuteMenuItem("WildReach/" + menu))
                Debug.LogWarning($"[Panel] Menu item not found: WildReach/{menu}");
    }

    static void Row(string menuA, string menuB)
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(menuA, GUILayout.Height(22))) EditorApplication.ExecuteMenuItem("WildReach/" + menuA);
        if (GUILayout.Button(menuB, GUILayout.Height(22))) EditorApplication.ExecuteMenuItem("WildReach/" + menuB);
        EditorGUILayout.EndHorizontal();
    }
}
