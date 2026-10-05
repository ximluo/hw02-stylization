#include "Assets/Shaders/Includes/InkNoise.hlsl"

// Watercolor sky: wash clouds with a darker pigment rim, a glow along the horizon that leans toward the
// sun or moon, a soft disc for the sun or moon itself, and splattered stars.
// Noise is sampled on the view direction in 3D, so the sky has no seam.
void WashSky_float(float3 Direction, float3 SkyColor, float3 CloudColor, float3 GlowColor, float3 DiscColor,
    float CloudScale, float CloudAmount, float DiscSize, float Stars, out float3 Color)
{
    float3 d = normalize(Direction);
#ifdef SHADERGRAPH_PREVIEW
    float3 toLight = normalize(float3(0.5, 0.5, 0));
#else
    float3 toLight = _MainLightPosition.xyz;
#endif

    float t = _Time.y * 0.02;
    float3 p = d * CloudScale;
    float3 warp = float3(InkFbm3(p * 0.8 + t), InkFbm3(p * 0.8 + 7.1 - t), InkFbm3(p * 0.8 + 3.3));
    float cloud = InkFbm3(p + warp * 1.6);
    float body = smoothstep(0.46, 0.56, cloud);
    float rim = body * (1 - smoothstep(0.56, 0.68, cloud));

    float3 cell = floor(d * 70);
    float star = step(0.975, InkHash3(cell)) * smoothstep(0.3, 0.12, length(frac(d * 70) - 0.5));
    float3 color = lerp(SkyColor, float3(1, 1, 1), star * Stars * (1 - body));

    // Withered: the clouds turn to heavy ink blots and the glow goes out.
    float3 cloudColor = lerp(CloudColor, float3(0.2, 0.17, 0.26), _Wither);
    float cloudStrength = lerp(0.5, 0.8, _Wither) * body + 0.35 * rim;
    color = lerp(color, cloudColor, cloudStrength * CloudAmount);

    float towardLight = dot(normalize(d.xz + 0.0001), normalize(toLight.xz + 0.0001)) * 0.5 + 0.5;
    float horizon = exp(-abs(d.y) * 11);
    color = lerp(color, GlowColor, horizon * (0.3 + 0.7 * towardLight) * 0.6 * (1 - _Wither));

    float facing = dot(d, toLight);
    float disc = smoothstep(cos(DiscSize * 1.12), cos(DiscSize), facing);
    float discRim = disc * (1 - smoothstep(cos(DiscSize * 0.92), cos(DiscSize * 0.7), facing));
    color = lerp(color, DiscColor * (1 - 0.22 * discRim), disc * (1 - 0.6 * _Wither));

    Color = color;
}
