using ClaudeBuddy.Achievements;
using ClaudeBuddy.Animation;
using ClaudeBuddy.Behaviors;
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

        // QA / marketing helper: `ClaudeBuddy --render <skinId> <out.png> [sizePx]` draws a
        // single skin to a centred PNG and exits — no window, no wandering. Lets us verify a
        // skin's look deterministically (the mascot never walks out of frame).
        if (args.Length >= 3 && args[0] == "--render")
        {
            return RenderSkinToFile(args[1], args[2], args.Length >= 4 ? int.Parse(args[3]) : 512);
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
    private static int RenderSkinToFile(string skinId, string outPath, int size)
    {
        try
        {
            var skins = new SkinManager();
            skins.Discover(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            skins.SetCurrent(skinId);

            var artist = new CharacterArtist();
            var pose = new Pose { MouthCurve = 0.7f, MouthOpen = 0.2f };

            var info = new SkiaSharp.SKImageInfo(size, size, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
            using SkiaSharp.SKSurface surface = SkiaSharp.SKSurface.Create(info);
            float anchorX = size * 0.5f;
            float anchorY = size * 0.62f;            // feet a little below centre
            float height = size * 0.42f;             // leave headroom for hats/buses
            artist.Draw(surface.Canvas, new SkiaSharp.SKPoint(anchorX, anchorY), anchorY,
                height, Facing.Right, 1f, 1f, pose, skins.Current.Palette, 1f);

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
