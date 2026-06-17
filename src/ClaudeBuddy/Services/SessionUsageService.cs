using System.Text.Json;
using ClaudeBuddy.Settings;

namespace ClaudeBuddy.Services;

/// <summary>An immutable read of the current Claude session "battery".</summary>
public readonly struct UsageSnapshot
{
    /// <summary>False when no Claude Code logs were found (the battery is hidden).</summary>
    public bool Available { get; init; }

    /// <summary>Tokens consumed inside the active rolling 5-hour window.</summary>
    public long TokensUsed { get; init; }

    /// <summary>The token budget the battery is scaled against.</summary>
    public long TokenLimit { get; init; }

    /// <summary>Portion of the budget used, 0 (fresh) .. 1 (spent).</summary>
    public float Fraction { get; init; }

    /// <summary>When the current window resets (5h after its first message).</summary>
    public DateTimeOffset ResetAt { get; init; }

    /// <summary>Live time remaining until the window resets (0 when idle/renewed).</summary>
    public TimeSpan TimeUntilReset { get; init; }

    /// <summary>Increments each time a brand-new 5-hour window begins (renewal).</summary>
    public int WindowId { get; init; }

    /// <summary>
    /// True only while a Claude session is actually live (token activity inside the last
    /// 5 hours). The battery is hidden entirely when this is false.
    /// </summary>
    public bool HasActiveSession { get; init; }

    /// <summary>Battery charge remaining, 0 (empty) .. 1 (full).</summary>
    public float Remaining => 1f - Fraction;
}

/// <summary>
/// Estimates how much of the user's rolling 5-hour Claude session has been used by
/// reading Claude Code's own local transcript logs (<c>~/.claude/projects/**/*.jsonl</c>),
/// the same data the community <c>ccusage</c> tool uses. It sums the token usage inside
/// the active 5-hour window and works out when that window resets.
///
/// PRIVACY: it only reads token-count fields and timestamps from the logs, entirely on
/// this machine. Nothing is sent anywhere. Anthropic does not expose the exact plan
/// limit locally, so the budget auto-calibrates to the user's own historical peak (or a
/// manual <see cref="AppSettings.SessionTokenLimit"/>); the battery is therefore an
/// estimate, not an official quota read-out.
/// </summary>
public sealed class SessionUsageService
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(5);
    private const long DefaultFloorTokens = 2_000_000;
    private const double PollSeconds = 30.0;

    private readonly ISettingsService _settings;
    private readonly string _projectsDir;
    private readonly object _gate = new();

    private Thread? _thread;
    private volatile bool _running;

    // Cached results (guarded by _gate).
    private bool _available;
    private long _tokensUsed;
    private long _limit = DefaultFloorTokens;
    private DateTimeOffset _windowStart;
    private bool _hasActiveWindow;
    private int _windowId;

    public SessionUsageService(ISettingsService settings)
    {
        _settings = settings;
        _projectsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");
    }

    public void Start()
    {
        if (_running || !Directory.Exists(_projectsDir))
        {
            return;
        }

        _running = true;
        _thread = new Thread(PollLoop)
        {
            IsBackground = true,
            Name = "ClaudeBuddy.Usage",
        };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        _thread?.Join(TimeSpan.FromSeconds(1));
        _thread = null;
    }

    /// <summary>The latest snapshot, with the reset countdown evaluated against now.</summary>
    public UsageSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                if (!_available)
                {
                    return new UsageSnapshot { Available = false };
                }

                DateTimeOffset reset = _windowStart + Window;
                TimeSpan remaining = _hasActiveWindow ? reset - DateTimeOffset.Now : TimeSpan.Zero;
                if (remaining < TimeSpan.Zero)
                {
                    remaining = TimeSpan.Zero;
                }

                // The battery tracks how much of the 5-hour session window is left — a
                // reliable, intuitive "how exhausted is my session" read. (The real token
                // quota isn't exposed locally, so a token-% battery was just guesswork.)
                float charge = _hasActiveWindow
                    ? Math.Clamp((float)(remaining.TotalHours / Window.TotalHours), 0f, 1f)
                    : 1f;

                return new UsageSnapshot
                {
                    Available = true,
                    TokensUsed = _tokensUsed,
                    TokenLimit = _limit,
                    Fraction = 1f - charge,
                    ResetAt = reset,
                    TimeUntilReset = remaining,
                    WindowId = _windowId,
                    HasActiveSession = _hasActiveWindow,
                };
            }
        }
    }

    private void PollLoop()
    {
        while (_running)
        {
            try
            {
                Refresh();
            }
            catch
            {
                // Usage is a nicety — never let a parse hiccup take down the app.
            }

            // Sleep in small slices so Stop() is responsive.
            for (int i = 0; i < PollSeconds * 10 && _running; i++)
            {
                Thread.Sleep(100);
            }
        }
    }

    private void Refresh()
    {
        List<(DateTimeOffset Ts, long Tokens)> entries = ReadRecentEntries();
        if (entries.Count == 0)
        {
            lock (_gate)
            {
                // No usage in recent history: a full, "renewed" battery.
                _available = true;
                _tokensUsed = 0;
                _hasActiveWindow = false;
            }

            return;
        }

        entries.Sort((a, b) => a.Ts.CompareTo(b.Ts));

        // Group into ccusage-style 5-hour blocks: a new block begins when either the
        // block has spanned 5h or there has been a >5h gap since the last message.
        var blockStart = FloorToHour(entries[0].Ts);
        DateTimeOffset blockLast = entries[0].Ts;
        long blockTokens = 0;
        long historicalMax = 0;

        foreach ((DateTimeOffset ts, long tokens) in entries)
        {
            if (ts - blockStart >= Window || ts - blockLast >= Window)
            {
                historicalMax = Math.Max(historicalMax, blockTokens);
                blockStart = FloorToHour(ts);
                blockTokens = 0;
            }

            blockTokens += tokens;
            blockLast = ts;
        }

        historicalMax = Math.Max(historicalMax, blockTokens);

        bool active = DateTimeOffset.Now < blockStart + Window;
        long used = active ? blockTokens : 0;

        AppSettings s = _settings.Current;
        s.ObservedMaxSessionTokens = Math.Max(s.ObservedMaxSessionTokens, historicalMax);
        long limit = s.SessionTokenLimit > 0
            ? s.SessionTokenLimit
            : Math.Max(DefaultFloorTokens, s.ObservedMaxSessionTokens);

        lock (_gate)
        {
            _available = true;
            _tokensUsed = used;
            _limit = limit;

            if (active && blockStart != _windowStart)
            {
                _windowId++; // a fresh window started — the engine treats this as a renewal
            }

            _windowStart = blockStart;
            _hasActiveWindow = active;
        }
    }

    private List<(DateTimeOffset, long)> ReadRecentEntries()
    {
        var entries = new List<(DateTimeOffset, long)>();
        // The active window is at most 5h; read a little extra to be safe.
        DateTime cutoff = DateTime.Now.AddHours(-6);

        foreach (string file in Directory.EnumerateFiles(_projectsDir, "*.jsonl", SearchOption.AllDirectories))
        {
            FileInfo info;
            try
            {
                info = new FileInfo(file);
            }
            catch
            {
                continue;
            }

            if (info.LastWriteTime < cutoff)
            {
                continue;
            }

            ReadFileEntries(file, entries);
        }

        return entries;
    }

    private static void ReadFileEntries(string file, List<(DateTimeOffset, long)> entries)
    {
        IEnumerable<string> lines;
        try
        {
            lines = File.ReadLines(file);
        }
        catch
        {
            return;
        }

        foreach (string line in lines)
        {
            if (line.Length == 0)
            {
                continue;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(line);
                JsonElement root = doc.RootElement;

                if (!root.TryGetProperty("timestamp", out JsonElement tsEl) ||
                    tsEl.ValueKind != JsonValueKind.String ||
                    !DateTimeOffset.TryParse(tsEl.GetString(), out DateTimeOffset ts))
                {
                    continue;
                }

                if (!root.TryGetProperty("message", out JsonElement msg) ||
                    msg.ValueKind != JsonValueKind.Object ||
                    !msg.TryGetProperty("usage", out JsonElement usage) ||
                    usage.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                // Count the "work" tokens (input + output + cache writes). Cache reads are
                // deliberately excluded: they're huge and cheap, and would swamp the metric.
                long tokens = GetLong(usage, "input_tokens")
                    + GetLong(usage, "output_tokens")
                    + GetLong(usage, "cache_creation_input_tokens");

                if (tokens > 0)
                {
                    entries.Add((ts, tokens));
                }
            }
            catch
            {
                // Skip malformed / partially-written lines (the active log is live).
            }
        }
    }

    private static long GetLong(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out JsonElement el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out long v)
            ? v
            : 0L;

    private static DateTimeOffset FloorToHour(DateTimeOffset ts) =>
        new(ts.Year, ts.Month, ts.Day, ts.Hour, 0, 0, ts.Offset);
}
