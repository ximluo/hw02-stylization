#include "Assets/Shaders/Includes/InkNoise.hlsl"

void GetMainLight_float(float3 WorldPos, out float3 Color, out float3 Direction, out float DistanceAtten, out float ShadowAtten)
{
#ifdef SHADERGRAPH_PREVIEW
    Direction = normalize(float3(0.5, 0.5, 0));
    Color = 1;
    DistanceAtten = 1;
    ShadowAtten = 1;
#else
#if SHADOWS_SCREEN
        float4 clipPos = TransformWorldToClip(WorldPos);
        float4 shadowCoord = ComputeScreenPos(clipPos);
#else
    float4 shadowCoord = TransformWorldToShadowCoord(WorldPos);
#endif

    Light mainLight = GetMainLight(shadowCoord);
    Direction = mainLight.direction;
    Color = mainLight.color;
    // _SunDim (set by the day-night script) fades the direct light out while the sun or moon is on the horizon.
    DistanceAtten = mainLight.distanceAttenuation * (1 - _SunDim);
    ShadowAtten = mainLight.shadowAttenuation * (1 - _SunDim);
#endif
}

void ComputeAdditionalLighting_float(float3 WorldPosition, float3 WorldNormal,
    float2 Thresholds, float3 RampedDiffuseValues,
    out float3 Color, out float Diffuse)
{
    Color = float3(0, 0, 0);
    Diffuse = 0;

#ifndef SHADERGRAPH_PREVIEW

    int pixelLightCount = GetAdditionalLightsCount();
    
    for (int i = 0; i < pixelLightCount; ++i)
    {
        Light light = GetAdditionalLight(i, WorldPosition);
        float4 tmp = unity_LightIndices[i / 4];
        uint light_i = tmp[i % 4];

        half shadowAtten = light.shadowAttenuation * AdditionalLightRealtimeShadow(light_i, WorldPosition, light.direction);
        
        half NdotL = saturate(dot(WorldNormal, light.direction));
        half distanceAtten = light.distanceAttenuation;

        half thisDiffuse = distanceAtten * shadowAtten * NdotL;
        
        half rampedDiffuse = 0;
        
        if (thisDiffuse < Thresholds.x)
        {
            rampedDiffuse = RampedDiffuseValues.x;
        }
        else if (thisDiffuse < Thresholds.y)
        {
            rampedDiffuse = RampedDiffuseValues.y;
        }
        else
        {
            rampedDiffuse = RampedDiffuseValues.z;
        }

        
        if (light.distanceAttenuation <= 0)
        {
            rampedDiffuse = 0.0;
        }

        Color += max(rampedDiffuse, 0) * light.color.rgb;
        Diffuse += rampedDiffuse;
    }
    
    if (Diffuse <= 0.3)
    {
        Color = float3(0, 0, 0);
        Diffuse = 0;
    }
    
#endif
}

void ChooseColor_float(float3 Highlight, float3 Midtone, float3 Shadow, float Diffuse, float2 Thresholds, out float3 OUT)
{
    if (Diffuse < Thresholds.x)
    {
        OUT = Shadow;
    }
    else if (Diffuse < Thresholds.y)
    {
        OUT = Midtone;
    }
    else
    {
        OUT = Highlight;
    }
}

void ChooseColor3_float(float3 Highlight, float3 Midtone, float3 Shadow, float Diffuse, float ThresholdHigh, float ThresholdLow, float Smoothness, out float3 OUT)
{
    float edge = max(Smoothness * 0.5, 0.0001);
    float low = smoothstep(ThresholdLow - edge, ThresholdLow + edge, Diffuse);
    float high = smoothstep(ThresholdHigh - edge, ThresholdHigh + edge, Diffuse);
    OUT = lerp(lerp(Shadow, Midtone, low), Highlight, high);
}

// Lets the shadow texture push the lighting value up or down so band edges and shadows break into brush strokes.
// Fully lit areas are left clean.
void ShadowPattern_float(float Diffuse, float Pattern, float Strength, float ThresholdHigh, out float Out)
{
    float fade = 1 - smoothstep(ThresholdHigh, ThresholdHigh + 0.3, Diffuse);
    Out = saturate(Diffuse + (Pattern - 0.5) * 2 * Strength * fade);
}

// Adds a stepped specular, a lit-side rim and the ramped additional lights on top of the main-light toon color.
void InkLighting_float(float3 Base, float3 WorldPos, float3 WorldNormal, float3 LightDir, float3 LightColor, float ShadowAtten,
    float3 Highlight, float ThresholdHigh, float ThresholdLow,
    float3 SpecColor, float SpecSize, float3 RimColor, float RimAmount, out float3 Color)
{
    float3 N = normalize(WorldNormal);
    float3 V = normalize(_WorldSpaceCameraPos - WorldPos);
    float NdotL = saturate(dot(N, LightDir));

    float spec = saturate(dot(N, normalize(LightDir + V)));
    float specEdge = 1 - 0.2 * SpecSize * SpecSize;
    float specBand = smoothstep(specEdge, specEdge + fwidth(spec) + 0.0001, spec) * step(0.0001, SpecSize);

    float rim = (1 - saturate(dot(N, V))) * pow(NdotL, 0.25);
    float rimEdge = 1 - RimAmount;
    float rimBand = smoothstep(rimEdge, rimEdge + fwidth(rim) + 0.0001, rim) * step(0.0001, RimAmount);

    float3 addColor;
    float addDiffuse;
    ComputeAdditionalLighting_float(WorldPos, N, float2(ThresholdLow, ThresholdHigh), float3(0, 0.5, 1), addColor, addDiffuse);

    Color = Base + (specBand * SpecColor + rimBand * RimColor) * LightColor * ShadowAtten + addColor * Highlight;
}

// Bobs each object around its pivot with its own phase. Bloom scales it from nothing (0) to full size (1),
// and with Shrink on, the global _Wither value closes each object at its own moment.
void InkSway_float(float3 PositionOS, float Amount, float Speed, float Bloom, float Shrink, out float3 Out)
{
    float3 pivot = TransformObjectToWorld(float3(0, 0, 0));
    float phase = dot(pivot, float3(1.7, 2.3, 1.1));
    float t = _Time.y * Speed + phase;
    float3 offsetWS = float3(sin(t), 0.6 * sin(t * 1.7 + 1.3), cos(t * 0.8)) * Amount;
    float puff = 1 + 0.06 * sin(t * 2.3);
    float open = saturate((1 - _Wither) * 1.6 - 0.6 * frac(phase * 3.7));
    open = lerp(1, open * open * (3 - 2 * open), Shrink);
    Out = (PositionOS * puff + TransformWorldToObjectDir(offsetWS, false)) * Bloom * open;
}

// Bands of a second color drift across the surface on stepped time, so it reads as redrawn frames.
// Glow swaps the lit color for the unlit highlight color, for things that give off their own light.
void InkShimmer_float(float3 Base, float3 WorldPos, float3 ShimmerColor, float Speed, float Strength,
    float3 Highlight, float Glow, out float3 Color)
{
    float t = floor(_Time.y * 8) / 8;
    float wave = sin(dot(WorldPos, float3(2.1, 3.3, 1.7)) + t * Speed);
    float band = smoothstep(0.55, 0.9, wave * 0.5 + 0.5);
    Color = lerp(lerp(Base, Highlight, Glow), ShimmerColor, band * Strength);
}

// Turns the shaded color into ink on paper: its brightness picks a value between an ink color and the
// paper color. The three toon tones are already discrete, so they come out as three ink values.
void SumiWash_float(float3 Base, float3 InkColor, float3 PaperColor, float Contrast, out float3 Color)
{
    float value = dot(Base, float3(0.299, 0.587, 0.114));
    Color = lerp(InkColor, PaperColor, saturate((value - 0.75) * Contrast + 0.5));
}
