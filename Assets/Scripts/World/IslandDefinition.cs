using System;
using System.Collections.Generic;
using UnityEngine;

// The declarative recipe for ONE island. Everything that used to be hardcoded across 40 scripts
// lives here as data you edit in the Inspector / Island Builder. Make a second island = make a
// second asset; the builder is island-agnostic.
//
// Zones and textures are DELIBERATELY decoupled:
//   - zones   = painted regions that drive scatter + grass + rules (the `zoneMap`)
//   - textures= a free set of TerrainLayers you paint anywhere with Unity's native brush
// so a single zone (Forest) can have as many ground textures as you like.
[CreateAssetMenu(menuName = "WildReach/Island Definition", fileName = "Island")]
public class IslandDefinition : ScriptableObject
{
    [Tooltip("Name of the Terrain object in the scene this island builds onto.")]
    public string terrainName = "Island1_Terrain";

    [Tooltip("Fixed seed -> deterministic scatter. Change it to reshuffle.")]
    public int seed = 12345;

    [Header("Zones (painted regions)")]
    public List<IslandZone> zones = new List<IslandZone>();

    [Tooltip("Per-texel zone index map (R channel = zone index + 1; 0 = unpainted). Painted by the Island Builder.")]
    public Texture2D zoneMap;
    public int zoneMapRes = 512;

    [Header("Ground textures (paint freely with Unity's brush — not tied to zones)")]
    public List<TerrainLayer> groundTextures = new List<TerrainLayer>();
}

[Serializable]
public class IslandZone
{
    public string name = "Zone";
    [Tooltip("Identity + overlay colour while painting.")]
    public Color color = new Color(0.30f, 0.75f, 0.35f);
    [Tooltip("Show this zone's painted region in the Scene overlay.")]
    public bool showOverlay = true;

    [Tooltip("What scatters in this zone.")]
    public ScatterPalette palette;

    [Header("Grass")]
    public bool grass = true;
    [Range(0f, 1f)] public float grassDensity = 0.7f;

    [Header("Rules")]
    public float slopeLimit = 32f;   // no scatter on ground steeper than this (deg)
    public float clearance  = 0f;    // keep this far from 'HomeTree' (0 = ignore)
}
