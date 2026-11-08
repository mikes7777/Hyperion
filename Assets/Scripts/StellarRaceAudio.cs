using UnityEngine;

/// <summary>Short original stereo interface cues; generated once, never on the audio thread.</summary>
[RequireComponent(typeof(AudioSource))]
public sealed class StellarRaceAudio : MonoBehaviour
{
    private AudioSource source;
    private AudioClip tick, go, lap, finish, select;
    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = .42f;
        tick = Tone("Countdown", .15f, 440f, 440f, false);
        go = Tone("Launch", .38f, 660f, 990f, true);
        lap = Tone("Lap complete", .32f, 523.25f, 783.99f, true);
        finish = Tone("Race complete", .75f, 523.25f, 1046.5f, true);
        select = Tone("Selection", .045f, 880f, 1046.5f, false);
    }
    public void Countdown() { Play(tick); }
    public void Go() { Play(go); }
    public void Lap() { Play(lap); }
    public void Finish() { Play(finish); }
    public void Select() { Play(select); }
    private void Play(AudioClip clip) { if (source != null && clip != null) source.PlayOneShot(clip); }
    private static AudioClip Tone(string name, float duration, float from, float to, bool chord)
    {
        const int rate = 22050;
        int count = Mathf.CeilToInt(duration * rate);
        float[] samples = new float[count * 2];
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            float p = t / duration;
            float hz = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, p));
            phase += hz * Mathf.PI * 2f / rate;
            float envelope = Mathf.Min(1f, t / .008f) * Mathf.Pow(1f - p, 1.6f);
            float sample = Mathf.Sin(phase) * .45f;
            if (chord) sample += Mathf.Sin(phase * 1.5f) * .15f;
            samples[i * 2] = sample * envelope;
            samples[i * 2 + 1] = sample * envelope;
        }
        AudioClip result = AudioClip.Create("Hyperion / " + name, count, 2, rate, false);
        result.SetData(samples, 0);
        return result;
    }
    private void OnDestroy()
    {
        if (tick != null) Destroy(tick);
        if (go != null) Destroy(go);
        if (lap != null) Destroy(lap);
        if (finish != null) Destroy(finish);
        if (select != null) Destroy(select);
    }
}
