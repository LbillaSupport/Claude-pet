using System.Diagnostics;
using ClaudeBuddy.Achievements;
using ClaudeBuddy.Animation;
using ClaudeBuddy.Behaviors;
using ClaudeBuddy.Core;
using ClaudeBuddy.Emotions;
using ClaudeBuddy.Input;
using ClaudeBuddy.Mods;
using ClaudeBuddy.Particles;
using ClaudeBuddy.Physics;
using ClaudeBuddy.Rendering;
using ClaudeBuddy.Routine;
using ClaudeBuddy.Services;
using ClaudeBuddy.Settings;
using ClaudeBuddy.Skins;
using ClaudeBuddy.UI;
using ClaudeBuddy.Utilities;
using SkiaSharp;

namespace ClaudeBuddy.Engine;

/// <summary>
/// The conductor. It owns no behaviour of its own — it simply advances every system
/// in the right order each frame, translates window input into interactions, and
/// renders the result. This is the one class that knows about "everything", keeping
/// all the others small, focused and independently testable.
/// </summary>
public sealed class MascotEngine
{
    private readonly ISettingsService _settings;
    private readonly World _world;
    private readonly Mascot _mascot;
    private readonly Animator _animator;
    private readonly PhysicsSystem _physics;
    private readonly BehaviorController _behavior;
    private readonly BehaviorCatalog _catalog;
    private readonly EmotionState _emotion;
    private readonly DailyRoutine _routine;
    private readonly ParticleSystem _particles;
    private readonly ParticleRenderer _particleRenderer;
    private readonly CharacterArtist _artist;
    private readonly CursorTracker _cursor;
    private readonly KeyboardActivityTracker _keyboard;
    private readonly SessionUsageService _usage;
    private readonly WorldDataService _worldData;
    private readonly UsageHudRenderer _hud;
    private readonly SkinManager _skins;
    private readonly ModManager _mods;
    private readonly IClaudeLauncher _claude;
    private readonly IStartupService _startup;
    private readonly IAudioService _audio;
    private readonly AchievementService _achievements;
    private readonly ContextMenu _menu;
    private readonly Rng _rng;
    private readonly GameTime _time;

    private LayeredWindow _window = null!;
    private SkiaRenderer _renderer = null!;

    // Interaction state.
    private bool _leftPressed;
    private bool _dragging;
    private bool _pendingClick;
    private double _pendingClickTime;
    private Vector2 _pressScreen;
    private Vector2 _grabOffset;
    private double _doubleClickSeconds = 0.5;

    // "Living drag" reaction state (soft grab, spin, dizziness, personality).
    private float _helicopterTimer;       // seconds spent spinning fast enough for spirals
    private bool _resistArmed = true;     // re-armed each time it leaves an edge
    private float _resistTimer;           // >0 while playfully refusing to be dragged off an edge
    private float _panicTimer;            // >0 while clinging to the cursor just before a fast throw
    private Vector2 _panicThrowVel;       // the throw to apply when the panic beat ends
    private float _abuseMeter;            // rises with rough handling, decays; triggers personality
    private int _lastPersonalityLine = -1;
    private float _refuseTimer;           // >0 while sulking / refusing to move
    private bool _dizzyShowing;           // a dizzy reaction is currently playing
    private float _impactCooldown;        // debounces collision reactions so a bounce-storm can't lag

    private bool _photoMode;
    private bool _claudeWasRunning;
    private float _surpriseCd;
    private float _pollAcc;
    private float _saveAcc;
    private float _weatherTimer = 30f;
    private float _weatherEmitAcc;
    private int _typeNoteAcc;
    private float _renderSurfaceAngle; // smoothed orientation for wall/ceiling clinging

    // Session "battery" + speech-bubble state.
    private string _bubble = string.Empty;
    private float _bubbleRemaining;
    private float _bubbleElapsed;
    private int _lastWindowId;
    private int _lastChargeBucket = -1;
    private bool _usageInitialised;
    private float _phraseCooldown = 25f;
    private float _usageMoodAcc;
    private float _renewedFlash;

    // "World data" (weather/dollar/crypto) reaction state. The buddy is deliberately
    // chatty: a first weather reaction within seconds of launch, then a data/fact bubble
    // every ~half-minute — lively without being spammy.
    private float _weatherReactCooldown = 8f;
    private WeatherMood _lastWeather = WeatherMood.Unknown;
    private float _factCooldown = 15f;
    private int _factIndex;
    private string _celebratedHoliday = string.Empty; // last holiday we celebrated

    // Short reactions and mid-air moves that "type along" must not interrupt.
    private static readonly HashSet<string> TypingNonInterruptible = new(StringComparer.OrdinalIgnoreCase)
    {
        "pet", "surprised", "scared", "claude-celebrate", "wave-goodbye", "greet", "trip", "jump", "climb",
    };

    private AppSettings S => _settings.Current;

    public MascotEngine(
        ISettingsService settings, World world, Mascot mascot, Animator animator,
        PhysicsSystem physics, BehaviorController behavior, BehaviorCatalog catalog,
        EmotionState emotion, DailyRoutine routine, ParticleSystem particles,
        ParticleRenderer particleRenderer, CharacterArtist artist, CursorTracker cursor,
        KeyboardActivityTracker keyboard, SessionUsageService usage, UsageHudRenderer hud,
        WorldDataService worldData, SkinManager skins, ModManager mods,
        IClaudeLauncher claude, IStartupService startup,
        IAudioService audio, AchievementService achievements, ContextMenu menu, Rng rng, GameTime time)
    {
        _settings = settings;
        _world = world;
        _mascot = mascot;
        _animator = animator;
        _physics = physics;
        _behavior = behavior;
        _catalog = catalog;
        _emotion = emotion;
        _routine = routine;
        _particles = particles;
        _particleRenderer = particleRenderer;
        _artist = artist;
        _cursor = cursor;
        _keyboard = keyboard;
        _usage = usage;
        _worldData = worldData;
        _hud = hud;
        _skins = skins;
        _mods = mods;
        _claude = claude;
        _startup = startup;
        _audio = audio;
        _achievements = achievements;
        _menu = menu;
        _rng = rng;
        _time = time;
    }

    public void Initialize(LayeredWindow window, SkiaRenderer renderer)
    {
        _window = window;
        _renderer = renderer;
        _world.SetDpiScale(window.DpiScale);

        // Content discovery.
        _skins.Discover(_settings.SettingsDirectory);
        _skins.SetCurrent(S.CurrentSkin);
        _mods.LoadAll(_catalog, _settings.SettingsDirectory);
        EnsureUserFolders();

        // Restore emotion + placement.
        _emotion.Happiness = S.Happiness;
        _mascot.Scale = MathUtil.Clamp(S.Scale, 0.6f, 1.8f);
        RestorePosition();

        // Wire events.
        _window.LeftButtonDown += OnLeftDown;
        _window.LeftButtonUp += OnLeftUp;
        _window.LeftDoubleClick += OnDoubleClick;
        _window.RightButtonUp += OnRightUp;
        _window.DisplayChanged += OnDisplayChanged;
        _behavior.BehaviorStarted += OnBehaviorStarted;
        _behavior.BehaviorClimax += OnBehaviorClimax;
        _physics.Landed += OnLanded;
        _physics.Impact += OnImpact;
        _achievements.Unlocked += OnAchievementUnlocked;

        _window.SetTopmost(S.AlwaysOnTop);
        _startup.SetEnabled(S.LaunchOnStartup); // keep registry in sync with settings
        _claudeWasRunning = _claude.IsClaudeRunning();
        _doubleClickSeconds = NativeMethods.GetDoubleClickTime() / 1000.0;

        // React to the keyboard (opt-out via settings). The hook only counts keystrokes.
        if (S.KeyboardReactions)
        {
            _keyboard.Start();
        }

        // Read the session usage "battery" (opt-out via settings). Reads local logs only.
        if (S.ShowBattery)
        {
            _usage.Start();
        }

        // Fetch fun real-world data (weather/dollar/crypto) for reactions (opt-out).
        if (S.WorldData)
        {
            _worldData.Start();
        }

        // First hello.
        Trigger("greet");
    }

    public void Frame()
    {
        _time.Tick();
        float dt = _time.Delta;
        float dpi = _window.DpiScale;

        _cursor.Update(dt);
        _keyboard.Update(dt);
        _animator.SetTypingIntensity(_keyboard.Intensity);
        UpdateUsageReactions(dt);
        UpdateWorldDataReactions(dt);
        RoutineProfile routine = _routine.Evaluate();

        // --- Interaction timing -----------------------------------------
        if (_leftPressed)
        {
            // Safety net: if the physical button is no longer down but we never got the
            // WM_LBUTTONUP (capture lost, message swallowed by another window, …), release
            // it ourselves so the mascot can never stay "glued" to the cursor.
            if (!IsLeftMouseButtonDown())
            {
                OnLeftUp(0, 0);
            }
            else
            {
                UpdateDrag(dpi);
            }
        }

        if (_pendingClick && (_time.Total - _pendingClickTime) > _doubleClickSeconds)
        {
            _pendingClick = false;
            OpenClaude();
        }

        // The "living drag" reactions (dizziness, helicopter, panic-fling, personality).
        UpdateDragReactions(dt);

        // While grabbed OR clinging for a panic beat, physics still runs (the spring moves
        // the body) but the behaviour brain is parked. "Refuse to move" also parks the
        // brain for a beat while leaving physics on. Photo mode freezes everything.
        bool held = _dragging || _panicTimer > 0f;
        bool brainAwake = !_photoMode && !held && _refuseTimer <= 0f;
        bool physicsAwake = !_photoMode;

        // --- Startle on a fast flick of the mouse -----------------------
        _surpriseCd = MathF.Max(0f, _surpriseCd - dt);
        if (brainAwake && _surpriseCd <= 0f && _cursor.Speed > EngineConstants.SurpriseCursorSpeed * dpi)
        {
            _surpriseCd = 3.5f;
            Trigger("surprised");
        }

        // --- Core simulation --------------------------------------------
        if (brainAwake)
        {
            _behavior.Update(_mascot, _world, _emotion, routine, _cursor.Position, dt, S.BehaviorFrequency);
            UpdateTypingReaction();
        }

        if (physicsAwake)
        {
            _physics.Step(_mascot, _world, dt, _behavior.ControlsLocomotion);
        }

        _emotion.Update(dt, routine);
        _mascot.AnimationSpeed = MathUtil.Clamp(
            S.AnimationSpeed * MathUtil.Lerp(0.8f, 1.15f, _emotion.Energy), 0.4f, 2.4f);

        (float lookX, float lookY) = ComputeEyeLook(dpi);
        if (physicsAwake)
        {
            _animator.SetDizziness(_mascot.Dizziness);
            _animator.Update(_mascot, _emotion, dt, lookX, lookY);
            _particles.Update(dt);
            UpdateWeather(dt);
            AccumulateStats(dt);
        }

        // --- Periodic housekeeping --------------------------------------
        _pollAcc += dt;
        if (_pollAcc >= 1f)
        {
            _pollAcc = 0f;
            PollClaude();
            EvaluateAchievements();
        }

        _saveAcc += dt;
        if (_saveAcc >= 20f)
        {
            _saveAcc = 0f;
            PersistAndSave();
        }

        Render(dpi);
    }

    public void Shutdown()
    {
        _keyboard.Stop();
        _usage.Stop();
        _worldData.Stop();
        PersistAndSave();
    }

    // ===================================================================
    //  Rendering
    // ===================================================================

    private void Render(float dpi)
    {
        int canvas = _renderer.Size;
        float anchorX = canvas * 0.5f;
        float anchorY = canvas * EngineConstants.CanvasFeetAnchor;

        int winX = (int)MathF.Round(_mascot.Position.X - anchorX);
        int winY = (int)MathF.Round(_mascot.Position.Y - anchorY);

        float height = _mascot.HeightPx(dpi);
        float groundCanvasY = _world.GroundY - winY;
        var feet = new SKPoint(_mascot.Position.X - winX, _mascot.Position.Y - winY);
        var windowTopLeft = new Vector2(winX, winY);

        // During a corner hop the climb drives the rotation directly (for the 360° flip);
        // otherwise ease toward the resting orientation for the current surface.
        if (_mascot.RenderAngleOverride is float forced)
        {
            _renderSurfaceAngle = forced;
        }
        else
        {
            _renderSurfaceAngle = MathUtil.DampAngle(_renderSurfaceAngle, _mascot.SurfaceAngle, 8f, _time.Delta);
        }
        bool rotated = MathF.Abs(_renderSurfaceAngle) > 1e-3f;

        // The "living drag" free-body tilt (grab/throw spin) pivots about the BODY CENTRE
        // so the rotated body always stays inside the 480px canvas (pivoting about the
        // off-centre grab point would swing parts of the body out of frame and clip them).
        // The "hang from where you grabbed" feel comes from the tilt itself plus the feet
        // position, not from moving the pivot. It's a separate channel from the surface
        // rotation; the two never both fire (a drag forces Surface = Floor → angle eases to 0).
        bool dragRotated = MathF.Abs(_mascot.BodyAngle) > 1e-3f;
        var bodyPivot = new SKPoint(feet.X, feet.Y - (0.46f * height));

        _renderer.Render(_window.Handle, winX, winY, 255, canvas2D =>
        {
            if (rotated)
            {
                canvas2D.Save();
                canvas2D.Translate(feet.X, feet.Y);
                canvas2D.RotateRadians(_renderSurfaceAngle); // pivot about the contact point
                canvas2D.Translate(-feet.X, -feet.Y);
            }

            if (dragRotated)
            {
                canvas2D.Save();
                canvas2D.Translate(bodyPivot.X, bodyPivot.Y);
                canvas2D.RotateRadians(_mascot.BodyAngle); // pivot about the body centre
                canvas2D.Translate(-bodyPivot.X, -bodyPivot.Y);
            }

            _artist.Draw(canvas2D, feet, groundCanvasY, height, _mascot.Facing,
                _mascot.SquashX, _mascot.SquashY, _animator.Current, _skins.Current.Palette, dpi);

            if (dragRotated)
            {
                canvas2D.Restore();
            }

            if (rotated)
            {
                canvas2D.Restore();
            }

            // Particles live in world space, so they are drawn after the body un-rotated.
            _particleRenderer.Draw(canvas2D, _particles.Active, windowTopLeft, dpi);

            // Battery + speech bubble float just past the head, always screen-upright.
            DrawUsageHud(canvas2D, feet, height, dpi, canvas);
        });
    }

    private void DrawUsageHud(SKCanvas canvas2D, SKPoint feet, float height, float dpi, int canvasSize)
    {
        UsageSnapshot u = _usage.Current;
        // The battery only shows with a live Claude session AND the toggle on.
        bool showBattery = S.ShowBattery && u.Available && u.HasActiveSession;
        // Speech bubbles (battery comments AND world-data lines) show whenever either
        // feature is on — they must NOT depend on the battery being visible.
        bool bubbleActive = !string.IsNullOrEmpty(_bubble) && BubbleAlpha() > 0f;

        if (!showBattery && !bubbleActive)
        {
            return;
        }

        // The BATTERY tracks the body's "up" direction so it rides onto walls/ceilings
        // alongside the crab (headroom is per-skin to clear tall hats).
        float ang = _renderSurfaceAngle;
        float headroom = _skins.Current.Palette.HudHeadroom;
        var up = new SKPoint(MathF.Sin(ang), -MathF.Cos(ang));
        var head = new SKPoint(feet.X + (up.X * headroom * height), feet.Y + (up.Y * headroom * height));

        if (showBattery)
        {
            float pulse = u.Remaining < 0.22f ? (0.5f + (0.5f * MathF.Sin((float)_time.Total * 6f))) : 0f;
            _hud.DrawBattery(canvas2D, head, dpi, u.Remaining, _renewedFlash > 0f, pulse);
        }

        if (bubbleActive)
        {
            // The bubble (drawn screen-upright, growing UPWARD from its tail) must sit
            // above the body IN SCREEN SPACE regardless of orientation — otherwise, when
            // the crab clings sideways or upside-down, the up-vector anchor lands beside/
            // under the body and the bubble overlaps it. So anchor the tail at the body's
            // topmost screen point: the higher of the feet and the rotated head, minus a
            // clearance roughly equal to the body's on-screen half-extent.
            float clearance = (height * 0.6f) + (15f * dpi);
            float topY = MathF.Min(feet.Y, head.Y) - clearance;
            float anchorX = MathUtil.Clamp((feet.X + head.X) * 0.5f, 0f, canvasSize);
            var tail = new SKPoint(anchorX, topY);
            _hud.DrawBubble(canvas2D, tail, dpi, _bubble, BubbleAlpha(), canvasSize);
        }
    }

    // ===================================================================
    //  Input handlers
    // ===================================================================

    private void OnLeftDown(int x, int y)
    {
        _leftPressed = true;
        _dragging = false;
        _pressScreen = new Vector2(x, y);
        _grabOffset = _mascot.Position - _pressScreen;

        // Remember WHERE on the body we took hold, in body-local design units (un-rotated
        // and DPI-normalised), so the grab spring can hang the crab from that exact point
        // — a corner, a leg, the head — instead of always from the centre.
        Vector2 worldOffset = _pressScreen - _mascot.Position;        // feet → grab point (world)
        Vector2 local = worldOffset.Rotate(-_mascot.BodyAngle);       // undo the current tilt
        _mascot.GrabLocalOffset = local / MathF.Max(0.5f, _window.DpiScale);

        _emotion.Nudge(Mood.Happy, 0.5f, 0.6f); // a click makes it smile
    }

    private void OnLeftUp(int x, int y)
    {
        if (!_leftPressed)
        {
            return;
        }

        _leftPressed = false;

        if (_dragging)
        {
            _dragging = false;
            _resistTimer = 0f;
            _animator.SetDragSpeed(0f);

            // Throw with the body's OWN smoothed velocity (built up by the follow in
            // StepDrag), not the raw, noisy cursor velocity — that's stable and already
            // clamped, so a flick throws cleanly instead of rocketing off-screen.
            Vector2 throwVel = _mascot.Velocity;

            // On a really fast fling it occasionally clings to the cursor for a panicky
            // beat — wide eyes, legs stretched toward the mouse — before it loses grip and
            // flies off. Otherwise it's thrown straight away.
            if (throwVel.Length > 1500f && _rng.Chance(0.4f))
            {
                _panicTimer = 0.18f;
                _panicThrowVel = throwVel;
                _mascot.Animation = AnimationState.Surprised;
                _emotion.Nudge(Mood.Surprised, 1f, 0.6f);
                // Keep it grabbed (frozen on the spring) until the panic beat elapses.
                return;
            }

            ReleaseThrow(throwVel);
            return;
        }

        // Not a drag: defer the "open Claude" action so a double-click can cancel it.
        _pendingClick = true;
        _pendingClickTime = _time.Total;
    }

    /// <summary>Lets go of the grab and flings the mascot, keeping its built-up spin.</summary>
    private void ReleaseThrow(Vector2 releaseVelocity)
    {
        PhysicsSystem.Throw(_mascot, releaseVelocity);
        if (releaseVelocity.Length > 600f)
        {
            _particles.EmitDust(new Vector2(_mascot.Position.X, _world.GroundY));
        }
    }

    // ===================================================================
    //  "Living drag" — dizziness, helicopter, panic-fling, personality
    // ===================================================================

    private static readonly string[] AbuseLines =
        { "...", "¿Otra vez?", "¿En serio?", "Ya basta...", "Me mareo...", "Uff, basta", "Pará un poco" };

    private void UpdateDragReactions(float dt)
    {
        // --- Panic cling: a beat of holding the cursor before a fast fling lets go. ---
        if (_panicTimer > 0f)
        {
            _panicTimer -= dt;
            if (_panicTimer <= 0f)
            {
                ReleaseThrow(_panicThrowVel);
            }
        }

        _refuseTimer = MathF.Max(0f, _refuseTimer - dt);
        _resistTimer = MathF.Max(0f, _resistTimer - dt);
        _impactCooldown = MathF.Max(0f, _impactCooldown - dt);
        _abuseMeter = MathF.Max(0f, _abuseMeter - (dt * 0.18f)); // slowly forgives

        // --- Helicopter: sustained fast spin makes the eyes spiral and banks up dizziness. ---
        if (MathF.Abs(_mascot.AngularVelocity) > EngineConstants.HelicopterAngularVel)
        {
            _helicopterTimer += dt;
            _mascot.Dizziness = MathUtil.Clamp01(_mascot.Dizziness + (dt * 0.6f));
        }
        else
        {
            _helicopterTimer = MathF.Max(0f, _helicopterTimer - dt);
        }

        // --- Dizziness from any fast spin (dragged or flung), recovering otherwise. ---
        float spin = MathF.Abs(_mascot.AngularVelocity);
        if (spin > EngineConstants.AngularRestThreshold)
        {
            _mascot.Dizziness = MathUtil.Clamp01(
                _mascot.Dizziness + (dt * EngineConstants.DizzySpinPerSecond * (spin / EngineConstants.DragMaxAngularVel)));
        }

        // Only recover once on the ground, settled, and not being whirled around.
        if (!_dragging && _mascot.OnGround && spin < EngineConstants.AngularRestThreshold)
        {
            _mascot.Dizziness = MathF.Max(0f, _mascot.Dizziness - (dt * EngineConstants.DizzyRecoveryPerSecond));
        }

        // --- Trigger / clear the dizzy reaction as the meter crosses its threshold. ---
        if (!_dizzyShowing && _mascot.Dizziness >= EngineConstants.DizzyTriggerThreshold
            && _mascot.OnGround && !_dragging && _panicTimer <= 0f)
        {
            _dizzyShowing = true;
            Trigger("dizzy");
            _particles.EmitStars(HeadWorld(), 8);
        }
        else if (_dizzyShowing && (_mascot.Dizziness < 0.2f || !_behavior.IsRunning("dizzy")))
        {
            _dizzyShowing = false;
        }
    }

    /// <summary>
    /// Every collision (floor/wall/ceiling) lands here with its speed. The speed band
    /// picks the reaction — a tap blinks, a slam pancakes — and rough handling builds the
    /// "abuse" meter that triggers a grumpy personality response.
    /// </summary>
    private void OnImpact(ImpactEvent e)
    {
        float speed = e.Speed;

        if (speed < EngineConstants.ImpactTinySpeed)
        {
            // Barely a bump: a tiny squash + a startled blink, nothing more.
            _mascot.Squash(1.06f, 0.95f);
            return;
        }

        // Debounce: a fast bounce can re-hit the same wall several frames running. Without
        // this each re-hit would emit a fresh particle burst → the pool fills and the
        // (linear) "recycle oldest" search runs hundreds of times a frame → a visible lag
        // spike. One reaction per bounce keeps it cheap and reads better anyway.
        if (_impactCooldown > 0f)
        {
            return;
        }

        _impactCooldown = EngineConstants.ImpactReactionCooldown;
        Vector2 head = HeadWorld();

        if (speed < EngineConstants.ImpactMediumSpeed)
        {
            // A noticeable knock: a few stars, a small recoil and a surprised flash.
            _particles.EmitStars(head, 4);
            RecoilFrom(e.Surface, 0.18f);
            _emotion.Nudge(Mood.Surprised, 0.5f, 0.8f);
            _mascot.Dizziness = MathUtil.Clamp01(_mascot.Dizziness + 0.12f);
            return;
        }

        if (speed < EngineConstants.ImpactHeavySpeed)
        {
            // A real whack: flattened squash, dust, recoil, and a chunk of dizziness. It
            // rebounds instantly (the squash sells the hit) — no freeze, which read as lag.
            _particles.EmitStars(head, 7);
            _particles.EmitDust(new Vector2(e.At.X, _world.GroundY), 5);
            ApplyImpactSquash(e.Surface, 0.32f);
            RecoilFrom(e.Surface, 0.28f);
            _emotion.Nudge(Mood.Surprised, 0.8f, 1f);
            _mascot.Dizziness = MathUtil.Clamp01(_mascot.Dizziness + EngineConstants.DizzyImpactHeavyBoost);
            BumpAbuse(0.35f);
            return;
        }

        // Extreme slam: a dramatic pancake, "birds" (stars + magic) around the head, and a
        // guaranteed bout of dizziness — but it still bounces off at once (no freeze-frame).
        _particles.EmitStars(head, 12);
        _particles.EmitMagic(head, 8);
        _particles.EmitDust(new Vector2(e.At.X, _world.GroundY), 9);
        ApplyImpactSquash(e.Surface, 0.55f);
        RecoilFrom(e.Surface, 0.35f);
        _emotion.Nudge(Mood.Scared, 1f, 1.4f);
        _mascot.Dizziness = 1f;
        BumpAbuse(0.6f);
    }

    /// <summary>Pushes the mascot back off the surface it just hit (a cartoon recoil).</summary>
    private void RecoilFrom(ImpactSurface surface, float fraction)
    {
        Vector2 v = _mascot.Velocity;
        switch (surface)
        {
            case ImpactSurface.LeftWall: v = v.WithX(MathF.Abs(v.X) * fraction + 40f); break;
            case ImpactSurface.RightWall: v = v.WithX(-MathF.Abs(v.X) * fraction - 40f); break;
            case ImpactSurface.Ceiling: v = v.WithY(MathF.Abs(v.Y) * fraction + 40f); break;
            default: break; // floor recoil is handled by the existing bounce
        }

        _mascot.Velocity = v;
    }

    /// <summary>Squashes the body against the surface it hit (flat on the floor, thin on a wall).</summary>
    private void ApplyImpactSquash(ImpactSurface surface, float amount)
    {
        if (surface is ImpactSurface.LeftWall or ImpactSurface.RightWall)
        {
            _mascot.Squash(1f - amount, 1f + (amount * 0.7f)); // squeezed thin sideways
        }
        else
        {
            _mascot.Squash(1f + amount, 1f - amount);          // flattened (pancake)
        }
    }

    /// <summary>
    /// Records rough handling. When it boils over, the crab shows some attitude: a grumpy
    /// face + Spanish bubble, and sometimes a flat refusal to move for a couple of seconds.
    /// </summary>
    private void BumpAbuse(float amount)
    {
        _abuseMeter += amount;
        if (_abuseMeter < 1f)
        {
            return;
        }

        _abuseMeter = 0f;

        // Pick a line that isn't the one we used last time.
        int line;
        do
        {
            line = _rng.Range(0, AbuseLines.Length);
        }
        while (AbuseLines.Length > 1 && line == _lastPersonalityLine);
        _lastPersonalityLine = line;

        ShowBubble(AbuseLines[line], 2.6f);
        _emotion.Nudge(_rng.Chance(0.5f) ? Mood.Sad : Mood.Scared, 0.8f, 2f);

        // Sometimes it sulks: plants itself and refuses to be steered for a beat.
        if (_rng.Chance(0.45f))
        {
            _refuseTimer = _rng.Range(1.4f, 2.4f);
            _mascot.Velocity = Vector2.Zero;
            _mascot.Animation = AnimationState.Sad;
        }
    }

    private void OnDoubleClick(int x, int y)
    {
        _pendingClick = false; // a double-click is a pet, never an open
        Pet();
    }

    private void OnRightUp(int x, int y)
    {
        // The menu's modal loop blocks RunLoop, so a timer keeps frames ticking while
        // it's open — otherwise the mascot freezes until the menu closes.
        _window.BeginModalTicks();
        MenuSelection selection;
        try
        {
            selection = _menu.Show(_window.Handle, x, y, BuildMenuState());
        }
        finally
        {
            _window.EndModalTicks();
        }

        HandleMenu(selection);
    }

    private void OnDisplayChanged()
    {
        _world.Refresh();
        _world.SetDpiScale(_window.DpiScale);
    }

    private static bool IsLeftMouseButtonDown() =>
        (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LBUTTON) & 0x8000) != 0;

    private void UpdateDrag(float dpi)
    {
        Vector2 cur = _cursor.Position;

        if (!_dragging && Vector2.Distance(cur, _pressScreen) > EngineConstants.DragThreshold * dpi)
        {
            _dragging = true;
            _mascot.BeingDragged = true;
            _mascot.Surface = Surface.Floor; // peeled off the wall when grabbed
            _mascot.RenderAngleOverride = null;
            _pendingClick = false;
            _emotion.Nudge(Mood.Surprised, 0.7f);
        }

        if (!_dragging)
        {
            return;
        }

        // The physics layer now owns the motion: it eases the GRABBED point toward the
        // cursor (stable, no gluing) and derives the body's own velocity for the throw.
        _mascot.Animation = AnimationState.Dragged;

        // Drag speed (0..1) drives how furiously the legs paddle in the air.
        float dragNorm = MathUtil.Clamp01(_cursor.Speed / (1400f * dpi));
        _animator.SetDragSpeed(dragNorm);

        // Playful edge resistance: as it's hauled toward a monitor edge it sometimes
        // braces and refuses for a beat (the spring target is held back from the edge so
        // it dramatically stretches), then yields. Randomised, re-armed when it leaves.
        float half = _mascot.HalfWidthPx(dpi);
        float edgePad = half + (40f * dpi);
        bool nearEdge = cur.X < _world.LeftWall + edgePad || cur.X > _world.RightWall - edgePad
                     || cur.Y < _world.CeilingY + edgePad;

        if (_resistTimer > 0f)
        {
            // Brace: pin the spring target just shy of the edge so the body stretches taut.
            Vector2 braced = new(
                MathUtil.Clamp(cur.X, _world.LeftWall + edgePad, _world.RightWall - edgePad),
                MathF.Max(cur.Y, _world.CeilingY + edgePad));
            _physics.DragTarget = braced;
            return;
        }

        if (nearEdge && _resistArmed && _rng.Chance(EngineConstants.EdgeResistChance))
        {
            _resistArmed = false;
            _resistTimer = _rng.Range(0.7f, 1.1f);
            _emotion.Nudge(Mood.Scared, 0.5f, 1f);
        }
        else if (!nearEdge)
        {
            _resistArmed = true; // moved away from the edge → can resist again next time
        }

        _physics.DragTarget = cur;
    }

    // ===================================================================
    //  Interactions
    // ===================================================================

    private void UpdateTypingReaction()
    {
        if (!_keyboard.IsTyping)
        {
            return;
        }

        // Perk up and "type along" while you work, but never stomp on a short, important
        // reaction (petting, a scare, celebrating Claude…) or an acrobatic mid-air move.
        bool canSwitch = _mascot.OnGround && !TypingNonInterruptible.Contains(_behavior.Current.Id);
        if (canSwitch && !_behavior.IsRunning("type-along"))
        {
            Trigger("type-along");
        }

        // A few little notes drift up every so often as it watches you.
        _typeNoteAcc += _keyboard.NewKeystrokes;
        if (_typeNoteAcc >= 8)
        {
            _typeNoteAcc = 0;
            _particles.EmitSparkles(HeadWorld(), 2);
        }
    }

    // ---- Session "battery" reactions ------------------------------------

    private static readonly string[] RenewedPhrases =
        { "¡Sesión renovada!", "¡Recargado al 100%!", "¡Energía nueva, vamos!" };
    private static readonly string[] FullPhrases =
        { "¡A full de energía!", "¡Listo para programar!", "Batería llena :)" };
    private static readonly string[] MidPhrases =
        { "Rindiendo bien...", "Vamos a buen ritmo.", "Media sesión, todo ok." };
    private static readonly string[] LowPhrases =
        { "Uf, me voy cansando...", "Cuidá los tokens.", "Queda poca batería." };
    private static readonly string[] CriticalPhrases =
        { "Casi sin batería...", "Necesito recargar.", "Modo ahorro :(" };

    private void UpdateUsageReactions(float dt)
    {
        if (_bubbleElapsed < _bubbleRemaining)
        {
            _bubbleElapsed += dt;
        }

        _renewedFlash = MathF.Max(0f, _renewedFlash - dt);

        if (!S.ShowBattery)
        {
            return;
        }

        UsageSnapshot u = _usage.Current;
        if (!u.Available)
        {
            return;
        }

        int bucket = ChargeBucket(u.Remaining);

        if (!_usageInitialised)
        {
            // Seed baselines on the first real read so we don't "celebrate" at launch.
            _usageInitialised = true;
            _lastWindowId = u.WindowId;
            _lastChargeBucket = bucket;
            return;
        }

        bool canReact = !_photoMode && !_dragging;

        // A brand-new 5-hour window started → recharged! Make a fuss.
        if (u.WindowId != _lastWindowId)
        {
            _lastWindowId = u.WindowId;
            _lastChargeBucket = bucket;
            _renewedFlash = 3.5f;
            ShowBubble(Pick(RenewedPhrases), 4.5f);
            if (canReact)
            {
                Trigger("celebrate");
                Vector2 head = HeadWorld();
                _particles.EmitConfetti(head);
                _particles.EmitStars(head, 12);
                _particles.EmitMagic(head, 14);
                _audio.Play("celebrate");
                _emotion.Nudge(Mood.Excited, 1f, 2.5f);
            }

            return;
        }

        // No live session → no battery, so stay quiet about it.
        if (!u.HasActiveSession)
        {
            _lastChargeBucket = bucket;
            return;
        }

        // Crossed into a different charge level → comment on it.
        if (bucket != _lastChargeBucket)
        {
            _lastChargeBucket = bucket;
            ShowBubble(BucketPhrase(bucket, u), 4f);
        }

        // A draining battery makes the crab visibly sleepier/lazier.
        _usageMoodAcc -= dt;
        if (_usageMoodAcc <= 0f)
        {
            _usageMoodAcc = 8f;
            if (u.Remaining < 0.18f)
            {
                _emotion.Nudge(Mood.Sleepy, 0.5f);
            }
            else if (u.Remaining < 0.4f)
            {
                _emotion.Nudge(Mood.Lazy, 0.3f);
            }
        }

        // The odd spontaneous remark when nothing else is showing.
        _phraseCooldown -= dt;
        if (_phraseCooldown <= 0f)
        {
            _phraseCooldown = _rng.Range(70f, 130f);
            if (_bubbleElapsed >= _bubbleRemaining)
            {
                ShowBubble(BucketPhrase(bucket, u), 3.5f);
            }
        }
    }

    private static int ChargeBucket(float remaining) =>
        remaining > 0.66f ? 3 : (remaining > 0.33f ? 2 : (remaining > 0.12f ? 1 : 0));

    private string BucketPhrase(int bucket, UsageSnapshot u)
    {
        // When low, sometimes show the actual reset countdown instead of a mood line.
        if (bucket <= 1 && u.TimeUntilReset > TimeSpan.Zero && _rng.Chance(0.5f))
        {
            int mins = (int)Math.Ceiling(u.TimeUntilReset.TotalMinutes);
            return mins >= 60 ? $"Vuelvo en {mins / 60}h {mins % 60}m" : $"Vuelvo en {mins}m";
        }

        return Pick(bucket switch
        {
            3 => FullPhrases,
            2 => MidPhrases,
            1 => LowPhrases,
            _ => CriticalPhrases,
        });
    }

    private string Pick(string[] options) => options[_rng.Range(0, options.Length)];

    private void ShowBubble(string text, float duration)
    {
        _bubble = text;
        _bubbleRemaining = duration;
        _bubbleElapsed = 0f;
    }

    // ===================================================================
    //  "World data" reactions — weather, dollar, crypto, fun facts
    // ===================================================================

    private static readonly string[] FunFacts =
    {
        "¿Sabías? Los pulpos tienen tres corazones.",
        "Un día en Venus dura más que su año.",
        "La miel nunca se echa a perder.",
        "Los flamencos son rosados por lo que comen.",
        "El 80% del cerebro es agua.",
        "Las nutrias se toman de la mano al dormir.",
        "Un rayo es más caliente que la superficie del Sol.",
        "Los gatos no sienten el sabor dulce.",
    };

    private void UpdateWorldDataReactions(float dt)
    {
        if (!S.WorldData)
        {
            return;
        }

        bool canReact = !_photoMode && !_dragging && _mascot.OnGround;
        WorldDataSnapshot w = _worldData.Current;

        // --- Holiday: celebrate once when today's holiday first becomes known. Wait
        //     until the crab is on the ground (not climbing/airborne) so the confetti +
        //     jump actually land; only mark it done once we truly celebrate. ---
        if (!string.IsNullOrEmpty(w.TodayHoliday) && w.TodayHoliday != _celebratedHoliday && canReact)
        {
            _celebratedHoliday = w.TodayHoliday;
            ShowBubble($"¡Feliz {w.TodayHoliday}!", 6f);
            Trigger("celebrate");
            _particles.EmitConfetti(HeadWorld());
            _particles.EmitStars(HeadWorld(), 10);
            _audio.Play("celebrate");
            _emotion.Nudge(Mood.Excited, 1f, 2.5f);
            return; // don't pile a weather/data bubble on top of the celebration
        }

        // --- Weather: react when the weather mood changes, then occasionally ---
        _weatherReactCooldown -= dt;
        if (w.HasWeather && canReact && _weatherReactCooldown <= 0f)
        {
            bool changed = w.Weather != _lastWeather;
            // React promptly on a change, otherwise just every few minutes.
            if (changed || _weatherReactCooldown <= -1f)
            {
                _lastWeather = w.Weather;
                _weatherReactCooldown = _rng.Range(90f, 150f); // re-react every ~2 min
                ReactToWeather(w);
            }
        }

        // --- Fun data bubbles: rotate facts / dollar / crypto / greeting ------
        _factCooldown -= dt;
        if (_factCooldown <= 0f)
        {
            _factCooldown = _rng.Range(35f, 55f); // a fresh line roughly every ~45s
            if (_bubbleElapsed >= _bubbleRemaining) // don't talk over another bubble
            {
                ShowBubble(NextDataLine(w), 5f);
            }
        }
    }

    private void ReactToWeather(WorldDataSnapshot w)
    {
        int t = (int)Math.Round(w.TemperatureC);
        switch (w.Weather)
        {
            case WeatherMood.Cold:
                Trigger("shiver");
                ShowBubble($"Brrr... {t}°, ¡qué frío!", 5f);
                _emotion.Nudge(Mood.Sad, 0.4f, 2f);
                break;
            case WeatherMood.Hot:
                Trigger("too-hot");
                ShowBubble($"Uf, {t}°... me derrito.", 5f);
                _emotion.Nudge(Mood.Lazy, 0.5f, 2f);
                break;
            case WeatherMood.Rain:
                Trigger("rainy");
                ShowBubble("Está lloviendo afuera...", 5f);
                break;
            case WeatherMood.Snow:
                Trigger("shiver");
                _particles.EmitSparkles(HeadWorld(), 12);
                ShowBubble("¡Está nevando!", 5f);
                break;
            case WeatherMood.Cool:
                Trigger("shiver");
                ShowBubble($"Fresquito, {t}°...", 5f);
                break;
            case WeatherMood.Warm:
                ShowBubble($"Lindo día, {t}°.", 4f);
                _emotion.Nudge(Mood.Happy, 0.3f);
                break;
            default:
                ShowBubble($"Afuera hay {t}°.", 4f);
                break;
        }
    }

    private string NextDataLine(WorldDataSnapshot w)
    {
        // Rotate through the available data sources so it's varied.
        for (int attempt = 0; attempt < 4; attempt++)
        {
            switch (_factIndex++ % 4)
            {
                case 0:
                    return GreetingLine();
                case 1:
                    if (w.HasDollar) return $"Dólar blue: ${w.DollarBlue}";
                    break;
                case 2:
                    if (w.HasCrypto) return $"Bitcoin: U$D {w.BtcUsd:N0}";
                    break;
                default:
                    return Pick(FunFacts);
            }
        }

        return Pick(FunFacts);
    }

    private string GreetingLine()
    {
        int hour = DateTime.Now.Hour;
        string day = DateTime.Now.DayOfWeek == DayOfWeek.Friday ? " ¡Feliz viernes!" : string.Empty;
        string part = hour switch
        {
            < 6 => "¿Tan tarde despierto?",
            < 12 => "¡Buen día!",
            < 19 => "¡Buenas tardes!",
            _ => "¡Buenas noches!",
        };
        return part + day;
    }

    private float BubbleAlpha()
    {
        if (string.IsNullOrEmpty(_bubble) || _bubbleElapsed >= _bubbleRemaining)
        {
            return 0f;
        }

        const float fadeIn = 0.25f;
        const float fadeOut = 0.5f;
        if (_bubbleElapsed < fadeIn)
        {
            return Easing.OutQuad(_bubbleElapsed / fadeIn);
        }

        float remaining = _bubbleRemaining - _bubbleElapsed;
        return remaining < fadeOut ? Easing.OutQuad(remaining / fadeOut) : 1f;
    }

    private void Pet()
    {
        Trigger("pet");
        _emotion.AddHappiness(EngineConstants.HappinessPerPet);
        _emotion.Nudge(Mood.Happy, 0.95f, 1.6f);
        _particles.EmitHearts(HeadWorld());
        _audio.Play("pet");
        S.Stats.PetCount++;
        S.Happiness = _emotion.Happiness;

        if (_emotion.RareContentUnlocked && _rng.Chance(0.25f))
        {
            _particles.EmitSparkles(HeadWorld());
        }
    }

    private void OpenClaude()
    {
        _claude.LaunchOrFocus();
        S.Stats.ClaudeOpenCount++;
        CelebrateClaude();
        _claudeWasRunning = true;
        PersistAndSave();
    }

    private void CelebrateClaude()
    {
        Trigger("claude-celebrate");
        _particles.EmitConfetti(HeadWorld());
        _audio.Play("celebrate");
        _emotion.Nudge(Mood.Excited, 1f, 2.5f);
    }

    private void PollClaude()
    {
        bool running = _claude.IsClaudeRunning();
        if (running && !_claudeWasRunning)
        {
            CelebrateClaude();
        }
        else if (!running && _claudeWasRunning)
        {
            Trigger("wave-goodbye");
            _emotion.Nudge(Mood.Sad, 0.6f, 2f);
        }

        _claudeWasRunning = running;
    }

    // ===================================================================
    //  Context menu
    // ===================================================================

    private MenuState BuildMenuState()
    {
        _skins.Discover(_settings.SettingsDirectory); // refresh so newly-dropped skins appear
        var skins = _skins.Skins
            .Select(s => new SkinMenuItem(s.Id, s.Name, s.Id == _skins.Current.Id))
            .ToList();

        return new MenuState
        {
            AlwaysOnTop = S.AlwaysOnTop,
            Muted = S.Muted,
            LaunchOnStartup = S.LaunchOnStartup,
            PhotoMode = _photoMode,
            ShowBattery = S.ShowBattery,
            WorldData = S.WorldData,
            AnimationSpeed = S.AnimationSpeed,
            Volume = S.Volume,
            BehaviorFrequency = S.BehaviorFrequency,
            Skins = skins,
        };
    }

    private void HandleMenu(MenuSelection sel)
    {
        switch (sel.Command)
        {
            case MenuCommand.OpenClaude:
                OpenClaude();
                break;
            case MenuCommand.ChangeSkin when sel.SkinId is not null:
                _skins.SetCurrent(sel.SkinId);
                S.CurrentSkin = sel.SkinId;
                S.Stats.SkinsUsed.Add(sel.SkinId);
                _particles.EmitSparkles(HeadWorld());
                break;
            case MenuCommand.AnimationSpeed:
                S.AnimationSpeed = sel.Value;
                break;
            case MenuCommand.Volume:
                S.Volume = sel.Value;
                S.Muted = sel.Value <= 0.001f;
                break;
            case MenuCommand.ToggleMute:
                S.Muted = !S.Muted;
                break;
            case MenuCommand.BehaviorFrequency:
                S.BehaviorFrequency = sel.Value;
                break;
            case MenuCommand.ToggleAlwaysOnTop:
                S.AlwaysOnTop = !S.AlwaysOnTop;
                _window.SetTopmost(S.AlwaysOnTop);
                break;
            case MenuCommand.ToggleLaunchOnStartup:
                S.LaunchOnStartup = !S.LaunchOnStartup;
                _startup.SetEnabled(S.LaunchOnStartup);
                break;
            case MenuCommand.ToggleBattery:
                S.ShowBattery = !S.ShowBattery;
                if (S.ShowBattery)
                {
                    _usage.Start(); // begins reading local logs again
                }
                else
                {
                    _usage.Stop();
                    _bubble = string.Empty; // clear any battery bubble immediately
                }

                break;
            case MenuCommand.ToggleWorldData:
                S.WorldData = !S.WorldData;
                if (S.WorldData)
                {
                    _worldData.Start(); // resume polling free public APIs
                }
                else
                {
                    _worldData.Stop();
                    _lastWeather = WeatherMood.Unknown;
                    _bubble = string.Empty;
                }

                break;
            case MenuCommand.ResetPosition:
                ResetPosition();
                break;
            case MenuCommand.PhotoMode:
                TogglePhotoMode();
                break;
            case MenuCommand.Achievements:
                ShowAchievements();
                break;
            case MenuCommand.Mods:
                OpenFolder(Path.Combine(_settings.SettingsDirectory, "Mods"));
                break;
            case MenuCommand.Settings:
                OpenFolder(_settings.SettingsDirectory);
                break;
            case MenuCommand.About:
                ShowAbout();
                break;
            case MenuCommand.Exit:
                _window.Stop();
                break;
        }

        if (sel.Command != MenuCommand.None && sel.Command != MenuCommand.Exit)
        {
            PersistAndSave();
        }
    }

    private void TogglePhotoMode()
    {
        _photoMode = !_photoMode;
        if (_photoMode)
        {
            _animator.Snap();
            ExportPhoto();
        }
    }

    private void ExportPhoto()
    {
        try
        {
            int size = _renderer.Size;
            var info = new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
            using SKSurface surface = SKSurface.Create(info);
            float dpi = _window.DpiScale;
            float anchorX = size * 0.5f;
            float anchorY = size * EngineConstants.CanvasFeetAnchor;
            _artist.Draw(surface.Canvas, new SKPoint(anchorX, anchorY), anchorY,
                _mascot.HeightPx(dpi), _mascot.Facing, _mascot.SquashX, _mascot.SquashY,
                _animator.Current, _skins.Current.Palette, dpi);

            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ClaudeBuddy");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, $"claude-buddy-{DateTime.Now:yyyyMMdd-HHmmss}.png");

            using SKImage image = surface.Snapshot();
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
            using FileStream fs = File.OpenWrite(file);
            data.SaveTo(fs);
        }
        catch
        {
            // Photo export is a nicety; never let it crash the app.
        }
    }

    private void ShowAchievements()
    {
        int unlocked = _achievements.UnlockedCount(S);
        var lines = _achievements.All.Select(a =>
        {
            bool got = S.UnlockedAchievements.Contains(a.Id);
            return $"{(got ? "★" : "☆")}  {a.Title} — {a.Description}";
        });

        string body = $"Unlocked {unlocked} / {_achievements.All.Count}\n\n" + string.Join("\n", lines);
        NativeMethods.MessageBox(_window.Handle, body, "Claude Buddy — Achievements",
            NativeMethods.MB_OK | NativeMethods.MB_ICONINFORMATION);
    }

    private void ShowAbout()
    {
        string text =
            "Claude Buddy\n" +
            "A wholesome desktop companion.\n\n" +
            "• Single click  →  open Claude\n" +
            "• Double click  →  pet\n" +
            "• Drag          →  pick up & throw\n" +
            "• Right click   →  menu\n\n" +
            "Procedurally drawn with SkiaSharp. Skins live in your Skins folder.";
        NativeMethods.MessageBox(_window.Handle, text, "About Claude Buddy",
            NativeMethods.MB_OK | NativeMethods.MB_ICONINFORMATION);
    }

    // ===================================================================
    //  Helpers
    // ===================================================================

    private void OnBehaviorStarted(BehaviorDefinition def)
    {
        S.Stats.BehaviorsSeen.Add(def.Id);
        if (def.Id is "jump" or "climb")
        {
            S.Stats.JumpCount++;
        }

        if (def.EnterParticle is ParticleKind kind)
        {
            EmitBehaviorParticle(kind);
        }

        if (def.EnterSound is { Length: > 0 } sound)
        {
            _audio.Play(sound);
        }
    }

    private void OnBehaviorClimax(BehaviorDefinition def)
    {
        // The big release of a build-up move: a layered, screen-filling burst.
        Vector2 head = HeadWorld();
        _particles.EmitMagic(head, 18);
        _particles.EmitStars(head, 12);
        _particles.EmitConfetti(head, 26);
        _audio.Play("magic");
        _emotion.Nudge(Mood.Excited, 1f, 2f);
    }

    private void EmitBehaviorParticle(ParticleKind kind)
    {
        Vector2 head = HeadWorld();
        switch (kind)
        {
            case ParticleKind.Confetti: _particles.EmitConfetti(head); break;
            case ParticleKind.Heart: _particles.EmitHearts(head); break;
            case ParticleKind.Star: _particles.EmitStars(head); break;
            case ParticleKind.Magic: _particles.EmitMagic(head); break;
            case ParticleKind.Note: _particles.EmitSparkles(head, 4); break;
            default: _particles.EmitSparkles(head); break;
        }
    }

    private void OnLanded(float impact)
    {
        if (impact > 0.25f)
        {
            _particles.EmitDust(new Vector2(_mascot.Position.X, _world.GroundY), (int)(impact * 6) + 2);
        }
    }

    private void OnAchievementUnlocked(Achievement achievement)
    {
        _particles.EmitConfetti(HeadWorld());
        _audio.Play("celebrate");
        _emotion.Nudge(Mood.Proud, 1f, 3f);
    }

    private void EvaluateAchievements()
    {
        int lifetimeDays = (int)(DateTimeOffset.UtcNow - S.FirstRunUtc).TotalDays;
        var ctx = new AchievementContext(S, _skins.Skins.Count, _catalog.Selectable.Count(), lifetimeDays);
        _achievements.Evaluate(ctx);
    }

    private void AccumulateStats(float dt)
    {
        if (_mascot.OnGround)
        {
            S.Stats.DistanceWalked += (long)(MathF.Abs(_mascot.Velocity.X) * dt);
        }

        S.Happiness = _emotion.Happiness;
    }

    private void UpdateWeather(float dt)
    {
        _weatherTimer -= dt;
        if (_weatherTimer <= 0f)
        {
            _weatherTimer = _rng.Range(45f, 95f);
            _world.Weather = S.WeatherEnabled && _rng.Chance(0.5f) ? RandomWeather() : WeatherKind.Clear;
        }

        if (!S.WeatherEnabled || _world.Weather == WeatherKind.Clear)
        {
            return;
        }

        _weatherEmitAcc -= dt;
        if (_weatherEmitAcc <= 0f)
        {
            _weatherEmitAcc = _rng.Range(0.12f, 0.35f);
            float half = _renderer.Size * 0.5f;
            float left = _mascot.Position.X - half;
            float right = _mascot.Position.X + half;
            float top = _mascot.Position.Y - (_renderer.Size * EngineConstants.CanvasFeetAnchor);
            _particles.EmitWeatherMote(_world.Weather, left, right, top);
        }
    }

    private WeatherKind RandomWeather()
    {
        ReadOnlySpan<WeatherKind> kinds = [WeatherKind.Snow, WeatherKind.Leaves, WeatherKind.Petals];
        return kinds[_rng.Range(0, kinds.Length)];
    }

    private (float X, float Y) ComputeEyeLook(float dpi)
    {
        Vector2 head = HeadWorld();
        Vector2 to = _cursor.Position - head;
        float range = EngineConstants.CursorNoticeRadius * dpi;
        float notice = MathUtil.Clamp01(1f - (to.Length / range));
        float scale = 0.4f + (0.6f * notice);
        float lx = MathUtil.Clamp(to.X / range, -1f, 1f) * scale;
        float ly = MathUtil.Clamp(to.Y / range, -1f, 1f) * scale;
        return (lx, ly);
    }

    private Vector2 HeadWorld() =>
        new(_mascot.Position.X, _mascot.Position.Y - (0.62f * _mascot.HeightPx(_window.DpiScale)));

    private void Trigger(string id)
    {
        RoutineProfile r = _routine.Evaluate();
        _behavior.Force(id, _mascot, _world, _emotion, r, _cursor.Position, S.BehaviorFrequency);
    }

    private void RestorePosition()
    {
        if (float.IsNaN(S.PositionX) || float.IsNaN(S.PositionY))
        {
            ResetPosition();
            return;
        }

        float half = _mascot.HalfWidthPx(_window.DpiScale);
        _mascot.Position = new Vector2(
            _world.ClampX(S.PositionX, half),
            MathUtil.Clamp(S.PositionY, _world.VirtualBounds.Top, _world.GroundY));
        _mascot.OnGround = _mascot.Position.Y >= _world.GroundY - 1f;
    }

    private void ResetPosition()
    {
        float cx = (_world.WorkArea.Left + _world.WorkArea.Right) * 0.5f;
        _mascot.Position = new Vector2(cx, _world.GroundY);
        _mascot.Velocity = Vector2.Zero;
        _mascot.OnGround = true;
        _mascot.Surface = Surface.Floor;
        _mascot.RenderAngleOverride = null;
        // Clear all "living drag" state so a reset always lands upright and clear-headed.
        _mascot.BodyAngle = 0f;
        _mascot.AngularVelocity = 0f;
        _mascot.Dizziness = 0f;
        _mascot.GrabLocalOffset = Vector2.Zero;
    }

    private void PersistAndSave()
    {
        S.PositionX = _mascot.Position.X;
        S.PositionY = _mascot.Position.Y;
        S.Scale = _mascot.Scale;
        S.Happiness = _emotion.Happiness;
        S.CurrentSkin = _skins.Current.Id == "classic" ? string.Empty : _skins.Current.Id;
        _settings.Save();
    }

    private void EnsureUserFolders()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(_settings.SettingsDirectory, "Skins"));
            Directory.CreateDirectory(Path.Combine(_settings.SettingsDirectory, "Mods"));
        }
        catch
        {
            // Non-fatal.
        }
    }

    private static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch
        {
            // Ignore.
        }
    }
}
