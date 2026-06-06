using System;
using System.Collections.Generic;
using UnityEngine;

// A reusable bag of "what scatters here, and how dense" — e.g. a "Temperate Forest" palette of
// trees + mushrooms with their spacing/density and rules. Drop the SAME palette onto a zone of any
// island. Density is spacing + a keep-probability, so it scales to any zone size (the same answer
// the live grass uses) and "rare" things (blossom trees) are just a low density.
[CreateAssetMenu(menuName = "WildReach/Scatter Palette", fileName = "Palette")]
public class ScatterPalette : ScriptableObject
{
    public List<ScatterEntry> entries = new List<ScatterEntry>();
}

[Serializable]
public class ScatterEntry
{
    public GameObject prefab;
    [Tooltip("Grid spacing in metres between candidate spots — bigger = sparser.")]
    public float spacing = 8f;
    [Range(0f, 1f), Tooltip("Density: chance a candidate spot is used. Low = rare (e.g. blossom trees).")]
    public float density = 0.6f;
    public float minScale = 0.85f;
    public float maxScale = 1.25f;
    public float slopeLimit = 35f;

    [Header("Placement")]
    public bool  groundSit = false;        // drop so the mesh bottom rests on soil (props), else base at ground
    public float sinkDepth = 0f;           // >0: push the base this many metres INTO the ground (roots embed; hides float on bumps)
    public float footprint = 0f;           // >0: sample terrain over this radius (m) and sit on the LOWEST point (no float on slopes)
    public float normalizeHeight = 0f;     // >0: scale to this height in metres (raw fbx like the moss log)
    [Tooltip("Cluster near already-placed open-ground props (e.g. mushrooms at tree bases), not open ground.")]
    public bool  nearProps = false;

    public string Label => prefab != null ? prefab.name : "(empty)";
}
