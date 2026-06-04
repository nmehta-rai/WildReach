using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Runtime GPU-instanced grass. Instead of baking permanent meshes (which can't scale), this draws
// grass clumps instanced in a moving window around EACH camera as it renders: chunks are generated
// on demand near the camera, cached, and regenerated as you move. Cost is bounded by the view radius,
// NOT the zone size — so a grass zone can be any size. Reads the zone map live, so each painted zone's
// grass is independent (toggling one never disturbs another). Renders in the editor and in play.
[ExecuteAlways]
public class GrassField : MonoBehaviour
{
    public string terrainName = "Island1_Terrain";
    public Texture2D zoneMap;
    public Mesh[] meshes;
    public Material material;
    public bool[]  zoneGrass;     // per zone index: grass on?
    public float[] zoneDensity;   // per zone index: 0..1
    public int   seed = 12345;
    public float drawDistance = 95f;
    public float baseSpacing = 0.5f;
    public float slopeLimit = 35f;

    const float ChunkSize = 12f;
    const int   MaxCachedChunks = 2500;

    Terrain terrain;
    Color32[] zonePixels; int zoneRes;
    readonly Dictionary<long, List<Matrix4x4>[]> chunks = new Dictionary<long, List<Matrix4x4>[]>();

    void OnEnable()  { Refresh(); RenderPipelineManager.beginCameraRendering += OnCam; }
    void OnDisable() { RenderPipelineManager.beginCameraRendering -= OnCam; }

    public void Refresh()
    {
        var go = !string.IsNullOrEmpty(terrainName) ? GameObject.Find(terrainName) : null;
        terrain = go != null ? go.GetComponent<Terrain>() : (Terrain.activeTerrain ?? FindFirstObjectByType<Terrain>());
        zonePixels = zoneMap != null ? zoneMap.GetPixels32() : null;
        zoneRes = zoneMap != null ? zoneMap.width : 0;
        if (material != null) material.enableInstancing = true;
        chunks.Clear();
    }

    void OnCam(ScriptableRenderContext ctx, Camera cam)
    {
        if (material == null || meshes == null || meshes.Length == 0 || zonePixels == null || terrain == null) return;
        if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;

        Vector3 focus = cam.transform.position;
        int reach = Mathf.CeilToInt(drawDistance / ChunkSize);
        int fcx = Mathf.FloorToInt(focus.x / ChunkSize), fcz = Mathf.FloorToInt(focus.z / ChunkSize);
        float maxSqr = (drawDistance + ChunkSize) * (drawDistance + ChunkSize);

        if (chunks.Count > MaxCachedChunks) chunks.Clear();   // crude bound; regen is cheap

        for (int cz = fcz - reach; cz <= fcz + reach; cz++)
        for (int cx = fcx - reach; cx <= fcx + reach; cx++)
        {
            float ccx = (cx + 0.5f) * ChunkSize, ccz = (cz + 0.5f) * ChunkSize;
            if ((new Vector2(ccx - focus.x, ccz - focus.z)).sqrMagnitude > maxSqr) continue;

            long key = ((long)cx << 32) | (uint)cz;
            if (!chunks.TryGetValue(key, out var lists)) { lists = GenerateChunk(cx, cz); chunks[key] = lists; }
            for (int m = 0; m < meshes.Length; m++)
            {
                var mats = lists[m];
                for (int i = 0; i < mats.Count; i += 1023)
                    Graphics.DrawMeshInstanced(meshes[m], 0, material, mats.GetRange(i, Mathf.Min(1023, mats.Count - i)), null,
                                               ShadowCastingMode.Off, false, 0, cam);
            }
        }
    }

    List<Matrix4x4>[] GenerateChunk(int cx, int cz)
    {
        var lists = new List<Matrix4x4>[meshes.Length];
        for (int m = 0; m < meshes.Length; m++) lists[m] = new List<Matrix4x4>();

        var tpos = terrain.transform.position; var size = terrain.terrainData.size; var td = terrain.terrainData;
        var rng = new System.Random(seed ^ (cx * 73856093) ^ (cz * 19349663));
        float x0 = cx * ChunkSize, z0 = cz * ChunkSize;

        for (float lz = 0; lz < ChunkSize; lz += baseSpacing)
        for (float lx = 0; lx < ChunkSize; lx += baseSpacing)
        {
            float x = x0 + lx + (float)(rng.NextDouble() - 0.5) * baseSpacing;
            float z = z0 + lz + (float)(rng.NextDouble() - 0.5) * baseSpacing;
            float u = (x - tpos.x) / size.x, v = (z - tpos.z) / size.z;
            if (u < 0 || u > 1 || v < 0 || v > 1) continue;

            int zx = Mathf.Clamp(Mathf.RoundToInt(u * (zoneRes - 1)), 0, zoneRes - 1);
            int zy = Mathf.Clamp(Mathf.RoundToInt(v * (zoneRes - 1)), 0, zoneRes - 1);
            int zi = zonePixels[zy * zoneRes + zx].r - 1;
            if (zi < 0 || zi >= zoneGrass.Length || !zoneGrass[zi]) continue;
            if (rng.NextDouble() > zoneDensity[zi]) continue;                 // density thinning
            if (td.GetSteepness(u, v) > slopeLimit) continue;

            float y = terrain.SampleHeight(new Vector3(x, 0, z)) + tpos.y;
            var trs = Matrix4x4.TRS(new Vector3(x, y, z),
                                    Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f),
                                    Vector3.one * (0.8f + (float)rng.NextDouble() * 0.6f));
            lists[rng.Next(meshes.Length)].Add(trs);
        }
        return lists;
    }
}
