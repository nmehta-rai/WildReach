using UnityEngine;

// A stylized foam wake: a trail pinned to the waterline that draws behind this object while it moves
// across the surface, so the player and ship "push the water aside" instead of gliding with no reaction.
// The trail lives on a free world-space marker (not a child) so it stays put on the water as the object passes.
public class WaterWake : MonoBehaviour
{
    [SerializeField] float waterLevel  = 1.5f;
    [SerializeField] float width       = 1f;     // ship = wide, player = thin
    [SerializeField] float trailTime   = 2f;     // how long the wake lingers
    [SerializeField] float minSpeed    = 0.6f;   // below this, no wake
    [SerializeField] float surfaceBand = 1.6f;   // only wake when near the surface (not deep underwater / high on land)

    Transform marker;
    TrailRenderer trail;
    Vector3 lastPos;

    public void Configure(float water, float w, float t) { waterLevel = water; width = w; trailTime = t; }

    void Start()
    {
        lastPos = transform.position;

        marker = new GameObject(name + "_Wake").transform;   // world-space, not parented
        trail = marker.gameObject.AddComponent<TrailRenderer>();
        trail.time            = trailTime;
        trail.startWidth      = width;
        trail.endWidth        = width * 0.25f;
        trail.numCapVertices  = 4;
        trail.numCornerVertices = 2;
        trail.minVertexDistance = 0.3f;
        trail.material        = WakeMaterial();
        trail.alignment       = LineAlignment.View;

        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.55f, 0f),       new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;

        trail.emitting = false;
    }

    void LateUpdate()
    {
        Vector3 p = transform.position;
        marker.position = new Vector3(p.x, waterLevel + 0.05f, p.z);

        Vector3 flat = new Vector3(p.x - lastPos.x, 0f, p.z - lastPos.z);
        float speed = flat.magnitude / Mathf.Max(Time.deltaTime, 1e-4f);
        lastPos = p;

        bool nearSurface = Mathf.Abs(p.y - waterLevel) < surfaceBand;
        trail.emitting = nearSurface && speed > minSpeed;
    }

    static Material _mat;
    static Material WakeMaterial()
    {
        if (_mat == null)
        {
            var s = Shader.Find("Sprites/Default");   // unlit, transparent, respects the trail's vertex colours
            _mat = new Material(s) { name = "WaterWake" };
        }
        return _mat;
    }
}
