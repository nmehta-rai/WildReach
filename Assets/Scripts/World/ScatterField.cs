using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Camera-windowed GPU-instanced renderer for heavy mesh props (trees, logs). Instead of thousands of
// GameObjects rendering at once, each prop type is baked to ONE combined mesh and only the instances
// within drawDistance of the rendering camera are drawn (instanced). Solid props also get a small
// pool of capsule colliders that follows the player, so you can't walk through nearby trees without
// paying for colliders on the whole forest. The same scalability idea as the grass field.
[ExecuteAlways]
public class ScatterField : MonoBehaviour
{
    [System.Serializable]
    public class Group
    {
        public string name;
        public Mesh mesh;
        public Material[] materials;
        public Matrix4x4[] instances;
        public Vector3[]   positions;     // instance world positions (cull + colliders)
        public float[]     scales;        // per-instance uniform scale (collider sizing)
        public bool  collide;
        public float colRadius = 0.6f, colHeight = 4f, colCenterY = 2f;   // capsule at unit scale
    }

    public Group[] groups;
    public float drawDistance = 160f;
    public float collisionDistance = 28f;

    readonly List<Matrix4x4> batch = new List<Matrix4x4>();
    readonly List<MeshCollider> pool = new List<MeshCollider>();   // real per-tree mesh colliders (like the HomeTree), windowed around the player
    Transform colRoot;
    Transform player;

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnCam;
        if (groups != null)
            foreach (var g in groups)
                if (g.materials != null) foreach (var m in g.materials) if (m != null) m.enableInstancing = true;
    }
    void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnCam;

    void OnCam(ScriptableRenderContext ctx, Camera cam)
    {
        if (groups == null) return;
        if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
        Vector3 c = cam.transform.position; float dd = drawDistance * drawDistance;

        foreach (var g in groups)
        {
            if (g.mesh == null || g.materials == null || g.instances == null) continue;
            batch.Clear();
            for (int i = 0; i < g.instances.Length; i++)
                if ((g.positions[i] - c).sqrMagnitude <= dd) batch.Add(g.instances[i]);
            if (batch.Count == 0) continue;

            for (int s = 0; s < g.mesh.subMeshCount; s++)
            {
                var mat = s < g.materials.Length ? g.materials[s] : g.materials[g.materials.Length - 1];
                if (mat == null) continue;
                for (int i = 0; i < batch.Count; i += 1023)
                    Graphics.DrawMeshInstanced(g.mesh, s, mat, batch.GetRange(i, Mathf.Min(1023, batch.Count - i)),
                                               null, ShadowCastingMode.On, true, 0, cam);
            }
        }
    }

    void Update()
    {
        if (groups == null) return;
        if (player == null) { var pm = FindAnyObjectByType<PlayerMovement>(); player = pm != null ? pm.transform : null; }
        if (player == null) return;

        if (colRoot == null) { var go = new GameObject("~Colliders") { hideFlags = HideFlags.DontSave }; colRoot = go.transform; }   // top-level = world-identity, so instance world matrices apply cleanly
        Vector3 pp = player.position; float cd = collisionDistance * collisionDistance;
        int used = 0;
        foreach (var g in groups)
        {
            if (!g.collide || g.instances == null) continue;
            for (int i = 0; i < g.positions.Length; i++)
            {
                if ((g.positions[i] - pp).sqrMagnitude > cd) continue;
                var col = used < pool.Count ? pool[used] : NewCol();
                used++;
                var m = g.instances[i];                                              // full per-instance transform: pos + spin + scale
                float sc = (g.scales != null && i < g.scales.Length) ? g.scales[i] : 1f;
                col.transform.SetPositionAndRotation(new Vector3(m.m03, m.m13, m.m23), m.rotation);
                col.transform.localScale = Vector3.one * sc;
                if (col.sharedMesh != g.mesh) col.sharedMesh = g.mesh;               // the REAL tree mesh — same solid collision the HomeTree uses, not a thin pole
                col.enabled = true;
            }
        }
        for (int i = used; i < pool.Count; i++) pool[i].enabled = false;
    }

    MeshCollider NewCol()
    {
        var go = new GameObject("c") { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(colRoot, false);
        var c = go.AddComponent<MeshCollider>();
        c.convex = false;   // solid non-convex bark, exactly like the HomeTree
        pool.Add(c);
        return c;
    }
}
