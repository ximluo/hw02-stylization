using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Builds the cherry tree. Bark, moss and rope are tubes swept along curves; blossoms are squashed spheres.
public static class CherryTreeBuilder
{
    const string ModelFolder = "Assets/Models/";
    const string MaterialFolder = "Assets/Materials/";
    static readonly Vector3 SpiralCenter = new Vector3(-0.15f, 2.55f, 0);

    [MenuItem("Tools/Build Cherry Tree")]
    public static void Build()
    {
        var old = GameObject.Find("Cherry Tree");
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("Cherry Tree");
        Random.InitState(11);

        var bark = new TubeMesh();
        var anchors = new List<Vector3>();

        var trunk = TrunkPath();
        bark.Add(trunk, t => Mathf.Lerp(0.40f, 0.06f, Mathf.Pow(t, 0.7f)));

        float[] angles = { 172, 196, 222, 140, 8, -16, -42, 40, 105, 75, 250, -70 };
        for (int i = 0; i < angles.Length; i++)
        {
            float a = angles[i] * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(a), 0.55f * Mathf.Sin(a) + 0.1f, Random.Range(-0.55f, 0.55f)).normalized;
            float length = Random.Range(1.7f, 2.5f) * (0.6f + 0.4f * Mathf.Abs(Mathf.Cos(a)));
            var branch = BranchPath(Nearest(trunk, SpiralCenter + direction * 0.6f), direction, length);
            bark.Add(branch, t => Mathf.Lerp(0.11f, 0.012f, t));
            for (int k = 5; k < branch.Count; k += 2) anchors.Add(branch[k]);

            var twigStart = branch[branch.Count / 2];
            var twigDirection = (direction + Random.onUnitSphere * 0.7f).normalized;
            var twig = BranchPath(twigStart, twigDirection, length * 0.45f);
            bark.Add(twig, t => Mathf.Lerp(0.05f, 0.01f, t));
            anchors.Add(twig[twig.Count - 1]);
            anchors.Add(twig[twig.Count / 2]);
        }

        for (int i = 0; i < 7; i++)
            bark.Add(RootPath(i * 360f / 7 + Random.Range(-14f, 14f)), t => Mathf.Lerp(0.2f, 0.015f, Mathf.Pow(t, 0.8f)));

        AddPart(root, "Bark", bark.ToMesh("Cherry Bark"), "Bark");

        var moss = new TubeMesh();
        var mossPath = new List<Vector3>();
        for (int i = 0; i <= 8; i++) mossPath.Add(new Vector3(0.12f * i / 8f, -0.1f + 1.15f * i / 8f, 0));
        moss.Add(mossPath, t => Mathf.Lerp(0.95f, 0.42f, Mathf.Pow(t, 0.6f)));
        AddPart(root, "Moss", moss.ToMesh("Cherry Moss"), "Moss");

        var ropeCenter = new Vector3(0.14f, 1.02f, 0);
        var rope = new TubeMesh();
        var ropePath = new List<Vector3>();
        for (int i = 0; i <= 24; i++)
        {
            float a = i * Mathf.PI * 2 / 24;
            ropePath.Add(ropeCenter + new Vector3(Mathf.Cos(a), 0.03f * Mathf.Sin(a * 2), Mathf.Sin(a)) * 0.47f);
        }
        rope.Add(ropePath, t => 0.07f);
        AddPart(root, "Rope", rope.ToMesh("Cherry Rope"), "Rope");

        var paper = LoadMaterial("Paper");
        var streamers = new GameObject("Paper Streamers");
        streamers.transform.SetParent(root.transform, false);
        for (int i = 0; i < 6; i++)
        {
            float a = (i * 60 + 20) * Mathf.Deg2Rad;
            var outward = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            for (int k = 0; k < 3; k++)
            {
                var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(strip.GetComponent<Collider>());
                strip.name = "Streamer";
                strip.transform.SetParent(streamers.transform, false);
                strip.transform.position = ropeCenter + outward * (0.56f + 0.03f * k) + Vector3.down * (0.12f + 0.15f * k)
                    + Vector3.Cross(Vector3.up, outward) * (k % 2 == 0 ? -0.035f : 0.035f);
                strip.transform.rotation = Quaternion.LookRotation(outward) * Quaternion.Euler(0, 0, k % 2 == 0 ? 28 : -28);
                strip.transform.localScale = new Vector3(0.13f, 0.19f, 0.02f);
                strip.GetComponent<MeshRenderer>().sharedMaterial = paper;
            }
        }

        var blossomMaterial = LoadMaterial("Blossom");
        var blossoms = new GameObject("Blossoms");
        blossoms.transform.SetParent(root.transform, false);
        foreach (var anchor in anchors)
        {
            int count = Random.Range(2, 5);
            for (int k = 0; k < count; k++)
            {
                var position = anchor + Vector3.Scale(Random.insideUnitSphere, new Vector3(0.45f, 0.32f, 0.45f));
                var flat = position - SpiralCenter;
                flat.z = 0;
                if (flat.magnitude < 0.95f) continue;

                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(puff.GetComponent<Collider>());
                puff.name = "Blossom";
                puff.transform.SetParent(blossoms.transform, false);
                puff.transform.position = position;
                puff.transform.rotation = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-25f, 25f));
                float size = Random.Range(0.2f, 0.36f);
                puff.transform.localScale = new Vector3(size, size * Random.Range(0.6f, 0.8f), size);
                puff.GetComponent<MeshRenderer>().sharedMaterial = blossomMaterial;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Cherry tree built: " + blossoms.transform.childCount + " blossoms");
    }

    // Leans right out of the ground, then curls counterclockwise into a spiral.
    static List<Vector3> TrunkPath()
    {
        var path = new List<Vector3>();
        for (int i = 0; i < 14; i++)
        {
            float u = i / 14f;
            path.Add(new Vector3(0.555f * Mathf.Pow(u, 1.6f), 2.6f * u - 0.3f, 0.1f * Mathf.Sin(u * Mathf.PI)));
        }
        for (int i = 0; i <= 44; i++)
        {
            float v = i / 44f;
            float angle = -20 * Mathf.Deg2Rad + v * Mathf.PI * 2 * 1.45f;
            float radius = Mathf.Lerp(0.75f, 0.17f, Mathf.Pow(v, 0.9f));
            path.Add(SpiralCenter + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -0.3f * v));
        }
        return path;
    }

    // Rises a little, then droops toward the tip, with a slight wobble.
    static List<Vector3> BranchPath(Vector3 start, Vector3 direction, float length)
    {
        var path = new List<Vector3>();
        var wobble = Vector3.Cross(direction, Random.onUnitSphere).normalized;
        float phase = Random.Range(0f, 6f);
        for (int i = 0; i <= 16; i++)
        {
            float s = i / 16f;
            var p = start + direction * (length * s);
            p += Vector3.up * (0.35f * Mathf.Sin(s * Mathf.PI) - 0.45f * s * s) * length * 0.35f;
            p += wobble * (0.07f * Mathf.Sin(s * 9 + phase) * length * s);
            path.Add(p);
        }
        return path;
    }

    static List<Vector3> RootPath(float degrees)
    {
        var path = new List<Vector3>();
        float a = degrees * Mathf.Deg2Rad;
        var outward = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
        float length = Random.Range(1.5f, 2.0f);
        for (int i = 0; i <= 12; i++)
        {
            float s = i / 12f;
            float height = 0.55f * (1 - s) * (1 - s) + 0.02f + 0.16f * Mathf.Pow(s, 4);
            path.Add(new Vector3(0.1f, 0, 0) + outward * (0.2f + length * s) + Vector3.up * height);
        }
        return path;
    }

    static Vector3 Nearest(List<Vector3> path, Vector3 target)
    {
        var best = path[0];
        foreach (var p in path)
            if ((p - target).sqrMagnitude < (best - target).sqrMagnitude) best = p;
        return best;
    }

    static Material LoadMaterial(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + name + ".mat");
    }

    static void AddPart(GameObject root, string name, Mesh mesh, string material)
    {
        string path = ModelFolder + mesh.name + ".asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        var part = new GameObject(name);
        part.transform.SetParent(root.transform, false);
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = LoadMaterial(material);
    }

    class TubeMesh
    {
        const int Sides = 10;
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> triangles = new List<int>();

        // Sweeps a ring along the path. radius(t) is the thickness at t in [0, 1]; V follows the path length
        // so a texture keeps the same scale on every tube.
        public void Add(List<Vector3> path, System.Func<float, float> radius)
        {
            int start = vertices.Count;
            int ring = Sides + 1;
            var side = Vector3.Cross(path[1] - path[0], Vector3.forward);
            if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
            float distance = 0;

            for (int i = 0; i < path.Count; i++)
            {
                var tangent = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                side = Vector3.ProjectOnPlane(side, tangent).normalized;
                var up = Vector3.Cross(tangent, side);
                if (i > 0) distance += Vector3.Distance(path[i], path[i - 1]);
                float r = radius(i / (path.Count - 1f));
                for (int j = 0; j < ring; j++)
                {
                    float a = j * Mathf.PI * 2 / Sides;
                    var normal = Mathf.Cos(a) * side + Mathf.Sin(a) * up;
                    vertices.Add(path[i] + normal * r);
                    normals.Add(normal);
                    uvs.Add(new Vector2((float)j / Sides, distance));
                }
            }

            for (int i = 0; i < path.Count - 1; i++)
                for (int j = 0; j < Sides; j++)
                {
                    int a = start + i * ring + j;
                    int b = a + ring;
                    AddTriangle(a, b, a + 1);
                    AddTriangle(a + 1, b, b + 1);
                }
        }

        void AddTriangle(int a, int b, int c)
        {
            var face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(face, normals[a] + normals[b] + normals[c]) < 0) (b, c) = (c, b);
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
