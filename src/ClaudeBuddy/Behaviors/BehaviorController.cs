using ClaudeBuddy.Core;
using ClaudeBuddy.Emotions;
using ClaudeBuddy.Engine;
using ClaudeBuddy.Physics;
using ClaudeBuddy.Routine;

namespace ClaudeBuddy.Behaviors;

/// <summary>
/// The runtime brain. It owns the "currently running" behaviour, drives the
/// mascot's locomotion for that behaviour, counts down per-behaviour cooldowns, and
/// asks the <see cref="BehaviorSelector"/> for something new when the current one
/// finishes. Interactions (petting, Claude opening, scares) bypass selection through
/// <see cref="Force"/>.
/// </summary>
public sealed class BehaviorController
{
    private readonly BehaviorCatalog _catalog;
    private readonly BehaviorSelector _selector;
    private readonly Rng _rng;
    private readonly Dictionary<string, float> _cooldowns = new(StringComparer.OrdinalIgnoreCase);

    // A queued sequence of behaviours — a little "story" (walk → find → examine → wave →
    // leave). Each step runs to its natural end, then the next is begun, until the queue
    // drains and autonomous selection resumes. Any interaction (Force) clears it.
    private readonly Queue<string> _chain = new();

    private float _elapsed;
    private float _duration;
    private float _targetX;
    private float _approachX;     // a specific world X for ApproachPoint (set by the engine)
    private bool _dynamicTarget;
    private bool _drivingLocomotion;
    private bool _climaxFired;

    // ---- Wall-climbing state machine (the "climb" behaviour) -------------
    private enum ClimbPhase { ToWall, Up, CeilingOut, CeilingBack, Down, CornerHop }

    private ClimbPhase _climbPhase;
    private Surface _climbWall;     // LeftWall or RightWall
    private float _climbApproachX;  // X to walk to on the floor (body fully on-screen)
    private float _climbWallX;      // contact X once latched (legs right at the wall edge)
    private float _climbPeakY;      // Y to ascend to
    private float _climbCeilingX;   // how far to crawl along the ceiling
    private bool _climbCeiling;     // whether to traverse the ceiling at the top
    private float _climbHang;       // brief pause-and-look timer at peaks

    // ---- Corner hop (a little 360° flip between surfaces) ---------------
    private Vector2 _hopFrom;
    private Vector2 _hopTo;
    private Vector2 _hopArc;        // perpendicular bulge so it arcs rather than slides
    private float _hopFromAngle;
    private float _hopSpinTotal;    // radians swept (net reorientation + one full turn)
    private float _hopT;            // 0..1 progress
    private float _hopDuration;
    private Surface _hopToSurface;
    private ClimbPhase _hopAfter;
    private bool _hopEndsClimb;     // last hop drops back onto the floor and finishes

    public BehaviorController(BehaviorCatalog catalog, BehaviorSelector selector, Rng rng)
    {
        _catalog = catalog;
        _selector = selector;
        _rng = rng;
        Current = catalog["idle"];
        _duration = 2f;
    }

    /// <summary>Raised when a new behaviour begins (drives particles, sounds, stats).</summary>
    public event Action<BehaviorDefinition>? BehaviorStarted;

    /// <summary>Raised once partway through a behaviour that defines a climax burst.</summary>
    public event Action<BehaviorDefinition>? BehaviorClimax;

    public BehaviorDefinition Current { get; private set; }

    /// <summary>True when the active behaviour is steering horizontal movement.</summary>
    public bool ControlsLocomotion => _drivingLocomotion;

    /// <summary>True while a queued behaviour "story" is still playing out.</summary>
    public bool ChainActive => _chain.Count > 0;

    public void Update(
        Mascot mascot,
        World world,
        EmotionState emotion,
        in RoutineProfile routine,
        Vector2 cursorWorld,
        float dt,
        float behaviorFrequency)
    {
        TickCooldowns(dt);

        _elapsed += dt;

        // Fire the climax burst (e.g. the power-up explosion) on the release, once.
        if (!_climaxFired && Current.ClimaxParticle is not null && _elapsed >= _duration * 0.5f)
        {
            _climaxFired = true;
            BehaviorClimax?.Invoke(Current);
        }

        bool finished = _elapsed >= _duration;

        // A reaction behaviour that runs out, or any behaviour reaching its time, advances
        // a running "story" to its next step, or otherwise hands control back to the
        // autonomous selector.
        if (finished)
        {
            if (_chain.Count > 0 && _catalog.TryGet(_chain.Dequeue(), out BehaviorDefinition? next))
            {
                Begin(next, mascot, world, emotion, routine, cursorWorld, behaviorFrequency);
            }
            else
            {
                Begin(_selector.Select(_catalog, _cooldowns, routine, emotion, Current.Id),
                      mascot, world, emotion, routine, cursorWorld, behaviorFrequency);
            }
        }

        ApplyMovement(mascot, world, emotion, routine, cursorWorld, dt);
    }

    /// <summary>Immediately switches to a named behaviour (interaction-driven).</summary>
    public void Force(string id, Mascot mascot, World world, EmotionState emotion,
        in RoutineProfile routine, Vector2 cursorWorld, float behaviorFrequency = 1f)
    {
        _chain.Clear(); // an interaction interrupts any running story
        if (_catalog.TryGet(id, out BehaviorDefinition? def))
        {
            Begin(def, mascot, world, emotion, routine, cursorWorld, behaviorFrequency);
        }
    }

    /// <summary>
    /// Kicks off a sequenced "story": the first behaviour begins now, each subsequent one
    /// starts when its predecessor finishes. Unknown ids are skipped. Any <see cref="Force"/>
    /// (an interaction) cancels the remainder.
    /// </summary>
    public void RunChain(IReadOnlyList<string> ids, Mascot mascot, World world, EmotionState emotion,
        in RoutineProfile routine, Vector2 cursorWorld, float behaviorFrequency = 1f)
    {
        _chain.Clear();
        for (int i = 1; i < ids.Count; i++)
        {
            _chain.Enqueue(ids[i]);
        }

        if (ids.Count > 0 && _catalog.TryGet(ids[0], out BehaviorDefinition? def))
        {
            Begin(def, mascot, world, emotion, routine, cursorWorld, behaviorFrequency);
        }
    }

    public bool IsRunning(string id) => Current.Id.Equals(id, StringComparison.OrdinalIgnoreCase);

    /// <summary>Sets the world X an <see cref="BehaviorMovement.ApproachPoint"/> behaviour walks to
    /// (e.g. the real taskbar clock). Call right before forcing such a behaviour.</summary>
    public void SetApproachX(float worldX) => _approachX = worldX;

    private void Begin(
        BehaviorDefinition def,
        Mascot mascot,
        World world,
        EmotionState emotion,
        in RoutineProfile routine,
        Vector2 cursorWorld,
        float behaviorFrequency)
    {
        // Put the behaviour we are leaving on cooldown so it isn't picked again at once.
        if (Current.Cooldown > 0f)
        {
            _cooldowns[Current.Id] = Current.Cooldown;
        }

        // Leaving a climb for anything else: let go of the wall and fall back down.
        if (mascot.Climbing && def.Id != "climb")
        {
            mascot.Surface = Surface.Floor;
            mascot.OnGround = false;
            mascot.RenderAngleOverride = null; // cancel any in-progress flip
        }

        Current = def;
        _elapsed = 0f;
        _climaxFired = false;

        float freq = MathUtil.Clamp(behaviorFrequency, 0.25f, 2f);
        float baseDuration = _rng.Range(def.MinDuration, def.MaxDuration);
        // Higher "behaviour frequency" => shorter stints => more variety. Sleep and the
        // climb are exempt: a nap should feel like a nap, and the climb self-terminates
        // when it gets back to the floor (its duration is only a safety backstop).
        bool exemptFromFrequency = def.Category == BehaviorCategory.Sleepy || def.Id == "climb";
        _duration = exemptFromFrequency ? baseDuration : baseDuration / freq;

        emotion.Nudge(def.Mood, def.MoodIntensity);

        // Choose a locomotion goal.
        _dynamicTarget = def.Movement is BehaviorMovement.ApproachCursor or BehaviorMovement.FleeCursor;
        _targetX = def.Movement switch
        {
            BehaviorMovement.Wander or BehaviorMovement.Run =>
                _rng.Range(world.LeftWall + 120f, world.RightWall - 120f),
            BehaviorMovement.EdgePeek =>
                mascot.Position.X < (world.LeftWall + world.RightWall) * 0.5f ? world.LeftWall + 30f : world.RightWall - 30f,
            BehaviorMovement.ApproachPoint =>
                MathUtil.Clamp(_approachX, world.LeftWall + mascot.HalfWidthPx(world.DpiScale), world.RightWall - mascot.HalfWidthPx(world.DpiScale)),
            _ => mascot.Position.X,
        };

        // One-shot physics kicks / setup for acrobatic behaviours.
        switch (def.Id)
        {
            case "jump":
                PhysicsSystem.Jump(mascot, 1f);
                break;
            case "backflip":
                PhysicsSystem.Jump(mascot, 1.4f); // big air for the flip
                break;
            case "trip":
                mascot.Velocity = mascot.Velocity.WithX((float)mascot.Facing * 60f);
                break;
            case "climb":
                SetupClimb(mascot, world);
                break;
        }

        mascot.Animation = def.Animation;
        BehaviorStarted?.Invoke(def);
    }

    private void ApplyMovement(
        Mascot mascot,
        World world,
        EmotionState emotion,
        in RoutineProfile routine,
        Vector2 cursorWorld,
        float dt)
    {
        _drivingLocomotion = false;

        // The crab climbs a wall (and maybe the ceiling) under its own little state machine.
        if (Current.Id == "climb")
        {
            StepClimb(mascot, world, routine, dt);
            return;
        }

        // Airborne acrobatics: pick the right air pose and end when we land again.
        if (Current.Id is "jump" or "backflip")
        {
            if (!mascot.OnGround)
            {
                // A backflip spins through the whole arc; a plain jump just stretches.
                mascot.Animation = Current.Id == "backflip"
                    ? AnimationState.Spin
                    : (mascot.Velocity.Y < 0f ? AnimationState.Jump : AnimationState.Fall);
            }
            else if (_elapsed > 0.15f)
            {
                mascot.Animation = AnimationState.Land;
                _elapsed = _duration; // finish so a new behaviour is chosen next frame
            }

            return;
        }

        if (Current.Movement == BehaviorMovement.None)
        {
            return;
        }

        if (!mascot.OnGround)
        {
            return; // let physics finish any air time before we steer again
        }

        float speedBase = Current.Movement is BehaviorMovement.Run or BehaviorMovement.FleeCursor
            ? EngineConstants.RunSpeed
            : EngineConstants.WalkSpeed;
        float speed = speedBase * routine.MovementSpeedScale * emotion.SpeedMultiplier;

        float goalX = _dynamicTarget ? ResolveDynamicTarget(mascot, world, cursorWorld) : _targetX;
        float delta = goalX - mascot.Position.X;
        float dir = MathF.Sign(delta);

        bool arrived = MathF.Abs(delta) < 10f;

        if (Current.Movement == BehaviorMovement.ApproachCursor && MathF.Abs(delta) < 80f)
        {
            // Reached the cursor: stop and study it with the behaviour's own pose.
            mascot.Velocity = mascot.Velocity.WithX(MathUtil.Damp(mascot.Velocity.X, 0f, 10f, dt));
            mascot.Animation = Current.Animation;
            return;
        }

        if (Current.Movement == BehaviorMovement.ApproachPoint && MathF.Abs(delta) < 26f)
        {
            // Arrived at the target spot (e.g. under the real clock): stop and play the pose.
            mascot.Velocity = mascot.Velocity.WithX(MathUtil.Damp(mascot.Velocity.X, 0f, 12f, dt));
            mascot.Animation = Current.Animation;
            return;
        }

        if (arrived && Current.Movement is BehaviorMovement.Wander or BehaviorMovement.Run)
        {
            // Got where it was going: end early to keep life varied.
            mascot.Velocity = mascot.Velocity.WithX(MathUtil.Damp(mascot.Velocity.X, 0f, 12f, dt));
            _elapsed = _duration;
            return;
        }

        if (dir != 0f)
        {
            mascot.Facing = dir < 0f ? Facing.Left : Facing.Right;
            mascot.Velocity = mascot.Velocity.WithX(dir * speed);
            mascot.Animation = SelectLocomotionAnim(speedBase, dir);
            _drivingLocomotion = true;
        }
    }

    private void SetupClimb(Mascot mascot, World world)
    {
        float half = mascot.HalfWidthPx(world.DpiScale);
        // The legs are drawn at the contact point, so latching the contact a hair inside
        // the edge puts the feet right on the wall (no gap), while the body extends inward.
        float inset = MathF.Max(2f, mascot.HeightPx(world.DpiScale) * 0.03f);
        bool left = mascot.Position.X < (world.LeftWall + world.RightWall) * 0.5f;

        _climbWall = left ? Surface.LeftWall : Surface.RightWall;
        _climbApproachX = left ? world.LeftWall + half : world.RightWall - half;
        _climbWallX = left ? world.LeftWall + inset : world.RightWall - inset;

        // Only crawl onto the ceiling from the left wall, so the orientation only ever
        // sweeps one way (floor → wall → ceiling) and never the long way round.
        float ceilingY = world.CeilingY + inset;
        _climbCeiling = left && _rng.Chance(0.5f);
        if (_climbCeiling)
        {
            _climbPeakY = ceilingY;
        }
        else
        {
            float lo = ceilingY + 120f;
            float hi = world.GroundY - 220f;
            _climbPeakY = hi > lo ? _rng.Range(lo, hi) : ceilingY;
        }

        _climbPhase = ClimbPhase.ToWall;
        _climbHang = 0f;
        mascot.Surface = Surface.Floor; // walk to the wall on the floor first
    }

    private void StepClimb(Mascot mascot, World world, in RoutineProfile routine, float dt)
    {
        _drivingLocomotion = true;
        float half = mascot.HalfWidthPx(world.DpiScale);
        float speed = EngineConstants.ClimbSpeed * routine.MovementSpeedScale;
        float x = mascot.Position.X;
        float y = mascot.Position.Y;

        switch (_climbPhase)
        {
            case ClimbPhase.ToWall:
            {
                float delta = _climbApproachX - x;
                if (MathF.Abs(delta) < 6f)
                {
                    // Reached the wall — latch on and start ascending. X is eased onto the
                    // wall edge during the climb-up so it grabs the wall smoothly.
                    mascot.Velocity = Vector2.Zero;
                    mascot.OnGround = false;
                    mascot.Surface = _climbWall;
                    mascot.Facing = _climbWall == Surface.LeftWall ? Facing.Right : Facing.Left;
                    mascot.Animation = AnimationState.Climb;
                    _climbPhase = ClimbPhase.Up;
                }
                else
                {
                    float dir = MathF.Sign(delta);
                    mascot.Facing = dir < 0f ? Facing.Left : Facing.Right;
                    mascot.Velocity = mascot.Velocity.WithX(dir * EngineConstants.WalkSpeed * routine.MovementSpeedScale);
                    mascot.Animation = dir < 0f ? AnimationState.WalkLeft : AnimationState.WalkRight;
                }

                return;
            }

            case ClimbPhase.Up:
            {
                mascot.Surface = _climbWall;
                mascot.Animation = AnimationState.Climb;
                x = MathUtil.Damp(x, _climbWallX, 12f, dt); // hug the wall edge
                y -= speed * dt;
                if (y <= _climbPeakY)
                {
                    mascot.Position = new Vector2(_climbWallX, _climbPeakY);
                    if (_climbCeiling)
                    {
                        // Flip up and over the corner onto the ceiling.
                        float h = mascot.HeightPx(world.DpiScale);
                        var to = new Vector2(_climbWallX + (h * 0.5f), _climbPeakY);
                        _climbCeilingX = MathF.Min(to.X + _rng.Range(120f, 360f), world.RightWall - half);
                        BeginHop(mascot, to, MathF.PI, Surface.Ceiling, ClimbPhase.CeilingOut, new Vector2(0f, -h * 0.35f), false);
                    }
                    else
                    {
                        _climbHang = _rng.Range(0.8f, 1.8f);
                        _climbPhase = ClimbPhase.Down;
                    }

                    return;
                }

                mascot.Position = new Vector2(x, y);
                return;
            }

            case ClimbPhase.CeilingOut:
            {
                mascot.Surface = Surface.Ceiling;
                mascot.Animation = AnimationState.Climb;
                x += speed * dt;
                if (x >= _climbCeilingX)
                {
                    x = _climbCeilingX;
                    _climbHang = _rng.Range(0.8f, 1.6f);
                    _climbPhase = ClimbPhase.CeilingBack;
                }

                mascot.Position = new Vector2(x, y);
                return;
            }

            case ClimbPhase.CeilingBack:
            {
                mascot.Surface = Surface.Ceiling;
                if (_climbHang > 0f)
                {
                    _climbHang -= dt;
                    mascot.Animation = AnimationState.LookAround;
                    return;
                }

                mascot.Animation = AnimationState.Climb;
                x -= speed * dt;
                if (x <= _climbWallX)
                {
                    // Flip back down off the ceiling onto the wall.
                    mascot.Position = new Vector2(_climbWallX, _climbPeakY);
                    float h = mascot.HeightPx(world.DpiScale);
                    var to = new Vector2(_climbWallX, _climbPeakY + (h * 0.5f));
                    float wallAngle = _climbWall == Surface.LeftWall ? MathUtil.HalfPi : -MathUtil.HalfPi;
                    BeginHop(mascot, to, wallAngle, _climbWall, ClimbPhase.Down, new Vector2(h * 0.3f, 0f), false);
                    return;
                }

                mascot.Position = new Vector2(x, y);
                return;
            }

            case ClimbPhase.Down:
            {
                if (_climbHang > 0f)
                {
                    _climbHang -= dt;
                    mascot.Surface = _climbWall;
                    mascot.Animation = AnimationState.LookAround;
                    return;
                }

                mascot.Surface = _climbWall;
                mascot.Animation = AnimationState.Climb;
                y += speed * dt;
                if (y >= world.GroundY)
                {
                    // Hop off the wall back onto the floor (a final little flip).
                    mascot.Position = new Vector2(_climbWallX, world.GroundY);
                    float h = mascot.HeightPx(world.DpiScale);
                    float dir = _climbWall == Surface.LeftWall ? 1f : -1f;
                    var to = new Vector2(_climbWallX + (dir * h * 0.5f), world.GroundY);
                    BeginHop(mascot, to, 0f, Surface.Floor, ClimbPhase.Down, new Vector2(0f, -h * 0.4f), endsClimb: true);
                    return;
                }

                mascot.Position = new Vector2(x, y);
                return;
            }

            case ClimbPhase.CornerHop:
            {
                _hopT += dt / _hopDuration;
                float t = MathUtil.Clamp01(_hopT);
                float e = Easing.InOutSine(t);

                // Arc the contact point and spin the body a full turn as it flips over.
                mascot.Position = Vector2.Lerp(_hopFrom, _hopTo, e) + (_hopArc * MathF.Sin(t * MathF.PI));
                mascot.RenderAngleOverride = _hopFromAngle + (_hopSpinTotal * e);
                mascot.Animation = AnimationState.Jump;

                if (t >= 1f)
                {
                    mascot.Position = _hopTo;
                    mascot.RenderAngleOverride = null; // resume eased orientation
                    if (_hopEndsClimb)
                    {
                        mascot.Surface = Surface.Floor;
                        mascot.OnGround = true;
                        mascot.Velocity = Vector2.Zero;
                        mascot.Animation = AnimationState.Land;
                        _elapsed = _duration; // finished — choose a new behaviour next frame
                    }
                    else
                    {
                        mascot.Surface = _hopToSurface;
                        mascot.Animation = AnimationState.Climb;
                        _climbPhase = _hopAfter;
                    }
                }

                return;
            }
        }
    }

    private void BeginHop(Mascot mascot, Vector2 to, float toAngle, Surface toSurface,
        ClimbPhase after, Vector2 arc, bool endsClimb)
    {
        _hopFrom = mascot.Position;
        _hopTo = to;
        _hopArc = arc;
        _hopFromAngle = mascot.SurfaceAngle; // current orientation (Surface not yet changed)
        // Net reorientation the short way, plus one full extra turn for the 360° flair.
        _hopSpinTotal = MathUtil.WrapPi(toAngle - _hopFromAngle) + MathUtil.Tau;
        _hopT = 0f;
        _hopDuration = 0.5f;
        _hopToSurface = toSurface;
        _hopAfter = after;
        _hopEndsClimb = endsClimb;
        _climbPhase = ClimbPhase.CornerHop;
        mascot.Animation = AnimationState.Jump;
    }

    private float ResolveDynamicTarget(Mascot mascot, World world, Vector2 cursorWorld)
    {
        if (Current.Movement == BehaviorMovement.FleeCursor)
        {
            // Run to whichever side is away from the cursor.
            float away = mascot.Position.X < cursorWorld.X ? world.LeftWall + 60f : world.RightWall - 60f;
            return away;
        }

        return cursorWorld.X;
    }

    private static AnimationState SelectLocomotionAnim(float speedBase, float dir)
    {
        bool running = speedBase >= EngineConstants.RunSpeed;
        if (dir < 0f)
        {
            return running ? AnimationState.RunLeft : AnimationState.WalkLeft;
        }

        return running ? AnimationState.RunRight : AnimationState.WalkRight;
    }

    private void TickCooldowns(float dt)
    {
        if (_cooldowns.Count == 0)
        {
            return;
        }

        // Iterate over a snapshot of keys so we can mutate the dictionary.
        foreach (string key in _cooldowns.Keys.ToArray())
        {
            float remaining = _cooldowns[key] - dt;
            if (remaining <= 0f)
            {
                _cooldowns.Remove(key);
            }
            else
            {
                _cooldowns[key] = remaining;
            }
        }
    }
}
