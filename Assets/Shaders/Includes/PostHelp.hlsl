#include "Assets/Shaders/Includes/InkNoise.hlsl"

// Watercolor-on-paper look: wobbly wash edges, pigment pooling where colors meet, paper grain,
// uneven wash density and an unpainted rough border.
void PaperWash_float(float2 UV, UnityTexture2D MainTex, float3 PaperColor, float Grain, float EdgeDarken, float Bleed,
    float Border, out float3 Color)
{
    float2 px = UV * _ScreenParams.xy;
    float2 texel = 1.0 / _ScreenParams.xy;

    float2 warp = float2(InkFbm(px / 70.0), InkFbm(px / 70.0 + 19.7)) - 0.5;
    float2 uv = UV + warp * Bleed * texel;
    float3 c = SAMPLE_TEXTURE2D(MainTex, MainTex.samplerstate, uv).rgb;

    // Withered: the color drains away.
    c = lerp(c, dot(c, float3(0.299, 0.587, 0.114)) * float3(0.93, 0.9, 1.0), 0.55 * _Wither);

    float3 dx = SAMPLE_TEXTURE2D(MainTex, MainTex.samplerstate, uv + float2(2, 0) * texel).rgb
              - SAMPLE_TEXTURE2D(MainTex, MainTex.samplerstate, uv - float2(2, 0) * texel).rgb;
    float3 dy = SAMPLE_TEXTURE2D(MainTex, MainTex.samplerstate, uv + float2(0, 2) * texel).rgb
              - SAMPLE_TEXTURE2D(MainTex, MainTex.samplerstate, uv - float2(0, 2) * texel).rgb;
    float pooling = saturate(length(dx) + length(dy));
    c *= 1 - EdgeDarken * pooling;

    float density = 1 - dot(c, float3(0.299, 0.587, 0.114));
    float grain = 0.6 * InkFbm(px / 3.0) + 0.4 * InkNoise(px / 1.3);
    c *= 1 + Grain * (grain - 0.5) * (0.4 + 1.6 * density);

    float wash = InkFbm(px / 240.0 + 5.2);
    c = lerp(c, float3(1, 1, 1), saturate((wash - 0.45) * 0.5) * density);

    c *= PaperColor;

    float3 paper = PaperColor * (1 + Grain * 0.5 * (grain - 0.5));
    Color = lerp(c, paper, PaperBorder(UV, Border));
}
