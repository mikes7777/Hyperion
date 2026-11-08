using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates and samples the closed orbital circuit used by Hyperion.
/// Everything is built at runtime so the project has no model or texture dependencies.
/// </summary>
public class StellarTrack : MonoBehaviour
{
    [SerializeField, Min(64)] private int sampleCount = 320;
    [SerializeField, Min(8f)] private float trackWidth = 18f;

    private Vector3[] centers;
    private Vector3[] tangents;
    private Vector3[] rights;
    private Vector3[] ups;
    private float trackLength;
    private bool built;

    public float TrackWidth { get { return trackWidth; } }
    public float TrackLength { get { return trackLength; } }
    public int SampleCount { get { return sampleCount; } }

    public void Build()
    {
        if (built) return;
        built = true;
        gameObject.name = "Stellar Circuit";
        PrecomputeFrames();
        BuildRoad();
        BuildRails();
        BuildLaneMarkers();
        BuildStartGantry();
    }

    private void Awake()
    {
        if (!built) Build();
    }

    private Vector3 CurvePoint(float normalizedProgress)
    {
        float a = Mathf.Repeat(normalizedProgress, 1f) * Mathf.PI * 2f;
        float radius = 112f + Mathf.Sin(a * 3f) * 13f + Mathf.Sin(a * 5f + 0.8f) * 6.5f;
        float x = Mathf.Cos(a) * radius;
        float z = Mathf.Sin(a) * radius;
        float y = Mathf.Sin(a * 2f) * 3.4f + Mathf.Sin(a * 5f - 0.4f) * 1.25f;
        return transform.position + new Vector3(x, y, z);
    }

    private void PrecomputeFrames()
    {
        centers = new Vector3[sampleCount];
        tangents = new Vector3[sampleCount];
        rights = new Vector3[sampleCount];
        ups = new Vector3[sampleCount];
        float step = 1f / sampleCount;
        for (int i = 0; i < sampleCount; i++)
        {
            float p = i * step;
            centers[i] = CurvePoint(p);
            Vector3 tangent = (CurvePoint(p + step * 0.45f) - CurvePoint(p - step * 0.45f)).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            Vector3 up = Vector3.Cross(tangent, right).normalized;
            float bank = Mathf.Sin(p * Mathf.PI * 6f + 0.3f) * 5.5f;
            Quaternion bankRotation = Quaternion.AngleAxis(bank, tangent);
            rights[i] = bankRotation * right;
            ups[i] = bankRotation * up;
            tangents[i] = tangent;
        }

        trackLength = 0f;
        for (int i = 0; i < sampleCount; i++)
            trackLength += Vector3.Distance(centers[i], centers[(i + 1) % sampleCount]);
    }

    private void BuildRoad()
    {
        const float thickness = 0.55f;
        var vertices = new Vector3[sampleCount * 4];
        var uv = new Vector2[vertices.Length];
        var triangles = new List<int>(sampleCount * 30);
        for (int i = 0; i < sampleCount; i++)
        {
            float distanceV = i / (float)sampleCount * 48f;
            Vector3 left = centers[i] - rights[i] * (trackWidth * 0.5f);
            Vector3 right = centers[i] + rights[i] * (trackWidth * 0.5f);
            int v = i * 4;
            vertices[v] = transform.InverseTransformPoint(left);
            vertices[v + 1] = transform.InverseTransformPoint(right);
            vertices[v + 2] = transform.InverseTransformPoint(left - ups[i] * thickness);
            vertices[v + 3] = transform.InverseTransformPoint(right - ups[i] * thickness);
            uv[v] = new Vector2(0f, distanceV);
            uv[v + 1] = new Vector2(1f, distanceV);
            uv[v + 2] = new Vector2(0f, distanceV);
            uv[v + 3] = new Vector2(1f, distanceV);
        }

        for (int i = 0; i < sampleCount; i++)
        {
            int n = (i + 1) % sampleCount;
            int a = i * 4;
            int b = n * 4;
            AddQuad(triangles, a, b, b + 1, a + 1);
            AddQuad(triangles, a + 2, a, b, b + 2);
            AddQuad(triangles, a + 1, a + 3, b + 3, b + 1);
            AddQuad(triangles, a + 3, a + 2, b + 2, b + 3);
        }

        var mesh = new Mesh { name = "Orbital Track Mesh" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        gameObject.AddComponent<MeshRenderer>().sharedMaterial = CreateRoadMaterial();
        gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private void BuildRails()
    {
        var railObject = new GameObject("Energy Rails");
        railObject.transform.SetParent(transform, false);
        var vertices = new List<Vector3>(sampleCount * 8);
        var triangles = new List<int>(sampleCount * 24);
        for (int side = -1; side <= 1; side += 2)
        {
            int sideBase = vertices.Count;
            for (int i = 0; i < sampleCount; i++)
            {
                Vector3 edge = centers[i] + rights[i] * (trackWidth * 0.5f * side);
                vertices.Add(transform.InverseTransformPoint(edge + ups[i] * 0.12f));
                vertices.Add(transform.InverseTransformPoint(edge + ups[i] * 1.35f));
            }
            for (int i = 0; i < sampleCount; i++)
            {
                int n = (i + 1) % sampleCount;
                int a = sideBase + i * 2;
                int b = sideBase + n * 2;
                AddQuad(triangles, a, b, b + 1, a + 1);
                AddQuad(triangles, a + 1, b + 1, b, a);
            }
        }

        var mesh = new Mesh { name = "Energy Rail Mesh" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        railObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        railObject.AddComponent<MeshRenderer>().sharedMaterial = CreateEmissiveMaterial("Rail Cyan", new Color(0.08f, 0.75f, 1f), 3.5f);
        railObject.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private void BuildLaneMarkers()
    {
        var markerObject = new GameObject("Velocity Lane Markers");
        markerObject.transform.SetParent(transform, false);
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int i = 0; i < sampleCount; i += 8)
        {
            int j = (i + 3) % sampleCount;
            float[] lanes = { -trackWidth / 6f, trackWidth / 6f };
            foreach (float lane in lanes)
            {
                Vector3 aCenter = centers[i] + rights[i] * lane + ups[i] * 0.035f;
                Vector3 bCenter = centers[j] + rights[j] * lane + ups[j] * 0.035f;
                const float halfWidth = 0.09f;
                int start = vertices.Count;
                vertices.Add(transform.InverseTransformPoint(aCenter - rights[i] * halfWidth));
                vertices.Add(transform.InverseTransformPoint(aCenter + rights[i] * halfWidth));
                vertices.Add(transform.InverseTransformPoint(bCenter + rights[j] * halfWidth));
                vertices.Add(transform.InverseTransformPoint(bCenter - rights[j] * halfWidth));
                AddQuad(triangles, start, start + 1, start + 2, start + 3);
            }
        }

        var mesh = new Mesh { name = "Lane Dash Mesh" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        markerObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        markerObject.AddComponent<MeshRenderer>().sharedMaterial = CreateEmissiveMaterial("Lane Violet", new Color(0.52f, 0.22f, 1f), 2.7f);
    }

    private void BuildStartGantry()
    {
        GetFrame(0f, out Vector3 center, out Vector3 tangent, out Vector3 right, out Vector3 up);
        var gate = new GameObject("Start Gate");
        gate.transform.position = center;
        gate.transform.rotation = Quaternion.LookRotation(tangent, up);
        gate.transform.SetParent(transform, true);
        Material cyan = CreateEmissiveMaterial("Gate Cyan", new Color(0.1f, 0.9f, 1f), 4f);
        Material violet = CreateEmissiveMaterial("Gate Violet", new Color(0.75f, 0.18f, 1f), 3.5f);
        CreateBeam(gate.transform, "Left Pylon", new Vector3(-trackWidth * 0.57f, 2.75f, 0f), new Vector3(0.28f, 5.5f, 0.28f), cyan);
        CreateBeam(gate.transform, "Right Pylon", new Vector3(trackWidth * 0.57f, 2.75f, 0f), new Vector3(0.28f, 5.5f, 0.28f), cyan);
        CreateBeam(gate.transform, "Header", new Vector3(0f, 5.35f, 0f), new Vector3(trackWidth * 1.16f, 0.28f, 0.28f), violet);
        var startLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        startLine.name = "Start Line";
        startLine.transform.SetParent(gate.transform, false);
        startLine.transform.localPosition = new Vector3(0f, 0.045f, 0f);
        startLine.transform.localScale = new Vector3(trackWidth * 0.9f, 0.035f, 1.4f);
        startLine.GetComponent<Renderer>().sharedMaterial = violet;
        DisablePrimitiveCollider(startLine);
    }

    private static void CreateBeam(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = name;
        beam.transform.SetParent(parent, false);
        beam.transform.localPosition = localPosition;
        beam.transform.localScale = localScale;
        beam.GetComponent<Renderer>().sharedMaterial = material;
        DisablePrimitiveCollider(beam);
    }

    private static void DisablePrimitiveCollider(GameObject primitive)
    {
        Collider primitiveCollider = primitive.GetComponent<Collider>();
        if (primitiveCollider == null) return;
        primitiveCollider.enabled = false;
        Object.Destroy(primitiveCollider);
    }

    private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
        triangles.Add(a); triangles.Add(c); triangles.Add(d);
    }

    private static Material CreateRoadMaterial()
    {
        var material = new Material(Shader.Find("Standard")) { name = "Vacuum Asphalt" };
        Color road = new Color(0.025f, 0.035f, 0.075f, 1f);
        material.color = road;
        if (material.HasProperty("_Color")) material.SetColor("_Color", road);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.78f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.48f);
        return material;
    }

    private static Material CreateEmissiveMaterial(string name, Color color, float intensity)
    {
        var material = new Material(Shader.Find("Standard")) { name = name };
        material.color = color;
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        material.EnableKeyword("_EMISSION");
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * intensity);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.35f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.82f);
        return material;
    }

    public void GetFrame(float normalizedProgress, out Vector3 center, out Vector3 tangent,
        out Vector3 right, out Vector3 up)
    {
        if (centers == null || centers.Length == 0) PrecomputeFrames();
        float scaled = Mathf.Repeat(normalizedProgress, 1f) * sampleCount;
        int a = Mathf.FloorToInt(scaled) % sampleCount;
        int b = (a + 1) % sampleCount;
        float blend = scaled - Mathf.Floor(scaled);
        center = Vector3.Lerp(centers[a], centers[b], blend);
        tangent = Vector3.Lerp(tangents[a], tangents[b], blend).normalized;
        right = Vector3.Lerp(rights[a], rights[b], blend).normalized;
        up = Vector3.Cross(tangent, right).normalized;
    }

    public float FindNearestProgress(Vector3 worldPosition, ref int hintIndex, out float lateral)
    {
        if (centers == null || centers.Length == 0) PrecomputeFrames();
        hintIndex = Mathf.Clamp(hintIndex, 0, sampleCount - 1);
        int best = hintIndex;
        float bestDistance = float.PositiveInfinity;
        const int searchRadius = 20;
        for (int offset = -searchRadius; offset <= searchRadius; offset++)
        {
            int index = WrapIndex(hintIndex + offset);
            float sqrDistance = (worldPosition - centers[index]).sqrMagnitude;
            if (sqrDistance < bestDistance) { bestDistance = sqrDistance; best = index; }
        }
        if (bestDistance > 35f * 35f)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                float sqrDistance = (worldPosition - centers[i]).sqrMagnitude;
                if (sqrDistance < bestDistance) { bestDistance = sqrDistance; best = i; }
            }
        }

        int next = (best + 1) % sampleCount;
        Vector3 segment = centers[next] - centers[best];
        float blend = segment.sqrMagnitude > 0.0001f
            ? Mathf.Clamp01(Vector3.Dot(worldPosition - centers[best], segment) / segment.sqrMagnitude) : 0f;
        Vector3 nearestCenter = Vector3.Lerp(centers[best], centers[next], blend);
        Vector3 nearestRight = Vector3.Lerp(rights[best], rights[next], blend).normalized;
        lateral = Vector3.Dot(worldPosition - nearestCenter, nearestRight);
        hintIndex = best;
        return Mathf.Repeat((best + blend) / sampleCount, 1f);
    }

    private int WrapIndex(int index)
    {
        index %= sampleCount;
        if (index < 0) index += sampleCount;
        return index;
    }
}
