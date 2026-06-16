using ClaudeBuddy.Core;

namespace ClaudeBuddy.Engine;

/// <summary>
/// The physical state of the character: where it is, how it is moving, and the
/// coarse pose flags that physics and behaviour write and the renderer reads.
/// Pure data — every system that mutates it does so explicitly, which keeps the
/// simulation easy to reason about.
/// </summary>
public sealed class Mascot
{
    /// <summary>Feet anchor position (bottom-centre of the body) in physical pixels.</summary>
    public Vector2 Position { get; set; }

    /// <summary>Linear velocity in physical pixels per second.</summary>
    public Vector2 Velocity { get; set; }

    public Facing Facing { get; set; } = Facing.Right;

    public bool OnGround { get; set; } = true;

    public bool BeingDragged { get; set; }

    /// <summary>User scale multiplier (DPI is applied separately by the renderer).</summary>
    public float Scale { get; set; } = 1.0f;

    /// <summary>Non-uniform squash &amp; stretch, 1 = neutral. Eases back each frame.</summary>
    public float SquashX { get; set; } = 1.0f;

    public float SquashY { get; set; } = 1.0f;

    /// <summary>The animation the renderer should currently express.</summary>
    public AnimationState Animation { get; set; } = AnimationState.Idle;

    /// <summary>Per-mascot animation speed (mood/behaviour can speed it up or down).</summary>
    public float AnimationSpeed { get; set; } = 1.0f;

    /// <summary>Height of the character at the current scale, in physical pixels.</summary>
    public float HeightPx(float dpiScale) => EngineConstants.CharacterHeight * Scale * dpiScale;

    public float HalfWidthPx(float dpiScale) => HeightPx(dpiScale) * 0.34f;

    /// <summary>Applies an instantaneous squash, e.g. on landing. Eased back over time.</summary>
    public void Squash(float x, float y)
    {
        SquashX = x;
        SquashY = y;
    }

    public void RelaxSquash(float dt)
    {
        SquashX = MathUtil.Damp(SquashX, 1f, 9f, dt);
        SquashY = MathUtil.Damp(SquashY, 1f, 9f, dt);
    }
}
