using Velopack;
using Velopack.Sources;
using ClaudeBuddy.Settings;

namespace ClaudeBuddy.Services;

/// <summary>
/// Keeps Claude Buddy up to date from GitHub Releases using Velopack (the modern
/// successor to Squirrel). The app is installed by a generated <c>Setup.exe</c>; this
/// service then polls the same GitHub repo for newer releases and applies them.
///
/// It checks shortly after launch and then <b>keeps re-checking on a timer</b> for the
/// whole session (default every 4 hours), so a long-running buddy picks up new versions
/// without the user ever having to relaunch it. When a newer release is found it downloads
/// it and <b>applies it and relaunches straight away</b> — we don't wait for a clean exit
/// because users routinely just kill the process or shut down the PC, which would otherwise
/// strand the staged update.
///
/// Everything is best-effort: no internet, a non-Velopack build (e.g. a plain dev run from
/// the bin folder), or any error simply means "no update this round"; the timer keeps
/// ticking and will try again later (so transient connectivity is handled for free).
///
/// IMPORTANT: <c>VelopackApp.Build().Run()</c> must be called once at the very top of
/// Main (see Program.cs) for install/update hooks to work.
/// </summary>
public sealed class UpdateService
{
    // The GitHub repo that publishes releases.
    private const string RepoUrl = "https://github.com/LbillaSupport/Claude-pet";

    // Wait a little after launch before the first check (don't compete with startup),
    // then re-check on this cadence for as long as the app is running.
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(4);

    private readonly ISettingsService _settings;
    private readonly CancellationTokenSource _cts = new();

    public UpdateService(ISettingsService settings) => _settings = settings;

    /// <summary>
    /// Starts the background update poller: an initial check shortly after launch, then a
    /// repeating check every few hours for the rest of the session. Returns immediately.
    /// </summary>
    public void StartBackgroundCheck()
    {
        if (!_settings.Current.AutoUpdate)
        {
            return;
        }

        _ = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    /// <summary>Stops the poller (called from shutdown).</summary>
    public void Stop()
    {
        try
        {
            _cts.Cancel();
        }
        catch
        {
            // Nothing useful to do if cancellation throws on a disposed source.
        }
    }

    private async Task PollLoopAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(FirstCheckDelay, token).ConfigureAwait(false);

            // Re-read the setting each round so toggling auto-update off takes effect, and
            // a single timer drives every subsequent check.
            using var timer = new PeriodicTimer(CheckInterval);
            do
            {
                if (_settings.Current.AutoUpdate)
                {
                    try
                    {
                        await CheckAndApplyAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // Best-effort: swallow and let the timer try again next interval.
                    }
                }
            }
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // App is shutting down — expected.
        }
    }

    private async Task CheckAndApplyAsync()
    {
        var manager = new UpdateManager(new GithubSource(RepoUrl, accessToken: null, prerelease: false));

        // Only meaningful for an installed (Velopack) build; a plain dev run reports false.
        if (!manager.IsInstalled)
        {
            return;
        }

        UpdateInfo? info = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
        if (info is null)
        {
            return; // already up to date
        }

        await manager.DownloadUpdatesAsync(info).ConfigureAwait(false);

        // Swap in the new version and relaunch now. This terminates the current process,
        // so it's the last thing we do. (A future refinement could prompt the user first.)
        manager.ApplyUpdatesAndRestart(info);
    }
}
