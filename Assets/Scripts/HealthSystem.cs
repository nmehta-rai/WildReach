using UnityEngine;

// Simple heart-based health. Starts at maxHearts (3) and is whole-hearts for now.
public class HealthSystem : MonoBehaviour
{
    [SerializeField] int maxHearts = 3;
    int hearts;

    public int MaxHearts => maxHearts;
    public int Hearts => hearts;
    public bool IsDead => hearts <= 0;

    void Awake() => hearts = maxHearts;

    public void Damage(int n = 1) => hearts = Mathf.Max(0, hearts - n);
    public void Heal(int n = 1)   => hearts = Mathf.Min(maxHearts, hearts + n);
    public void SetMaxHearts(int n) { maxHearts = Mathf.Max(1, n); hearts = Mathf.Min(hearts, maxHearts); }
}
