using ClaudeBuddy.Settings;

namespace ClaudeBuddy.Services;

/// <summary>Plays the mascot's sound effects. Currently a silent no-op (see below).</summary>
public interface IAudioService
{
    /// <summary>Plays a named effect (e.g. "pet", "jump"). No-op while the app is silent.</summary>
    void Play(string key);
}

/// <summary>
/// Claude Buddy is intentionally silent — the user asked for no sound, and a quiet
/// desktop pet is the wholesome default. This is a no-op implementation kept behind the
/// <see cref="IAudioService"/> seam (rather than ripping every <c>_audio.Play(...)</c>
/// call out of the engine) so re-enabling sound later is a one-file change.
/// </summary>
public sealed class AudioService : IAudioService
{
    public AudioService(ISettingsService settings)
    {
        // Settings are unused while silent; the constructor signature is kept for DI.
        _ = settings;
    }

    /// <summary>No-op: the app makes no sound.</summary>
    public void Play(string key)
    {
    }
}
