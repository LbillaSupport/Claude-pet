using ClaudeBuddy.Settings;
using Velopack;
using Velopack.Sources;

namespace ClaudeBuddy.Services;

/// <summary>
/// Keeps Claude Buddy up to date from GitHub Releases using Velopack (the modern
/// successor to Squirrel). The app is installed by a generated <c>Setup.exe</c>; this
/// service then checks the same GitHub repo for newer releases and applies them silently.
///
/// On startup (in the background) it asks the configured GitHub repo for the latest
/// Velopack release. If one is newer than the running build it downloads the delta and
/// stages it; the swap + relaunch is performed by Velopack on the next exit (a running
/// exe can't overwrite itself). Everything is best-effort: no network, a non-Velopack
/// build (e.g. a plain dev run), or any error simply means "no update this run".
///
/// IMPORTANT: <c>VelopackApp.Build().Run()</c> must be called once at the very top of
/// Main (see Program.cs) for install/update hooks to work.
/// </summary>
public sealed class UpdateService
{
    // The GitHub repo that publishes releases.
    private const string RepoUrl = "https://github.com/LbillaSupport/Claude-pet";

    private readonly ISettingsService _settings;
    private UpdateInfo? _staged;
    private UpdateManager? _manager;

    public UpdateService(ISettingsService settings) => _settings = settings;

    /// <summary>
    /// Kicks off a background check. Returns immediately. If a newer release exists it is
    /// downloaded and staged to be applied on exit (<see cref="ApplyStagedUpdateOnExit"/>).
    /// </summary>
    public void StartBackgroundCheck()
    {
        if (!_settings.Current.AutoUpdate)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await CheckAndStageAsync().ConfigureAwait(false);
            }
            catch
            {
                // Updates are a nicety — a failed check must never disrupt the app.
            }
        });
    }

    /// <summary>
    /// If a newer build was downloaded this session, applies it now (on exit) and
    /// relaunches. Called from the engine shutdown path.
    /// </summary>
    public void ApplyStagedUpdateOnExit()
    {
        if (_manager is null || _staged is null)
        {
            return;
        }

        try
        {
            // Swaps in the new version and restarts the app for the user.
            _manager.ApplyUpdatesAndRestart(_staged);
        }
        catch
        {
            // If the swap can't run we just try again next session — never worse off.
        }
    }

    private async Task CheckAndStageAsync()
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

        _manager = manager;
        _staged = info; // applied on exit by ApplyStagedUpdateOnExit
    }
}
