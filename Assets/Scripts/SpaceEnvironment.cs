using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Creates the lighting, star field, planets, rings, and distant sun for Hyperion.
/// Everything is generated at runtime so the project has no external art dependencies.
/// </summary>
public static class SpaceEnvironment
{
    private const int StarCount = 1800;
    private static Mesh smoothSphere;

    public static GameObject Build(Transform parent = null)
    {
        ConfigureAmbientLight();

        GameObject root = new GameObject("Space Environment");
        if (parent != null)
            root.transform.SetParent(parent, false);

        CreateKeyLight(root.transform);
        CreateStarField(root.transform);

        CreatePlanet(
            root.transform,
            "Vesper Prime",
            new Vector3(-1320f, 430f, 1420f),
            390f,
            new Color(0.24f, 0.34f, 0.95f),
            new Color(0.02f, 0.04f, 0.18f),
            true,
            new Color(0.34f, 0.88f, 1f),
            new Vector3(0.18f, 0.9f, 0.12f),
            0.7f);

        CreatePlanet(
            root.transform,
            "Helios Ember",
            new Vector3(1580f, -260f, -1320f),
            320f,
            new Color(0.96f, 0.20f, 0.08f),
            new Color(0.18f, 0.015f, 0.01f),
            false,
            new Color(1f, 0.19f, 0.04f),
            new Vector3(-0.1f, 1f, 0.08f),
            0.34f);

        CreatePlanet(
            root.transform,
            "Oris",
            new Vector3(270f, 650f, 2200f),
            160f,
            new Color(0.11f, 0.82f, 0.78f),
            new Color(0.01f, 0.12f, 0.18f),
            false,
            new Color(0.18f, 1f, 0.88f),
            new Vector3(0.05f, 1f, -0.22f),
            1.15f);

        CreatePlanet(
            root.transform,
            "Nyx",
            new Vector3(-2020f, -310f, -1280f),
            270f,
            new Color(0.50f, 0.16f, 0.78f),
            new Color(0.055f, 0.012f, 0.10f),
            true,
            new Color(0.78f, 0.30f, 1f),
            new Vector3(0.2f, 0.76f, 0.34f),
            -0.42f);

        CreateDistantSun(root.transform);
        return root;
    }

    private static void ConfigureAmbientLight()
    {
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.28f, 0.34f, 0.47f);
        RenderSettings.ambientEquatorColor = new Color(0.15f, 0.19f, 0.28f);
        RenderSettings.ambientGroundColor = new Color(0.045f, 0.06f, 0.1f);
        RenderSettings.ambientIntensity = 0.82f;
        RenderSettings.reflectionIntensity = 0.7f;
        Shader skyShader = Resources.Load<Shader>("HyperionNebula");
        if (skyShader != null && skyShader.isSupported) RenderSettings.skybox = new Material(skyShader) { name = "Vesper Nebula Sky" };
        else RenderSettings.skybox = null;
        DynamicGI.UpdateEnvironment();
    }

    private static void CreateKeyLight(Transform parent)
    {
        GameObject lightObject = new GameObject("Solar Key Light");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.rotation = Quaternion.Euler(28f, -38f, 0f);

        Light key = lightObject.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(0.72f, 0.84f, 1f);
        key.intensity = 1.65f;
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.72f;
        key.shadowBias = 0.025f;

        GameObject fillObject = new GameObject("Warm Orbital Fill");
        fillObject.transform.SetParent(parent, false);
        fillObject.transform.rotation = Quaternion.Euler(19f, 145f, 0f);
        Light fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(1f, 0.55f, 0.32f);
        fill.intensity = 0.55f;
        fill.shadows = LightShadows.None;
    }

    private static void CreateStarField(Transform parent)
    {
        GameObject starObject = new GameObject("Procedural Star Field");
        starObject.transform.SetParent(parent, false);

        ParticleSystem particles = starObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = StarCount;
        main.startLifetime = 100000f;
        main.startSpeed = 0f;
        main.startSize = 0.32f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false;

        ParticleSystemRenderer particleRenderer = starObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.maxParticleSize = 0.018f;
        particleRenderer.material = CreateStarMaterial();

        ParticleSystem.Particle[] stars = new ParticleSystem.Particle[StarCount];
        Random.State oldState = Random.state;
        Random.InitState(817204);

        for (int i = 0; i < stars.Length; i++)
        {
            Vector3 direction = Random.onUnitSphere;
            float radius = Random.Range(3300f, 3900f);
            float brightness = Random.Range(0.38f, 1f);
            float blueBias = Random.Range(0f, 1f);

            Color color = Color.Lerp(
                new Color(1f, 0.83f, 0.68f, brightness),
                new Color(0.60f, 0.82f, 1f, brightness),
                blueBias);

            stars[i].position = direction * radius;
            stars[i].startLifetime = 100000f;
            stars[i].remainingLifetime = 100000f;
            stars[i].startSize = Random.Range(1.0f, 2.9f) * (Random.value > 0.965f ? 2.1f : 1f);
            stars[i].startColor = color;
            stars[i].rotation = Random.Range(0f, Mathf.PI * 2f);
        }

        Random.state = oldState;
        particles.SetParticles(stars, stars.Length);
        particles.Play(false);
    }

    private static Material CreateStarMaterial()
    {
        Shader shader = Resources.Load<Shader>("HyperionStars");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");

        Material material = new Material(shader);
        material.name = "Generated Star Glow";
        material.hideFlags = HideFlags.DontSave;
        material.mainTexture = CreateGlowTexture();
        return material;
    }

    private static Texture2D CreateGlowTexture()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        texture.name = "Generated Star Texture";
        texture.hideFlags = HideFlags.DontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDistance = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.8f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static void CreatePlanet(
        Transform parent,
        string planetName,
        Vector3 position,
        float radius,
        Color lightColor,
        Color shadowColor,
        bool hasRings,
        Color emissionColor,
        Vector3 rotationAxis,
        float rotationSpeed)
    {
        GameObject planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        planet.name = planetName;
        planet.GetComponent<MeshFilter>().sharedMesh = GetSmoothSphere();
        planet.transform.SetParent(parent, false);
        planet.transform.position = position;
        planet.transform.localScale = Vector3.one * radius * 2f;
        RemovePrimitiveCollider(planet);

        MeshRenderer renderer = planet.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = CreatePlanetMaterial(planetName + " Surface", lightColor, shadowColor, emissionColor);

        StellarBodySpin spin = planet.AddComponent<StellarBodySpin>();
        spin.axis = rotationAxis.normalized;
        spin.degreesPerSecond = rotationSpeed;

        CreateAtmosphere(planet.transform, radius, emissionColor);

        GameObject moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        moon.name = planetName + " Moon";
        moon.GetComponent<MeshFilter>().sharedMesh = GetSmoothSphere();
        moon.transform.SetParent(parent, false);
        moon.transform.position = position + new Vector3(radius * 1.7f, radius * 0.42f, -radius * 1.3f);
        moon.transform.localScale = Vector3.one * radius * 0.19f;
        RemovePrimitiveCollider(moon);
        moon.GetComponent<MeshRenderer>().sharedMaterial = CreateLitMaterial(
            planetName + " Moon Material",
            Color.Lerp(shadowColor, Color.gray, 0.28f),
            0.1f,
            0.3f,
            Color.black);
    }

    private static Material CreatePlanetMaterial(string materialName, Color lightColor, Color shadowColor, Color emissionColor)
    {
        Shader shader = Resources.Load<Shader>("HyperionPlanet");
        if (shader == null || !shader.isSupported) return CreateLitMaterial(materialName, Color.Lerp(lightColor, shadowColor, 0.34f), 0.2f, 0.6f, emissionColor * 0.03f);
        Material material = new Material(shader) { name = materialName };
        material.SetColor("_Color", lightColor);
        material.SetColor("_ShadowColor", shadowColor);
        material.SetColor("_CloudColor", Color.Lerp(lightColor, Color.white, 0.65f));
        material.SetFloat("_Seed", lightColor.r * 11f + lightColor.g * 7f);
        return material;
    }

    private static void CreateAtmosphere(Transform planet, float radius, Color glowColor)
    {
        GameObject atmosphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        atmosphere.name = "Atmosphere Glow";
        atmosphere.GetComponent<MeshFilter>().sharedMesh = GetSmoothSphere();
        atmosphere.transform.SetParent(planet, false);
        atmosphere.transform.localPosition = Vector3.zero;
        atmosphere.transform.localRotation = Quaternion.identity;
        atmosphere.transform.localScale = Vector3.one * 1.045f;
        RemovePrimitiveCollider(atmosphere);

        Shader shader = Resources.Load<Shader>("HyperionAtmosphere");
        if (shader == null)
            shader = Shader.Find("Standard");
        Material material = new Material(shader);
        material.name = "Generated Atmosphere";
        material.hideFlags = HideFlags.DontSave;
        Color atmosphereColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0.6f);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", atmosphereColor);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", atmosphereColor);
        atmosphere.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void CreateSurfaceBand(Transform planet, float radius, Color color)
    {
        GameObject band = new GameObject("Equatorial Light Band");
        band.transform.SetParent(planet, false);
        band.transform.localPosition = Vector3.zero;
        band.transform.localRotation = Quaternion.Euler(8f, 0f, -12f);
        band.transform.localScale = Vector3.one / (radius * 2f);

        MeshFilter filter = band.AddComponent<MeshFilter>();
        filter.sharedMesh = CreateAnnulusMesh(radius * 1.01f, radius * 1.025f, 128);
        MeshRenderer renderer = band.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CreateEmissionMaterial("Surface Band Material", color * 0.38f);
    }

    private static void CreateRing(Transform planet, float innerRadius, float outerRadius, Color color)
    {
        GameObject ring = new GameObject("Orbital Rings");
        ring.transform.SetParent(planet, false);
        ring.transform.localPosition = Vector3.zero;
        ring.transform.localRotation = Quaternion.Euler(22f, 0f, -17f);
        ring.transform.localScale = Vector3.one / (planet.localScale.x > 0f ? planet.localScale.x : 1f);

        MeshFilter filter = ring.AddComponent<MeshFilter>();
        filter.sharedMesh = CreateStriatedRingMesh(innerRadius, outerRadius);
        MeshRenderer renderer = ring.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CreateEmissionMaterial("Generated Ring Material", Color.Lerp(color, new Color(0.65f, 0.56f, 0.44f), 0.65f) * 0.42f);
    }

    private static Mesh CreateAnnulusMesh(float innerRadius, float outerRadius, int segments)
    {
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 12];

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            int vertex = i * 2;
            vertices[vertex] = direction * innerRadius;
            vertices[vertex + 1] = direction * outerRadius;
            uvs[vertex] = new Vector2((float)i / segments, 0f);
            uvs[vertex + 1] = new Vector2((float)i / segments, 1f);
        }

        for (int i = 0; i < segments; i++)
        {
            int vertex = i * 2;
            int triangle = i * 12;
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 1;
            triangles[triangle + 2] = vertex + 2;
            triangles[triangle + 3] = vertex + 1;
            triangles[triangle + 4] = vertex + 3;
            triangles[triangle + 5] = vertex + 2;

            triangles[triangle + 6] = vertex + 2;
            triangles[triangle + 7] = vertex + 1;
            triangles[triangle + 8] = vertex;
            triangles[triangle + 9] = vertex + 2;
            triangles[triangle + 10] = vertex + 3;
            triangles[triangle + 11] = vertex + 1;
        }

        Mesh mesh = new Mesh();
        mesh.name = "Generated Annulus";
        mesh.hideFlags = HideFlags.DontSave;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void CreateDistantSun(Transform parent)
    {
        GameObject sun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sun.name = "Distant Cyan Star";
        sun.transform.SetParent(parent, false);
        sun.transform.position = new Vector3(2520f, 1210f, 2220f);
        sun.transform.localScale = Vector3.one * 120f;
        RemovePrimitiveCollider(sun);
        sun.GetComponent<MeshRenderer>().sharedMaterial = CreateEmissionMaterial(
            "Distant Star Material",
            new Color(0.18f, 0.78f, 1f) * 2.8f);

        Light glow = sun.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(0.2f, 0.76f, 1f);
        glow.range = 680f;
        glow.intensity = 1.35f;
        glow.shadows = LightShadows.None;
    }

    private static Material CreateLitMaterial(string name, Color color, float metallic, float smoothness, Color emission)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Diffuse");

        Material material = new Material(shader);
        material.name = name;
        material.hideFlags = HideFlags.DontSave;
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", smoothness);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        if (emission.maxColorComponent > 0f)
        {
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", emission);
        }
        return material;
    }

    private static Material CreateEmissionMaterial(string name, Color emission)
    {
        return CreateLitMaterial(name, emission * 0.28f, 0.28f, 0.86f, emission);
    }


    private static Mesh GetSmoothSphere()
    {
        if (smoothSphere != null) return smoothSphere;
        const int longitude = 96, latitude = 48;
        var vertices = new Vector3[(longitude + 1) * (latitude + 1)];
        var normals = new Vector3[vertices.Length];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[longitude * latitude * 6];
        for (int y = 0; y <= latitude; y++)
        {
            float phi = y / (float)latitude * Mathf.PI;
            for (int x = 0; x <= longitude; x++)
            {
                float theta = x / (float)longitude * Mathf.PI * 2f;
                int i = y * (longitude + 1) + x;
                normals[i] = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                vertices[i] = normals[i] * 0.5f;
                uv[i] = new Vector2(x / (float)longitude, y / (float)latitude);
            }
        }
        int index = 0;
        for (int y = 0; y < latitude; y++) for (int x = 0; x < longitude; x++)
        {
            int a = y * (longitude + 1) + x, b = a + longitude + 1;
            triangles[index++] = a; triangles[index++] = a + 1; triangles[index++] = b;
            triangles[index++] = a + 1; triangles[index++] = b + 1; triangles[index++] = b;
        }
        smoothSphere = new Mesh { name = "Smooth Planet Sphere", vertices = vertices, normals = normals, uv = uv, triangles = triangles };
        smoothSphere.RecalculateBounds();
        return smoothSphere;
    }

    private static Mesh CreateStriatedRingMesh(float inner, float outer)
    {
        const int bands = 22, segments = 128;
        var vertices = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();
        for (int band = 0; band < bands; band++)
        {
            if (band == 9 || band == 15) continue;
            float r0 = Mathf.Lerp(inner, outer, band / (float)bands);
            float r1 = Mathf.Lerp(inner, outer, (band + 0.72f) / bands);
            int start = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 direction = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(direction * r0); vertices.Add(direction * r1);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = start + i * 2;
                triangles.Add(a); triangles.Add(a + 1); triangles.Add(a + 2);
                triangles.Add(a + 1); triangles.Add(a + 3); triangles.Add(a + 2);
                triangles.Add(a + 2); triangles.Add(a + 1); triangles.Add(a);
                triangles.Add(a + 2); triangles.Add(a + 3); triangles.Add(a + 1);
            }
        }
        var mesh = new Mesh { name = "Striated Orbital Ring" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    private static void RemovePrimitiveCollider(GameObject gameObject)
    {
        Collider collider = gameObject.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Object.Destroy(collider);
        }
    }
}

/// <summary>Small helper used by generated planets to keep the sky alive.</summary>
public sealed class StellarBodySpin : MonoBehaviour
{
    public Vector3 axis = Vector3.up;
    public float degreesPerSecond = 0.5f;

    private void Update()
    {
        transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
