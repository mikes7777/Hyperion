using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Vesper Ring: a banked, elevated circuit sampled at equal distances. All race
/// progress is distance / TrackLength, shared by player timing, rivals and HUD.
/// </summary>
public sealed class StellarTrack : MonoBehaviour
{
    [SerializeField, Min(128)] private int sampleCount = 768;
    [SerializeField, Min(12f)] private float trackWidth = 22f;
    private Vector3[] centers, tangents, rights, ups;
    private float[] curvature;
    private float trackLength;
    private bool built;
    private readonly List<Mesh> ownedMeshes = new List<Mesh>();
    private readonly List<Material> ownedMaterials = new List<Material>();
    private Texture2D asphaltTexture;
    public float TrackWidth { get { return trackWidth; } }
    public float TrackLength { get { EnsureFrames(); return trackLength; } }
    public int SampleCount { get { return sampleCount; } }

    // Broad arcs, an uphill S, and a long launch straight. Closed Catmull-Rom
    // interpolation avoids the tiny-radius ripples of the V1 radial circuit.
    private static readonly Vector3[] ControlPoints =
    {
        new Vector3(310f, 2f, -115f), new Vector3(315f, 7f, 35f),
        new Vector3(270f, 19f, 190f), new Vector3(130f, 30f, 265f),
        new Vector3(-15f, 23f, 225f), new Vector3(-130f, 39f, 275f),
        new Vector3(-280f, 30f, 205f), new Vector3(-345f, 12f, 55f),
        new Vector3(-290f, -5f, -85f), new Vector3(-180f, -12f, -130f),
        new Vector3(-115f, -2f, -245f), new Vector3(30f, 10f, -285f),
        new Vector3(190f, 6f, -260f), new Vector3(285f, 0f, -225f)
    };

    private void Awake() { Build(); }
    public void Build()
    {
        if (built) return;
        built = true;
        gameObject.name = "VESPER RING // Orbital Circuit";
        PrecomputeFrames();
        BuildRoad();
        BuildBarriers();
        BuildMarkings();
        BuildArchitecture();
    }
    private void EnsureFrames() { if (centers == null) PrecomputeFrames(); }
    private Vector3 CurvePoint(float progress)
    {
        float value = Mathf.Repeat(progress, 1f) * ControlPoints.Length;
        int i = Mathf.FloorToInt(value);
        float t = value - i;
        Vector3 a = ControlPoints[(i + ControlPoints.Length - 1) % ControlPoints.Length];
        Vector3 b = ControlPoints[i % ControlPoints.Length];
        Vector3 c = ControlPoints[(i + 1) % ControlPoints.Length];
        Vector3 d = ControlPoints[(i + 2) % ControlPoints.Length];
        return transform.TransformPoint(0.5f * ((2f * b) + (-a + c) * t +
            (2f * a - 5f * b + 4f * c - d) * t * t + (-a + 3f * b - 3f * c + d) * t * t * t));
    }
    private void PrecomputeFrames()
    {
        sampleCount = Mathf.Max(128, sampleCount);
        const int denseCount = 8192;
        var dense = new Vector3[denseCount + 1];
        var distances = new float[denseCount + 1];
        dense[0] = CurvePoint(0f);
        for (int i = 1; i <= denseCount; i++)
        {
            dense[i] = CurvePoint(i / (float)denseCount);
            distances[i] = distances[i - 1] + Vector3.Distance(dense[i - 1], dense[i]);
        }
        trackLength = distances[denseCount];
        centers = new Vector3[sampleCount]; tangents = new Vector3[sampleCount];
        rights = new Vector3[sampleCount]; ups = new Vector3[sampleCount];
        curvature = new float[sampleCount];
        int segment = 0;
        for (int i = 0; i < sampleCount; i++)
        {
            float target = trackLength * i / sampleCount;
            while (segment < denseCount - 1 && distances[segment + 1] < target) segment++;
            float blend = Mathf.InverseLerp(distances[segment], distances[segment + 1], target);
            centers[i] = Vector3.Lerp(dense[segment], dense[segment + 1], blend);
        }
        float spacing = trackLength / sampleCount;
        for (int i = 0; i < sampleCount; i++)
            tangents[i] = (centers[Wrap(i + 1)] - centers[Wrap(i - 1)]).normalized;
        for (int i = 0; i < sampleCount; i++)
        {
            curvature[i] = Vector3.SignedAngle(tangents[Wrap(i - 2)], tangents[Wrap(i + 2)], Vector3.up)
                * Mathf.Deg2Rad / (spacing * 4f);
        }
        for (int i = 0; i < sampleCount; i++)
        {
            // Ease banking over ~45m so Catmull-Rom curvature changes never
            // create a sudden twist across the width of the road.
            float bankCurvature = 0f, weightSum = 0f;
            for (int k = -8; k <= 8; k++)
            {
                float weight = 9f - Mathf.Abs(k);
                bankCurvature += curvature[Wrap(i + k)] * weight;
                weightSum += weight;
            }
            bankCurvature /= weightSum;
            Vector3 right = Vector3.Cross(Vector3.up, tangents[i]).normalized;
            Vector3 up = Vector3.Cross(tangents[i], right).normalized;
            float bank = -Mathf.Clamp(bankCurvature * 1550f, -19f, 19f);
            Quaternion roll = Quaternion.AngleAxis(bank, tangents[i]);
            rights[i] = roll * right;
            ups[i] = roll * up;
        }
    }
    public float GetProgressAtDistance(float progress, float meters)
    { EnsureFrames(); return progress + meters / Mathf.Max(1f, trackLength); }
    public float GetCurvature(float progress) { return Mathf.Abs(GetSignedCurvature(progress)); }
    public float GetSignedCurvature(float progress)
    {
        EnsureFrames();
        float s = Mathf.Repeat(progress, 1f) * sampleCount;
        int a = Mathf.FloorToInt(s) % sampleCount;
        return Mathf.Lerp(curvature[a], curvature[Wrap(a + 1)], s - Mathf.Floor(s));
    }
    public void GetFrame(float progress, out Vector3 center, out Vector3 tangent, out Vector3 right, out Vector3 up)
    {
        EnsureFrames();
        float s = Mathf.Repeat(progress, 1f) * sampleCount;
        int a = Mathf.FloorToInt(s) % sampleCount, b = Wrap(a + 1);
        float t = s - Mathf.Floor(s);
        center = Vector3.Lerp(centers[a], centers[b], t);
        tangent = Vector3.Lerp(tangents[a], tangents[b], t).normalized;
        up = Vector3.Lerp(ups[a], ups[b], t).normalized;
        right = Vector3.Cross(up, tangent).normalized;
        up = Vector3.Cross(tangent, right).normalized;
    }
    public float FindNearestProgress(Vector3 worldPosition, ref int hintIndex, out float lateral)
    {
        EnsureFrames();
        hintIndex = Wrap(hintIndex);
        int best = hintIndex;
        float bestBlend = 0f, bestDistance = float.PositiveInfinity;
        // Project onto complete segments, including both neighbours around the
        // hint. The V1 nearest-vertex method jumped at every half-segment.
        for (int delta = -22; delta <= 22; delta++)
            TestSegment(worldPosition, Wrap(hintIndex + delta), ref best, ref bestBlend, ref bestDistance);
        if (bestDistance > 45f * 45f)
            for (int i = 0; i < sampleCount; i++) TestSegment(worldPosition, i, ref best, ref bestBlend, ref bestDistance);
        float p = Mathf.Repeat((best + bestBlend) / sampleCount, 1f);
        GetFrame(p, out Vector3 center, out Vector3 tangent, out Vector3 right, out Vector3 up);
        lateral = Vector3.Dot(worldPosition - center, right);
        hintIndex = best;
        return p;
    }
    private void TestSegment(Vector3 point, int index, ref int best, ref float bestBlend, ref float bestDistance)
    {
        Vector3 direction = centers[Wrap(index + 1)] - centers[index];
        float blend = Mathf.Clamp01(Vector3.Dot(point - centers[index], direction) / Mathf.Max(0.0001f, direction.sqrMagnitude));
        float distance = (point - centers[index] - direction * blend).sqrMagnitude;
        if (distance < bestDistance) { bestDistance = distance; best = index; bestBlend = blend; }
    }
    private int Wrap(int i) { return (i % sampleCount + sampleCount) % sampleCount; }

    private void BuildRoad()
    {
        Material road = Material("Titanium asphalt", new Color(0.10f, 0.125f, 0.17f), 0.35f, 0.32f);
        asphaltTexture = new Texture2D(128, 128, TextureFormat.RGB24, true);
        asphaltTexture.name = "Original asphalt grain";
        var rng = new System.Random(72);
        var pixels = new Color[128 * 128];
        for (int i = 0; i < pixels.Length; i++)
        {
            float grain = 0.75f + (float)rng.NextDouble() * 0.25f;
            pixels[i] = new Color(grain, grain, grain);
        }
        asphaltTexture.SetPixels(pixels); asphaltTexture.Apply();
        asphaltTexture.wrapMode = TextureWrapMode.Repeat;
        road.mainTexture = asphaltTexture;
        var deck = new MeshBuilder(this);
        float half = trackWidth * 0.5f;
        for (int i = 0; i < sampleCount; i++)
        {
            int n = Wrap(i + 1);
            Vector3 l = centers[i] - rights[i] * half, r = centers[i] + rights[i] * half;
            Vector3 nl = centers[n] - rights[n] * half, nr = centers[n] + rights[n] * half;
            float v = i * trackLength / sampleCount / 7f, nv = (i + 1) * trackLength / sampleCount / 7f;
            deck.Quad(l, nl, nr, r, new Vector2(0f, v), new Vector2(0f, nv), new Vector2(3f, nv), new Vector2(3f, v));
        }
        deck.Build("Continuous road surface", road, true);
        Material hull = Material("Track structural alloy", new Color(0.045f, 0.065f, 0.11f), 0.7f, 0.45f);
        var shell = new MeshBuilder(this);
        for (int i = 0; i < sampleCount; i++)
        {
            int n = Wrap(i + 1);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 a = centers[i] + rights[i] * half * side;
                Vector3 b = centers[n] + rights[n] * half * side;
                if (side < 0) shell.Quad(a - ups[i] * 1.3f, b - ups[n] * 1.3f, b, a);
                else shell.Quad(a, b, b - ups[n] * 1.3f, a - ups[i] * 1.3f);
            }
            shell.Quad(centers[i] - rights[i] * half - ups[i] * 1.3f,
                centers[i] + rights[i] * half - ups[i] * 1.3f,
                centers[n] + rights[n] * half - ups[n] * 1.3f,
                centers[n] - rights[n] * half - ups[n] * 1.3f);
        }
        shell.Build("Armoured deck underside", hull);
    }
    private void BuildBarriers()
    {
        Material dark = Material("Ceramic safety wall", new Color(0.13f, 0.18f, 0.24f), 0.6f, 0.5f);
        Material cyan = Emissive("Cyan guide light", new Color(0.04f, 0.82f, 1f), 2.2f);
        Material amber = Emissive("Amber guide light", new Color(1f, 0.29f, 0.07f), 1.7f);
        var walls = new MeshBuilder(this);
        var lights = new[] { new MeshBuilder(this), new MeshBuilder(this) };
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                int n = Wrap(i + 1);
                Vector3 a = centers[i] + rights[i] * (trackWidth * 0.5f + 0.20f) * side;
                Vector3 b = centers[n] + rights[n] * (trackWidth * 0.5f + 0.20f) * side;
                Vector3 ao = rights[i] * 0.22f, bo = rights[n] * 0.22f;
                Vector3 ah = ups[i] * 1.35f, bh = ups[n] * 1.35f;
                walls.Quad(a - ao, b - bo, b - bo + bh, a - ao + ah);
                walls.Quad(a + ao + ah, b + bo + bh, b + bo, a + ao);
                walls.Quad(a - ao + ah, b - bo + bh, b + bo + bh, a + ao + ah);
                int color = side < 0 ? 0 : 1;
                lights[color].Quad(a - ao + ah + ups[i] * 0.025f, b - bo + bh + ups[n] * 0.025f,
                    b + bo + bh + ups[n] * 0.025f, a + ao + ah + ups[i] * 0.025f);
                // An inward-facing thin strip stays visible from the chase camera.
                Vector3 innerA = a - rights[i] * side * 0.235f;
                Vector3 innerB = b - rights[n] * side * 0.235f;
                if (side < 0) lights[color].Quad(innerA + ups[i] * 1.12f, innerB + ups[n] * 1.12f, innerB + ups[n] * 0.98f, innerA + ups[i] * 0.98f);
                else lights[color].Quad(innerA + ups[i] * 0.98f, innerB + ups[n] * 0.98f, innerB + ups[n] * 1.12f, innerA + ups[i] * 1.12f);
            }
        }
        walls.Build("Physical safety barriers", dark, true);
        lights[0].Build("Port continuous guidance", cyan);
        lights[1].Build("Starboard continuous guidance", amber);
    }
    private void BuildMarkings()
    {
        var white = new MeshBuilder(this); var red = new MeshBuilder(this); var light = new MeshBuilder(this);
        for (int i = 0; i < sampleCount; i++)
        {
            int n = Wrap(i + 1);
            for (int side = -1; side <= 1; side += 2)
            {
                float edge = side * (trackWidth * 0.5f - 0.95f);
                MeshBuilder curb = (i / 4) % 2 == 0 ? white : red;
                Ribbon(curb, i, n, edge, 0.70f, 0.032f);
                Ribbon(light, i, n, side * (trackWidth * 0.5f - 1.55f), 0.055f, 0.038f);
            }
            if (i % 10 < 4)
            {
                Ribbon(white, i, n, -trackWidth / 6f, 0.075f, 0.027f);
                Ribbon(white, i, n, trackWidth / 6f, 0.075f, 0.027f);
            }
        }
        white.Build("Porcelain kerbs and lane paint", Material("Porcelain paint", new Color(0.78f, 0.86f, 0.91f), 0.1f, 0.35f));
        red.Build("Coral kerb segments", Material("Coral paint", new Color(0.90f, 0.14f, 0.065f), 0.1f, 0.4f));
        light.Build("Continuous edge filaments", Emissive("Edge filament", new Color(0.17f, 0.65f, 0.85f), 1.1f));
    }
    private void Ribbon(MeshBuilder mesh, int a, int b, float lane, float halfWidth, float height)
    {
        mesh.Quad(centers[a] + rights[a] * (lane - halfWidth) + ups[a] * height,
            centers[b] + rights[b] * (lane - halfWidth) + ups[b] * height,
            centers[b] + rights[b] * (lane + halfWidth) + ups[b] * height,
            centers[a] + rights[a] * (lane + halfWidth) + ups[a] * height);
    }
    private void BuildArchitecture()
    {
        Material structure = Material("Orbital structure", new Color(0.075f, 0.11f, 0.17f), 0.7f, 0.55f);
        Material glow = Emissive("Portal light", new Color(0.1f, 0.84f, 1f), 2.4f);
        Material warning = Emissive("Turn marker amber", new Color(1f, 0.40f, 0.1f), 1.4f);
        var supports = new MeshBuilder(this);
        var portals = new MeshBuilder(this);
        var portalGlow = new MeshBuilder(this);
        for (int i = 0; i < sampleCount; i += 24)
        {
            Quaternion rotation = Quaternion.LookRotation(tangents[i], ups[i]);
            supports.Box(centers[i] - ups[i] * 2.4f, rotation, new Vector3(trackWidth + 3f, 1.1f, 1.4f));
            for (int side = -1; side <= 1; side += 2)
                supports.Box(centers[i] + rights[i] * side * (trackWidth * 0.5f + 0.7f) - ups[i] * 6f,
                    rotation * Quaternion.Euler(0f, 0f, side * -12f), new Vector3(0.8f, 10f, 1.4f));
        }
        // The Helix Gallery is a rhythm of illuminated ribs visible at speed.
        for (int i = 0; i < 10; i++) AddPortal(0.41f + i * 0.0085f, portals, portalGlow, 8.2f);
        AddPortal(0f, portals, portalGlow, 8.7f);
        AddPortal(0.25f, portals, portalGlow, 7.8f);
        AddPortal(0.67f, portals, portalGlow, 7.8f);
        supports.Build("Suspended viaduct supports", structure);
        portals.Build("Architectural gateway ribs", structure);
        portalGlow.Build("Luminous gateway inlays", glow);
        AddSign(0f, "H Y P E R I O N", "VESPER RING  /  START • FINISH", glow);
        AddSign(0.25f, "VESPER VIADUCT", "SECTOR 02  /  CLIMB", glow);
        AddSign(0.67f, "SOLAR RUN", "SECTOR 03  /  FULL THROTTLE", glow);
        var start = new MeshBuilder(this);
        GetFrame(0f, out Vector3 c, out Vector3 t, out Vector3 r, out Vector3 u);
        for (int x = -10; x < 10; x++) for (int z = 0; z < 2; z++)
        {
            if ((x + z) % 2 != 0) continue;
            Vector3 p = c + r * x + t * z + u * 0.04f;
            start.Quad(p, p + t * 0.9f, p + t * 0.9f + r * 0.9f, p + r * 0.9f);
        }
        start.Build("Chequered timing line", Material("Timing line", Color.white, 0f, 0.2f));
        // Braking references and large illuminated chevrons before the most technical arcs.
        float[] approaches = { 0.17f, 0.31f, 0.51f, 0.72f };
        foreach (float p in approaches)
        {
            for (int b = 1; b <= 3; b++) AddBrakeBoard(GetProgressAtDistance(p, -b * 50f), b * 50, structure);
            var arrows = new MeshBuilder(this);
            float turn = Mathf.Sign(GetSignedCurvature(p));
            for (int k = 0; k < 5; k++)
            {
                float q = GetProgressAtDistance(p, k * 9f);
                GetFrame(q, out c, out t, out r, out u);
                c += r * -turn * (trackWidth * 0.5f - 0.27f) + u * 2.1f;
                // Both faces for visibility from either direction.
                Vector3 a = c - t * 0.8f + u * 0.6f;
                Vector3 b = c + t * 0.1f;
                Vector3 d = c - t * 0.8f - u * 0.6f;
                arrows.Quad(a, b + t * 0.35f, b, a - t * 0.35f);
                arrows.Quad(b, d, d - t * 0.35f, b + t * 0.35f);
                arrows.Quad(a - t * 0.35f, b, b + t * 0.35f, a);
                arrows.Quad(b + t * 0.35f, d - t * 0.35f, d, b);
            }
            arrows.Build("Illuminated corner chevrons", warning);
        }
    }
    private void AddPortal(float p, MeshBuilder frame, MeshBuilder light, float height)
    {
        GetFrame(p, out Vector3 c, out Vector3 t, out Vector3 r, out Vector3 u);
        Quaternion rotation = Quaternion.LookRotation(t, u);
        for (int side = -1; side <= 1; side += 2)
        {
            frame.Box(c + r * (trackWidth * 0.5f + 1.5f) * side + u * height * 0.5f, rotation, new Vector3(1f, height, 1.2f));
            light.Box(c + r * (trackWidth * 0.5f + 0.94f) * side + u * height * 0.5f, rotation, new Vector3(0.13f, height - 0.6f, 0.75f));
        }
        frame.Box(c + u * height, rotation, new Vector3(trackWidth + 4f, 1f, 1.2f));
        light.Box(c + u * (height - 0.56f), rotation, new Vector3(trackWidth + 1.8f, 0.10f, 0.75f));
    }
    private void AddSign(float p, string title, string subtitle, Material material)
    {
        GetFrame(p, out Vector3 c, out Vector3 t, out Vector3 r, out Vector3 u);
        CreateText(title, c + u * 7.9f - t * 0.72f, Quaternion.LookRotation(t, u), 0.48f, Color.white);
        CreateText(subtitle, c + u * 7.05f - t * 0.72f, Quaternion.LookRotation(t, u), 0.15f, new Color(0.3f, 0.9f, 1f));
    }
    private void AddBrakeBoard(float p, int distance, Material material)
    {
        GetFrame(p, out Vector3 c, out Vector3 t, out Vector3 r, out Vector3 u);
        Vector3 location = c - r * (trackWidth * 0.5f + 0.75f) + u * 2.8f;
        var panel = new MeshBuilder(this);
        Quaternion rotation = Quaternion.LookRotation(t, u);
        panel.Box(location, rotation, new Vector3(1.65f, 1.05f, 0.15f));
        panel.Build("Braking reference " + distance, material);
        CreateText(distance.ToString(), location - t * 0.09f, rotation, 0.28f, Color.white);
    }
    private void CreateText(string value, Vector3 position, Quaternion rotation, float size, Color color)
    {
        GameObject label = new GameObject(value);
        label.transform.SetParent(transform, false);
        label.transform.position = position; label.transform.rotation = rotation;
        TextMesh text = label.AddComponent<TextMesh>();
        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.font = font;
        if (font != null) label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        text.text = value; text.fontSize = 64; text.characterSize = size;
        text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
        text.color = color;
        label.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }
    private Material Material(string name, Color color, float metallic, float smoothness)
    {
        Shader shader = Shader.Find("Standard");

        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Diffuse");

        if (shader == null)
        {
            Debug.LogError("Hyperion: No compatible track shader found!");
            return null;
        }

        var result = new Material(shader);
        result.name = name;
        result.color = color;

        if (result.HasProperty("_Metallic"))
            result.SetFloat("_Metallic", metallic);

        if (result.HasProperty("_Glossiness"))
            result.SetFloat("_Glossiness", smoothness);

        ownedMaterials.Add(result);
        return result;
    }
    private Material Emissive(string name, Color color, float intensity)
    {
        Material result = Material(name, color, 0.3f, 0.55f);
        result.EnableKeyword("_EMISSION"); result.SetColor("_EmissionColor", color * intensity);
        return result;
    }
    private static void ReleaseGenerated(UnityEngine.Object resource)
    {
        if (Application.isPlaying) UnityEngine.Object.Destroy(resource);
        else UnityEngine.Object.DestroyImmediate(resource);
    }
    private void OnDestroy() { ReleaseGeneratedResources(); }
    public void ReleaseGeneratedResources()
    {
        foreach (Mesh mesh in ownedMeshes) if (mesh != null) ReleaseGenerated(mesh);
        foreach (Material material in ownedMaterials) if (material != null) ReleaseGenerated(material);
        if (asphaltTexture != null) ReleaseGenerated(asphaltTexture);
        ownedMeshes.Clear(); ownedMaterials.Clear(); asphaltTexture = null;
    }
    // World-space geometry is transformed once into the track's local space.
    // Separate face vertices keep safety wall edges crisp and deck normals upward.
    private sealed class MeshBuilder
    {
        private readonly StellarTrack owner;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<int> indices = new List<int>();
        public MeshBuilder(StellarTrack track) { owner = track; }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        { Quad(a, b, c, d, Vector2.zero, Vector2.up, Vector2.one, Vector2.right); }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = vertices.Count;
            vertices.Add(owner.transform.InverseTransformPoint(a)); vertices.Add(owner.transform.InverseTransformPoint(b));
            vertices.Add(owner.transform.InverseTransformPoint(c)); vertices.Add(owner.transform.InverseTransformPoint(d));
            uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            indices.Add(i); indices.Add(i + 1); indices.Add(i + 2);
            indices.Add(i); indices.Add(i + 2); indices.Add(i + 3);
        }
        public void Box(Vector3 c, Quaternion q, Vector3 size)
        {
            Vector3 r = q * Vector3.right * size.x * 0.5f;
            Vector3 u = q * Vector3.up * size.y * 0.5f;
            Vector3 f = q * Vector3.forward * size.z * 0.5f;
            Quad(c - r - u - f, c - r + u - f, c + r + u - f, c + r - u - f);
            Quad(c + r - u + f, c + r + u + f, c - r + u + f, c - r - u + f);
            Quad(c - r - u + f, c - r + u + f, c - r + u - f, c - r - u - f);
            Quad(c + r - u - f, c + r + u - f, c + r + u + f, c + r - u + f);
            Quad(c - r + u - f, c - r + u + f, c + r + u + f, c + r + u - f);
            Quad(c - r - u + f, c - r - u - f, c + r - u - f, c + r - u + f);
        }
        public void Build(string name, Material material, bool collision = false)
        {
            if (vertices.Count == 0) return;
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); owner.ownedMeshes.Add(mesh);
            var obj = new GameObject(name); obj.transform.SetParent(owner.transform, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            if (collision) obj.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
    }
}
