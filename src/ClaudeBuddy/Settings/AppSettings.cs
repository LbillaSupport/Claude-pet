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

    /// <summary>
    /// UI + chatter language. <see cref="Core.Language.Auto"/> (default) detects it from the
    /// OS UI language at startup; the right-click menu lets the user pin a specific one.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Core.Language Language { get; set; } = Core.Language.Auto;

    /// <summary>
    /// Ambient fake weather (drifting snow/leaves/petals around the mascot). Off by
    /// default so particles only ever appear from deliberate behaviours (petting, dancing,
    /// celebrating, …) rather than floating around all the time.
    /// </summary>
    public bool WeatherEnabled { get; set; }

    /// <summary>
    /// When true, the mascot reacts while you type (it installs a count-only keyboard
    /// hook — see <c>Input/KeyboardActivityTracker</c>). Set false to never install any
    /// keyboard hook at all.
    /// </summary>
    public bool KeyboardReactions { get; set; } = true;

    /// <summary>Show the little session "battery" above the mascot.</summary>
    public bool ShowBattery { get; set; } = true;

    /// <summary>
    /// Fetch fun real-world data (weather, ARS blue dollar, BTC) from free public APIs so
    /// the mascot can react (shiver when it's cold, etc.) and chat about it. Touches the
    /// internet; turn off for a fully offline buddy. Only an approximate city is derived
    /// from your IP and nothing is ever sent out — see <c>Services/WorldDataService</c>.
    /// </summary>
    public bool WorldData { get; set; } = true;

    /// <summary>
    /// Token budget for the rolling 5-hour session window. 0 = auto-calibrate from your
    /// own historical peak usage (see <see cref="ObservedMaxSessionTokens"/>). Set a real
    /// number if you know your plan's limit and want the battery to track it exactly.
    /// </summary>
    public long SessionTokenLimit { get; set; }

    /// <summary>Largest 5-hour-window usage ever observed; used to auto-scale the battery.</summary>
    public long ObservedMaxSessionTokens { get; set; }

    /// <summary>
    /// Check GitHub Releases for a newer version on startup and update silently in the
    /// background. On by default so non-technical users always get fixes automatically.
    /// </summary>
    public bool AutoUpdate { get; set; } = true;

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

/// <summary>Persisted lifetime statistics. These also feed Claw'd's "memory": every so
/// often he comments on them ("ya me lanzaste 327 veces"), which is what makes him feel
/// like he actually remembers the time you've spent together.</summary>
public sealed class Stats
{
    public long PetCount { get; set; }
    public long ClaudeOpenCount { get; set; }
    public long JumpCount { get; set; }
    public long DistanceWalked { get; set; }

    /// <summary>Times the user has picked Claw'd up and flung him.</summary>
    public long ThrowCount { get; set; }

    /// <summary>Times Claw'd has pulled off a backflip.</summary>
    public long BackflipCount { get; set; }

    /// <summary>Times Claw'd has waved hello.</summary>
    public long GreetCount { get; set; }

    /// <summary>Highest point above the ground (in physical px) ever reached — the altitude record.</summary>
    public long MaxThrowHeightPx { get; set; }

    /// <summary>Best "keep-up" combo: most consecutive mid-air catches without touching any
    /// wall/floor/ceiling. The juggling mini-game's high score.</summary>
    public long BestKeepUpCombo { get; set; }

    /// <summary>When Claw'd was last petted (for "hace mucho que no me mimás" lines).</summary>
    public DateTimeOffset LastPettedUtc { get; set; }

    /// <summary>When the app was last running — used to greet the user back after a gap.</summary>
    public DateTimeOffset LastSeenUtc { get; set; }

    public HashSet<string> SkinsUsed { get; set; } = new();
    public HashSet<string> BehaviorsSeen { get; set; } = new();
}
