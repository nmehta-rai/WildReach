using UnityEngine;
using UnityEditor;
using System.IO;

// Builds the Island 1 (home island) Unity Terrain from the authored heightmap.
// Source: _tools/heightmap_island1.py -> Assets/Terrain/Island1_Heightmap.raw (16-bit LE, 1025x1025).
// Re-run any time after regenerating the .raw; it rebuilds the same terrain in place.
//
// Scale law: 1 unit = 1 metre. Island is 1024 x 1024 m, up to 300 m tall, sea level at 30 m.
// The terrain is centred on the world origin (corner at -512,-512), mountains to the +Z (north),
// the launch beach to the -Z (south).
public static class BuildIsland
{
    const string RawPath  = "Assets/Terrain/Island1_Heightmap.raw";
    const string DataPath = "Assets/Terrain/Island1_TerrainData.asset";
    const int    Res      = 1025;                 // 2^10 + 1
    const float  WorldM   = 1024f;                // island span in metres
    const float  HeightM  = 300f;                 // max terrain height
    const float  SeaNorm  = 0.10f;                // sea level in the heightmap (normalized)
    const float  TerrainY = -SeaNorm * HeightM;   // lower the terrain so its coastline sits at world Y=0 (= the global ocean)

    [MenuItem("WildReach/Build Island Terrain")]
    static void Build()
    {
        if (!File.Exists(RawPath))
        {
            Debug.LogError($"[Island] Heightmap not found: {RawPath}. Run _tools/heightmap_island1.py first.");
            return;
        }

        byte[] bytes = File.ReadAllBytes(RawPath);
        long expected = (long)Res * Res * 2;
        if (bytes.LongLength != expected)
        {
            Debug.LogError($"[Island] {RawPath} is {bytes.LongLength} bytes, expected {expected} " +
                           $"({Res}x{Res} 16-bit). Regenerate the heightmap.");
            return;
        }

        // RAW is row-major, row 0 = south/beach. Unity heights[z, x].
        var heights = new float[Res, Res];
        for (int z = 0; z < Res; z++)
            for (int x = 0; x < Res; x++)
            {
                int i = (z * Res + x) * 2;
                ushort v = (ushort)(bytes[i] | (bytes[i + 1] << 8));
                heights[z, x] = v / 65535f;
            }

        // Reuse the TerrainData asset if it exists, so references survive a rebuild.
        var td = AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath);
        if (td == null)
        {
            td = new TerrainData();
            AssetDatabase.CreateAsset(td, DataPath);
        }
        td.heightmapResolution = Res;                          // set BEFORE size
        td.size = new Vector3(WorldM, HeightM, WorldM);
        td.SetHeights(0, 0, heights);
        EditorUtility.SetDirty(td);

        // Build / reuse the terrain GameObject, centred on the origin.
        var go = GameObject.Find("Island1_Terrain");
        if (go == null)
        {
            go = Terrain.CreateTerrainGameObject(td);
            go.name = "Island1_Terrain";
        }
        else
        {
            var t = go.GetComponent<Terrain>();
            var c = go.GetComponent<TerrainCollider>();
            if (t != null) t.terrainData = td;
            if (c != null) c.terrainData = td;
        }
        // Centre the island on the origin and lower it so its coastline sits at the global ocean (Y=0).
        go.transform.position = new Vector3(-WorldM / 2f, TerrainY, -WorldM / 2f);

        AssetDatabase.SaveAssets();
        Selection.activeGameObject = go;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log($"[Island] Built Island1_Terrain (1024x1024 m, 300 m tall), coastline at world Y=0 " +
                  $"(terrain lowered to y={TerrainY}). Mountains north (+Z), launch beach south (-Z), " +
                  "home-tree vale on the west slope. Run Setup Water for the global ocean, then Setup Ground to paint.");
    }
}
