using UnityEngine;

// Drives a single GLOBAL wind vector that shaders read (the grass blade shader bends by it; trees/foliage can too
// later). One source of truth for wind so everything sways together. Gentle base wind + slow gusts. Runs in the
// editor too so you can see the grass move while dressing the scene.
[ExecuteAlways]
[DisallowMultipleComponent]
public class WindManager : MonoBehaviour
{
    [Tooltip("Compass direction the wind blows (degrees).")]
    [Range(0f, 360f)] public float direction = 35f;
    [Tooltip("How far blade tips bend (metres).")]
    public float strength = 0.25f;
    [Tooltip("Sway speed.")]
    public float speed = 1.6f;
    [Tooltip("How much the strength pulses in gusts (0 = steady).")]
    [Range(0f, 1f)] public float gustiness = 0.4f;
    [Tooltip("How fast gusts come and go.")]
    public float gustSpeed = 0.25f;

    static readonly int WindParams = Shader.PropertyToID("_WindParams");

    void OnEnable()  { Apply(); }
    void Update()    { Apply(); }

    void Apply()
    {
        float a = direction * Mathf.Deg2Rad;
        var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        // gust: a slow swell layered on the base strength (Time.time works in edit + play via ExecuteAlways)
        float gust = 1f + gustiness * Mathf.Sin(Time.time * gustSpeed * Mathf.PI * 2f);
        float s = strength * gust;
        Shader.SetGlobalVector(WindParams, new Vector4(dir.x * s, dir.y * s, speed, 0f));
    }
}
