#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Assets/Shaders/Includes/InkNoise.hlsl"

SAMPLER(sampler_point_clamp);

void GetDepth_float(float2 uv, out float Depth)
{
    Depth = SHADERGRAPH_SAMPLE_SCENE_DEPTH(uv);
}


void GetNormal_float(float2 uv, out float3 Normal)
{
    Normal = SAMPLE_TEXTURE2D(_NormalsBuffer, sampler_point_clamp, uv).rgb;
}

float EyeDepth(float2 uv)
{
    return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
}

// Sobel on eye depth, relative to the center depth so far objects are not over-outlined.
float DepthSobel(float2 uv, float2 d)
{
    float tl = EyeDepth(uv + d * float2(-1, 1));
    float tc = EyeDepth(uv + d * float2(0, 1));
    float tr = EyeDepth(uv + d * float2(1, 1));
    float ml = EyeDepth(uv + d * float2(-1, 0));
    float mr = EyeDepth(uv + d * float2(1, 0));
    float bl = EyeDepth(uv + d * float2(-1, -1));
    float bc = EyeDepth(uv + d * float2(0, -1));
    float br = EyeDepth(uv + d * float2(1, -1));
    float gx = (tr + 2 * mr + br) - (tl + 2 * ml + bl);
    float gy = (tl + 2 * tc + tr) - (bl + 2 * bc + br);
    return sqrt(gx * gx + gy * gy) / EyeDepth(uv);
}

// Roberts cross on the view-space normal buffer.
float NormalRoberts(UnityTexture2D Normals, float2 uv, float2 d)
{
    float3 a = SAMPLE_TEXTURE2D(Normals, sampler_point_clamp, uv + d * float2(-1, -1)).rgb;
    float3 b = SAMPLE_TEXTURE2D(Normals, sampler_point_clamp, uv + d * float2(1, 1)).rgb;
    float3 c = SAMPLE_TEXTURE2D(Normals, sampler_point_clamp, uv + d * float2(1, -1)).rgb;
    float3 e = SAMPLE_TEXTURE2D(Normals, sampler_point_clamp, uv + d * float2(-1, 1)).rgb;
    return sqrt(dot(a - b, a - b) + dot(c - e, c - e));
}

// Depth edges are sampled through a noise-warped UV that changes on stepped time; normal edges stay put
// so the drawing keeps its structure.
float InkEdge(float2 uv, UnityTexture2D Normals, float2 warp, float2 d, float2 dNormal, float DepthThreshold, float NormalThreshold)
{
    float2 warpedUV = uv + warp;

    // Surfaces seen at a grazing angle have a steep depth slope; raise the threshold there.
    float facing = saturate(SAMPLE_TEXTURE2D(Normals, sampler_point_clamp, warpedUV).b * 2 - 1);
    float depthLimit = DepthThreshold * lerp(6, 1, facing);

    float depthEdge = smoothstep(depthLimit, depthLimit * 1.4, DepthSobel(warpedUV, d));
    float normalEdge = smoothstep(NormalThreshold, NormalThreshold * 1.3, NormalRoberts(Normals, uv, dNormal));
    return max(depthEdge, normalEdge);
}

// Brush-like ink line drawn over the image: the width follows a slow "pressure" noise, light strokes
// break up like a dry brush, and the line stops at the paper margin.
void InkOutline_float(float2 UV, UnityTexture2D MainTex, UnityTexture2D Normals, float3 OutlineColor, float Width,
    float DepthThreshold, float NormalThreshold, float Wobble, float WobbleFPS, float DryBrush, float Border, out float3 Color)
{
    float2 texel = 1.0 / _ScreenParams.xy;
    float2 px = UV * _ScreenParams.xy;
    float t = floor(_Time.y * WobbleFPS);
    float2 cell = px / 45.0;

    // The withered mode is drawn with a heavier, shakier line.
    Width *= 1 + 0.5 * _Wither;
    Wobble *= 1 + _Wither;

    float2 warp = (float2(InkNoise(cell + t * 7.3), InkNoise(cell + 31.7 + t * 5.1)) - 0.5) * Wobble * texel;
    float pressure = lerp(0.5, 1.5, InkNoise(cell * 0.6 + t * 3.1));
    float2 d = texel * Width * pressure;
    float2 dNormal = texel * Width * 0.6;

    // Four sub-pixel taps soften the stair-steps of the depth buffer.
    float edge = 0;
    edge += InkEdge(UV + texel * float2(0.35, 0.15), Normals, warp, d, dNormal, DepthThreshold, NormalThreshold);
    edge += InkEdge(UV + texel * float2(-0.15, 0.35), Normals, warp, d, dNormal, DepthThreshold, NormalThreshold);
    edge += InkEdge(UV + texel * float2(-0.35, -0.15), Normals, warp, d, dNormal, DepthThreshold, NormalThreshold);
    edge += InkEdge(UV + texel * float2(0.15, -0.35), Normals, warp, d, dNormal, DepthThreshold, NormalThreshold);
    edge *= 0.25;

    float bristles = smoothstep(0.2, 0.55, InkNoise(px / 2.5 + t * 1.9));
    edge *= lerp(1, bristles, DryBrush * saturate(1.3 - pressure));
    edge *= 1 - PaperBorder(UV, Border);

    float3 scene = SAMPLE_TEXTURE2D(MainTex, MainTex.samplerstate, UV).rgb;
    Color = lerp(scene, OutlineColor, saturate(edge));
}
