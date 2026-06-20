using UnityEngine;

// Rotates the directional sun through a full day, driving its colour/intensity plus ambient
// and fog from a dark night to a warm noon. Runs the auto cycle in Play mode; in the Editor
// it still applies live so you can scrub `timeOfDay` or use the WildReach/Time menu to dress.
//
// Play-mode keys:  N = snap-toggle day <-> night (pauses auto)   P = pause/resume the auto cycle
[ExecuteAlways]
public class DayNightCycle : MonoBehaviour
{
    [SerializeField] Light sun;
    [Tooltip("Seconds for one full day-night cycle (Play mode). Higher = slower. Re-run Setup Day-Night Cycle to apply.")]
    [SerializeField] float dayLengthSeconds = 1800f;   // 30 min full cycle (~15 min daylight); was 720 = too fast
    [Tooltip("How far the sun/moon discs are parked from the camera.")]
    [SerializeField] float discDistance = 600f;
    [Range(0f, 1f)] [Tooltip("0/1 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset.")]
    [SerializeField] float timeOfDay = 0.35f;
    [SerializeField] bool autoCycle = true;
    [SerializeField] float sunAzimuth = -35f;   // compass direction the sun travels (Y rotation)

    [Header("Day")]
    [SerializeField] Color dayColor     = new Color(1f, 0.95f, 0.84f);
    [SerializeField] float dayIntensity = 1.15f;
    [SerializeField] Color dayAmbient   = new Color(0.55f, 0.52f, 0.46f);
    [SerializeField] Color dayFog       = new Color(0.74f, 0.82f, 0.90f);   // cool atmospheric haze, not orange
    [Header("Sunrise / sunset")]
    [SerializeField] Color horizonColor = new Color(1f, 0.55f, 0.30f);
    [Header("Night")]
    [SerializeField] float nightIntensity = 0.05f;
    [SerializeField] Color nightAmbient   = new Color(0.11f, 0.13f, 0.21f);   // moonlit navy, not black
    [SerializeField] Color nightFog       = new Color(0.07f, 0.09f, 0.16f);

    [Header("Sky bodies (2D billboards)")]
    [SerializeField] Transform sunDisc;          // the visible glowing sun in the sky (shown in day)

    [Header("Moon")]
    [SerializeField] Light moon;                 // cool directional fill, fades in at night
    [SerializeField] Transform moonDisc;         // the visible glowing moon in the sky
    [SerializeField] Color moonColor     = new Color(0.6f, 0.72f, 1f);
    [SerializeField] float moonIntensity = 0.28f;

    public bool IsDay => sun != null && -sun.transform.forward.y > 0.15f;

    // Read/restore the time of day (used by the save system so a resumed game keeps the same time).
    public float TimeOfDay { get => timeOfDay; set { timeOfDay = Mathf.Repeat(value, 1f); Apply(); } }

    void Awake()      { EnsureSun(); }
    void OnValidate() { EnsureSun(); Apply(); }

    void EnsureSun()
    {
        if (sun != null) return;
        sun = GetComponent<Light>();
        if (sun != null && sun.type == LightType.Directional) return;
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { sun = l; return; }
    }

    void Update()
    {
        if (Application.isPlaying)
        {
            if (Input.GetKeyDown(KeyCode.P)) autoCycle = !autoCycle;
            if (Input.GetKeyDown(KeyCode.N)) { autoCycle = false; timeOfDay = IsDay ? 0.0f : 0.5f; }

            if (autoCycle)
            {
                timeOfDay += Time.deltaTime / Mathf.Max(1f, dayLengthSeconds);
                if (timeOfDay >= 1f) timeOfDay -= 1f;
            }
        }
        Apply();
    }

    void Apply()
    {
        if (sun == null) return;

        sun.transform.rotation = Quaternion.Euler(timeOfDay * 360f - 90f, sunAzimuth, 0f);

        float height = -sun.transform.forward.y;          // >0 above horizon
        float day = Mathf.Clamp01(height / 0.35f);        // 0 at/below horizon, 1 once well up
        float night = 1f - day;

        sun.intensity = Mathf.Lerp(nightIntensity, dayIntensity, day);
        sun.color     = Color.Lerp(horizonColor, dayColor, day);   // orange low, warm-white high
        sun.enabled   = day > 0.01f;                               // sun off at night (moon takes over)

        RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, day);
        RenderSettings.fogColor     = Color.Lerp(nightFog, dayFog, day);

        // Sun disc: visible + parked at the sun's direction while the sun is up.
        PlaceDisc(sunDisc, sun.transform, day > 0.02f);

        // Moon ORBITS OPPOSITE the sun (half a cycle out of phase) on the same azimuth track, so as the sun sets on
        // one horizon the moon rises on the other — they cross the sky in turn instead of the moon popping in fixed.
        float moonUp = 0f;
        if (moon != null)
        {
            moon.transform.rotation = Quaternion.Euler((timeOfDay + 0.5f) * 360f - 90f, sunAzimuth, 0f);
            moonUp = -moon.transform.forward.y;                       // >0 once the moon is above the horizon
            moon.color = moonColor;
            moon.intensity = moonIntensity * night;                   // cool fill only while the sun is gone
            moon.enabled = night > 0.02f;
        }
        // Moon disc: DayNightCycle parks it at the MOON's direction itself (not via a separately-wired SkyBillboard,
        // which could point at the wrong light — that was the "moon stuck on the sun side" bug). Single source of truth.
        PlaceDisc(moonDisc, moon != null ? moon.transform : null, moonUp > 0.02f);
    }

    // Park a sky-body disc in the direction its light comes FROM, at a fixed distance from the camera, facing it.
    // Repairs any SkyBillboard's sourceLight so it can't fight us by following a stale/wrong light.
    void PlaceDisc(Transform disc, Transform body, bool visible)
    {
        if (disc == null) return;
        if (disc.gameObject.activeSelf != visible) disc.gameObject.SetActive(visible);
        if (!visible || body == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        var bb = disc.GetComponent<SkyBillboard>();
        if (bb != null) { bb.sourceLight = body; bb.distance = discDistance; }
        Vector3 dir = -body.forward;                                   // toward the body = opposite the light travel
        disc.position = cam.transform.position + dir * discDistance;
        disc.rotation = Quaternion.LookRotation(disc.position - cam.transform.position, Vector3.up);
    }

    // Used by the WildReach/Time menu for editor dressing.
    public void SetDay()   { timeOfDay = 0.5f; autoCycle = false; Apply(); }
    public void SetNight() { timeOfDay = 0.0f; autoCycle = false; Apply(); }
}
