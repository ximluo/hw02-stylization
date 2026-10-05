using UnityEngine;

// Carries the sun across the sky, brings the moon up from the other side at night, and recolors the
// main light, the skybox and the night lights to match.
public class DayNightCycle : MonoBehaviour
{
    [System.Serializable]
    public struct Look
    {
        public Color light, sky, cloud, glow, disc;
    }

    public Light sun;
    public Light[] nightLights;
    public float dayLength = 36f;
    [Range(0, 1)] public float startTime = 0.153f;
    public float nightLightBoost = 1.5f;

    public Look day = new Look
    {
        light = new Color(1f, 0.98f, 0.94f), sky = Color.white, cloud = new Color(0.78f, 0.66f, 0.9f),
        glow = new Color(0.98f, 0.93f, 0.55f), disc = new Color(0.95f, 0.36f, 0.28f),
    };
    public Look dusk = new Look
    {
        light = new Color(1f, 0.62f, 0.42f), sky = new Color(1f, 0.86f, 0.74f), cloud = new Color(0.8f, 0.45f, 0.55f),
        glow = new Color(1f, 0.6f, 0.3f), disc = new Color(0.98f, 0.4f, 0.2f),
    };
    public Look night = new Look
    {
        light = new Color(0.42f, 0.5f, 0.85f), sky = new Color(0.2f, 0.22f, 0.42f), cloud = new Color(0.12f, 0.13f, 0.3f),
        glow = new Color(0.45f, 0.5f, 0.8f), disc = new Color(0.96f, 0.96f, 0.88f),
    };

    static readonly int SunDimId = Shader.PropertyToID("_SunDim");
    // The sun's arc leans toward -Z so the front of the scene is lit at noon.
    static readonly Vector3 ArcUp = new Vector3(0, 0.819f, -0.574f);
    Material sky;
    float[] baseIntensities;

    void Start()
    {
        // Work on a copy so the skybox asset keeps its daytime colors.
        sky = new Material(RenderSettings.skybox);
        RenderSettings.skybox = sky;
    }

    void Update()
    {
        Apply(Mathf.Repeat(startTime + Time.time / dayLength, 1));
    }

    // time: 0 sunrise, 0.25 noon, 0.5 sunset, 0.75 midnight
    public void Apply(float time)
    {
        float angle = time * Mathf.PI * 2;
        float height = Mathf.Sin(angle);
        Vector3 toSun = Mathf.Cos(angle) * Vector3.left + height * ArcUp;
        // Below the horizon the light comes from the moon, directly opposite the sun.
        sun.transform.rotation = Quaternion.LookRotation(height >= 0 ? -toSun : toSun);

        float dayAmount = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.1f, 0.25f, height));
        float duskAmount = 0.85f * (1 - Mathf.Clamp01(Mathf.Abs(height) / 0.35f));
        sun.color = Blend(day.light, dusk.light, night.light, dayAmount, duskAmount);
        // Fade the direct light out at the horizon so the switch between sun and moon is not visible.
        Shader.SetGlobalFloat(SunDimId, 1 - Mathf.SmoothStep(0, 1, Mathf.Abs(height) / 0.12f));

        var material = sky != null ? sky : RenderSettings.skybox;
        material.SetColor("_SkyColor", Blend(day.sky, dusk.sky, night.sky, dayAmount, duskAmount));
        material.SetColor("_CloudColor", Blend(day.cloud, dusk.cloud, night.cloud, dayAmount, duskAmount));
        material.SetColor("_GlowColor", Blend(day.glow, dusk.glow, night.glow, dayAmount, duskAmount));
        material.SetColor("_DiscColor", Blend(day.disc, dusk.disc, night.disc, dayAmount, duskAmount));
        material.SetFloat("_Stars", 1 - dayAmount);

        if (baseIntensities == null || baseIntensities.Length != nightLights.Length)
        {
            baseIntensities = new float[nightLights.Length];
            for (int i = 0; i < nightLights.Length; i++) baseIntensities[i] = nightLights[i].intensity;
        }
        for (int i = 0; i < nightLights.Length; i++)
            nightLights[i].intensity = baseIntensities[i] * (1 + nightLightBoost * (1 - dayAmount));
    }

    static Color Blend(Color dayColor, Color duskColor, Color nightColor, float dayAmount, float duskAmount)
    {
        return Color.Lerp(Color.Lerp(nightColor, dayColor, dayAmount), duskColor, duskAmount);
    }

    void OnDisable()
    {
        Shader.SetGlobalFloat(SunDimId, 0);
    }
}
