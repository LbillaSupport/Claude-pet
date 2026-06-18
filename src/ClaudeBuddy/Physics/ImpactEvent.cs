using ClaudeBuddy.Core;

namespace ClaudeBuddy.Physics;

/// <summary>Which edge of the screen the mascot just collided with.</summary>
public enum ImpactSurface
{
    Floor,
    LeftWall,
    RightWall,
    Ceiling,
}

/// <summary>
/// A single collision the moment it happens: which surface was hit, the speed of the
/// hit (px/s, used to pick a reaction — blink → recoil → pancake) and where on screen.
/// Raised by <see cref="PhysicsSystem.Impact"/>; the engine turns it into squash,
/// particles, dizziness and personality reactions.
/// </summary>
public readonly record struct ImpactEvent(ImpactSurface Surface, float Speed, Vector2 At);
