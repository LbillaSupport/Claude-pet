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
    private Vector2 _dragVelocity;
    private double _doubleClickSeconds = 0.5;

    private bool _photoMode;
    private bool _claudeWasRunning;
    private float _surpriseCd;
    private float _pollAcc;
    private float _saveAcc;
    private float _weatherTimer = 30f;
    private float _weatherEmitAcc;

    private AppSettings S => _settings.Current;

    public MascotEngine(
        ISettingsService settings, World world, Mascot mascot, Animator animator,
        PhysicsSystem physics, BehaviorController behavior, BehaviorCatalog catalog,
        EmotionState emotion, DailyRoutine routine, ParticleSystem particles,
        ParticleRenderer particleRenderer, CharacterArtist artist, CursorTracker cursor,
        SkinManager skins, ModManager mods, IClaudeLauncher claude, IStartupService startup,
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
        _physics.Landed += OnLanded;
        _achievements.Unlocked += OnAchievementUnlocked;

        _window.SetTopmost(S.AlwaysOnTop);
        _startup.SetEnabled(S.LaunchOnStartup); // keep registry in sync with settings
        _claudeWasRunning = _claude.IsClaudeRunning();
        _doubleClickSeconds = NativeMethods.GetDoubleClickTime() / 1000.0;

        // First hello.
        Trigger("greet");
    }

    public void Frame()
    {
        _time.Tick();
        float dt = _time.Delta;
        float dpi = _window.DpiScale;

        _cursor.Update(dt);
        RoutineProfile routine = _routine.Evaluate();

        // --- Interaction timing -----------------------------------------
        if (_leftPressed)
        {
            UpdateDrag(dpi);
        }

        if (_pendingClick && (_time.Total - _pendingClickTime) > _doubleClickSeconds)
        {
            _pendingClick = false;
            OpenClaude();
        }

        bool simulate = !_photoMode && !_dragging;

        // --- Startle on a fast flick of the mouse -----------------------
        _surpriseCd = MathF.Max(0f, _surpriseCd - dt);
        if (simulate && _surpriseCd <= 0f && _cursor.Speed > EngineConstants.SurpriseCursorSpeed * dpi)
        {
            _surpriseCd = 3.5f;
            Trigger("surprised");
        }

        // --- Core simulation --------------------------------------------
        if (simulate)
        {
            _behavior.Update(_mascot, _world, _emotion, routine, _cursor.Position, dt, S.BehaviorFrequency);
            _physics.Step(_mascot, _world, dt, _behavior.ControlsLocomotion);
        }

        _emotion.Update(dt, routine);
        _mascot.AnimationSpeed = MathUtil.Clamp(
            S.AnimationSpeed * MathUtil.Lerp(0.8f, 1.15f, _emotion.Energy), 0.4f, 2.4f);

        (float lookX, float lookY) = ComputeEyeLook(dpi);
        if (simulate)
        {
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

        _renderer.Render(_window.Handle, winX, winY, 255, canvas2D =>
        {
            _artist.Draw(canvas2D, feet, groundCanvasY, height, _mascot.Facing,
                _mascot.SquashX, _mascot.SquashY, _animator.Current, _skins.Current.Palette, dpi);
            _particleRenderer.Draw(canvas2D, _particles.Active, windowTopLeft, dpi);
        });
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
            PhysicsSystem.Throw(_mascot, _dragVelocity);
            if (_dragVelocity.Length > 600f)
            {
                _particles.EmitDust(new Vector2(_mascot.Position.X, _world.GroundY));
            }

            return;
        }

        // Not a drag: defer the "open Claude" action so a double-click can cancel it.
        _pendingClick = true;
        _pendingClickTime = _time.Total;
    }

    private void OnDoubleClick(int x, int y)
    {
        _pendingClick = false; // a double-click is a pet, never an open
        Pet();
    }

    private void OnRightUp(int x, int y)
    {
        MenuSelection selection = _menu.Show(_window.Handle, x, y, BuildMenuState());
        HandleMenu(selection);
    }

    private void OnDisplayChanged()
    {
        _world.Refresh();
        _world.SetDpiScale(_window.DpiScale);
    }

    private void UpdateDrag(float dpi)
    {
        Vector2 cur = _cursor.Position;

        if (!_dragging && Vector2.Distance(cur, _pressScreen) > EngineConstants.DragThreshold * dpi)
        {
            _dragging = true;
            _mascot.BeingDragged = true;
            _pendingClick = false;
            _emotion.Nudge(Mood.Surprised, 0.7f);
        }

        if (_dragging)
        {
            Vector2 p = cur + _grabOffset;
            float half = _mascot.HalfWidthPx(dpi);
            p = new Vector2(
                _world.ClampX(p.X, half),
                MathUtil.Clamp(p.Y, _world.VirtualBounds.Top + 10, _world.GroundY));
            _mascot.Position = p;
            _mascot.OnGround = false;
            _mascot.Animation = AnimationState.Dragged;
            _dragVelocity = _cursor.Velocity;
        }
    }

    // ===================================================================
    //  Interactions
    // ===================================================================

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
