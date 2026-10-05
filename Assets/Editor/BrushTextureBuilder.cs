using System.IO;
using UnityEditor;
using UnityEngine;

// Paints the seamless brush-stroke texture that breaks up the toon shadows.
public static class BrushTextureBuilder
{
    const string TexturePath = "Assets/Textures/Brush Shadow.png";
    const int Size = 512;

    [MenuItem("Tools/Build Brush Shadow Texture")]
    public static void Build()
    {
        var values = new float[Size * Size];
        for (int i = 0; i < values.Length; i++) values[i] = 0.5f;

        var random = new System.Random(3);
        for (int i = 0; i < 560; i++) Stroke(values, random);

        float mean = 0;
        foreach (float v in values) mean += v;
        mean /= values.Length;

        var pixels = new Color[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            float g = Mathf.Clamp01(0.5f + (values[i] - mean) * 1.3f);
            pixels[i] = new Color(g, g, g, 1);
        }

        var texture = new Texture2D(Size, Size, TextureFormat.RGB24, false);
        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(TexturePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.sRGBTexture = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.SaveAndReimport();
    }

    // One tapered, nearly horizontal stroke that lightens or darkens the canvas. Pixels wrap around the
    // edges so the texture tiles, and noise along the stroke leaves dry-brush gaps.
    static void Stroke(float[] values, System.Random random)
    {
        float centerX = (float)random.NextDouble() * Size;
        float centerY = (float)random.NextDouble() * Size;
        float angle = ((float)random.NextDouble() - 0.5f) * 0.5f;
        float length = 40 + (float)random.NextDouble() * 150;
        float width = 3 + (float)random.NextDouble() * 10;
        float strength = ((float)random.NextDouble() - 0.5f) * 0.9f;
        float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
        int reach = (int)(length * 0.5f + width) + 2;
        float seed = (float)random.NextDouble() * 100;

        for (int dy = -reach; dy <= reach; dy++)
            for (int dx = -reach; dx <= reach; dx++)
            {
                float along = dx * cos + dy * sin;
                float across = -dx * sin + dy * cos;
                float a = Mathf.Abs(along) / (length * 0.5f);
                float c = Mathf.Abs(across) / width;
                if (a >= 1 || c >= 1) continue;

                float taper = Mathf.Max(1 - a * a, 0.05f);
                float body = Mathf.Clamp01(1 - c / taper);
                float dry = Mathf.PerlinNoise(along * 0.05f + seed, across * 0.9f + seed);
                float weight = Mathf.SmoothStep(0, 1, body) * Mathf.SmoothStep(0.25f, 0.6f, dry + 0.3f * (1 - a));

                int x = (((int)centerX + dx) % Size + Size) % Size;
                int y = (((int)centerY + dy) % Size + Size) % Size;
                values[y * Size + x] += strength * weight;
            }
    }
}
