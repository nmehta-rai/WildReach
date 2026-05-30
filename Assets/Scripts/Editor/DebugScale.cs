using UnityEngine;
using UnityEditor;

// Prints the real, live scale numbers to the Console so we stop guessing from screenshots.
public static class DebugScale
{
    [MenuItem("WildReach/Debug Scale")]
    static void Run()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain != null && terrain.terrainData != null)
            Debug.Log($"[SCALE] Terrain size = {terrain.terrainData.size} (x,z metres), at pos {terrain.transform.position}");
        else
            Debug.Log("[SCALE] No active terrain found.");

        var capsule = GameObject.Find("Capsule");
        if (capsule != null)
        {
            var col = capsule.GetComponent<CapsuleCollider>();
            if (col != null)
                Debug.Log($"[SCALE] Capsule collider height = {col.height * capsule.transform.lossyScale.y}m (world), capsule lossyScale = {capsule.transform.lossyScale}");

            // Real rendered height = combined bounds of every renderer under the character.
            var rends = capsule.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                Debug.Log($"[SCALE] VISIBLE character mesh bounds = {b.size} (w,h,d metres). Rendered HEIGHT = {b.size.y:F2}m");
            }
            Debug.Log($"[SCALE] Capsule position = {capsule.transform.position}");
        }
        else Debug.Log("[SCALE] No 'Capsule' found in scene.");

        var tree = GameObject.Find("MotherTree");
        if (tree != null)
        {
            var rends = tree.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                Debug.Log($"[SCALE] MotherTree rendered HEIGHT = {b.size.y:F2}m, scale = {tree.transform.localScale}");
            }
        }
    }
}
