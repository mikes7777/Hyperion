using System.Collections.Generic;
using UnityEngine;

/// <summary>Cosmetic animation only. No collider or gameplay forces live on the visual rig.</summary>
public sealed class StellarVehicleVisuals : MonoBehaviour
{
    private Transform[] steering, wheels, flames;
    private TrailRenderer[] trails;
    private Material brakeMaterial;
    private ArcadeCarController player;
    private StellarAICar rival;
    private float spin, steerAngle, previousSpeed;
    private Transform porscheFrontLeft;
    private Transform porscheFrontRight;
    private Transform porscheRear;

    private Quaternion frontLeftOriginal;
    private Quaternion frontRightOriginal;
    private Quaternion rearOriginal;

    public void Configure(Transform[] steeringPivots, Transform[] wheelPivots, Transform[] boostFlames,
        TrailRenderer[] driftTrails, Material brakeLightMaterial)
    {
        steering = steeringPivots; wheels = wheelPivots; flames = boostFlames;
        trails = driftTrails; brakeMaterial = brakeLightMaterial;
    }

    private void Start()
    {
        // Controllers are attached after the factory returns the root object.
        player = GetComponent<ArcadeCarController>();
        rival = GetComponent<StellarAICar>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Front Left Wheel_0")
                porscheFrontLeft = t;
            else if (t.name == "Front Right Wheel_1")
                porscheFrontRight = t;
            else if (t.name == "Rear Wheel_2")
                porscheRear = t;
        }

        if (porscheFrontLeft != null)
            frontLeftOriginal = porscheFrontLeft.localRotation;

        if (porscheFrontRight != null)
            frontRightOriginal = porscheFrontRight.localRotation;

        if (porscheRear != null)
            rearOriginal = porscheRear.localRotation;
    }

    private void Update()
    {
        float speed = player != null ? player.SpeedKph : rival != null ? rival.SpeedKph : 0f;
        bool paused = player != null ? player.IsPaused : rival != null && rival.IsPaused;
        if (paused) speed = 0f;
        bool driving = player != null ? player.CanDrive && !paused : speed > 1f;
        float desiredSteer = player != null && driving
            ? ((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
             - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f)) * Mathf.Lerp(25f, 9f, speed / 300f) : 0f;
        steerAngle = Mathf.Lerp(steerAngle, desiredSteer, 1f - Mathf.Exp(-12f * Time.deltaTime));
        float signedSpeed = player != null && player.Body != null && driving
            ? Vector3.Dot(player.Body.velocity, transform.forward) * 3.6f : speed;
        spin = Mathf.Repeat(spin + signedSpeed / 3.6f / 0.42f * Mathf.Rad2Deg * Time.deltaTime, 360f);
        if (wheels != null)
            for (int i = 0; i < wheels.Length; i++)
            {
                wheels[i].localRotation = Quaternion.Euler(spin, 0f, 0f);
                steering[i].localRotation = i < 2 ? Quaternion.Euler(0f, steerAngle, 0f) : Quaternion.identity;
            }
        Quaternion wheelSpin = Quaternion.Euler(spin, 0f, 0f);

        Quaternion porscheSteering = Quaternion.Euler(0f, 0f, steerAngle);

        if (porscheFrontLeft != null)
            porscheFrontLeft.localRotation =
                frontLeftOriginal * porscheSteering * wheelSpin;

        if (porscheFrontRight != null)
            porscheFrontRight.localRotation =
                frontRightOriginal * porscheSteering * wheelSpin;

        if (porscheRear != null)
            porscheRear.localRotation =
                rearOriginal * wheelSpin;
        bool braking = player != null ? driving && (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            : speed < previousSpeed - 0.2f;
        if (brakeMaterial != null)
            brakeMaterial.SetColor("_EmissionColor", new Color(1f, 0.015f, 0.035f) * (braking ? 5f : 1.3f));
        previousSpeed = speed;
        bool boosting = player != null && player.IsBoosting && !paused;
        if (flames != null)
            for (int i = 0; i < flames.Length; i++)
            {
                flames[i].gameObject.SetActive(boosting);
                if (boosting) flames[i].localScale = new Vector3(1f, 1f, 0.85f + Mathf.Sin(Time.time * 64f + i) * 0.16f);
            }
        if (trails != null)
            foreach (TrailRenderer trail in trails)
                if (trail != null) trail.emitting = player != null && player.IsDrifting && speed > 45f && !paused;
    }

    private void OnDestroy() { ReleaseGeneratedResources(); }

    public void ReleaseGeneratedResources()
    {
        // Brake lamps are intentionally unique per car, unlike shared paint/tire materials.
        if (brakeMaterial != null) StellarCarFactory.DestroyGenerated(brakeMaterial);
        brakeMaterial = null;
        var ownedMeshes = new HashSet<Mesh>();
        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            if (filter.sharedMesh != null && (filter.sharedMesh.name.StartsWith("Hyperion ") || filter.sharedMesh.name.StartsWith("Combined ")))
                ownedMeshes.Add(filter.sharedMesh);
        foreach (Mesh mesh in ownedMeshes) StellarCarFactory.DestroyGenerated(mesh);
    }
}
