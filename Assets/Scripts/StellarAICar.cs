using System.Collections.Generic;
using UnityEngine;

/// <summary>Distance-based rivals with corner preview, braking and traffic-aware passing.</summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class StellarAICar : MonoBehaviour
{
    public bool IsPaused { get { return paused; } }
    public float TotalProgress { get; private set; }
    public float SpeedKph { get; private set; }
    public bool Finished { get; private set; }
    public float FinishTime { get; private set; } = -1f;
    public float LaneOffset { get { return lane * (track != null ? track.TrackWidth * 0.38f : 1f); } }

    private static readonly List<StellarAICar> Racers = new List<StellarAICar>();
    private StellarTrack track;
    private StellarCarSpec spec;
    private ArcadeCarController player;
    private Rigidbody body;
    private float lane, targetLane, laneTimer, currentSpeedMps;
    private float pace, catchup, minBand, maxBand, laneResponse, mistakeChance, personality;
    private float mistakeTimer;
    private bool running, paused;

    private void OnEnable() { if (!Racers.Contains(this)) Racers.Add(this); }
    private void OnDisable() { Racers.Remove(this); }

    public void Initialize(StellarTrack raceTrack, StellarCarSpec carSpec, ArcadeCarController playerCar,
        float startProgress, float startLane, float paceMultiplier, float rubberBandStrength,
        float minimumBand, float maximumBand, float steeringResponse, float mistakes, float personalityOffset)
    {
        track = raceTrack; spec = carSpec; player = playerCar;
        TotalProgress = startProgress;
        lane = targetLane = startLane;
        pace = paceMultiplier; catchup = rubberBandStrength; minBand = minimumBand; maxBand = maximumBand;
        laneResponse = steeringResponse; mistakeChance = mistakes; personality = personalityOffset;
        laneTimer = 0.6f + Mathf.Abs(personality);
        currentSpeedMps = 0f; Finished = false; FinishTime = -1f;
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        Warp();
    }

    public void SetRunning(bool enabled)
    {
        running = enabled;
        if (!enabled) { currentSpeedMps = SpeedKph = 0f; }
    }
    public void SetPaused(bool value) { paused = value; }

    public void MarkFinished(float raceTime)
    {
        if (Finished) return;
        Finished = true;
        FinishTime = raceTime;
        // Finished rivals clear the line instead of becoming immovable roadblocks.
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders) collider.enabled = false;
    }

    private void FixedUpdate()
    {
        if (!running || paused || track == null || body == null) return;
        float dt = Time.fixedDeltaTime;
        float gap = player == null ? 0f : (TotalProgress - player.TotalProgress) * track.TrackLength;
        float band = Mathf.Clamp(1f - gap * catchup, minBand, maxBand);
        float desiredSpeed = spec.TopSpeedKph / 3.6f * pace * band;
        // Preview braking distance, not a sine-wave guess at the shape of a corner.
        const float brakingAcceleration = 38f;
        float lateralAcceleration = (27f + spec.Handling * 1.1f) * Mathf.Clamp(pace, 0.8f, 1.08f);
        for (int i = 0; i <= 12; i++)
        {
            float distance = i * 11f;
            float curvature = track.GetCurvature(TotalProgress + distance / track.TrackLength);
            // A small margin absorbs curvature peaks between preview samples.
            float cornerSpeed = Mathf.Sqrt(lateralAcceleration / Mathf.Max(0.0001f, curvature)) * 0.96f;
            float entrySpeed = Mathf.Sqrt(cornerSpeed * cornerSpeed + 2f * brakingAcceleration * distance);
            desiredSpeed = Mathf.Min(desiredSpeed, entrySpeed);
        }
        mistakeTimer = Mathf.Max(0f, mistakeTimer - dt);
        if (mistakeTimer > 0f) desiredSpeed *= 0.82f;
        laneTimer -= dt;
        if (laneTimer <= 0f)
        {
            PlanLane();
            laneTimer = Random.Range(0.65f, 1.15f);
            if (Random.value < mistakeChance * 0.12f) mistakeTimer = Random.Range(0.6f, 1.2f);
        }

        float nearestGap = float.PositiveInfinity;
        float trafficSpeed = desiredSpeed;
        foreach (StellarAICar other in Racers)
        {
            if (other == this || other.track != track || other.Finished) continue;
            float ahead = (other.TotalProgress - TotalProgress) * track.TrackLength;
            if (ahead > 0f && ahead < 20f && Mathf.Abs(other.LaneOffset - LaneOffset) < 2.6f && ahead < nearestGap)
            { nearestGap = ahead; trafficSpeed = other.currentSpeedMps; }
        }
        if (player != null && !Finished)
        {
            float ahead = (player.TotalProgress - TotalProgress) * track.TrackLength;
            if (ahead > 0f && ahead < 20f && Mathf.Abs(player.LateralOffset - LaneOffset) < 2.6f && ahead < nearestGap)
            { nearestGap = ahead; trafficSpeed = player.SpeedKph / 3.6f; }
        }
        if (nearestGap < 18f)
            desiredSpeed = Mathf.Min(desiredSpeed, Mathf.Max(0f, trafficSpeed + (nearestGap - 8f) * 1.3f));
        float acceleration = desiredSpeed < currentSpeedMps ? 48f : 12f + spec.Acceleration * 1.4f;
        currentSpeedMps = Mathf.MoveTowards(currentSpeedMps, desiredSpeed, acceleration * dt);
        SpeedKph = currentSpeedMps * 3.6f;
        TotalProgress += currentSpeedMps * dt / track.TrackLength;
        // Bound lateral movement to prevent rivals sweeping sideways through cars.
        lane = Mathf.MoveTowards(lane, targetLane, (1.4f + laneResponse * 0.65f) / (track.TrackWidth * 0.38f) * dt);
        Warp();
    }

    private void PlanLane()
    {
        float bestScore = float.NegativeInfinity;
        float halfLaneRange = track.TrackWidth * 0.38f;
        float corner = track.GetSignedCurvature(TotalProgress + 45f / track.TrackLength);
        float preferred = Mathf.Clamp(corner * 55f + personality * 0.16f, -0.55f, 0.55f);
        for (int choice = -2; choice <= 2; choice++)
        {
            float candidate = choice * 0.34f;
            float meters = candidate * halfLaneRange;
            float score = -Mathf.Abs(candidate - preferred) * 1.1f - Mathf.Abs(candidate - lane) * 0.55f;
            foreach (StellarAICar other in Racers)
            {
                if (other == this || other.track != track || other.Finished) continue;
                float distance = (other.TotalProgress - TotalProgress) * track.TrackLength;
                if (distance > -8f && distance < 38f && Mathf.Abs(other.LaneOffset - meters) < 2.8f)
                    score -= distance < 9f ? 12f : 4f;
                // Do not cut across a car beside us while trying to pass someone ahead.
                if (Mathf.Abs(distance) < 7f && Between(other.LaneOffset, LaneOffset, meters)) score -= 20f;
            }
            if (player != null)
            {
                float distance = (player.TotalProgress - TotalProgress) * track.TrackLength;
                if (distance > -9f && distance < 40f && Mathf.Abs(player.LateralOffset - meters) < 3f) score -= 9f;
                if (Mathf.Abs(distance) < 7f && Between(player.LateralOffset, LaneOffset, meters)) score -= 20f;
            }
            if (score > bestScore) { bestScore = score; targetLane = candidate; }
        }
    }

    private static bool Between(float value, float from, float to)
    {
        return value >= Mathf.Min(from, to) - 1.4f && value <= Mathf.Max(from, to) + 1.4f;
    }

    private void Warp()
    {
        if (track == null || body == null) return;
        track.GetFrame(TotalProgress, out Vector3 center, out Vector3 tangent, out Vector3 right, out Vector3 up);
        Vector3 position = center + right * LaneOffset + up * 0.72f;
        Quaternion rotation = Quaternion.LookRotation(tangent, up);
        if (running) { body.MovePosition(position); body.MoveRotation(rotation); }
        else { body.position = position; body.rotation = rotation; }
    }
}
