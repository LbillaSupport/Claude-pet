using System.Diagnostics;

namespace ClaudeBuddy.Core;

/// <summary>
/// Tracks delta time for the main loop. All simulation is driven by
/// <see cref="Delta"/> seconds so behaviour is identical whether the machine runs
/// at 30 or 144 FPS. The delta is clamped to avoid "explosions" after the app was
/// paused (e.g. the user locked their workstation).
/// </summary>
public sealed class GameTime
{
    private const float MaxDeltaSeconds = 1f / 15f; // never simulate more than ~66ms at once
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _lastTicks;

    /// <summary>Seconds elapsed since the previous frame, clamped for stability.</summary>
    public float Delta { get; private set; }

    /// <summary>Unscaled wall-clock seconds since the engine started.</summary>
    public double Total { get; private set; }

    /// <summary>Total simulated frames since start.</summary>
    public long FrameCount { get; private set; }

    /// <summary>A smoothed frames-per-second estimate for diagnostics / Photo Mode.</summary>
    public float Fps { get; private set; }

    public void Tick()
    {
        long now = _stopwatch.ElapsedTicks;
        float raw = (float)((now - _lastTicks) / (double)Stopwatch.Frequency);
        _lastTicks = now;

        Delta = MathUtil.Clamp(raw, 0f, MaxDeltaSeconds);
        Total += Delta;
        FrameCount++;

        if (raw > 1e-5f)
        {
            float instantaneous = 1f / raw;
            Fps = Fps <= 0f ? instantaneous : MathUtil.Lerp(Fps, instantaneous, 0.1f);
        }
    }
}
