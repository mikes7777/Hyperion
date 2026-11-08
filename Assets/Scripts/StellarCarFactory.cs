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

    /// <summary>Builds a complete car whose root owns the only physics collider.</summary>
    public static GameObject CreateCar(StellarCarSpec spec, string objectName, bool player)
    {
        GameObject root = new GameObject(objectName);
        GameObject model = new GameObject("Visuals");
        model.transform.SetParent(root.transform, false);

        Material body = GetMaterial(spec.BodyColor, 0.78f, 0.83f);
        Material bodyDark = GetMaterial(Color.Lerp(spec.BodyColor, Color.black, 0.48f), 0.68f, 0.72f);
        Material accent = GetMaterial(spec.AccentColor, 0.52f, 0.88f);
        Material carbon = GetMaterial(new Color(0.012f, 0.016f, 0.025f), 0.22f, 0.42f);
        Material glass = GetMaterial(new Color(0.025f, 0.095f, 0.15f), 0.20f, 0.93f);
        Material tire = GetMaterial(new Color(0.008f, 0.009f, 0.012f), 0.02f, 0.18f);
        Material frontLight = GetEmissiveMaterial(new Color(0.45f, 0.94f, 1.00f), 4.5f);
        Material rearLight = GetEmissiveMaterial(new Color(1.00f, 0.025f, 0.08f), 4.2f);

        float bodyWidth = spec.BodyStyle == 2 ? 1.88f : 1.98f;
        float bodyLength = spec.BodyStyle == 3 ? 4.72f : 4.48f;
        float roofHeight = spec.BodyStyle == 1 ? 0.55f : 0.49f;

        // Low, layered silhouette. Positive local Z is forward.
        AddBox(model.transform, "Undertray", new Vector3(0f, 0.39f, 0f), new Vector3(bodyWidth * 1.02f, 0.18f, bodyLength), carbon);
        AddBox(model.transform, "LowerBody", new Vector3(0f, 0.58f, -0.02f), new Vector3(bodyWidth, 0.42f, bodyLength * 0.94f), bodyDark);
        AddBox(model.transform, "MainBody", new Vector3(0f, 0.78f, 0.02f), new Vector3(bodyWidth * 0.93f, 0.45f, bodyLength * 0.79f), body);
        AddBox(model.transform, "Hood", new Vector3(0f, 0.96f, 1.22f), new Vector3(bodyWidth * 0.79f, 0.18f, 1.48f), body, new Vector3(4f, 0f, 0f));
        AddBox(model.transform, "RearDeck", new Vector3(0f, 0.98f, -1.44f), new Vector3(bodyWidth * 0.84f, 0.20f, 0.98f), body, new Vector3(-3f, 0f, 0f));

        float cabinZ = spec.BodyStyle == 3 ? -0.20f : -0.08f;
        AddBox(model.transform, "Cabin", new Vector3(0f, 1.20f, cabinZ), new Vector3(bodyWidth * 0.63f, roofHeight, 1.72f), glass);
        AddBox(model.transform, "Roof", new Vector3(0f, 1.48f, cabinZ - 0.08f), new Vector3(bodyWidth * 0.57f, 0.10f, 1.18f), carbon);
        AddBox(model.transform, "WindshieldGlint", new Vector3(0f, 1.27f, cabinZ + 0.62f), new Vector3(bodyWidth * 0.58f, 0.045f, 0.60f), accent, new Vector3(-24f, 0f, 0f));

        AddBox(model.transform, "FrontFenderL", new Vector3(-0.88f, 0.72f, 1.35f), new Vector3(0.34f, 0.48f, 1.12f), body);
        AddBox(model.transform, "FrontFenderR", new Vector3(0.88f, 0.72f, 1.35f), new Vector3(0.34f, 0.48f, 1.12f), body);
        AddBox(model.transform, "RearFenderL", new Vector3(-0.90f, 0.73f, -1.42f), new Vector3(0.38f, 0.50f, 1.15f), body);
        AddBox(model.transform, "RearFenderR", new Vector3(0.90f, 0.73f, -1.42f), new Vector3(0.38f, 0.50f, 1.15f), body);

        AddBox(model.transform, "FrontSplitter", new Vector3(0f, 0.35f, bodyLength * 0.485f), new Vector3(bodyWidth * 1.08f, 0.08f, 0.34f), carbon);
        AddBox(model.transform, "RearDiffuser", new Vector3(0f, 0.36f, -bodyLength * 0.475f), new Vector3(bodyWidth * 1.02f, 0.12f, 0.38f), carbon);
        AddBox(model.transform, "SideSkirtL", new Vector3(-bodyWidth * 0.51f, 0.40f, 0f), new Vector3(0.10f, 0.16f, bodyLength * 0.72f), carbon);
        AddBox(model.transform, "SideSkirtR", new Vector3(bodyWidth * 0.51f, 0.40f, 0f), new Vector3(0.10f, 0.16f, bodyLength * 0.72f), carbon);

        AddBox(model.transform, "HeadlightL", new Vector3(-0.58f, 0.91f, bodyLength * 0.44f), new Vector3(0.42f, 0.09f, 0.10f), frontLight, new Vector3(0f, -8f, 0f));
        AddBox(model.transform, "HeadlightR", new Vector3(0.58f, 0.91f, bodyLength * 0.44f), new Vector3(0.42f, 0.09f, 0.10f), frontLight, new Vector3(0f, 8f, 0f));
        AddBox(model.transform, "TailLightL", new Vector3(-0.58f, 0.86f, -bodyLength * 0.445f), new Vector3(0.48f, 0.08f, 0.08f), rearLight);
        AddBox(model.transform, "TailLightR", new Vector3(0.58f, 0.86f, -bodyLength * 0.445f), new Vector3(0.48f, 0.08f, 0.08f), rearLight);

        AddWheels(model.transform, tire, accent);

        if (spec.BodyStyle == 0 || spec.BodyStyle == 3)
        {
            AddBox(model.transform, "WingPillarL", new Vector3(-0.64f, 1.10f, -1.86f), new Vector3(0.09f, 0.48f, 0.10f), carbon);
            AddBox(model.transform, "WingPillarR", new Vector3(0.64f, 1.10f, -1.86f), new Vector3(0.09f, 0.48f, 0.10f), carbon);
            AddBox(model.transform, "RearWing", new Vector3(0f, 1.34f, -1.86f), new Vector3(bodyWidth * 1.08f, 0.10f, 0.47f), carbon, new Vector3(-5f, 0f, 0f));
            AddBox(model.transform, "WingEdge", new Vector3(0f, 1.38f, -2.06f), new Vector3(bodyWidth * 1.04f, 0.13f, 0.06f), accent);
        }
        else if (spec.BodyStyle == 2)
        {
            AddBox(model.transform, "Spine", new Vector3(0f, 1.31f, -0.45f), new Vector3(0.12f, 0.22f, 2.55f), accent);
            AddBox(model.transform, "RearFinL", new Vector3(-0.69f, 1.01f, -1.70f), new Vector3(0.09f, 0.44f, 0.82f), carbon, new Vector3(0f, 0f, 12f));
            AddBox(model.transform, "RearFinR", new Vector3(0.69f, 1.01f, -1.70f), new Vector3(0.09f, 0.44f, 0.82f), carbon, new Vector3(0f, 0f, -12f));
        }

        BoxCollider rootCollider = root.AddComponent<BoxCollider>();
        rootCollider.center = new Vector3(0f, 0.78f, 0f);
        rootCollider.size = new Vector3(bodyWidth * 0.94f, 1.14f, bodyLength * 0.91f);

        Rigidbody bodyPhysics = root.AddComponent<Rigidbody>();
        bodyPhysics.mass = 1225f;
        bodyPhysics.drag = 0.10f;
        bodyPhysics.angularDrag = 3.5f;
        bodyPhysics.useGravity = false;
        bodyPhysics.interpolation = RigidbodyInterpolation.Interpolate;
        bodyPhysics.collisionDetectionMode = player ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.ContinuousSpeculative;
        bodyPhysics.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        bodyPhysics.isKinematic = !player;
        return root;
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

    private static void AddWheels(Transform parent, Material tire, Material hub)
    {
        Vector3[] positions =
        {
            new Vector3(-1.02f, 0.45f, 1.37f), new Vector3(1.02f, 0.45f, 1.37f),
            new Vector3(-1.04f, 0.46f, -1.43f), new Vector3(1.04f, 0.46f, -1.43f)
        };
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject wheel = AddPrimitive(parent, "Wheel " + (i + 1), PrimitiveType.Cylinder, positions[i], new Vector3(0.43f, 0.22f, 0.43f), new Vector3(0f, 0f, 90f), tire);
            AddPrimitive(wheel.transform, "Hub", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.64f, 1.035f, 0.64f), Vector3.zero, hub);
        }
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
            UnityEngine.Object.Destroy(primitiveCollider);
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
