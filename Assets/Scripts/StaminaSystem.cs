using UnityEngine;

public class StaminaSystem : MonoBehaviour
{
    [SerializeField] float maxStamina = 100f;
    [SerializeField] float runDrainRate = 20f;
    [SerializeField] float climbDrainRate = 15f;
    [SerializeField] float rechargeRate = 10f;
    [SerializeField] float rechargeDelay = 1f;

    float stamina;
    float rechargeTimer;

    public float Stamina => stamina;
    public float MaxStamina => maxStamina;
    public bool IsEmpty => stamina <= 0f;

    void Awake() => stamina = maxStamina;

    public void DrainRun(float dt)
    {
        stamina = Mathf.Max(0, stamina - runDrainRate * dt);
        rechargeTimer = rechargeDelay;
    }

    public void DrainClimb(float dt)
    {
        stamina = Mathf.Max(0, stamina - climbDrainRate * dt);
        rechargeTimer = rechargeDelay;
    }

    public void Tick(float dt, bool draining)
    {
        if (!draining)
        {
            if (rechargeTimer > 0) { rechargeTimer -= dt; return; }
            stamina = Mathf.Min(maxStamina, stamina + rechargeRate * dt);
        }
    }
}
