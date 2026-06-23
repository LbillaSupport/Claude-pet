using ClaudeBuddy.Achievements;
using ClaudeBuddy.Animation;
using ClaudeBuddy.Behaviors;
using ClaudeBuddy.Content;
using ClaudeBuddy.Core;
using ClaudeBuddy.Emotions;
using ClaudeBuddy.Engine;
using ClaudeBuddy.Input;
using ClaudeBuddy.Mods;
using ClaudeBuddy.Particles;
using ClaudeBuddy.Physics;
using ClaudeBuddy.Rendering;
using ClaudeBuddy.Routine;
using ClaudeBuddy.Services;
using ClaudeBuddy.Skins;
using ClaudeBuddy.UI;
using Microsoft.Extensions.DependencyInjection;
using Velopack;

namespace ClaudeBuddy;

/// <summary>
/// Composition root. Builds the dependency-injection container, wires every system as
/// a singleton, brings up the native window + Skia renderer, and hands control to the
/// game loop. Single-instance guarded so only one buddy ever lives on the desktop.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // MUST be the first thing that runs: Velopack intercepts install/update/uninstall
        // hooks here and exits before the app proper starts. No-op for a plain dev run.
        VelopackApp.Build().Run();

        // QA / marketing helper: `ClaudeBuddy --render <skinId> <out.png> [sizePx] [prop]`
        // draws a single skin to a centred PNG and exits — no window, no wandering. `prop`
        // can be "thermometer" or "fan" to preview the weather props. Lets us verify a
        // skin/prop's look deterministically (the mascot never walks out of frame).
        if (args.Length >= 3 && args[0] == "--render")
        {
            int sz = args.Length >= 4 && int.TryParse(args[3], out int n) ? n : 512;
            string prop = args.Length >= 5 ? args[4] : string.Empty;
            return RenderSkinToFile(args[1], args[2], sz, prop);
        }

        // Marketing helper: `ClaudeBuddy --render-frames <skinId> <outDir> [sizePx]` renders a
        // short showcase of several animations (run through the real Animator) as numbered PNG
        // frames into <outDir> — deterministic, no window, no live screen capture. A small build
        // script stitches them into the animated GIF for the README (System.Drawing has a proven
        // GIF encoder, so we don't ship a hand-rolled one in the app).
        if (args.Length >= 3 && args[0] == "--render-frames")
        {
            int sz = args.Length >= 4 && int.TryParse(args[3], out int gn) ? gn : 320;
            return RenderFramesToDir(args[1], args[2], sz);
        }

        using var mutex = new Mutex(initiallyOwned: true, "ClaudeBuddy.SingleInstance.v1", out bool isNew);
        if (!isNew)
        {
            return 0; // a buddy is already on the desktop
        }

        try
        {
            using ServiceProvider provider = BuildContainer();

            // Settings must be loaded before any consumer reads them.
            provider.GetRequiredService<ISettingsService>().Load();

            // Quietly check GitHub Releases for a newer build (best-effort, background).
            var updater = provider.GetRequiredService<UpdateService>();
            updater.StartBackgroundCheck();

            var window = provider.GetRequiredService<LayeredWindow>();
            window.Create(EngineConstants.CanvasDesignSize);

            int canvasPx = (int)Math.Round(EngineConstants.CanvasDesignSize * window.DpiScale);
            using var renderer = new SkiaRenderer(canvasPx);

            var engine = provider.GetRequiredService<MascotEngine>();
            engine.Initialize(window, renderer);

            try
            {
                window.RunLoop(engine.Frame);
            }
            finally
            {
                updater.Stop();
                engine.Shutdown();
            }

            return 0;
        }
        catch (Exception ex)
        {
            LogCrash(ex);
            return 1;
        }
    }

    private static ServiceProvider BuildContainer()
    {
        var services = new ServiceCollection();

        // Shared infrastructure.
        services.AddSingleton<Rng>();
        services.AddSingleton<GameTime>();
        services.AddSingleton<Localization>();
        services.AddSingleton<Strings>();
        services.AddSingleton<Phrasebook>();

        // World / entity / simulation systems.
        services.AddSingleton<World>();
        services.AddSingleton<Mascot>();
        services.AddSingleton<EmotionState>();
        services.AddSingleton<DailyRoutine>();
        services.AddSingleton<PhysicsSystem>();
        services.AddSingleton<Animator>();
        services.AddSingleton<ParticleSystem>();
        services.AddSingleton<CursorTracker>();
        services.AddSingleton<KeyboardActivityTracker>();

        // Behaviour.
        services.AddSingleton<BehaviorCatalog>();
        services.AddSingleton<BehaviorSelector>();
        services.AddSingleton<BehaviorController>();

        // Rendering.
        services.AddSingleton<CharacterArtist>();
        services.AddSingleton<ParticleRenderer>();
        services.AddSingleton<UsageHudRenderer>();
        services.AddSingleton<LayeredWindow>();

        // Content.
        services.AddSingleton<SkinManager>();
        services.AddSingleton<ModManager>();
        services.AddSingleton<AchievementService>();

        // Services.
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<SessionUsageService>();
        services.AddSingleton<WorldDataService>();
        services.AddSingleton<DesktopProbe>();
        services.AddSingleton<SystemAudioProbe>();
        services.AddSingleton<UpdateService>();
        services.AddSingleton<IClaudeLauncher, ClaudeLauncher>();
        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<IAudioService, AudioService>();

        // UI + orchestration.
        services.AddSingleton<ContextMenu>();
        services.AddSingleton<MascotEngine>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Renders one skin to a centred PNG and returns 0/1. Used by the `--render` CLI mode
    /// to verify a skin's appearance without launching the live (wandering) mascot.
    /// </summary>
    private static int RenderSkinToFile(string skinId, string outPath, int size, string prop = "")
    {
        try
        {
            var skins = new SkinManager();
            skins.Discover(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            skins.SetCurrent(skinId);

            var artist = new CharacterArtist();
            Pose pose;

            // If `prop` names an AnimationState, run the real Animator for ~1s so we capture
            // the exact live pose (e.g. Shiver/Hot). Otherwise just draw a neutral pose,
            // optionally forcing a weather prop on.
            if (Enum.TryParse<AnimationState>(prop, ignoreCase: true, out AnimationState state))
            {
                var animator = new Animator(new Core.Rng());
                var mascot = new ClaudeBuddy.Engine.Mascot { Animation = state };
                var emotion = new ClaudeBuddy.Emotions.EmotionState();
                for (int i = 0; i < 90; i++) // ~1.5s at 60fps to settle the blend
                {
                    animator.Update(mascot, emotion, 1f / 60f, lookX: 0f, lookY: 0f);
                }

                pose = animator.Current;
            }
            else if (Enum.TryParse<HeldPropKind>(prop, ignoreCase: true, out HeldPropKind held) && held != HeldPropKind.None)
            {
                // Preview a held imaginary prop on a neutral, presenting pose.
                pose = new Pose { MouthCurve = 0.55f, MouthOpen = 0.12f, ArmRight = 1.7f, HeldProp = held, HeldPropAmount = 1f };
            }
            else
            {
                pose = new Pose { MouthCurve = 0.7f, MouthOpen = 0.2f };
                if (prop == "thermometer") { pose.ThermometerProp = 1f; pose.ArmLeft = 1.7f; pose.ArmRight = 1.7f; }
                else if (prop == "fan") { pose.FanProp = 1f; pose.ArmRight = 1.6f; }
            }

            var info = new SkiaSharp.SKImageInfo(size, size, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
            using SkiaSharp.SKSurface surface = SkiaSharp.SKSurface.Create(info);
            float anchorX = size * 0.5f;
            float anchorY = size * 0.62f;            // feet a little below centre
            float height = size * 0.42f;             // leave headroom for hats/buses
            artist.Draw(surface.Canvas, new SkiaSharp.SKPoint(anchorX, anchorY), anchorY,
                height, Facing.Right, 1f, 1f, pose, skins.Current.Palette, 1f);

            // QA: `--render <skin> out.png <sz> bubble` also draws a battery + a deliberately
            // long speech bubble so the word-wrap / no-clip / upright-battery fixes can be
            // verified deterministically.
            if (prop == "bubble" || prop == "bubbleedge")
            {
                var hud = new UsageHudRenderer();
                float headroom = skins.Current.Palette.HudHeadroom;
                float by = anchorY - (headroom * height);

                // "bubbleedge" simulates the crab hugging the RIGHT screen edge: only the left
                // ~45% of the window is on-screen, so the HUD must stay inside that slice.
                float visLeft = 0f;
                float visRight = prop == "bubbleedge" ? size * 0.45f : size;
                float bx = MathF.Min(anchorX, visRight - (27f));

                hud.DrawBattery(surface.Canvas, new SkiaSharp.SKPoint(bx, by), 1f, 0.62f, charging: false, pulse: 0f);
                hud.DrawBubble(surface.Canvas, new SkiaSharp.SKPoint(bx, by - 32f), 1f,
                    "¡Buenas tardes! ¡Feliz viernes! ¿Sabías que los pulpos tienen tres corazones?", 1f, visLeft, visRight);
            }

            // QA: `--render <skin> out.png <sz> combo` draws the juggling combo counter so its
            // look (gold number + dark outline + pop) can be checked deterministically.
            if (prop == "combo")
            {
                var hud = new UsageHudRenderer();
                float headroom = skins.Current.Palette.HudHeadroom;
                float cy = MathF.Max(34f, anchorY - ((headroom + 0.25f) * height));
                hud.DrawComboCounter(surface.Canvas, new SkiaSharp.SKPoint(anchorX, cy), 1f, 5,
                    new SkiaSharp.SKColor(0xF2, 0x8A, 0x2E), 1f, 0.6f);

                // Also draw a multi-line bubble lifted above the number, mirroring the engine's
                // stacking, so the "bubble overlapping the combo number" case can be verified.
                float comboH = MathF.Min(58f, 28f + (3.5f * 5));
                float by = cy - (comboH * 0.7f) - 8f;
                hud.DrawBubble(surface.Canvas, new SkiaSharp.SKPoint(anchorX, by), 1f,
                    "¡Mi récord de malabares es x5! ¿Lo superamos juntos?", 1f, 0f, size);
            }

            // QA: `--render <skin> out.png <sz> portal` draws a Portal-style portal beside the
            // character so the clone-event art can be checked deterministically.
            if (prop == "portal")
            {
                artist.DrawPortal(surface.Canvas, anchorX + (0.42f * size), anchorY - (0.22f * size),
                    height, scale: 1f, alpha: 1f, phase: 0.3f);
            }

            using SkiaSharp.SKImage image = surface.Snapshot();
            using SkiaSharp.SKData data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            using FileStream fs = File.Create(outPath);
            data.SaveTo(fs);
            return 0;
        }
        catch (Exception ex)
        {
            LogCrash(ex);
            return 1;
        }
    }

    /// <summary>
    /// Renders a short looping showcase of several animations to an animated GIF, running the
    /// real <see cref="Animator"/> so the motion is identical to the live app. Deterministic and
    /// window-free — used to produce the README demo without any live screen capture.
    /// </summary>
    private static int RenderFramesToDir(string skinId, string outDir, int size)
    {
        try
        {
            Directory.CreateDirectory(outDir);

            var skins = new SkinManager();
            skins.Discover(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

            var artist = new CharacterArtist();
            var animator = new Animator(new Core.Rng());
            var mascot = new ClaudeBuddy.Engine.Mascot();
            var emotion = new ClaudeBuddy.Emotions.EmotionState();

            float anchorX = size * 0.5f;
            float anchorY = size * 0.62f;
            float height = size * 0.40f;
            var info = new SkiaSharp.SKImageInfo(size, size, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);

            void Save(SkiaSharp.SKSurface surface, ref int n)
            {
                using SkiaSharp.SKImage img = surface.Snapshot();
                using SkiaSharp.SKData data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                using FileStream fs = File.Create(Path.Combine(outDir, $"frame_{n:D4}.png"));
                data.SaveTo(fs);
                n++;
            }

            int frameNo = 0;

            // "showcase" = a continuous spin that cycles through every skin, each shown for one
            // full turn, so the README demo shows all the characters AND the animation at once.
            if (string.Equals(skinId, "showcase", StringComparison.OrdinalIgnoreCase))
            {
                string[] ids = { "classic", "pikachu", "amongus", "ghost", "mate", "creeper", "ghast", "nicolaia", "galgo" };
                const int framesPerSkin = 22; // one full 360° turn per skin
                mascot.Animation = AnimationState.Idle;

                foreach (string id in ids)
                {
                    skins.SetCurrent(id);
                    for (int f = 0; f < framesPerSkin; f++)
                    {
                        animator.Update(mascot, emotion, 1f / 24f, lookX: 0f, lookY: 0f);
                        Pose pose = animator.Current;
                        // Drive a clean, continuous full turn ourselves (independent of any state).
                        pose.WholeBodyRotation = (f / (float)framesPerSkin) * (MathF.PI * 2f);

                        using SkiaSharp.SKSurface surface = SkiaSharp.SKSurface.Create(info);
                        surface.Canvas.Clear(new SkiaSharp.SKColor(0x14, 0x14, 0x18));
                        artist.Draw(surface.Canvas, new SkiaSharp.SKPoint(anchorX, anchorY), anchorY, height,
                            Facing.Right, 1f, 1f, pose, skins.Current.Palette, 1f);
                        Save(surface, ref frameNo);
                    }
                }

                return 0;
            }

            // Otherwise: a little story of several animations on one skin.
            skins.SetCurrent(skinId);
            (AnimationState State, int Frames)[] beats =
            {
                (AnimationState.Wave, 24),
                (AnimationState.Happy, 18),
                (AnimationState.Dance, 40),
                (AnimationState.Spin, 24),
                (AnimationState.Pet, 24),
                (AnimationState.Dizzy, 36),
                (AnimationState.Stretch, 24),
                (AnimationState.Idle, 16),
            };

            const float dt = 1f / 24f; // 24 fps demo
            foreach ((AnimationState state, int frames) in beats)
            {
                mascot.Animation = state;
                mascot.Dizziness = state == AnimationState.Dizzy ? 1f : 0f;
                for (int f = 0; f < frames; f++)
                {
                    animator.Update(mascot, emotion, dt, lookX: 0.15f, lookY: 0.1f);
                    using SkiaSharp.SKSurface surface = SkiaSharp.SKSurface.Create(info);
                    surface.Canvas.Clear(new SkiaSharp.SKColor(0x14, 0x14, 0x18));
                    artist.Draw(surface.Canvas, new SkiaSharp.SKPoint(anchorX, anchorY), anchorY, height,
                        Facing.Right, mascot.SquashX, mascot.SquashY, animator.Current, skins.Current.Palette, 1f);
                    Save(surface, ref frameNo);
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            LogCrash(ex);
            return 1;
        }
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeBuddy");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "crash.log"),
                $"[{DateTime.Now:u}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Nothing more we can do.
        }
    }
}
