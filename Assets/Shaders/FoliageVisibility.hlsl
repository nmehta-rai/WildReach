#ifndef WILDREACH_FOLIAGE_VISIBILITY
#define WILDREACH_FOLIAGE_VISIBILITY
float4 _FoliageViewTarget;
float _FoliageViewRadius;
float _FoliageFadeEnabled;

void ClipFoliageView(float3 worldPosition, float2 screenPosition)
{
    if (_FoliageFadeEnabled < 0.5) return;
    float3 cameraPosition = GetCameraPositionWS();
    float3 segment = _FoliageViewTarget.xyz - cameraPosition;
    float t = saturate(dot(worldPosition - cameraPosition, segment) / max(dot(segment, segment), 0.001));
    float clearance = length(worldPosition - (cameraPosition + t * segment));
    float visibility = smoothstep(_FoliageViewRadius * 0.35, _FoliageViewRadius, clearance);
    // A feathered opening, kept in the same opaque/depth passes to avoid sorting errors.
    float noise = frac(52.9829189 * frac(dot(floor(screenPosition), float2(0.06711056, 0.00583715))));
    clip(visibility - noise - 0.001);
}
#endif
