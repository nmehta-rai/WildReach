using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

// Builds a DENSE, clustered forest patch around the player (not 35 trees sprinkled over the whole
// island). Two layers: a canopy of forest + blossom trees, and a ground layer of glowing mushroom
// clusters + moss logs. Everything conforms to the terrain, random yaw/scale, keeps a small clear
// glade around the spawn so you don't open inside a trunk. Re-runnable: wipes the previous patch.
public static class BuildForestSlice
{
    struct Prop
    {
        public string path; public int count; public float minS, maxS, spacing, normalizeHeight;
        public bool ground; // ground props can sit inside the glade
    }

    static readonly Prop[] Palette =
    {
        new Prop{ path="Assets/Environment/ForestTrees/ForestTree.prefab",                  count=46, minS=0.8f, maxS=1.5f, spacing=5.5f, ground=false },
        new Prop{ path="Assets/Environment/ForestTrees/BlossomTree.prefab",                 count=8,  minS=0.8f, maxS=1.2f, spacing=7f,   ground=false },
        new Prop{ path="Assets/Environment/Mushrooms/EmbercapCluster/EmbercapCluster_Glow.prefab", count=14, minS=0.7f, maxS=1.3f, spacing=3.5f, ground=true },
        new Prop{ path="Assets/Environment/Mushrooms/EmbercapGrove/EmbercapGrove_Glow.prefab",     count=8,  minS=0.7f, maxS=1.2f, spacing=4f,   ground=true },
        new Prop{ path="Assets/Environment/MossLog/MossLog.fbx",                            count=5,  minS=0.9f, maxS=1.2f, spacing=6f,   normalizeHeight=1.2f, ground=true },
    };

    const float Radius      = 48f;  // forest patch radius around the player
    const float GladeRadius = 7f;   // keep trees this far from spawn (clear standing room)
    const float MaxSteep    = 32f;
    const float HeroClear   = 13f;  // stay off the Hollowroot

    [MenuItem("WildReach/Build Forest Slice")]
    static void Build()
    {
        var terrain = Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
        if (terrain == null) { Debug.LogError("[ForestSlice] No terrain in the scene."); return; }
        var td = terrain.terrainData; var tpos = terrain.transform.position;

        var player = Object.FindAnyObjectByType<PlayerMovement>();
        var hero   = GameObject.Find("HomeTree");
        Vector3 center = player ? player.transform.position : (hero ? hero.transform.position : Vector3.zero);
        Vector3 heroPos = hero ? hero.transform.position : new Vector3(1e9f, 0, 1e9f);

        var old = GameObject.Find("ForestSlice");
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("ForestSlice");
        Undo.RegisterCreatedObjectUndo(root, "Build Forest Slice");

        var placed = new List<Vector3>();
        int total = 0;

        foreach (var p in Palette)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(p.path);
            if (asset == null) { Debug.LogWarning("[ForestSlice] Missing " + p.path); continue; }

            // for raw FBX props (moss log) work out a uniform scale that hits the target height
            float baseScale = 1f;
            if (p.normalizeHeight > 0f)
            {
                var probe = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                var rs = probe.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    if (b.size.y > 0.01f) baseScale = p.normalizeHeight / b.size.y;
                }
                Object.DestroyImmediate(probe);
            }

            int made = 0, tries = 0;
            while (made < p.count && tries < p.count * 30)
            {
                tries++;
                Vector2 off = Random.insideUnitCircle * Radius;
                float x = center.x + off.x, z = center.z + off.y;

                float nx = (x - tpos.x) / td.size.x, nz = (z - tpos.z) / td.size.z;
                if (nx < 0 || nx > 1 || nz < 0 || nz > 1) continue;
                if (td.GetSteepness(nx, nz) > MaxSteep) continue;
                if (!p.ground && off.magnitude < GladeRadius) continue;           // clear glade for trees
                if (new Vector2(x - heroPos.x, z - heroPos.z).magnitude < HeroClear) continue;
                if (placed.Any(q => new Vector2(x - q.x, z - q.z).magnitude < p.spacing)) continue;

                float gy = terrain.SampleHeight(new Vector3(x, 0, z)) + tpos.y;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                inst.transform.SetParent(root.transform, true);

                // Normalized props (moss log) get an absolute scale from the probe; everything else
                // MULTIPLIES the prefab's own scale — mushrooms bake a 100x root scale (tiny source
                // mesh), and overwriting it shrank them to invisible specks.
                float r = Random.Range(p.minS, p.maxS);
                inst.transform.localScale = p.normalizeHeight > 0f
                    ? Vector3.one * (baseScale * r)
                    : inst.transform.localScale * r;
                // KEEP the prefab's own rotation (mushrooms bake a -90° X to stand upright) and add
                // a random yaw about world-up on top of it — overwriting it made them lie flat.
                inst.transform.rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * inst.transform.rotation;

                inst.transform.position = new Vector3(x, gy - (p.ground ? 0f : 0.1f), z);
                if (p.ground)   // drop ground props so the mesh bottom rests on the soil (no sinking/floating)
                {
                    var rends = inst.GetComponentsInChildren<Renderer>();
                    if (rends.Length > 0)
                    {
                        var b = rends[0].bounds; foreach (var rr in rends) b.Encapsulate(rr.bounds);
                        inst.transform.position += Vector3.up * (gy - b.min.y - 0.03f);
                    }
                }
                placed.Add(new Vector3(x, gy, z));
                made++; total++;
            }
            Debug.Log($"[ForestSlice] {made}/{p.count} {System.IO.Path.GetFileNameWithoutExtension(p.path)}");
        }

        Selection.activeGameObject = root;
        Debug.Log($"[ForestSlice] {total} props clustered around the player (r={Radius}m). Re-run to reshuffle; delete awkward ones.");
    }
}
