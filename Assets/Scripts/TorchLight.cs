using UnityEngine;

// Gives a torch light a living flicker — gentle intensity + range noise so it reads as fire, not a bulb.
public class TorchLight : MonoBehaviour
{
    [SerializeField] float flicker = 0.18f;
    [SerializeField] float speed = 3.2f;

    Light l; float baseI, baseR, seed;

    void Awake()
    {
        l = GetComponent<Light>();
        if (l != null) { baseI = l.intensity; baseR = l.range; }
        seed = transform.position.x * 7.13f + transform.position.z * 3.71f;
    }

    void Update()
    {
        if (l == null) return;
        float n = Mathf.PerlinNoise(seed, Time.time * speed);
        l.intensity = baseI * (1f - flicker * 0.5f + n * flicker);
        l.range = baseR * (0.96f + n * 0.08f);
    }
}
