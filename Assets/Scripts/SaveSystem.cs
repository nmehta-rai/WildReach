using UnityEngine;

// Persists the player's position + facing + time of day so quitting and relaunching RESUMES where you left off
// (stored in PlayerPrefs). Autosaves on an interval and on quit/pause. "Restart to Spawn" (pause menu) just moves
// the player to the spawn point; the next autosave captures that, so a later resume correctly starts at spawn.
// First-ever launch has no save -> the player stays at their scene spawn position.
[DisallowMultipleComponent]
public class SaveSystem : MonoBehaviour
{
    [SerializeField] float autosaveInterval = 5f;

    Transform player; Rigidbody rb; DayNightCycle dnc; float timer;
    const string K = "wr.save.";

    void Start()
    {
        var move = Object.FindFirstObjectByType<PlayerMovement>();
        if (move != null) { player = move.transform; rb = move.GetComponent<Rigidbody>(); }
        dnc = Object.FindFirstObjectByType<DayNightCycle>();
        Load();
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer >= autosaveInterval) { timer = 0f; Save(); }
    }

    void OnApplicationQuit()              => Save();
    void OnApplicationPause(bool paused)  { if (paused) Save(); }

    public void Save()
    {
        if (player == null) return;
        var p = player.position;
        PlayerPrefs.SetInt(K + "has", 1);
        PlayerPrefs.SetFloat(K + "px", p.x); PlayerPrefs.SetFloat(K + "py", p.y); PlayerPrefs.SetFloat(K + "pz", p.z);
        PlayerPrefs.SetFloat(K + "ry", player.eulerAngles.y);
        if (dnc != null) PlayerPrefs.SetFloat(K + "tod", dnc.TimeOfDay);
        PlayerPrefs.Save();
    }

    public void Load()
    {
        if (player == null || PlayerPrefs.GetInt(K + "has", 0) == 0) return;   // first run -> stay at scene spawn
        player.position = new Vector3(PlayerPrefs.GetFloat(K + "px"), PlayerPrefs.GetFloat(K + "py"), PlayerPrefs.GetFloat(K + "pz"));
        player.rotation = Quaternion.Euler(0f, PlayerPrefs.GetFloat(K + "ry"), 0f);
        if (rb != null) rb.linearVelocity = Vector3.zero;
        if (dnc != null && PlayerPrefs.HasKey(K + "tod")) dnc.TimeOfDay = PlayerPrefs.GetFloat(K + "tod");
    }

    public void ClearSave() { PlayerPrefs.DeleteKey(K + "has"); }
}
