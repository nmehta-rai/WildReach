using UnityEngine;

// Opens only the foliage between the camera and the character, including leaves around the camera.
[DefaultExecutionOrder(1000)]
public class FoliageViewClearance : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float targetHeight = 0.9f;
    [SerializeField] float clearanceRadius = 1.25f;

    static readonly int Target = Shader.PropertyToID("_FoliageViewTarget");
    static readonly int Radius = Shader.PropertyToID("_FoliageViewRadius");
    static readonly int Enabled = Shader.PropertyToID("_FoliageFadeEnabled");

    public void SetTarget(Transform value) => target = value;

    void LateUpdate()
    {
        Shader.SetGlobalFloat(Enabled, target ? 1 : 0);
        if (!target) return;
        Shader.SetGlobalVector(Target, target.position + Vector3.up * targetHeight);
        Shader.SetGlobalFloat(Radius, clearanceRadius);
    }

    void OnDisable() => Shader.SetGlobalFloat(Enabled, 0);
}
