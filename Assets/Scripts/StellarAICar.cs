using UnityEngine;

/// <summary>Kinematic spline-following rival with distinct pace, lane choices, and rubber banding.</summary>
[RequireComponent(typeof(Rigidbody))]
public class StellarAICar : MonoBehaviour
{
    public float TotalProgress { get; private set; }
    public float SpeedKph { get; private set; }
    public bool Finished { get; private set; }
    public float FinishTime { get; private set; } = -1f;

    private StellarTrack track;
    private StellarCarSpec spec;
    private ArcadeCarController player;
    private Rigidbody body;
    private float lane;
    private float targetLane;
    private float laneTimer;
    private float currentSpeedMps;
    private float pace;
    private float catchup;
    private float minBand;
    private float maxBand;
    private float laneResponse;
    private float mistakeChance;
    private float personality;
    private bool running;
    private bool paused;

    public void Initialize(
        StellarTrack raceTrack,
        StellarCarSpec carSpec,
        ArcadeCarController playerCar,
        float startProgress,
        float startLane,
        float paceMultiplier,
        float rubberBandStrength,
        float minimumBand,
        float maximumBand,
        float steeringResponse,
        float mistakes,
        float personalityOffset)
    {
        track = raceTrack;
        spec = carSpec;
        player = playerCar;
        TotalProgress = startProgress;
        lane = targetLane = startLane;
        pace = paceMultiplier;
        catchup = rubberBandStrength;
        minBand = minimumBand;
        maxBand = maximumBand;
        laneResponse = steeringResponse;
        mistakeChance = mistakes;
        personality = personalityOffset;
        laneTimer = Random.Range(1.6f, 4.5f);
        currentSpeedMps = 0f;
        Finished = false;
        FinishTime = -1f;

        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        Warp();
    }

    public void SetRunning(bool enabled)
    {
        running = enabled && !Finished;
        if (!running) currentSpeedMps = enabled ? currentSpeedMps : 0f;
    }

    public void SetPaused(bool value) { paused = value; }

    public void MarkFinished(float raceTime)
    {
        if (Finished) return;
        Finished = true;
        FinishTime = raceTime;
        running = false;
        currentSpeedMps = 0f;
    }

    private void FixedUpdate()
    {
        if (!running || paused || Finished || track == null || body == null) return;

        float gapMeters = player == null ? 0f : (TotalProgress - player.TotalProgress) * track.TrackLength;
        float rubber = Mathf.Clamp(1f - gapMeters * catchup, minBand, maxBand);
        float normalized = Mathf.Repeat(TotalProgress, 1f);
        float curveLoad = Mathf.Abs(Mathf.Sin(normalized * Mathf.PI * 6f + personality));
        float curveSafety = Mathf.Lerp(1f, 0.955f, curveLoad);
        float targetSpeedKph = spec.TopSpeedKph * pace * rubber * curveSafety + personality * 2.2f;
        float targetSpeedMps = Mathf.Max(20f, targetSpeedKph / 3.6f);
        currentSpeedMps = Mathf.MoveTowards(currentSpeedMps, targetSpeedMps, (10f + spec.Acceleration * 1.4f) * Time.fixedDeltaTime);
        SpeedKph = currentSpeedMps * 3.6f;
        TotalProgress += currentSpeedMps * Time.fixedDeltaTime / Mathf.Max(1f, track.TrackLength);

        laneTimer -= Time.fixedDeltaTime;
        if (laneTimer <= 0f)
        {
            bool mistake = Random.value < mistakeChance;
            float passingBias = player != null && Mathf.Abs(gapMeters) < 45f ? -Mathf.Sign(player.LateralOffset) * 0.6f : Random.Range(-0.72f, 0.72f);
            targetLane = Mathf.Clamp(passingBias + (mistake ? Random.Range(-0.35f, 0.35f) : 0f), -0.78f, 0.78f);
            laneTimer = Random.Range(2.2f, 5.2f);
        }
        lane = Mathf.Lerp(lane, targetLane, 1f - Mathf.Exp(-laneResponse * Time.fixedDeltaTime));

        Warp();
    }

    private void Warp()
    {
        if (track == null || body == null) return;
        float normalized = Mathf.Repeat(TotalProgress, 1f);
        track.GetFrame(normalized, out Vector3 center, out Vector3 tangent, out Vector3 right, out Vector3 up);
        Vector3 position = center + right * (lane * track.TrackWidth * 0.38f) + up * 0.72f;
        Quaternion rotation = Quaternion.LookRotation(tangent, up);
        if (running)
        {
            body.MovePosition(position);
            body.MoveRotation(rotation);
        }
        else
        {
            body.position = position;
            body.rotation = rotation;
        }
    }
}
