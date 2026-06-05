using System.Linq;
using UnityEngine;

// Resolves a named painted zone on an island and answers "is this world point inside it?". Shared by the
// Predator and ZoneRoamer so any creature can be bounded to a territory. Mirrors GrassField's
// world -> uv -> zone-index (R channel = zone index + 1) mapping.
public class ZoneSampler
{
    Color32[] px; int res, index = -1; Vector3 tpos, tsize; bool ready;
    public bool Ready => ready;

    // Bind to a zone by name. island may be auto-resolved (the one matching this terrain) if passed null.
    // Returns true only if the zone resolved AND its map is readable.
    public bool Bind(ref IslandDefinition island, Terrain terrain, string zoneName)
    {
        ready = false; index = -1;
        if (terrain == null) return false;
        if (island == null)
            island = Resources.FindObjectsOfTypeAll<IslandDefinition>()
                     .FirstOrDefault(d => d.terrainName == terrain.gameObject.name)
                     ?? Resources.FindObjectsOfTypeAll<IslandDefinition>().FirstOrDefault();
        if (island == null || island.zoneMap == null) return false;

        for (int i = 0; i < island.zones.Count; i++)
            if (string.Equals(island.zones[i].name, zoneName, System.StringComparison.OrdinalIgnoreCase)) { index = i; break; }
        if (index < 0) return false;

        try { px = island.zoneMap.GetPixels32(); } catch { px = null; }
        res = island.zoneMap.width;
        tpos = terrain.transform.position; tsize = terrain.terrainData.size;
        ready = px != null && res > 0;
        return ready;
    }

    public bool Contains(Vector3 w)
    {
        if (!ready) return false;
        float u = (w.x - tpos.x) / tsize.x, v = (w.z - tpos.z) / tsize.z;
        if (u < 0f || u > 1f || v < 0f || v > 1f) return false;
        int zx = Mathf.Clamp(Mathf.RoundToInt(u * (res - 1)), 0, res - 1);
        int zy = Mathf.Clamp(Mathf.RoundToInt(v * (res - 1)), 0, res - 1);
        return px[zy * res + zx].r - 1 == index;
    }
}
