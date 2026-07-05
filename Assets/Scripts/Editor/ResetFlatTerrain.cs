using UnityEngine;
using UnityEditor;

// Scraps any existing terrain and drops in one clean FLAT square terrain for the
// slice. Scale law: 1 unit = 1 metre. Tweak SizeMetres to resize the slice.
public static class ResetFlatTerrain
{
    const float SizeMetres   = 150f; // square footprint (metres per side)
    const float HeightCap    = 30f;  // vertical capacity for future sculpting (flat for now)
    const int   HeightmapRes = 257;  // 2^n + 1
    const string DataPath    = "Assets/Slice_Terrain.asset";

    [MenuItem("WildReach/Reset Flat Terrain")]
    static void Reset()
    {
        // Remove existing terrains from the scene.
        foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include))
            Object.DestroyImmediate(t.gameObject);

        // Fresh flat terrain data (heights default to 0 == flat).
        var td = new TerrainData
        {
            heightmapResolution = HeightmapRes,
            size = new Vector3(SizeMetres, HeightCap, SizeMetres),
        };
        AssetDatabase.DeleteAsset(DataPath); // overwrite any prior slice terrain
        AssetDatabase.CreateAsset(td, DataPath);

        var go = Terrain.CreateTerrainGameObject(td);
        go.name = "SliceTerrain";
        // Centre the square on the world origin so the player spawns mid-island.
        go.transform.position = new Vector3(-SizeMetres / 2f, 0f, -SizeMetres / 2f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = go;
        Debug.Log($"Flat {SizeMetres}x{SizeMetres}m terrain created, centred on origin. " +
                  "Run 'WildReach/Setup Ground' once the grass texture is in to skin it.");
    }
}
