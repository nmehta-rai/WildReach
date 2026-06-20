using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

// Auto-discovering control panel. The [MenuItem("WildReach/...")] attributes are the SINGLE
// source of truth — this window reflects over every one of them (via TypeCache) and renders a
// button automatically, grouped by category. Add a tool and its button appears the moment it
// compiles; delete a tool and its button disappears. No hand-maintained list, so nothing ever
// goes stale or missing.
//
// Category comes from the menu path when it's namespaced ("WildReach/Forest/Build X" -> "Forest"),
// otherwise it's inferred from keywords in the name. Migrate old flat items to "WildReach/Cat/Name"
// over time and they'll self-organise here AND become submenus in the native WildReach menu.
public class WildReachPanel : EditorWindow
{
    Vector2 scroll;
    string  filter = "";
    List<Tool> tools;

    struct Tool { public string category, label, execPath; public int prio; }

    // category render order (anything not listed falls to the end, alphabetically)
    static readonly string[] Order =
    {
        "Island & Terrain", "Forest", "Props", "Hero Tree",
        "Character", "Creatures", "World", "Lighting", "HUD / UI", "Misc",
    };

    // keyword -> category, first match wins (for flat menu items without an explicit category)
    static readonly (string kw, string cat)[] Keywords =
    {
        ("grass","Forest"), ("forest","Forest"), ("scatter","Forest"), ("blossom","Forest"),
        ("canopy","Hero Tree"), ("hollowroot","Hero Tree"), ("mothertree","Hero Tree"), ("wisp","Hero Tree"), ("tree leaves","Hero Tree"), ("glow tree","Hero Tree"),
        ("mushroom","Props"), ("moss","Props"), ("spiral","Props"), ("autumn","Props"),
        ("hud","HUD / UI"), ("inventory","HUD / UI"), ("vitals","HUD / UI"),
        ("climb","Character"), ("anim","Character"), ("player","Character"), ("azure","Character"), ("rig","Character"), ("jump","Character"), ("swim","Character"), ("gallery","Character"), ("clip","Character"), ("character","Character"),
        ("water","World"), ("ship","World"), ("sail","World"),
        ("creature","Creatures"), ("tortoise","Creatures"),
        ("golden","Lighting"), ("cinematic","Lighting"), ("day-night","Lighting"), ("time","Lighting"), ("light","Lighting"),
        ("island","Island & Terrain"), ("terrain","Island & Terrain"), ("ground","Island & Terrain"), ("texture","Island & Terrain"), ("sculpt","Island & Terrain"),
    };

    [MenuItem("WildReach/Control Panel %#w", false, -100)]   // Cmd+Shift+W
    static void Open()
    {
        var w = GetWindow<WildReachPanel>("WildReach");
        w.minSize = new Vector2(240, 400);
        w.Rebuild();
    }

    void OnEnable() => Rebuild();
    void OnFocus()  => Rebuild();   // catch tools added/removed since last focus

    void Rebuild()
    {
        tools = new List<Tool>();
        var seen = new HashSet<string>();
        foreach (var mi in TypeCache.GetMethodsWithAttribute<MenuItem>())
        {
            foreach (var a in mi.GetCustomAttributes<MenuItem>(false))
            {
                string path = a.menuItem;
                if (a.validate || string.IsNullOrEmpty(path) || !path.StartsWith("WildReach/")) continue;
                string exec = StripHotkey(path);
                if (exec.Contains("Control Panel") || !seen.Add(exec)) continue;

                string rest = exec.Substring("WildReach/".Length);
                int slash = rest.IndexOf('/');
                string category, label;
                if (slash >= 0) { category = rest.Substring(0, slash); label = rest.Substring(slash + 1); }
                else            { label = rest; category = Infer(label); }

                tools.Add(new Tool { category = category, label = label, execPath = exec, prio = a.priority });
            }
        }
    }

    static string StripHotkey(string path)
    {
        // a trailing " %#w" / " _g" shortcut token is not part of the executable menu path
        return Regex.Replace(path, @"\s[%#&_]*[A-Za-z0-9]$", "").TrimEnd();
    }

    static string Infer(string label)
    {
        string l = label.ToLowerInvariant();
        foreach (var (kw, cat) in Keywords) if (l.Contains(kw)) return cat;
        return "Misc";
    }

    void OnGUI()
    {
        if (tools == null) Rebuild();

        // search bar
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        filter = GUILayout.TextField(filter, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField);
        if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(22))) filter = "";
        if (GUILayout.Button("⟳", EditorStyles.toolbarButton, GUILayout.Width(26))) Rebuild();
        EditorGUILayout.EndHorizontal();

        scroll = EditorGUILayout.BeginScrollView(scroll);

        var shown = tools.Where(t => string.IsNullOrEmpty(filter) ||
                                     t.label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

        foreach (var cat in shown.Select(t => t.category).Distinct().OrderBy(CatRank).ThenBy(c => c))
        {
            Section(cat);
            foreach (var t in shown.Where(t => t.category == cat).OrderBy(t => t.prio).ThenBy(t => t.label))
                if (GUILayout.Button(t.label, GUILayout.Height(22)))
                {
                    // defer to AFTER this OnGUI pass — tools that reimport assets mid-render unbalance the
                    // GUILayout stack ("EndLayoutGroup: BeginLayoutGroup must be called first")
                    var exec = t.execPath;
                    EditorApplication.delayCall += () =>
                    {
                        if (!EditorApplication.ExecuteMenuItem(exec))
                            Debug.LogWarning("[Panel] Could not execute: " + exec);
                    };
                }
        }

        GUILayout.Space(8);
        EditorGUILayout.HelpBox($"{shown.Count} tools, auto-discovered from [MenuItem] attributes. " +
                                "Name new tools WildReach/<Category>/<Tool> to group them.", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    static int CatRank(string cat) { int i = Array.IndexOf(Order, cat); return i < 0 ? Order.Length : i; }

    static void Section(string title)
    {
        GUILayout.Space(8);
        EditorGUILayout.LabelField(title, new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 });
        EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color(1, 1, 1, 0.15f));
        GUILayout.Space(2);
    }
}
