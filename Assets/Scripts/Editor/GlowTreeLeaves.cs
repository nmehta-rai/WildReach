using UnityEngine;
using UnityEditor;

// Makes the blue canopy leaf materials self-illuminate (ethereal cyan-blue), so the hero
// tree glows like a magical landmark. Emission respects the alpha cutout, so only the leaf
// shapes glow, not the gaps. Reads subtle in day, magical at night.
public static class GlowTreeLeaves
{
    const string Dir = "Assets/Environment/MotherTree/Canopy/";

    [MenuItem("WildReach/Glow Tree Leaves")]
    static void Glow()
    {
        int n = 0;
        for (int i = 1; i <= 4; i++)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Dir + $"Leaf_Mat_{i}.mat");
            if (mat == null) continue;

            var tex = mat.GetTexture("_BaseMap") as Texture2D;
            mat.EnableKeyword("_EMISSION");
            if (tex) mat.SetTexture("_EmissionMap", tex);                  // leaves emit their own colour
            mat.SetColor("_EmissionColor", new Color(0.45f, 0.8f, 1f) * 0.5f); // ethereal cyan-blue (subtle in day)
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
            n++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Canopy] {n} leaf materials now glow cyan-blue. " +
                  "Strongest read at night — flip WildReach/Time - Night to see the magical tree.");
    }
}
