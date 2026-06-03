using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

// Plays the project's existing Humanoid clips on whatever Humanoid model this sits on, so we can
// review every animation on the new rig. Left/Right arrows cycle clips; the current one loops.
// Uses the Playables API directly (no Animator Controller needed) — Humanoid clips retarget onto
// the model's avatar automatically.
public class AnimationGallery : MonoBehaviour
{
    public AnimationClip[] clips;
    public string[] labels;

    Animator anim;
    PlayableGraph graph;
    AnimationClipPlayable clip;
    int idx;

    void OnEnable()
    {
        anim = GetComponent<Animator>();
        if (clips != null && clips.Length > 0) Build();
    }

    void Build()
    {
        if (graph.IsValid()) graph.Destroy();
        graph = PlayableGraph.Create("AnimationGallery");
        var output = AnimationPlayableOutput.Create(graph, "out", anim);
        clip = AnimationClipPlayable.Create(graph, clips[idx]);
        output.SetSourcePlayable(clip);
        graph.Play();
    }

    void Update()
    {
        if (clips == null || clips.Length == 0) return;
        if (Input.GetKeyDown(KeyCode.RightArrow)) { idx = (idx + 1) % clips.Length; Build(); }
        if (Input.GetKeyDown(KeyCode.LeftArrow))  { idx = (idx - 1 + clips.Length) % clips.Length; Build(); }
        // manual loop
        if (clip.IsValid() && clips[idx].length > 0f && clip.GetTime() >= clips[idx].length)
            clip.SetTime(0);
    }

    void OnGUI()
    {
        if (clips == null || clips.Length == 0) return;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.white;
        string name = (labels != null && idx < labels.Length) ? labels[idx] : clips[idx].name;
        GUI.Label(new Rect(24, 20, 1000, 40), $"[{idx + 1}/{clips.Length}]  {name}", style);
        GUI.Label(new Rect(24, 52, 1000, 30), "← / →  switch animation", new GUIStyle(GUI.skin.label) { fontSize = 15 });
    }

    void OnDisable() { if (graph.IsValid()) graph.Destroy(); }
}
