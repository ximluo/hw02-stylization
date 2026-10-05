#ifndef INK_NOISE_INCLUDED
#define INK_NOISE_INCLUDED

// Set from script: 0 while the tree is in bloom, 1 when it has withered.
float _Wither;
// Set from script: 1 while the sun or moon sits on the horizon, 0 otherwise.
float _SunDim;

float InkHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float InkNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3 - 2 * f);
    return lerp(lerp(InkHash(i), InkHash(i + float2(1, 0)), f.x),
                lerp(InkHash(i + float2(0, 1)), InkHash(i + float2(1, 1)), f.x), f.y);
}

float InkFbm(float2 p)
{
    float sum = 0;
    float amp = 0.5;
    for (int i = 0; i < 4; i++)
    {
        sum += amp * InkNoise(p);
        p = p * 2.03 + 17.3;
        amp *= 0.5;
    }
    return sum;
}

float InkHash3(float3 p)
{
    return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453);
}

float InkNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3 - 2 * f);
    float bottom = lerp(lerp(InkHash3(i), InkHash3(i + float3(1, 0, 0)), f.x),
                        lerp(InkHash3(i + float3(0, 1, 0)), InkHash3(i + float3(1, 1, 0)), f.x), f.y);
    float top = lerp(lerp(InkHash3(i + float3(0, 0, 1)), InkHash3(i + float3(1, 0, 1)), f.x),
                     lerp(InkHash3(i + float3(0, 1, 1)), InkHash3(i + float3(1, 1, 1)), f.x), f.y);
    return lerp(bottom, top, f.z);
}

float InkFbm3(float3 p)
{
    float sum = 0;
    float amp = 0.5;
    for (int i = 0; i < 4; i++)
    {
        sum += amp * InkNoise3(p);
        p = p * 2.03 + 17.3;
        amp *= 0.5;
    }
    return sum;
}

// 1 on the unpainted paper margin, 0 inside the painting, with a rough edge between them.
float PaperBorder(float2 uv, float border)
{
    float2 q = abs(uv - 0.5) * 2;
    float edge = max(q.x, q.y) + (InkFbm(uv * _ScreenParams.xy / 55.0) - 0.5) * 0.14;
    return smoothstep(1 - border, 1 - border + 0.05, edge) * step(0.0001, border);
}

#endif
