using ClaudeBuddy.Core;
using ClaudeBuddy.Engine;

namespace ClaudeBuddy.Physics;

/// <summary>
/// A small, soft platformer-style integrator. It gives the mascot weight: gravity,
/// momentum, friction, a bouncy landing, screen-edge collision, and throw physics
/// when the user flings it. Landings trigger a squash &amp; stretch that the renderer
/// reads, which is most of what sells the "alive" feeling.
///
/// It also runs the "living drag": while grabbed, the body isn't glued to the cursor —
/// it hangs from the <em>local</em> point you took hold of, on a soft spring, with real
/// angular momentum (so a circular drag spins it like a helicopter). The spin and linear
/// velocity both survive the throw and settle back to upright once it lands.
/// </summary>
public sealed class PhysicsSystem
{
    /// <summary>Raised the moment the mascot touches the ground after being airborne
    /// (impact 0..1 where 1 ≈ a full jump-velocity slam). Kept for the simple dust hook.</summary>
    public event Action<float>? Landed;

    /// <summary>Raised at every collision (floor/wall/ceiling) with the contact speed so the
    /// engine can pick a reaction band — a tiny tap blinks, a slam pancakes.</summary>
    public event Action<ImpactEvent>? Impact;

    /// <summary>The current cursor position (physical px) the grab spring pulls toward.
    /// The engine refreshes this each frame while a drag is in progress.</summary>
    public Vector2 DragTarget { get; set; }

    /// <summary>
    /// Advances the mascot one step. When <paramref name="behaviourControlled"/> is
    /// true the behaviour is driving locomotion (walking), so we don't fight it with
    /// gravity unless it is genuinely off the ground.
    /// </summary>
    public void Step(Mascot mascot, World world, float dt, bool behaviourControlled)
    {
        if (mascot.BeingDragged)
        {
            StepDrag(mascot, world, dt);
            return;
        }

        if (mascot.Climbing)
        {
            // Clinging to a wall/ceiling: the climb behaviour owns the position and
            // there is no gravity. Just keep easing the squash back out.
            mascot.RelaxSquash(dt);
            SettleAngle(mascot, dt, grounded: true);
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
                float speed = vel.Y;

                if (vel.Y > 320f)
                {
                    // Bounce, losing energy each time.
                    vel = vel.WithY(-vel.Y * EngineConstants.Bounciness);
                    pos = pos.WithY(groundY);
                    // Squash proportional to impact; renderer eases it back out.
                    mascot.Squash(1f + (0.28f * impact), 1f - (0.26f * impact));
                    Landed?.Invoke(impact);
                    Impact?.Invoke(new ImpactEvent(ImpactSurface.Floor, speed, new Vector2(pos.X, groundY)));
                }
                else
                {
                    // Too slow to bounce — settle.
                    vel = vel.WithY(0f);
                    pos = pos.WithY(groundY);
                    mascot.OnGround = true;
                    mascot.Squash(1f + (0.16f * impact), 1f - (0.16f * impact));
                    Landed?.Invoke(impact);
                    Impact?.Invoke(new ImpactEvent(ImpactSurface.Floor, speed, new Vector2(pos.X, groundY)));
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

        // ---- Ceiling -----------------------------------------------------
        // Without this a hard upward throw would sail off the top of the screen forever.
        // Keep the whole body on-screen and give it a little boing off the top.
        float headRoom = mascot.HeightPx(world.DpiScale) * 0.95f;
        float ceiling = world.CeilingY + headRoom;
        if (pos.Y < ceiling)
        {
            pos = pos.WithY(ceiling);
            if (vel.Y < 0f)
            {
                float speed = -vel.Y;
                vel = vel.WithY(-vel.Y * EngineConstants.Bounciness);
                Impact?.Invoke(new ImpactEvent(ImpactSurface.Ceiling, speed, new Vector2(pos.X, ceiling)));
            }
        }

        // ---- Side walls --------------------------------------------------
        float half = mascot.HalfWidthPx(world.DpiScale);
        if (pos.X < world.LeftWall + half)
        {
            pos = pos.WithX(world.LeftWall + half);
            if (vel.X < 0f)
            {
                float speed = -vel.X;
                vel = vel.WithX(-vel.X * EngineConstants.Bounciness);
                mascot.Facing = Facing.Right;
                Impact?.Invoke(new ImpactEvent(ImpactSurface.LeftWall, speed, pos));
            }
        }
        else if (pos.X > world.RightWall - half)
        {
            pos = pos.WithX(world.RightWall - half);
            if (vel.X > 0f)
            {
                float speed = vel.X;
                vel = vel.WithX(-vel.X * EngineConstants.Bounciness);
                mascot.Facing = Facing.Left;
                Impact?.Invoke(new ImpactEvent(ImpactSurface.RightWall, speed, pos));
            }
        }

        mascot.Position = pos;
        mascot.Velocity = vel;
        mascot.RelaxSquash(dt);

        // The free-body spin keeps turning after a throw and rights itself once it's
        // down and slow — momentum is preserved, never snapped.
        SettleAngle(mascot, dt, grounded: mascot.OnGround);
    }

    /// <summary>
    /// The soft-grab solve. The grabbed point follows the cursor with a touch of lag, and
    /// pulling off-centre tilts the body so it hangs from where you took hold. Kept dead
    /// simple and unconditionally stable: the follow is an exponential ease (frame-rate
    /// independent, can never explode), and velocity is *derived* from how far it actually
    /// moved this frame — so the throw on release still flings it with real momentum.
    /// </summary>
    private void StepDrag(Mascot mascot, World world, float dt)
    {
        float dpi = world.DpiScale;
        Vector2 startPos = mascot.Position;

        // The held point should land on the cursor; solve for the feet position that puts
        // it there given the current tilt (so the body hangs from the grab point).
        Vector2 leverWorld = (mascot.GrabLocalOffset * dpi).Rotate(mascot.BodyAngle);
        Vector2 desiredFeet = DragTarget - leverWorld;

        // Exponential follow — snappy but with a hair of lag. Unconditionally stable.
        Vector2 pos = MathUtil.Damp(startPos, desiredFeet, EngineConstants.DragFollow, dt);

        // Keep the whole body on-screen.
        float half = mascot.HalfWidthPx(dpi);
        pos = new Vector2(
            world.ClampX(pos.X, half),
            MathUtil.Clamp(pos.Y, world.VirtualBounds.Top + 10, world.GroundY));

        // Derive velocity from the real displacement so a flick carries momentum into the
        // throw (clamped so a single stutter frame can't manufacture a rocket).
        if (dt > 1e-4f)
        {
            Vector2 instantVel = (pos - startPos) / dt;
            float vlen = instantVel.Length;
            if (vlen > EngineConstants.MaxThrowSpeed)
            {
                instantVel = instantVel.Normalized() * EngineConstants.MaxThrowSpeed;
            }

            // Smooth it a touch so the throw uses a stable recent velocity, not 1 frame.
            mascot.Velocity = MathUtil.Damp(mascot.Velocity, instantVel, 18f, dt);
        }

        // --- Tilt: the further the grab point is below/around the centre, and the faster
        //     it's hauled, the more it lazily swings to hang from that point. A gentle
        //     spring toward a target lean — never the runaway torque of before. ---
        float dpiH = mascot.HeightPx(dpi);
        Vector2 leverFromCentre = leverWorld - new Vector2(0f, -0.46f * dpiH);
        // The drag direction wants to trail behind the grab point (like a pendulum).
        Vector2 pull = desiredFeet - startPos;
        float targetAngle = MathUtil.Clamp(
            Vector2.Cross(leverFromCentre.Normalized(), pull) * EngineConstants.DragTiltScale,
            -EngineConstants.DragMaxTilt, EngineConstants.DragMaxTilt);
        float prevAngle = mascot.BodyAngle;
        mascot.BodyAngle = MathUtil.Damp(mascot.BodyAngle, targetAngle, EngineConstants.DragTiltStiffness, dt);
        // Track the angular rate (smoothed) so a fast circular whirl spins out into the
        // throw and can build dizziness — derived from the real per-frame turn.
        if (dt > 1e-4f)
        {
            float rate = MathUtil.WrapPi(mascot.BodyAngle - prevAngle) / dt;
            rate = MathUtil.Clamp(rate, -EngineConstants.DragMaxAngularVel, EngineConstants.DragMaxAngularVel);
            mascot.AngularVelocity = MathUtil.Damp(mascot.AngularVelocity, rate, 12f, dt);
        }

        // --- Squash toward the pull: a hard yank stretches the body, a gentle move barely
        //     deforms it (squeezing a stress ball). ---
        float stretch = MathUtil.Clamp(pull.Length * EngineConstants.DragSquashScale, 0f, 0.3f);
        float vertical = pull.Length > 1f ? MathF.Abs(pull.Y) / pull.Length : 0f;
        mascot.Squash(1f - (stretch * vertical * 0.5f), 1f + (stretch * vertical));

        mascot.Position = pos;
        mascot.OnGround = false;
    }

    /// <summary>
    /// Carries the free-body spin forward each frame and, once the body is grounded and
    /// turning slowly, gently rights it back to upright — a spring, never an instant snap.
    /// Only ever runs when there's actually a tilt to resolve, so it costs nothing at rest.
    /// </summary>
    private static void SettleAngle(Mascot mascot, float dt, bool grounded)
    {
        if (MathF.Abs(mascot.BodyAngle) < 1e-4f && MathF.Abs(mascot.AngularVelocity) < 1e-4f)
        {
            return;
        }

        // Free flight: spin keeps turning, bleeding off so it doesn't whirl forever.
        if (!grounded)
        {
            mascot.BodyAngle = MathUtil.WrapPi(mascot.BodyAngle + (mascot.AngularVelocity * dt));
            mascot.AngularVelocity *= MathF.Exp(-EngineConstants.AngularReleaseDamping * dt);
            return;
        }

        // Grounded: a fast spin still rolls a moment, then it firmly rights itself upright.
        if (MathF.Abs(mascot.AngularVelocity) >= EngineConstants.AngularRestThreshold)
        {
            mascot.BodyAngle = MathUtil.WrapPi(mascot.BodyAngle + (mascot.AngularVelocity * dt));
            mascot.AngularVelocity *= MathF.Exp(-EngineConstants.AngularReleaseDamping * dt);
            return;
        }

        mascot.AngularVelocity = MathUtil.Damp(mascot.AngularVelocity, 0f, EngineConstants.AngularRestStiffness, dt);
        mascot.BodyAngle = MathUtil.Damp(mascot.BodyAngle, 0f, EngineConstants.AngularRestStiffness, dt);
        if (MathF.Abs(mascot.BodyAngle) < 0.01f && MathF.Abs(mascot.AngularVelocity) < 0.05f)
        {
            mascot.BodyAngle = 0f;
            mascot.AngularVelocity = 0f;
        }
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

    /// <summary>
    /// Applies a throw impulse from a drag-release gesture. The body keeps whatever spin
    /// it built up during the drag, so flinging a spinning crab launches it spinning.
    /// </summary>
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
        // AngularVelocity is deliberately left untouched → momentum is preserved.
        if (MathF.Abs(v.X) > 30f)
        {
            mascot.Facing = v.X < 0f ? Facing.Left : Facing.Right;
        }
    }
}
