using UnityEngine;
using UnityEditor;

// Gives every creature the kit it needs to be hit + killed: MobHurt (takes a hit, flashes, knocks back),
// HealthSystem (hearts), MobDeath (the death sequence). Works on the SOURCE PREFABS in Assets/Creatures so every
// spawn is killable, plus any live scene mobs. Leopards are tankier. Re-runnable.
public static class SetupCombat
{
    const string CreatureDir = "Assets/Creatures";

    [MenuItem("WildReach/Combat/Make Mobs Damageable")]
    static void Go()
    {
        int prefabs = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { CreatureDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            if (EnsureKit(root, System.IO.Path.GetFileNameWithoutExtension(path))) prefabs++;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        int scene = 0;
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (mb is Predator || mb is ZoneRoamer || mb is CreatureWander)
                if (EnsureKit(mb.gameObject, mb.gameObject.name)) { EditorUtility.SetDirty(mb.gameObject); scene++; }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Combat] Kitted {prefabs} creature prefab(s) + {scene} live scene mob(s) (MobHurt + HealthSystem + MobDeath). Leopard = 4 hearts, critters = 2. Spawns inherit it — go kill something.");
    }

    static bool EnsureKit(GameObject go, string name)
    {
        bool changed = false;
        int hearts = name.ToLower().Contains("leopard") ? 4 : 2;

        var hp = go.GetComponent<HealthSystem>();
        if (hp == null) { hp = go.AddComponent<HealthSystem>(); changed = true; }
        hp.SetMaxHearts(hearts);

        var mh = go.GetComponent<MobHurt>();
        if (mh == null) { mh = go.AddComponent<MobHurt>(); changed = true; }
        var so = new SerializedObject(mh);   // set the values on NEW and EXISTING components alike
        so.FindProperty("knockback").floatValue = 10f;
        so.FindProperty("knockTime").floatValue = 0.25f;
        so.ApplyModifiedProperties();

        if (go.GetComponent<MobDeath>() == null) { go.AddComponent<MobDeath>(); changed = true; }

        // some creatures (e.g. the floaty Dreamsprite) have NO collider, so the swing sweep finds nothing — give
        // them a trigger sphere so they're hittable without becoming a solid wall.
        if (go.GetComponentInChildren<Collider>() == null)
        {
            var rend = go.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                var sph = go.AddComponent<SphereCollider>();
                sph.isTrigger = true;
                Bounds b = rend.bounds;
                sph.center = go.transform.InverseTransformPoint(b.center);
                Vector3 ext = b.extents;
                float ls = Mathf.Max(go.transform.lossyScale.x, Mathf.Max(go.transform.lossyScale.y, go.transform.lossyScale.z));
                sph.radius = Mathf.Max(ext.x, Mathf.Max(ext.y, ext.z)) / (ls > 0.0001f ? ls : 1f);
                changed = true;
            }
        }
        return changed;
    }
}
