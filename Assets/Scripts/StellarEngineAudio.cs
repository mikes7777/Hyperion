using System;
using UnityEngine;

/// <summary>Layered engine, induction, road, tire and boost synth. Audio callback uses System.Math only.</summary>
[RequireComponent(typeof(AudioSource), typeof(ArcadeCarController))]
public sealed class StellarEngineAudio : MonoBehaviour
{
    private ArcadeCarController car;
    private AudioSource source;
    private AudioClip clip;
    [SerializeField] private AudioClip porscheEngine;
    private volatile float targetFrequency = 46f, targetAmplitude, targetBoost, targetTire, targetWind;
    private int sampleRate = 44100;
    private double phase, subPhase;
    private float frequency = 46f, amplitude, boost, tire, wind, filteredNoise;
    private uint noiseState = 7919u;
    private int gear = 1;
    private float shiftDip;
    public int Gear { get { return gear; } }

    private void Awake()
    {
        car = GetComponent<ArcadeCarController>();
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0.65f;
        source.dopplerLevel = 0f;
        sampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
        porscheEngine = Resources.Load<AudioClip>("porsche911");

        if (porscheEngine != null)
        {
            source.clip = porscheEngine;
            source.loop = true;
            source.volume = 0f;
            source.Play();
        }
        else
        {
            Debug.LogWarning("Porsche engine audio not found!");
        }
    }

    private void Update()
    {
        bool active = car != null && car.CanDrive && car.Body != null && !car.Body.isKinematic;
        if (!active)
        {
            targetAmplitude = targetBoost = targetTire = targetWind = 0f;

            if (porscheEngine != null)
                source.volume = 0f;

            return;
        }
        float speed = car.SpeedKph;
        if (porscheEngine != null)
        {
            bool accelerating = Input.GetKey(KeyCode.W) ||
                                Input.GetKey(KeyCode.UpArrow);

            float speedFactor = Mathf.Clamp01(speed / 200f);

            source.pitch = Mathf.Lerp(0.75f, 1.35f, speedFactor);

            float targetVolume = accelerating
                ? Mathf.Lerp(0.35f, 0.85f, speedFactor)
                : 0f;

            source.volume = Mathf.MoveTowards(
                source.volume,
                targetVolume,
                Time.deltaTime * 2f
            );
        }
        int nextGear = gear;
        if (gear < 6 && speed > gear * 56f + 3f) nextGear++;
        else if (gear > 1 && speed < (gear - 1) * 56f - 5f) nextGear--;
        if (nextGear != gear) { shiftDip = 0.12f; gear = nextGear; }
        shiftDip = Mathf.Max(0f, shiftDip - Time.deltaTime);
        float gearSpeed = Mathf.Clamp01((speed - (gear - 1) * 56f) / 56f);
        float rpm = Mathf.Lerp(1800f, 8800f, gearSpeed);
        if (gear == 6)
            rpm = Mathf.Lerp(5500f, 9000f, Mathf.InverseLerp(280f, 400f, speed));

        targetFrequency = rpm / 60f * 3f;
        bool throttle = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        targetAmplitude = (throttle ? 0.17f : 0.095f) * (shiftDip > 0f ? 0.5f : 1f);
        targetBoost = car.IsBoosting ? 0.10f : 0f;
        targetTire = car.IsDrifting ? Mathf.InverseLerp(10f, 35f, Mathf.Abs(car.SlipAngle)) * 0.09f : 0f;
        targetWind = Mathf.InverseLerp(80f, 370f, speed) * 0.065f;
    }

    private void FillAudio(float[] data)
    {
        // Values are copied once per buffer; never call Unity APIs from this audio thread.
        float f = targetFrequency, a = targetAmplitude, b = targetBoost, t = targetTire, w = targetWind;
        double dt = 1.0 / sampleRate;
        for (int i = 0; i < data.Length; i++)
        {
            frequency += (f - frequency) * 0.002f;
            amplitude += (a - amplitude) * 0.003f;
            boost += (b - boost) * 0.002f;
            tire += (t - tire) * 0.002f;
            wind += (w - wind) * 0.002f;
            phase += frequency * dt;
            subPhase += frequency * 0.5 * dt;
            phase -= Math.Floor(phase);
            subPhase -= Math.Floor(subPhase);
            noiseState = noiseState * 1664525u + 1013904223u;
            float noise = ((noiseState >> 8) / 8388607.5f) - 1f;
            filteredNoise += (noise - filteredNoise) * 0.06f;
            double p = phase * Math.PI * 2.0;
            float engine = (float)(Math.Sin(p) * 0.56 + Math.Sin(p * 2.0) * 0.24 + Math.Sin(p * 3.0) * 0.10 + Math.Sin(subPhase * Math.PI * 2.0) * 0.34);
            float induction = (float)Math.Sin(p * 5.0) * boost * 0.30f + (noise - filteredNoise) * boost * 0.45f;
            float scrub = (float)Math.Sin(p * 6.6) * tire * 0.4f + noise * tire * 0.3f;
            data[i] = engine * amplitude + induction + scrub + filteredNoise * wind;
        }
    }

    private void OnDestroy()
    {
        targetAmplitude = targetBoost = targetTire = targetWind = 0f;
        if (source != null) { source.Stop(); source.clip = null; }
        if (clip != null) Destroy(clip);
    }
}
