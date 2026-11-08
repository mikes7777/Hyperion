using UnityEngine;

/// <summary>
/// Stable arcade handling built around the generated track instead of WheelColliders.
/// The Rigidbody still collides with rivals and barriers, while spline-relative grip
/// and ride-height forces keep the car controllable on the floating circuit.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class ArcadeCarController : MonoBehaviour
{
    public StellarCarSpec Spec { get; private set; }
    public Rigidbody Body { get; private set; }
    public float SpeedKph { get; private set; }
    public float Nitro { get; private set; } = 100f;
    public float TotalProgress { get; private set; }
    public float NormalizedProgress { get; private set; }
    public float LateralOffset { get; private set; }
    public bool CanDrive { get; private set; }
    public bool IsBoosting { get; private set; }

    private StellarTrack track;
    private int nearestHint;
    private int lapBase;
    private float previousProgress;
    private float steerVisual;
    private bool paused;
    private Vector3 pausedVelocity;
    private Vector3 pausedAngularVelocity;

    public void Initialize(StellarTrack raceTrack, StellarCarSpec carSpec, float startProgress)
    {
        track = raceTrack;
        Spec = carSpec;
        Body = GetComponent<Rigidbody>();
        Body.useGravity = false;
        Body.mass = 1250f;
        Body.drag = 0.08f;
        Body.angularDrag = 3.5f;
        Body.interpolation = RigidbodyInterpolation.Interpolate;
        Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        Body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        Nitro = 100f;
        WarpToProgress(startProgress, 0f);
        SetDriving(false);
    }

    public void SetDriving(bool enabled)
    {
        CanDrive = enabled;
        paused = false;
        if (Body == null) return;
        Body.isKinematic = !enabled;
        if (!enabled)
        {
            Body.velocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            IsBoosting = false;
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
        transform.position = center + right * lane + up * 0.72f;
        transform.rotation = Quaternion.LookRotation(tangent, up);
        if (Body != null)
        {
            Body.position = transform.position;
            Body.rotation = transform.rotation;
            Body.velocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }
        lapBase = Mathf.FloorToInt(totalProgress);
        NormalizedProgress = normalized;
        previousProgress = normalized;
        TotalProgress = totalProgress;
        LateralOffset = lane;
        nearestHint = Mathf.RoundToInt(normalized * track.SampleCount) % track.SampleCount;
    }

    public void RecoverToTrack()
    {
        WarpToProgress(TotalProgress + 0.003f, 0f);
        if (CanDrive && !paused) Body.isKinematic = false;
    }

    private void FixedUpdate()
    {
        if (!CanDrive || paused || track == null || Body == null || Body.isKinematic) return;

        float currentProgress = track.FindNearestProgress(Body.position, ref nearestHint, out float lateral);
        if (currentProgress < 0.25f && previousProgress > 0.75f) lapBase++;
        else if (currentProgress > 0.75f && previousProgress < 0.25f) lapBase--;

        NormalizedProgress = currentProgress;
        previousProgress = currentProgress;
        TotalProgress = lapBase + currentProgress;
        LateralOffset = lateral;

        track.GetFrame(currentProgress, out Vector3 center, out Vector3 trackTangent, out Vector3 trackRight, out Vector3 trackUp);
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, trackUp).normalized;
        if (forward.sqrMagnitude < 0.25f) forward = trackTangent;

        float forwardSpeed = Vector3.Dot(Body.velocity, forward);
        SpeedKph = Mathf.Abs(forwardSpeed) * 3.6f;
        float throttle = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) ? 1f : 0f;
        float braking = (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) ? 1f : 0f;
        float steer = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) steer -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) steer += 1f;

        IsBoosting = throttle > 0f && SpeedKph > 55f && Nitro > 0.2f &&
                     (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        float topSpeedMps = Spec.TopSpeedKph / 3.6f;
        float allowedTopSpeed = topSpeedMps * (IsBoosting ? 1.17f : 1f);
        float acceleration = 14f + Spec.Acceleration * 1.75f;
        if (IsBoosting)
        {
            acceleration += 15f + Spec.Nitro * 0.85f;
            Nitro = Mathf.Max(0f, Nitro - (29f - Spec.Nitro * 0.55f) * Time.fixedDeltaTime);
        }
        else
        {
            Nitro = Mathf.Min(100f, Nitro + (4.6f + Spec.Nitro * 0.28f) * Time.fixedDeltaTime);
        }

        float edge = Mathf.Abs(lateral) / (track.TrackWidth * 0.5f);
        float edgePenalty = Mathf.InverseLerp(0.78f, 1.08f, edge);
        if (throttle > 0f && forwardSpeed < allowedTopSpeed)
            Body.AddForce(forward * acceleration * (1f - edgePenalty * 0.68f), ForceMode.Acceleration);

        if (braking > 0f && forwardSpeed > 0.2f)
            Body.AddForce(-forward * 42f, ForceMode.Acceleration);

        float overspeed = Mathf.Max(0f, forwardSpeed - allowedTopSpeed);
        if (overspeed > 0f) Body.AddForce(-forward * overspeed * 2.4f, ForceMode.Acceleration);
        Body.AddForce(-forward * Mathf.Max(0f, forwardSpeed) * 0.055f, ForceMode.Acceleration);

        float speed01 = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / Mathf.Max(1f, topSpeedMps));
        float steeringDegrees = (35f + Spec.Handling * 4.2f) * (0.3f + speed01 * 0.7f);
        if (Mathf.Abs(forwardSpeed) > 1.4f)
        {
            Quaternion steered = Quaternion.AngleAxis(steer * steeringDegrees * Time.fixedDeltaTime, trackUp) * Body.rotation;
            Body.MoveRotation(steered);
        }

        // Strong lateral tire grip with a small amount of high-speed slip.
        float lateralVelocity = Vector3.Dot(Body.velocity, trackRight);
        float grip = (4.8f + Spec.Handling * 0.82f) * Mathf.Lerp(1f, 0.72f, speed01);
        Body.AddForce(-trackRight * lateralVelocity * grip, ForceMode.Acceleration);

        // Ride-height spring keeps the car hovering just above the zero-thickness road mesh.
        float currentHeight = Vector3.Dot(Body.position - center, trackUp);
        float verticalSpeed = Vector3.Dot(Body.velocity, trackUp);
        float spring = (0.72f - currentHeight) * 84f - verticalSpeed * 13f;
        Body.AddForce(trackUp * spring, ForceMode.Acceleration);

        // Track assist is intentionally light: players can still slide wide and hit the rails.
        float headingError = Vector3.SignedAngle(forward, trackTangent, trackUp);
        float assist = Mathf.Lerp(0.55f, 1.7f, Mathf.Clamp01(edge)) + (1f - speed01) * 0.25f;
        Body.AddTorque(Vector3.up * Mathf.Clamp(headingError, -25f, 25f) * assist * 0.12f, ForceMode.Acceleration);

        if (edgePenalty > 0f)
        {
            Body.AddForce(-Body.velocity * edgePenalty * 1.6f, ForceMode.Acceleration);
            Body.AddForce(-Mathf.Sign(lateral) * trackRight * edgePenalty * 7f, ForceMode.Acceleration);
        }

        steerVisual = Mathf.Lerp(steerVisual, steer, 1f - Mathf.Exp(-9f * Time.fixedDeltaTime));
        Transform visual = transform.Find("Visuals");
        if (visual != null) visual.localRotation = Quaternion.Euler(0f, steerVisual * 2.4f, -steerVisual * 1.7f);

        if (Body.position.y < center.y - 8f || Mathf.Abs(lateral) > track.TrackWidth * 1.35f)
            RecoverToTrack();
    }
}
