using Velopack;
using Velopack.Sources;
using ClaudeBuddy.Settings;

namespace ClaudeBuddy.Services;

/// <summary>
/// Keeps Claude Buddy up to date from GitHub Releases using Velopack (the modern
/// successor to Squirrel). The app is installed by a generated <c>Setup.exe</c>; this
/// service then checks the same GitHub repo for newer releases and applies them.
///
/// On startup (in the background) it asks the configured GitHub repo for the latest
/// Velopack release. If one is newer than the running build it downloads it and then
/// <b>applies it and restarts straight away</b> — a brief, one-time relaunch into the new
/// version. We don't wait for a clean exit because users routinely just kill the process
/// or shut down the PC, which would otherwise strand the staged update forever.
///
/// Everything is best-effort: no network, a non-Velopack build (e.g. a plain dev run from
/// Visual Studio / the bin folder), or any error simply means "no update this run".
///
/// IMPORTANT: <c>VelopackApp.Build().Run()</c> must be called once at the very top of
/// Main (see Program.cs) for install/update hooks to work.
/// </summary>
public sealed class UpdateService
{
    // The GitHub repo that publishes releases.
    private const string RepoUrl = "https://github.com/LbillaSupport/Claude-pet";

    private readonly ISettingsService _settings;

    public UpdateService(ISettingsService settings) => _settings = settings;

    /// <summary>
    /// Kicks off a background check. Returns immediately. If a newer release exists it is
    /// downloaded and applied, then the app relaunches itself into the new version.
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
                await CheckAndApplyAsync().ConfigureAwait(false);
            }
            catch
            {
                // Updates are a nicety — a failed check must never disrupt the app.
            }
        });
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
