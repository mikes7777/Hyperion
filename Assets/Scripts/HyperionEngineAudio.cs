using UnityEngine;

/// <summary>Small procedural engine synth so the prototype has responsive audio with no asset files.</summary>
[RequireComponent(typeof(AudioSource), typeof(ArcadeCarController))]
public sealed class StellarEngineAudio : MonoBehaviour
{
    private ArcadeCarController car;
    private AudioSource source;
    private AudioClip clip;
    private volatile float frequency = 58f;
    private volatile float amplitude = 0.08f;
    private int sampleRate = 44100;
    private double phaseA;
    private double phaseB;

    private void Awake()
    {
        car = GetComponent<ArcadeCarController>();
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0.28f;
        source.volume = 0.72f;
        source.dopplerLevel = 0.15f;
        sampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
        clip = AudioClip.Create("Procedural Hypercar Engine", sampleRate, 1, sampleRate, true, FillAudio);
        source.clip = clip;
        source.Play();
    }

    private void Update()
    {
        float speed = car != null ? car.SpeedKph : 0f;
        float ratio = Mathf.Clamp01(speed / 340f);
        frequency = 52f + ratio * 126f + (car != null && car.IsBoosting ? 24f : 0f);
        amplitude = 0.035f + ratio * 0.075f;
        source.pitch = 0.92f + ratio * 0.18f;
    }

    private void FillAudio(float[] data)
    {
        double outputRate = sampleRate;
        float localFrequency = frequency;
        float localAmplitude = amplitude;
        for (int i = 0; i < data.Length; i++)
        {
            phaseA += localFrequency / outputRate;
            phaseB += localFrequency * 0.502 / outputRate;
            if (phaseA >= 1.0) phaseA -= 1.0;
            if (phaseB >= 1.0) phaseB -= 1.0;
            float saw = (float)(phaseA * 2.0 - 1.0);
            float sub = Mathf.Sin((float)(phaseB * Mathf.PI * 2.0));
            data[i] = (saw * 0.36f + sub * 0.64f) * localAmplitude;
        }
    }

    private void OnDestroy()
    {
        if (source != null) source.Stop();
        if (clip != null) Destroy(clip);
    }
}
