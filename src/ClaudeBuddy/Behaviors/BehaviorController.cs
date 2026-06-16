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

    private float _elapsed;
    private float _duration;
    private float _targetX;
    private bool _dynamicTarget;
    private bool _drivingLocomotion;

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

    public BehaviorDefinition Current { get; private set; }

    /// <summary>True when the active behaviour is steering horizontal movement.</summary>
    public bool ControlsLocomotion => _drivingLocomotion;

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

        bool finished = _elapsed >= _duration;

        // A reaction behaviour that runs out, or any behaviour reaching its time,
        // hands control back to the autonomous selector.
        if (finished)
        {
            Begin(_selector.Select(_catalog, _cooldowns, routine, emotion, Current.Id),
                  mascot, world, emotion, routine, cursorWorld, behaviorFrequency);
        }

        ApplyMovement(mascot, world, emotion, routine, cursorWorld, dt);
    }

    /// <summary>Immediately switches to a named behaviour (interaction-driven).</summary>
    public void Force(string id, Mascot mascot, World world, EmotionState emotion,
        in RoutineProfile routine, Vector2 cursorWorld, float behaviorFrequency = 1f)
    {
        if (_catalog.TryGet(id, out BehaviorDefinition? def))
        {
            Begin(def, mascot, world, emotion, routine, cursorWorld, behaviorFrequency);
        }
    }

    public bool IsRunning(string id) => Current.Id.Equals(id, StringComparison.OrdinalIgnoreCase);

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

        Current = def;
        _elapsed = 0f;

        float freq = MathUtil.Clamp(behaviorFrequency, 0.25f, 2f);
        float baseDuration = _rng.Range(def.MinDuration, def.MaxDuration);
        // Higher "behaviour frequency" => shorter stints => more variety. Sleep is
        // deliberately exempt so naps still feel like naps.
        _duration = def.Category == BehaviorCategory.Sleepy ? baseDuration : baseDuration / freq;

        emotion.Nudge(def.Mood, def.MoodIntensity);

        // Choose a locomotion goal.
        _dynamicTarget = def.Movement is BehaviorMovement.ApproachCursor or BehaviorMovement.FleeCursor;
        _targetX = def.Movement switch
        {
            BehaviorMovement.Wander or BehaviorMovement.Run =>
                _rng.Range(world.LeftWall + 120f, world.RightWall - 120f),
            BehaviorMovement.EdgePeek =>
                mascot.Position.X < (world.LeftWall + world.RightWall) * 0.5f ? world.LeftWall + 30f : world.RightWall - 30f,
            _ => mascot.Position.X,
        };

        // One-shot physics kicks for acrobatic behaviours.
        switch (def.Id)
        {
            case "jump":
            case "climb":
                PhysicsSystem.Jump(mascot, def.Id == "climb" ? 1.15f : 1f);
                break;
            case "trip":
                mascot.Velocity = mascot.Velocity.WithX((float)mascot.Facing * 60f);
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

        // Airborne acrobatics: pick the right air pose and end when we land again.
        if (Current.Id is "jump" or "climb")
        {
            if (!mascot.OnGround)
            {
                mascot.Animation = mascot.Velocity.Y < 0f ? AnimationState.Jump : AnimationState.Fall;
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
