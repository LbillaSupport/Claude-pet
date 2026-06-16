using ClaudeBuddy.Core;
using ClaudeBuddy.Utilities;

namespace ClaudeBuddy.Input;

/// <summary>
/// Polls the global mouse position every frame (the mascot must react to the cursor
/// even when it is nowhere near the window). Exposes a smoothed velocity so "the
/// user moved the mouse fast" can trigger a surprise without jittering.
/// </summary>
public sealed class CursorTracker
{
    private bool _initialised;

    public Vector2 Position { get; private set; }

    public Vector2 Velocity { get; private set; }

    public float Speed => Velocity.Length;

    public void Update(float dt)
    {
        if (!NativeMethods.GetCursorPos(out NativeMethods.POINT p))
        {
            return;
        }

        var now = new Vector2(p.X, p.Y);

        if (!_initialised)
        {
            Position = now;
            _initialised = true;
            return;
        }

        if (dt > 1e-4f)
        {
            Vector2 instant = (now - Position) / dt;
            Velocity = MathUtil.Damp(Velocity, instant, 18f, dt);
        }

        Position = now;
    }
}
