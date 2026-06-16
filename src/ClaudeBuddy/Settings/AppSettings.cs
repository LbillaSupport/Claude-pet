using System.Text.Json.Serialization;

namespace ClaudeBuddy.Settings;

/// <summary>
/// The full, serializable application state. This single POCO is persisted to
/// <c>%AppData%\ClaudeBuddy\settings.json</c> and round-trips with
/// <see cref="System.Text.Json"/>. Every field has a sensible default so a missing
/// or partial file still produces a valid configuration.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Folder name of the active skin under <c>/Skins</c>. Empty = built-in.</summary>
    public string CurrentSkin { get; set; } = string.Empty;

    public string FavoriteSkin { get; set; } = string.Empty;

    /// <summary>Last known mascot position in virtual-desktop pixels. NaN = centre.</summary>
    public float PositionX { get; set; } = float.NaN;

    public float PositionY { get; set; } = float.NaN;

    /// <summary>Overall character scale multiplier (0.5 – 2.0).</summary>
    public float Scale { get; set; } = 1.0f;

    /// <summary>Master volume (0 – 1).</summary>
    public float Volume { get; set; } = 0.7f;

    public bool Muted { get; set; }

    /// <summary>Global animation speed multiplier (0.5 = languid, 2.0 = caffeinated).</summary>
    public float AnimationSpeed { get; set; } = 1.0f;

    /// <summary>How often the mascot picks a new behaviour (0.25 = calm, 2.0 = hyper).</summary>
    public float BehaviorFrequency { get; set; } = 1.0f;

    public bool AlwaysOnTop { get; set; } = true;

    public bool LaunchOnStartup { get; set; }

    public bool WeatherEnabled { get; set; } = true;

    /// <summary>Accumulated happiness (0 – 1). Rises with petting, decays slowly.</summary>
    public float Happiness { get; set; } = 0.5f;

    /// <summary>Lifetime interaction counters that drive achievements.</summary>
    public Stats Stats { get; set; } = new();

    /// <summary>IDs of achievements the user has unlocked.</summary>
    public HashSet<string> UnlockedAchievements { get; set; } = new();

    /// <summary>IDs of any content (skins, particles, dances) unlocked via rewards.</summary>
    public HashSet<string> UnlockedContent { get; set; } = new();

    /// <summary>First-run timestamp, used by the "kept alive for N days" achievement.</summary>
    public DateTimeOffset FirstRunUtc { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public bool IsFirstEverRun { get; set; }
}

/// <summary>Persisted lifetime statistics.</summary>
public sealed class Stats
{
    public long PetCount { get; set; }
    public long ClaudeOpenCount { get; set; }
    public long JumpCount { get; set; }
    public long DistanceWalked { get; set; }
    public HashSet<string> SkinsUsed { get; set; } = new();
    public HashSet<string> BehaviorsSeen { get; set; } = new();
}
