using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class StellarCarSpec
{
    public string Id;
    public string DisplayName;
    public string ClassName;
    public string Description;
    public Color BodyColor;
    public Color AccentColor;
    public float TopSpeedKph;
    [Range(1f, 10f)] public float Acceleration;
    [Range(1f, 10f)] public float Handling;
    [Range(1f, 10f)] public float Nitro;
    [Range(0, 3)] public int BodyStyle;

    public StellarCarSpec(string id, string displayName, string className, string description,
        Color bodyColor, Color accentColor, float topSpeedKph, float acceleration,
        float handling, float nitro, int bodyStyle)
    {
        Id = id;
        DisplayName = displayName;
        ClassName = className;
        Description = description;
        BodyColor = bodyColor;
        AccentColor = accentColor;
        TopSpeedKph = topSpeedKph;
        Acceleration = acceleration;
        Handling = handling;
        Nitro = nitro;
        BodyStyle = bodyStyle;
    }
}

public static class StellarCarCatalog
{
    // The requested GT3 RS is first so it is always selected by default.
    public static readonly StellarCarSpec[] Cars =
    {
        new StellarCarSpec(
            "gt3rs", "GT3 RS", "TRACK LEGEND",
            "Razor-sharp turn-in, huge grip, and the confidence to attack every apex.",
            new Color(1.00f, 0.24f, 0.035f), new Color(1.00f, 0.82f, 0.57f),
            318f, 8.7f, 9.5f, 7.8f, 0),
        new StellarCarSpec(
            "nebula-r", "NEBULA R", "ORBITAL GT",
            "Balanced and planted, designed to carry impossible speed through long arcs.",
            new Color(0.025f, 0.73f, 1.00f), new Color(0.68f, 0.96f, 1.00f),
            330f, 8.4f, 8.6f, 8.7f, 1),
        new StellarCarSpec(
            "ion-tempest", "ION TEMPEST", "ELECTRIC HYPER",
            "Instant acceleration and a violent nitro surge reward brave corner exits.",
            new Color(0.47f, 0.16f, 1.00f), new Color(0.91f, 0.76f, 1.00f),
            324f, 9.7f, 8.1f, 9.6f, 2),
        new StellarCarSpec(
            "solaris-v12", "SOLARIS V12", "VELOCITY PROTOTYPE",
            "Huge terminal speed, calm steering, and an engine built for the void.",
            new Color(1.00f, 0.64f, 0.025f), new Color(1.00f, 0.94f, 0.54f),
            347f, 8.9f, 7.6f, 8.3f, 3)
    };
}

public static class StellarCarFactory
{
    private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

    /// <summary>Original sculpted models; positive local Z is forward. Root owns collision.</summary>
    public static GameObject CreateCar(StellarCarSpec spec, string objectName, bool player)
    {

        GameObject root = new GameObject(objectName);
        Transform model = new GameObject("Visuals").transform;
        model.SetParent(root.transform, false);

        // Load the real Porsche GT3 RS model
        if (spec.Id == "gt3rs")
        {
            GameObject porschePrefab = Resources.Load<GameObject>("PorscheGT3RS");

            if (porschePrefab != null)
            {
                GameObject porsche = UnityEngine.Object.Instantiate(porschePrefab, model);
                porsche.name = "Porsche GT3 RS Model";
                porsche.transform.localPosition = new Vector3(0.020f, -0.061f, -0.016f);
                porsche.transform.localRotation = Quaternion.Euler(-89.311f, 180f, 0f);
                porsche.transform.localScale = new Vector3(2.261706f, 2.261706f, 2.261706f);
            }
            else
            {
                Debug.LogWarning("PorscheGT3RS prefab not found!");
            }
        }

        // Race controllers place their origin at the magnetic suspension's ride height.
        model.localPosition = new Vector3(0f, -0.68f, 0f);
        float length = spec.BodyStyle == 3 ? 5.08f : spec.BodyStyle == 2 ? 4.72f : 4.60f;
        float width = spec.BodyStyle == 3 ? 2.20f : 2.10f;
        Material paint = GetMaterial(spec.BodyColor, 0.62f, 0.86f);
        Material carbon = GetMaterial(new Color(0.018f, 0.024f, 0.036f), 0.32f, 0.50f);
        Material glass = GetMaterial(new Color(0.028f, 0.065f, 0.095f), 0.68f, 0.96f);
        Material metal = GetMaterial(new Color(0.51f, 0.57f, 0.64f), 0.90f, 0.73f);
        Material tire = GetMaterial(new Color(0.012f, 0.014f, 0.018f), 0.02f, 0.24f);
        Material accent = GetMaterial(spec.AccentColor, 0.55f, 0.75f);
        Material headlight = GetEmissiveMaterial(new Color(0.68f, 0.88f, 1f), 3.2f);
        // Per-car brake material: animating one car must not light every opponent.
        Material taillight = new Material(GetEmissiveMaterial(new Color(1f, 0.025f, 0.055f), 1.5f));
        Material boost = GetEmissiveMaterial(new Color(0.16f, 0.65f, 1f), 4f);
        float frontAxle = length * 0.285f;
        float rearAxle = -length * 0.293f;
        if (spec.Id != "gt3rs")
        {
            BuildBody(model, spec, length, width, frontAxle, rearAxle, paint, carbon);
            BuildCabin(model, spec.BodyStyle, length, glass, carbon, paint);
        }

        for (int side = -1; side <= 1; side += 2)
        {
            float sign = side;
            AddBox(model, "Sill blade", new Vector3(sign * width * 0.488f, 0.21f, -0.05f),
                new Vector3(0.065f, 0.10f, length * 0.49f), carbon);
            AddBox(model, "Mirror stalk", new Vector3(sign * 0.91f, 1.01f, 0.58f),
                new Vector3(0.22f, 0.035f, 0.04f), carbon, new Vector3(0f, sign * -18f, 0f));
            AddPrimitive(model, "Aero mirror", PrimitiveType.Sphere, new Vector3(sign * 1.055f, 1.04f, 0.59f),
                new Vector3(0.22f, 0.10f, 0.29f), Vector3.zero, paint);
            // Swept light ribbons and inset intakes give the nose a recognizable face.
            AddBox(model, "Headlight housing", new Vector3(sign * 0.72f, 0.675f, length * 0.444f),
                new Vector3(0.44f, 0.042f, 0.20f), carbon, new Vector3(0f, sign * -12f, 0f));
            AddBox(model, "LED signature", new Vector3(sign * 0.72f, 0.699f, length * 0.453f),
                new Vector3(0.37f, 0.016f, 0.08f), headlight, new Vector3(0f, sign * -12f, 0f));
            AddBox(model, "Lower intake", new Vector3(sign * 0.65f, 0.44f, length * 0.508f),
                new Vector3(0.43f, 0.14f, 0.07f), carbon, new Vector3(0f, sign * -7f, 0f));
            AddBox(model, "Rear lamp", new Vector3(sign * 0.57f, 0.72f, -length * 0.507f),
                new Vector3(0.78f, 0.04f, 0.075f), taillight);
            AddBox(model, "Side air intake", new Vector3(sign * width * 0.505f, 0.59f, -0.57f),
                new Vector3(0.025f, 0.21f, 0.48f), carbon, new Vector3(0f, 0f, sign * -12f));
            AddBox(model, "Door handle", new Vector3(sign * width * 0.486f, 0.88f, -0.06f),
                new Vector3(0.026f, 0.024f, 0.20f), carbon);
            for (int slat = 0; slat < 5; slat++)
                AddBox(model, "Fender cooling louvre", new Vector3(sign * 0.77f, 0.99f, rearAxle + (slat - 2) * 0.085f),
                    new Vector3(0.22f, 0.016f, 0.035f), carbon, new Vector3(0f, sign * -10f, 0f));
        }
        AddBox(model, "Nose intake", new Vector3(0f, 0.42f, length * 0.508f), new Vector3(0.92f, 0.16f, 0.06f), carbon);
        AddBox(model, "Front splitter", new Vector3(0f, 0.22f, length * 0.480f), new Vector3(width * 0.91f, 0.055f, 0.28f), carbon);
        AddBox(model, "Rear diffuser", new Vector3(0f, 0.25f, -length * 0.484f), new Vector3(width * 0.88f, 0.08f, 0.32f), carbon);
        for (int fin = -3; fin <= 3; fin++)
            AddBox(model, "Diffuser strake", new Vector3(fin * 0.20f, 0.30f, -length * 0.484f), new Vector3(0.025f, 0.16f, 0.32f), carbon);
        // Rear deck heat-exchange slats.
        for (int i = 0; i < 7; i++)
            AddBox(model, "Engine vent", new Vector3(0f, 0.88f, -0.95f - i * 0.085f), new Vector3(0.79f, 0.018f, 0.034f), carbon);
        if (spec.BodyStyle == 0 || spec.BodyStyle == 3)
        {
            float wingZ = -length * 0.405f;
            for (int side = -1; side <= 1; side += 2)
            {
                AddBox(model, "Swan neck wing mount", new Vector3(side * 0.61f, 1.03f, wingZ + 0.08f), new Vector3(0.045f, 0.38f, 0.09f), carbon, new Vector3(-16f, 0f, 0f));
                AddBox(model, "Wing end plate", new Vector3(side * width * 0.50f, 1.20f, wingZ), new Vector3(0.035f, 0.19f, 0.38f), paint);
            }
            AddBox(model, "Aerofoil", new Vector3(0f, 1.21f, wingZ), new Vector3(width, 0.055f, 0.36f), carbon, new Vector3(-6f, 0f, 0f));
        }
        else
            AddBox(model, "Active rear lip", new Vector3(0f, 0.91f, -length * 0.432f), new Vector3(width * 0.82f, 0.065f, 0.20f), carbon, new Vector3(-13f, 0f, 0f));

        StellarVehicleVisuals visuals = root.AddComponent<StellarVehicleVisuals>();
        var steering = new Transform[4];
        var wheels = new Transform[4];
        var skidTrails = new TrailRenderer[2];
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float side = i % 2 == 0 ? -1f : 1f;
            Transform pivot = new GameObject(front ? "Front steering" : "Rear axle").transform;
            pivot.SetParent(model, false);
            pivot.localPosition = new Vector3(side * (width * 0.49f), 0.43f, front ? frontAxle : rearAxle);
            steering[i] = pivot;
            Transform spin = new GameObject("Rotating wheel").transform;
            spin.SetParent(pivot, false);
            wheels[i] = spin;
            if (spec.Id != "gt3rs")
            {
                BuildWheel(spin, side, tire, metal, carbon, accent);
            }
            if (spec.Id != "gt3rs")
{
    AddBox(pivot, "Brake caliper", new Vector3(side * 0.13f, 0.07f, -0.23f), new Vector3(0.055f, 0.21f, 0.09f), GetMaterial(new Color(0.94f, 0.12f, 0.04f), 0.25f, 0.6f));
}
            if (!front)
            {
                GameObject skid = new GameObject("Drift ion trail");
                skid.transform.SetParent(model, false);
                skid.transform.localPosition = new Vector3(pivot.localPosition.x, 0.018f, rearAxle);
                TrailRenderer trail = skid.AddComponent<TrailRenderer>();
                trail.sharedMaterial = GetEmissiveMaterial(new Color(0.25f, 0.65f, 0.90f), 1.2f);
                trail.time = 0.65f; trail.minVertexDistance = 0.25f;
                trail.startWidth = 0.13f; trail.endWidth = 0.015f;
                trail.startColor = new Color(0.4f, 0.7f, 1f, 0.5f); trail.endColor = new Color(0.1f, 0.25f, 0.5f, 0f);
                trail.emitting = false;
                skidTrails[i - 2] = trail;
            }
        }
        Transform[] flames = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;

            if (spec.Id != "gt3rs")
            {
                AddPrimitive(model, "Titanium exhaust",
                    PrimitiveType.Cylinder,
                    new Vector3(side * 0.39f, 0.39f, -length * 0.508f),
                    new Vector3(0.13f, 0.09f, 0.13f),
                    new Vector3(90f, 0f, 0f), metal);
            }

            Vector3 flamePosition = spec.Id == "gt3rs"
                ? new Vector3(side * 0.05f, 0.39f, -2.388f)
                : new Vector3(side * 0.39f, 0.39f, -length * 0.527f);

            flames[i] = BuildFlame(model, flamePosition, boost);
            flames[i].gameObject.SetActive(false);
        }
    
        CombineDirectMeshes(model);
        if (spec.Id == "gt3rs")
        {
            foreach (Transform child in model)
            {
                if (child.name.StartsWith("Combined surface"))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
        visuals.Configure(steering, wheels, flames, skidTrails, taillight);

        BoxCollider rootCollider = root.AddComponent<BoxCollider>();
        rootCollider.center = new Vector3(0f, 0.02f, 0f);
        rootCollider.size = new Vector3(width * 0.93f, 1.12f, length * 0.94f);
        Rigidbody physics = root.AddComponent<Rigidbody>();
        physics.mass = 1225f;
        physics.drag = 0.10f;
        physics.angularDrag = 3.5f;
        physics.useGravity = false;
        physics.interpolation = RigidbodyInterpolation.Interpolate;
        physics.collisionDetectionMode = player ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.ContinuousSpeculative;
        physics.isKinematic = !player;
        return root;
    }

    // Dense longitudinal sampling creates curved shoulders and actual wheel arch openings.
    private static void BuildBody(Transform parent, StellarCarSpec spec, float length, float width,
        float frontAxle, float rearAxle, Material paint, Material carbon)
    {
        const int rows = 72, columns = 24;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int row = 0; row <= rows; row++)
        {
            float z = Mathf.Lerp(-length * 0.5f, length * 0.5f, row / (float)rows);
            float zn = z / (length * 0.5f);
            float halfWidth = BodyWidth(zn, width);
            for (int col = 0; col <= columns; col++)
            {
                float x = Mathf.Lerp(-1f, 1f, col / (float)columns);
                vertices.Add(new Vector3(x * halfWidth, DeckHeight(zn, x, spec.BodyStyle), EndContour(z, zn, x)));
                if (row < rows && col < columns)
                {
                    int a = row * (columns + 1) + col;
                    Quad(triangles, a, a + columns + 1, a + columns + 2, a + 1);
                }
            }
        }
        MeshObject(parent, "Sculpted body shell", vertices, triangles, paint);
        for (int side = -1; side <= 1; side += 2)
        {
            vertices = new List<Vector3>(); triangles = new List<int>();
            for (int row = 0; row <= rows; row++)
            {
                float z = Mathf.Lerp(-length * 0.5f, length * 0.5f, row / (float)rows);
                float zn = z / (length * 0.5f);
                float x = BodyWidth(zn, width);
                float y = DeckHeight(zn, 1f, spec.BodyStyle);
                float lower = 0.25f;
                float axleDistance = Mathf.Min(Mathf.Abs(z - frontAxle), Mathf.Abs(z - rearAxle));
                if (axleDistance < 0.475f) lower = Mathf.Max(lower, 0.43f + Mathf.Sqrt(0.475f * 0.475f - axleDistance * axleDistance));
                vertices.Add(new Vector3(x * side, y, EndContour(z, zn, 1f)));
                vertices.Add(new Vector3((x - 0.045f) * side, Mathf.Min(lower, y - 0.025f), EndContour(z, zn, 1f)));
                if (row < rows)
                {
                    int a = row * 2;
                    if (side > 0) Quad(triangles, a, a + 2, a + 3, a + 1);
                    else Quad(triangles, a + 1, a + 3, a + 2, a);
                }
            }
            MeshObject(parent, "Contoured side with wheel arches", vertices, triangles, paint);
        }
        for (int end = -1; end <= 1; end += 2)
        {
            vertices = new List<Vector3>(); triangles = new List<int>();
            float zn = end;
            for (int col = 0; col <= columns; col++)
            {
                float x = Mathf.Lerp(-1f, 1f, col / (float)columns);
                float xx = x * BodyWidth(zn, width);
                vertices.Add(new Vector3(xx, DeckHeight(zn, x, spec.BodyStyle), EndContour(end * length * 0.5f, zn, x)));
                vertices.Add(new Vector3(xx * 0.96f, 0.28f, EndContour(end * length * 0.5f, zn, x)));
                if (col < columns)
                {
                    int a = col * 2;
                    if (end > 0) Quad(triangles, a, a + 1, a + 3, a + 2);
                    else Quad(triangles, a + 2, a + 3, a + 1, a);
                }
            }
            MeshObject(parent, end > 0 ? "Nose fascia" : "Rear fascia", vertices, triangles, paint);
        }
    }

    private static float EndContour(float z, float zn, float x)
    {
        return z - Mathf.Pow(Mathf.Max(0f, zn), 8f) * Mathf.Pow(Mathf.Abs(x), 3f) * 0.18f
            + Mathf.Pow(Mathf.Max(0f, -zn), 8f) * Mathf.Pow(Mathf.Abs(x), 3f) * 0.12f;
    }

    private static float BodyWidth(float z, float width)
    {
        float taper = Mathf.Pow(Mathf.Abs(z), 5f) * (z > 0f ? 0.20f : 0.14f);
        return width * 0.5f * (1f - taper + 0.025f * Mathf.Cos(z * Mathf.PI * 3f));
    }

    private static float DeckHeight(float z, float x, int style)
    {
        float edge = Mathf.Abs(x);
        float deck = 0.82f - Mathf.Max(0f, z) * 0.19f + Mathf.Max(0f, -z) * 0.015f;
        float shoulders = Mathf.Exp(-Mathf.Pow((Mathf.Abs(z) - 0.57f) * 5f, 2f)) * Mathf.Pow(edge, 2f) * 0.27f;
        float shoulderRound = -Mathf.Pow(edge, 8f) * 0.045f;
        if (style == 0) deck += 0.025f * Mathf.Cos(z * 3f);
        if (style == 2) deck -= Mathf.Max(0f, z) * 0.045f;
        if (style == 3) deck -= 0.045f;
        return deck + shoulders + shoulderRound;
    }

    private static void BuildCabin(Transform parent, int style, float length, Material glass, Material carbon, Material paint)
    {
        const int rows = 24, columns = 24;
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        float start = style == 0 ? -1.35f : style == 3 ? -0.92f : -1.12f;
        float end = style == 3 ? 1.05f : 0.86f;
        float roofHeight = style == 0 ? 0.56f : style == 3 ? 0.43f : 0.49f;
        for (int row = 0; row <= rows; row++)
        {
            float t = row / (float)rows;
            float bulge = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.50f);
            float z = Mathf.Lerp(start, end, t);
            float cabinWidth = Mathf.Lerp(0.60f, 0.64f, t) * (0.55f + bulge * 0.45f);
            float roof = roofHeight * bulge;
            for (int col = 0; col <= columns; col++)
            {
                float angle = col / (float)columns * Mathf.PI;
                vertices.Add(new Vector3(-Mathf.Cos(angle) * cabinWidth, DeckHeight(z / (length * 0.5f), 0f, style) + 0.012f + Mathf.Pow(Mathf.Max(0f, Mathf.Sin(angle)), 0.58f) * roof, z));
                if (row < rows && col < columns)
                {
                    int a = row * (columns + 1) + col;
                    Quad(triangles, a, a + columns + 1, a + columns + 2, a + 1);
                }
            }
        }
        MeshObject(parent, "Panoramic glass canopy", vertices, triangles, glass);
        // Carbon roof inset follows the exact canopy curvature rather than a box roof.
        vertices = new List<Vector3>(); triangles = new List<int>();
        for (int row = 0; row <= 12; row++)
        {
            float t = Mathf.Lerp(0.38f, 0.68f, row / 12f);
            float b = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.50f);
            for (int col = 0; col <= 8; col++)
            {
                float angle = Mathf.Lerp(0.29f, 0.71f, col / 8f) * Mathf.PI;
                vertices.Add(new Vector3(-Mathf.Cos(angle) * Mathf.Lerp(0.60f, 0.64f, t) * (0.55f + b * 0.45f),
                    DeckHeight(Mathf.Lerp(start, end, t) / (length * 0.5f), 0f, style) + 0.019f + Mathf.Pow(Mathf.Max(0f, Mathf.Sin(angle)), 0.58f) * roofHeight * b, Mathf.Lerp(start, end, t)));
                if (row < 12 && col < 8)
                {
                    int a = row * 9 + col;
                    Quad(triangles, a, a + 9, a + 10, a + 1);
                }
            }
        }
        MeshObject(parent, "Carbon roof inset", vertices, triangles, carbon);
        // Painted windshield surrounds make a coupe canopy read as a real cabin.
        foreach (float pillar in new float[] { 0.25f, 0.73f })
        {
            vertices = new List<Vector3>(); triangles = new List<int>();
            for (int row = 0; row <= 1; row++)
            {
                float t = pillar + row * 0.018f;
                float b = Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)));
                for (int col = 0; col <= columns; col++)
                {
                    float angle = col / (float)columns * Mathf.PI;
                    vertices.Add(new Vector3(-Mathf.Cos(angle) * Mathf.Lerp(0.60f, 0.64f, t) * (0.55f + b * 0.45f),
                        DeckHeight(Mathf.Lerp(start, end, t) / (length * 0.5f), 0f, style) + 0.024f + Mathf.Pow(Mathf.Max(0f, Mathf.Sin(angle)), 0.58f) * roofHeight * b,
                        Mathf.Lerp(start, end, t)));
                    if (row == 0 && col < columns)
                        Quad(triangles, col, col + columns + 1, col + columns + 2, col + 1);
                }
            }
            MeshObject(parent, "Window surround", vertices, triangles, paint);
        }

        for (int side = -1; side <= 1; side += 2)
            AddBox(parent, "Window sill", new Vector3(side * 0.56f, 0.837f, -0.12f), new Vector3(0.035f, 0.035f, 1.35f), paint);
    }

    private static void BuildWheel(Transform parent, float side, Material tire, Material metal, Material carbon, Material accent)
    {
        const int segments = 40, rings = 12;
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int seg = 0; seg <= segments; seg++)
        {
            float a = seg / (float)segments * Mathf.PI * 2f;
            for (int ring = 0; ring <= rings; ring++)
            {
                float b = ring / (float)rings * Mathf.PI * 2f;
                float radial = 0.354f + Mathf.Cos(b) * 0.066f;
                vertices.Add(new Vector3(Mathf.Sin(b) * 0.155f, Mathf.Sin(a) * radial, Mathf.Cos(a) * radial));
                if (seg < segments && ring < rings)
                {
                    int v = seg * (rings + 1) + ring;
                    Quad(triangles, v, v + 1, v + rings + 2, v + rings + 1);
                }
            }
        }
        MeshObject(parent, "Performance tire", vertices, triangles, tire);
        AddPrimitive(parent, "Ventilated brake disc", PrimitiveType.Cylinder, new Vector3(side * 0.137f, 0f, 0f), new Vector3(0.54f, 0.006f, 0.54f), new Vector3(0f, 0f, 90f), metal);
        AddPrimitive(parent, "Rim barrel", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.58f, 0.12f, 0.58f), new Vector3(0f, 0f, 90f), carbon);
        // Thin spokes sit proud of the dark barrel, with an independent center-lock cap.
        for (int spoke = 0; spoke < 10; spoke++)
        {
            float angle = spoke / 10f * Mathf.PI * 2f;
            AddBox(parent, "Forged spoke", new Vector3(side * 0.16f, Mathf.Cos(angle) * 0.17f, Mathf.Sin(angle) * 0.17f),
                new Vector3(0.026f, 0.24f, 0.028f), metal, new Vector3(angle * Mathf.Rad2Deg, 0f, 0f));
        }
        AddPrimitive(parent, "Center lock", PrimitiveType.Cylinder, new Vector3(side * 0.177f, 0f, 0f), new Vector3(0.11f, 0.023f, 0.11f), new Vector3(0f, 0f, 90f), accent);
        CombineDirectMeshes(parent);
    }

    // Merge static surfaces by material. Wheel rigs and exhaust plumes keep their transforms.
    // A complete car uses tens of renderers rather than hundreds of tiny detail renderers.
    private static void CombineDirectMeshes(Transform parent)
    {
        var groups = new Dictionary<Material, List<CombineInstance>>();
        var originals = new List<GameObject>();
        foreach (Transform child in parent)
        {
            if (child.name == "Ion exhaust plume") continue;
            MeshFilter filter = child.GetComponent<MeshFilter>();
            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null || filter.sharedMesh == null || renderer.sharedMaterial == null) continue;
            Material material = renderer.sharedMaterial;
            if (!groups.ContainsKey(material)) groups.Add(material, new List<CombineInstance>());
            groups[material].Add(new CombineInstance
            {
                mesh = filter.sharedMesh,
                transform = parent.worldToLocalMatrix * child.localToWorldMatrix
            });
            renderer.enabled = false;
            originals.Add(child.gameObject);
        }
        foreach (var entry in groups)
        {
            Mesh mesh = new Mesh { name = "Combined " + entry.Key.name };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.CombineMeshes(entry.Value.ToArray(), true, true);
            GameObject obj = new GameObject("Combined surface");
            obj.transform.SetParent(parent, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = entry.Key;
        }
        foreach (GameObject obj in originals)
        {
            Mesh source = obj.GetComponent<MeshFilter>().sharedMesh;
            // Primitive meshes belong to Unity. Only custom meshes marked as ours are released.
            if (source != null && source.name.StartsWith("Hyperion ", StringComparison.Ordinal))
                DestroyGenerated(source);
            DestroyGenerated(obj);
        }
    }

    private static Transform BuildFlame(Transform parent, Vector3 position, Material material)
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        vertices.Add(new Vector3(0f, 0f, -0.95f));
        for (int i = 0; i < 16; i++)
        {
            float a = i / 16f * Mathf.PI * 2f;
            vertices.Add(new Vector3(Mathf.Cos(a) * 0.065f, Mathf.Sin(a) * 0.065f, 0f));
        }
        for (int i = 0; i < 16; i++) { triangles.Add(0); triangles.Add((i + 1) % 16 + 1); triangles.Add(i + 1); }
        GameObject flame = MeshObject(parent, "Ion exhaust plume", vertices, triangles, material);
        flame.transform.localPosition = position;
        return flame.transform;
    }

    private static GameObject MeshObject(Transform parent, string name, List<Vector3> vertices, List<int> triangles, Material material)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Mesh mesh = new Mesh { name = "Hyperion " + name };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        obj.AddComponent<MeshRenderer>().sharedMaterial = material;
        return obj;
    }

    public static void DestroyGenerated(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
        else UnityEngine.Object.DestroyImmediate(obj);
    }

    private static void Quad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
        triangles.Add(a); triangles.Add(c); triangles.Add(d);
    }

    public static Material GetMaterial(Color color, float metallic = 0f, float smoothness = 0.5f)
    {
        Color32 c = color;
        string key = "lit-" + c.r + "-" + c.g + "-" + c.b + "-" + c.a + "-" + Mathf.RoundToInt(metallic * 100f) + "-" + Mathf.RoundToInt(smoothness * 100f);
        Material cached;
        if (MaterialCache.TryGetValue(key, out cached) && cached != null) return cached;
        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");
        Material material = new Material(shader);
        material.name = "Stellar " + key;
        material.hideFlags = HideFlags.DontSave;
        SetColor(material, color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        MaterialCache[key] = material;
        return material;
    }

    public static Material GetEmissiveMaterial(Color color, float intensity = 2f)
    {
        Color32 c = color;
        string key = "emission-" + c.r + "-" + c.g + "-" + c.b + "-" + Mathf.RoundToInt(intensity * 100f);
        Material cached;
        if (MaterialCache.TryGetValue(key, out cached) && cached != null) return cached;
        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");
        Material material = new Material(shader);
        material.name = "Stellar " + key;
        material.hideFlags = HideFlags.DontSave;
        SetColor(material, color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.15f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.88f);
        material.EnableKeyword("_EMISSION");
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * intensity);
        MaterialCache[key] = material;
        return material;
    }

    private static void SetColor(Material material, Color color)
    {
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
    }

    private static GameObject AddBox(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        return AddPrimitive(parent, name, PrimitiveType.Cube, position, scale, Vector3.zero, material);
    }

    private static GameObject AddBox(Transform parent, string name, Vector3 position, Vector3 scale, Material material, Vector3 euler)
    {
        return AddPrimitive(parent, name, PrimitiveType.Cube, position, scale, euler, material);
    }

    private static GameObject AddPrimitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Vector3 euler, Material material)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = position;
        primitive.transform.localRotation = Quaternion.Euler(euler);
        primitive.transform.localScale = scale;
        Collider primitiveCollider = primitive.GetComponent<Collider>();
        if (primitiveCollider != null)
        {
            primitiveCollider.enabled = false;
            DestroyGenerated(primitiveCollider);
        }
        Renderer renderer = primitive.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
        return primitive;
    }
}
