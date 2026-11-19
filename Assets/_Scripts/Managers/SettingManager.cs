using System;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.UI;
using Track = MoreMountains.Tools.MMSoundManager.MMSoundManagerTracks;

// Apply the player preferences after MMSoundManager loads its mixer settings.
[DefaultExecutionOrder(100)]
public sealed class SettingManager : MonoBehaviour
{
    const string MusicKey = "nonogram.settings.music";
    const string SfxKey = "nonogram.settings.sfx";
    const string PatternKey = "nonogram.settings.pattern";
    const string DebugKey = "nonogram.settings.debug";

    [SerializeField] Toggle musicToggle;
    [SerializeField] Toggle sfxToggle;
    [SerializeField] Toggle patternToggle;
    [SerializeField] GameObject levelListButton;
    [SerializeField] Toggle debugToggle;

    public static bool PatternModeEnabled => PlayerPrefs.GetInt(PatternKey, 0) != 0;
    public static bool DebugModeEnabled => PlayerPrefs.GetInt(DebugKey, 0) != 0;
    public static event Action<bool> PatternModeChanged;

    void Start()
    {
        if (debugToggle && levelListButton)
        {
            debugToggle.SetIsOnWithoutNotify(DebugModeEnabled);
            debugToggle.onValueChanged.AddListener(SetDebugMode);
            levelListButton.SetActive(DebugModeEnabled);
        }

        bool musicEnabled = PlayerPrefs.GetInt(MusicKey, 1) != 0;
        bool sfxEnabled = PlayerPrefs.GetInt(SfxKey, 1) != 0;
        musicToggle.SetIsOnWithoutNotify(musicEnabled);
        sfxToggle.SetIsOnWithoutNotify(sfxEnabled);
        patternToggle.SetIsOnWithoutNotify(PatternModeEnabled);
        ApplyTrack(Track.Music, musicEnabled);
        ApplySoundEffects(sfxEnabled);

        musicToggle.onValueChanged.AddListener(SetMusicEnabled);
        sfxToggle.onValueChanged.AddListener(SetSoundEffectsEnabled);
        patternToggle.onValueChanged.AddListener(SetPatternMode);
    }

    void OnDestroy()
    {
        if (musicToggle) musicToggle.onValueChanged.RemoveListener(SetMusicEnabled);
        if (sfxToggle) sfxToggle.onValueChanged.RemoveListener(SetSoundEffectsEnabled);
        if (patternToggle) patternToggle.onValueChanged.RemoveListener(SetPatternMode);
        if (debugToggle) debugToggle.onValueChanged.RemoveListener(SetDebugMode);
    }

    void SetMusicEnabled(bool enabled)
    {
        Save(MusicKey, enabled);
        ApplyTrack(Track.Music, enabled);
    }

    void SetSoundEffectsEnabled(bool enabled)
    {
        Save(SfxKey, enabled);
        ApplySoundEffects(enabled);
    }

    void SetPatternMode(bool enabled)
    {
        Save(PatternKey, enabled);
        PatternModeChanged?.Invoke(enabled);
    }

    void SetDebugMode(bool enabled)
    {
        Save(DebugKey, enabled);
        levelListButton.SetActive(enabled);
    }

    static void ApplySoundEffects(bool enabled)
    {
        ApplyTrack(Track.Sfx, enabled);
        ApplyTrack(Track.UI, enabled);
    }

    static void ApplyTrack(Track track, bool enabled)
    {
        var soundManager = MMSoundManager.Instance;
        if (enabled) soundManager.UnmuteTrack(track);
        else soundManager.MuteTrack(track);
    }

    static void Save(string key, bool enabled)
    {
        PlayerPrefs.SetInt(key, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }
}
