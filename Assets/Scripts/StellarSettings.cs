using UnityEngine;

/// <summary>Small persistent settings shared by presentation, camera and sound.</summary>
public static class StellarSettings
{
    public static float MasterVolume { get; set; } = 0.72f;
    public static float Shake { get; set; } = 0.35f;
    public static bool CameraShake { get { return Shake > 0.01f; } }
    public static bool HighQuality { get; set; } = true;

    public static void Load()
    {
        MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Hyperion.V2.Volume", 0.72f));
        Shake = Mathf.Clamp01(PlayerPrefs.GetFloat("Hyperion.V2.Shake", 0.35f));
        HighQuality = PlayerPrefs.GetInt("Hyperion.V2.HighQuality", 1) == 1;
        Apply();
    }

    public static void Apply()
    {
        AudioListener.volume = Mathf.Clamp01(MasterVolume);
        QualitySettings.antiAliasing = HighQuality ? 4 : 0;
        QualitySettings.shadows = HighQuality ? ShadowQuality.All : ShadowQuality.Disable;
        QualitySettings.shadowDistance = HighQuality ? 120f : 0f;
        QualitySettings.pixelLightCount = HighQuality ? 4 : 1;
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat("Hyperion.V2.Volume", MasterVolume);
        PlayerPrefs.SetFloat("Hyperion.V2.Shake", Shake);
        PlayerPrefs.SetInt("Hyperion.V2.HighQuality", HighQuality ? 1 : 0);
        PlayerPrefs.Save();
        Apply();
    }
}
