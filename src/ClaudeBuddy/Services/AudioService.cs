using ClaudeBuddy.Settings;
using ClaudeBuddy.Utilities;

namespace ClaudeBuddy.Services;

/// <summary>Plays the mascot's tiny sound effects, honouring the mute/volume settings.</summary>
public interface IAudioService
{
    /// <summary>Plays a named effect (e.g. "pet", "jump") if a matching wav exists.</summary>
    void Play(string key);
}

/// <summary>
/// A deliberately tiny audio layer built on <c>winmm</c> PlaySound, so there are no
/// extra dependencies. Effects are loaded from <c>Assets/Audio/{key}.wav</c>; if a
/// file is missing the call is a silent no-op (SND_NODEFAULT prevents the system
/// "ding"), which means the app ships and runs perfectly even before any audio art
/// is added. Mute is honoured immediately.
/// </summary>
public sealed class AudioService : IAudioService
{
    private readonly ISettingsService _settings;
    private readonly string _audioRoot;

    public AudioService(ISettingsService settings)
    {
        _settings = settings;
        _audioRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Audio");
    }

    public void Play(string key)
    {
        AppSettings s = _settings.Current;
        if (s.Muted || s.Volume <= 0.001f || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        string path = Path.Combine(_audioRoot, key + ".wav");
        if (!File.Exists(path))
        {
            return;
        }

        // Async + no-default keeps playback off the simulation thread and silent on miss.
        NativeMethods.PlaySound(
            path, IntPtr.Zero,
            NativeMethods.SND_ASYNC | NativeMethods.SND_FILENAME | NativeMethods.SND_NODEFAULT);
    }
}
