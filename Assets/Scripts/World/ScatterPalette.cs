using System;
using System.Collections.Generic;
using UnityEngine;

// A reusable bag of "what scatters here, and how much" — e.g. a "Temperate Forest" palette of
// trees + bushes + mushrooms with their counts and rules. Drop the SAME palette onto a zone of
// any island. This is the literal answer to "which trees, how many".
[CreateAssetMenu(menuName = "WildReach/Scatter Palette", fileName = "Palette")]
public class ScatterPalette : ScriptableObject
{
    public List<ScatterEntry> entries = new List<ScatterEntry>();
}

[Serializable]
public class ScatterEntry
{
    public GameObject prefab;
    [Min(0)] public int count = 20;       // how many of this prefab in a zone using this palette
    public float minScale = 0.85f;
    public float maxScale = 1.25f;
    public float spacing  = 4f;            // min distance between this prefab's own instances
    public float slopeLimit = 35f;         // skip ground steeper than this (deg)

    [Header("Placement")]
    public bool  groundSit = false;        // drop so the mesh bottom rests on soil (props); else base at ground (trees)
    public float normalizeHeight = 0f;     // >0: scale to this height in metres (raw fbx like the moss log)

    public string Label => prefab != null ? prefab.name : "(empty)";
}
