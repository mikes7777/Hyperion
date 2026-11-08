using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Native Unity checks; use Tools > Hyperion > Validate V2 after the first import.
/// Batch: Unity -batchmode -nographics -projectPath <folder> -executeMethod
/// HyperionValidation.RunBatch -logFile validation.log
/// These checks create a temporary additive scene and never modify the race scene.
/// </summary>
public static class HyperionValidation
{
    [MenuItem("Tools/Hyperion/Validate V2")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run validation outside Play mode.");
        string scenePath = "Assets/Scenes/Hyperion.unity";
        Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null, "Race scene is missing.");
        bool built = false;
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.enabled && scene.path == scenePath) built = true;
        Require(built, "Enable Hyperion.unity in Build Settings.");
        Require(Shader.Find("Standard") != null, "The Built-in Standard shader is unavailable.");
        Scene previous = SceneManager.GetActiveScene();
        Scene temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        StellarTrack generatedTrack = null;
        try
        {
            SceneManager.SetActiveScene(temporary);
            var trackObject = new GameObject("Validation Circuit");
            StellarTrack track = trackObject.AddComponent<StellarTrack>();
            generatedTrack = track;
            track.Build();
            Require(track.TrackLength > 1000f, "V2 circuit should exceed one kilometre.");
            Require(track.TrackWidth >= 18f, "Circuit is too narrow.");
            const int steps = 512;
            float minStep = float.MaxValue, maxStep = 0f;
            int hint = 0;
            for (int i = 0; i < steps; i++)
            {
                float p = i / (float)steps;
                track.GetFrame(p, out Vector3 c, out Vector3 t, out Vector3 r, out Vector3 u);
                Require(Finite(c) && Finite(t) && Finite(r) && Finite(u), "Nonfinite circuit frame at " + i);
                Require(Mathf.Abs(t.magnitude - 1f) < 0.01f && Mathf.Abs(r.magnitude - 1f) < 0.01f && Mathf.Abs(u.magnitude - 1f) < 0.01f,
                    "Circuit frame is not normalized at " + i);
                Require(Mathf.Abs(Vector3.Dot(t, r)) < 0.02f && Vector3.Dot(Vector3.Cross(t, r), u) > 0.99f,
                    "Circuit frame orientation is inconsistent at " + i);
                track.GetFrame((i + 1f) / steps, out Vector3 next, out _, out _, out _);
                float distance = Vector3.Distance(c, next);
                minStep = Mathf.Min(minStep, distance);
                maxStep = Mathf.Max(maxStep, distance);
                float projected = track.FindNearestProgress(c + r * 2f, ref hint, out float lateral);
                float errorMetres = Mathf.Abs(Mathf.DeltaAngle(projected * 360f, p * 360f)) / 360f * track.TrackLength;
                Require(errorMetres < 0.75f && Mathf.Abs(lateral - 2f) < 0.25f,
                    "Nearest-road projection failed at " + i + ": " + errorMetres.ToString("F2") + " m");
            }
            Require(minStep > 0f && maxStep / minStep < 1.10f,
                "Track progress is not approximately proportional to distance.");
            foreach (StellarCarSpec spec in StellarCarCatalog.Cars)
            {
                GameObject car = StellarCarFactory.CreateCar(spec, "Validation " + spec.DisplayName, false);
                try
                {
                    Require(car.GetComponent<Rigidbody>() != null, spec.DisplayName + " has no Rigidbody.");
                    BoxCollider collider = car.GetComponent<BoxCollider>();
                    Require(collider != null && collider.size.x > 1f && collider.size.z > 3f,
                        spec.DisplayName + " has invalid collision dimensions.");
                    Require(car.transform.Find("Visuals") != null, spec.DisplayName + " has no visual root.");
                    ValidateMeshes(car);
                }
                finally
                {
                    car.GetComponent<StellarVehicleVisuals>().ReleaseGeneratedResources();
                    UnityEngine.Object.DestroyImmediate(car);
                }
            }
            ValidateMeshes(trackObject);
            foreach (Shader shader in Resources.LoadAll<Shader>(""))
                Require(!ShaderUtil.ShaderHasError(shader), "Shader has compilation errors: " + shader.name);
            Debug.Log("Hyperion V2 validation passed: scene/build settings, " + StellarCarCatalog.Cars.Length
                + " cars, circuit frames/projection, equal-distance progress, finite meshes, resource shaders. Track "
                + track.TrackLength.ToString("F0") + " m. This does not replace a race playtest.");
        }
        finally
        {
            if (generatedTrack != null) generatedTrack.ReleaseGeneratedResources();
            EditorSceneManager.CloseScene(temporary, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static void ValidateMeshes(GameObject root)
    {
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            Require(mesh != null && mesh.vertexCount > 0, "Empty mesh: " + filter.name);
            foreach (Vector3 vertex in mesh.vertices)
                Require(Finite(vertex), "Nonfinite vertex: " + filter.name);
            int[] triangles = mesh.triangles;
            Require(triangles.Length % 3 == 0, "Malformed triangle list: " + filter.name);
            foreach (int index in triangles)
                Require(index >= 0 && index < mesh.vertexCount, "Out-of-range mesh index: " + filter.name);
            Renderer renderer = filter.GetComponent<Renderer>();
            if (renderer == null) continue;
            foreach (Material material in renderer.sharedMaterials)
                Require(material != null && material.shader != null,
                    "Missing material: " + filter.name);
        }
    }

    private static bool Finite(Vector3 value)
    {
        return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z)
            || float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
