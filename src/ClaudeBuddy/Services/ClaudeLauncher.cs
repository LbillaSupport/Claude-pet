using System.Diagnostics;

namespace ClaudeBuddy.Services;

/// <summary>Finds, launches and tracks the Claude Desktop application.</summary>
public interface IClaudeLauncher
{
    /// <summary>Launches Claude Desktop (or opens claude.ai as a fallback).</summary>
    bool LaunchOrFocus();

    /// <summary>True when a Claude Desktop process is currently running.</summary>
    bool IsClaudeRunning();
}

/// <summary>
/// Locates Claude Desktop using its known install conventions, with a graceful
/// fallback chain: a running instance → the per-user install → the <c>claude://</c>
/// protocol → the website. The single-click "open Claude" interaction routes here.
/// </summary>
public sealed class ClaudeLauncher : IClaudeLauncher
{
    private const string ProcessName = "claude";
    private const string WebFallback = "https://claude.ai/";

    public bool IsClaudeRunning()
    {
        try
        {
            return Process.GetProcessesByName(ProcessName).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public bool LaunchOrFocus()
    {
        // 1. Direct executable in the per-user install location(s).
        foreach (string exe in CandidateExecutables())
        {
            if (File.Exists(exe) && TryStart(exe))
            {
                return true;
            }
        }

        // 2. The custom URL protocol registered by the installer.
        if (TryStart("claude://"))
        {
            return true;
        }

        // 3. Last resort: the web app, so the click always "does something".
        return TryStart(WebFallback);
    }

    private static IEnumerable<string> CandidateExecutables()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Stub launcher placed directly in the install root.
        yield return Path.Combine(local, "AnthropicClaude", "claude.exe");

        // Squirrel-style versioned folders: pick the newest app-* directory.
        string root = Path.Combine(local, "AnthropicClaude");
        if (Directory.Exists(root))
        {
            string? newest = Directory
                .EnumerateDirectories(root, "app-*")
                .OrderByDescending(d => d)
                .FirstOrDefault();
            if (newest is not null)
            {
                yield return Path.Combine(newest, "claude.exe");
            }
        }
    }

    private static bool TryStart(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true,
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
