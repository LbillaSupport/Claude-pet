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
    private static int Main()
    {
        // MUST be the first thing that runs: Velopack intercepts install/update/uninstall
        // hooks here and exits before the app proper starts. No-op for a plain dev run.
        VelopackApp.Build().Run();

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
                engine.Shutdown();
            }

            // If a newer build was downloaded this session, swap it in now (on exit) and
            // relaunch — a running exe can't overwrite itself, so this is the safe moment.
            updater.ApplyStagedUpdateOnExit();

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
