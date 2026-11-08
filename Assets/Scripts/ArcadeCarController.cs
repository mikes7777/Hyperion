using UnityEngine;

/// <summary>
/// Track-normal arcade chassis. Grip acts in the car's frame, not the road frame,
/// so steering changes the trajectory and the handbrake can break rear traction.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public sealed class ArcadeCarController : MonoBehaviour
{
    public StellarCarSpec Spec { get; private set; }
    public Rigidbody Body { get; private set; }
    public float SpeedKph { get; private set; }
    public float Nitro { get; private set; } = 100f;
    public bool IsPaused { get { return paused; } }
    public float TotalProgress { get; private set; }
    public float NormalizedProgress { get; private set; }
    public float LateralOffset { get; private set; }
    public bool CanDrive { get; private set; }
    public bool IsBoosting { get; private set; }
    public bool IsDrifting { get; private set; }
    public float SlipAngle { get; private set; }
    public float Steering { get { return steering; } }

    private const float RideHeight = 0.72f;
    private StellarTrack track;
    private Transform visual;
    private int nearestHint;
    private float previousProgress;
    private float steering, throttle, brake;
    private bool handbrake, boostRequested, boostExhausted, paused;
    private float recoveryCooldown;
    private Vector3 pausedVelocity, pausedAngularVelocity;

    public void Initialize(StellarTrack raceTrack, StellarCarSpec carSpec, float startProgress)
    {
        track = raceTrack;
        Spec = carSpec;
        visual = transform.Find("Visuals");
        Body = GetComponent<Rigidbody>();
        Body.useGravity = false;
        Body.mass = 1250f;
        Body.drag = 0.015f;
        Body.angularDrag = 8f;
        Body.interpolation = RigidbodyInterpolation.Interpolate;
        Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        // Pitch and roll must follow the road's bank and elevation.
        Body.constraints = RigidbodyConstraints.None;
        Body.centerOfMass = new Vector3(0f, -0.1f, 0f);
        Nitro = 100f;
        WarpToProgress(startProgress, 0f);
        SetDriving(false);
    }

    public void SetDriving(bool enabled)
    {
        CanDrive = enabled;
        paused = false;
        if (Body == null) return;
        if (!enabled && !Body.isKinematic)
        {
            Body.velocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }
        Body.isKinematic = !enabled;
        if (!enabled)
        {
            IsBoosting = IsDrifting = false;
            throttle = brake = steering = SpeedKph = SlipAngle = 0f;
        }
    }

    public void SetPaused(bool shouldPause)
    {
        if (Body == null || shouldPause == paused) return;
        paused = shouldPause;
        if (shouldPause)
        {
            pausedVelocity = Body.velocity;
            pausedAngularVelocity = Body.angularVelocity;
            Body.isKinematic = true;
            IsBoosting = IsDrifting = false;
        }
        else if (CanDrive)
        {
            Body.isKinematic = false;
            Body.velocity = pausedVelocity;
            Body.angularVelocity = pausedAngularVelocity;
        }
    }

    public void WarpToProgress(float totalProgress, float lane)
    {
        if (track == null) return;
        float normalized = Mathf.Repeat(totalProgress, 1f);
        track.GetFrame(normalized, out Vector3 center, out Vector3 tangent, out Vector3 right, out Vector3 up);
        Vector3 position = center + right * lane + up * RideHeight;
        Quaternion rotation = Quaternion.LookRotation(tangent, up);
        transform.SetPositionAndRotation(position, rotation);
        if (Body != null)
        {
            Body.position = position;
            Body.rotation = rotation;
            if (!Body.isKinematic)
            {
                Body.velocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
        }
        NormalizedProgress = previousProgress = normalized;
        TotalProgress = totalProgress;
        LateralOffset = lane;
        SpeedKph = SlipAngle = steering = 0f;
        IsBoosting = IsDrifting = false;
        nearestHint = Mathf.RoundToInt(normalized * track.SampleCount) % track.SampleCount;
    }

    public void RecoverToTrack()
    {
        if (!CanDrive || paused || recoveryCooldown > 0f) return;
        // Never advance distance for a reset: repeated recovery cannot award laps.
        WarpToProgress(TotalProgress, 0f);
        recoveryCooldown = 2f;
    }

    private void Update()
    {
        if (!CanDrive || paused) return;
        throttle = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f;
        brake = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f;
        float desiredSteer = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                           - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        steering = Mathf.MoveTowards(steering, desiredSteer, Time.deltaTime * (desiredSteer == 0f ? 5.5f : 3.8f));
        handbrake = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        boostRequested = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (!boostRequested) boostExhausted = false;
        if (Input.GetKeyDown(KeyCode.F)) RecoverToTrack();
    }

    private void FixedUpdate()
    {
        if (!CanDrive || paused || track == null || Body == null || Body.isKinematic) return;
        float dt = Time.fixedDeltaTime;
        recoveryCooldown = Mathf.Max(0f, recoveryCooldown - dt);
        float progress = track.FindNearestProgress(Body.position, ref nearestHint, out float lateral);
        float delta = Mathf.DeltaAngle(previousProgress * 360f, progress * 360f) / 360f;
        // Reject discontinuous projections (e.g. a collision throwing a car across the circuit).
        float plausibleDistance = Mathf.Max(12f, Body.velocity.magnitude * dt * 4f);
        if (Mathf.Abs(delta) * track.TrackLength <= plausibleDistance) TotalProgress += delta;
        previousProgress = NormalizedProgress = progress;
        LateralOffset = lateral;
        track.GetFrame(progress, out Vector3 center, out Vector3 tangent, out Vector3 roadRight, out Vector3 up);
        Vector3 forward = Vector3.ProjectOnPlane(Body.rotation * Vector3.forward, up).normalized;
        if (forward.sqrMagnitude < 0.1f) forward = tangent;
        Vector3 right = Vector3.Cross(up, forward).normalized;
        Vector3 velocity = Body.velocity;
        float speed = Vector3.Dot(velocity, forward);
        float sideSpeed = Vector3.Dot(velocity, right);
        float planarSpeed = Vector3.ProjectOnPlane(velocity, up).magnitude;
        SpeedKph = planarSpeed * 3.6f;
        SlipAngle = Mathf.Atan2(sideSpeed, Mathf.Max(4f, Mathf.Abs(speed))) * Mathf.Rad2Deg;
        IsDrifting = handbrake && speed > 12f;
        IsBoosting = boostRequested && !boostExhausted && !handbrake && throttle > 0f && brake == 0f && speed > 12f && Nitro > 0.01f;
        if (IsBoosting)
        {
            Nitro = Mathf.Max(0f, Nitro - (26f - Spec.Nitro * 0.7f) * dt);
            if (Nitro <= 0f) boostExhausted = true;
        }
        else Nitro = Mathf.Min(100f, Nitro + (IsDrifting ? 13f : 6f) * dt);

        float topSpeed = Spec.TopSpeedKph / 3.6f * (IsBoosting ? 1.19f : 1f);
        float ratio = Mathf.Clamp01(Mathf.Max(0f, speed) / topSpeed);
        float acceleration = (12f + Spec.Acceleration * 1.6f) * Mathf.Lerp(1f, 0.48f, ratio * ratio);
        if (throttle > 0f && speed < topSpeed)
            Body.AddForce(forward * (acceleration + (IsBoosting ? 22f : 0f)), ForceMode.Acceleration);
        if (brake > 0f && speed > 0f)
            Body.AddForce(-forward * Mathf.Min(52f, speed / dt), ForceMode.Acceleration);
        if (handbrake && speed > 0f)
            Body.AddForce(-forward * Mathf.Min(9f, speed / dt), ForceMode.Acceleration);
        if (throttle == 0f && brake == 0f)
            Body.AddForce(-forward * speed * 0.10f, ForceMode.Acceleration);
        if (speed > topSpeed)
            Body.AddForce(-forward * (speed - topSpeed) * 3f, ForceMode.Acceleration);
        // Limited reverse provides a way out of barriers without needing to reset.
        if (brake > 0f && throttle == 0f && speed < 0.4f && speed > -8f)
            Body.AddForce(-forward * 10f, ForceMode.Acceleration);

        float gripRate = IsDrifting ? 2.2f : 8f + Spec.Handling * 0.45f;
        float maxGrip = IsDrifting ? 19f : 30f + Spec.Handling * 1.5f;
        Body.AddForce(-right * Mathf.Clamp(sideSpeed * gripRate, -maxGrip, maxGrip), ForceMode.Acceleration);
        float yawRate = Mathf.Min(1.22f, (31f + Spec.Handling * 1.4f) / Mathf.Max(12f, Mathf.Abs(speed)));
        if (IsDrifting) yawRate *= 1.28f;
        float direction = speed >= 0f ? 1f : -1f;
        float yaw = steering * yawRate * Mathf.Rad2Deg * Mathf.Clamp01(Mathf.Abs(speed) / 5f) * direction;
        // Gentle counter-steer stabilizes a drift after the player releases steering.
        if (!handbrake) yaw += Mathf.Clamp(SlipAngle, -30f, 30f) * 0.38f;
        Vector3 steered = Quaternion.AngleAxis(yaw * dt, up) * forward;
        Vector3 smoothedUp = Vector3.Slerp(Body.rotation * Vector3.up, up, 1f - Mathf.Exp(-12f * dt));
        Body.MoveRotation(Quaternion.LookRotation(Vector3.ProjectOnPlane(steered, smoothedUp).normalized, smoothedUp));
        Body.angularVelocity = Vector3.zero;

        float height = Vector3.Dot(Body.position - center, up);
        float spring = (RideHeight - height) * 130f - Vector3.Dot(velocity, up) * 20f;
        Body.AddForce(up * spring, ForceMode.Acceleration);
        float edge = Mathf.Abs(lateral) / (track.TrackWidth * 0.5f);
        float shoulder = Mathf.InverseLerp(0.84f, 1.04f, edge);
        if (shoulder > 0f)
        {
            Body.AddForce(-roadRight * Mathf.Sign(lateral) * shoulder * 18f, ForceMode.Acceleration);
            Body.AddForce(-Vector3.ProjectOnPlane(velocity, up) * shoulder * 0.45f, ForceMode.Acceleration);
        }
        if (visual != null)
        {
            Quaternion lean = Quaternion.Euler(-throttle * ratio * 0.6f + brake * 1.8f, 0f, -steering * ratio * 2.2f);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, lean, 1f - Mathf.Exp(-9f * dt));
        }
        if (height < -5f || height > 12f || edge > 1.5f)
        {
            recoveryCooldown = 0f;
            RecoverToTrack();
        }
    }
}
