using ClaudeBuddy.Core;
using ClaudeBuddy.Engine;

namespace ClaudeBuddy.Physics;

/// <summary>
/// A small, soft platformer-style integrator. It gives the mascot weight: gravity,
/// momentum, friction, a bouncy landing, screen-edge collision, and throw physics
/// when the user flings it. Landings trigger a squash &amp; stretch that the renderer
/// reads, which is most of what sells the "alive" feeling.
/// </summary>
public sealed class PhysicsSystem
{
    /// <summary>Raised the moment the mascot touches the ground after being airborne.</summary>
    public event Action<float>? Landed;

    /// <summary>
    /// Advances the mascot one step. When <paramref name="behaviourControlled"/> is
    /// true the behaviour is driving locomotion (walking), so we don't fight it with
    /// gravity unless it is genuinely off the ground.
    /// </summary>
    public void Step(Mascot mascot, World world, float dt, bool behaviourControlled)
    {
        if (mascot.BeingDragged)
        {
            // While grabbed, the input layer owns the position; just decay squash.
            mascot.RelaxSquash(dt);
            return;
        }

        float groundY = world.GroundY;
        Vector2 pos = mascot.Position;
        Vector2 vel = mascot.Velocity;

        bool wasAirborne = !mascot.OnGround;

        // Gravity always pulls; behaviour locomotion only sets horizontal velocity.
        if (!mascot.OnGround)
        {
            vel = vel.WithY(vel.Y + (EngineConstants.Gravity * dt));
            // Light air drag so thrown arcs settle naturally.
            vel = vel.WithX(MathUtil.Damp(vel.X, 0f, EngineConstants.AirFriction, dt));
        }
        else if (!behaviourControlled)
        {
            // Idle on the ground: bleed off any residual horizontal momentum.
            vel = vel.WithX(MathUtil.Damp(vel.X, 0f, EngineConstants.GroundFriction, dt));
        }

        pos += vel * dt;

        // ---- Ground collision -------------------------------------------
        if (pos.Y >= groundY)
        {
            if (wasAirborne && vel.Y > 50f)
            {
                float impact = MathUtil.Clamp01(vel.Y / EngineConstants.JumpVelocity);

                if (vel.Y > 320f)
                {
                    // Bounce, losing energy each time.
                    vel = vel.WithY(-vel.Y * EngineConstants.Bounciness);
                    pos = pos.WithY(groundY);
                    // Squash proportional to impact; renderer eases it back out.
                    mascot.Squash(1f + (0.28f * impact), 1f - (0.26f * impact));
                    Landed?.Invoke(impact);
                }
                else
                {
                    // Too slow to bounce — settle.
                    vel = vel.WithY(0f);
                    pos = pos.WithY(groundY);
                    mascot.OnGround = true;
                    mascot.Squash(1f + (0.16f * impact), 1f - (0.16f * impact));
                    Landed?.Invoke(impact);
                }
            }
            else
            {
                vel = vel.WithY(0f);
                pos = pos.WithY(groundY);
                mascot.OnGround = true;
            }
        }
        else if (pos.Y < groundY - 1f)
        {
            mascot.OnGround = false;
        }

        // ---- Side walls --------------------------------------------------
        float half = mascot.HalfWidthPx(world.DpiScale);
        if (pos.X < world.LeftWall + half)
        {
            pos = pos.WithX(world.LeftWall + half);
            if (vel.X < 0f)
            {
                vel = vel.WithX(-vel.X * EngineConstants.Bounciness);
                mascot.Facing = Facing.Right;
            }
        }
        else if (pos.X > world.RightWall - half)
        {
            pos = pos.WithX(world.RightWall - half);
            if (vel.X > 0f)
            {
                vel = vel.WithX(-vel.X * EngineConstants.Bounciness);
                mascot.Facing = Facing.Left;
            }
        }

        mascot.Position = pos;
        mascot.Velocity = vel;
        mascot.RelaxSquash(dt);
    }

    /// <summary>Launches the mascot upward with the configured jump impulse.</summary>
    public static void Jump(Mascot mascot, float strength = 1f)
    {
        if (!mascot.OnGround)
        {
            return;
        }

        mascot.OnGround = false;
        mascot.Velocity = mascot.Velocity.WithY(-EngineConstants.JumpVelocity * strength);
        mascot.Squash(0.84f, 1.2f); // anticipation stretch
    }

    /// <summary>Applies a throw impulse from a drag-release gesture.</summary>
    public static void Throw(Mascot mascot, Vector2 releaseVelocity)
    {
        Vector2 v = releaseVelocity * EngineConstants.ThrowImpulseScale;
        float speed = v.Length;
        if (speed > EngineConstants.MaxThrowSpeed)
        {
            v = v.Normalized() * EngineConstants.MaxThrowSpeed;
        }

        mascot.BeingDragged = false;
        mascot.OnGround = false;
        mascot.Velocity = v;
        if (MathF.Abs(v.X) > 30f)
        {
            mascot.Facing = v.X < 0f ? Facing.Left : Facing.Right;
        }
    }
}
