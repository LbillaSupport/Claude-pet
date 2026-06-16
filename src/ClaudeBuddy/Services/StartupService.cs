using Microsoft.Win32;

namespace ClaudeBuddy.Services;

/// <summary>Manages whether Claude Buddy launches when the user signs in.</summary>
public interface IStartupService
{
    bool IsEnabled();

    void SetEnabled(bool enabled);
}

/// <summary>
/// Toggles a per-user <c>Run</c> registry entry. Per-user (HKCU) means no elevation
/// is ever required, matching the <c>asInvoker</c> manifest.
/// </summary>
public sealed class StartupService : IStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClaudeBuddy";

    public bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is not null;
        }
        catch
        {
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (enabled)
            {
                string exe = Environment.ProcessPath ?? string.Empty;
                if (!string.IsNullOrEmpty(exe))
                {
                    key.SetValue(ValueName, $"\"{exe}\"");
                }
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Startup is a convenience; failing to set it should never crash the app.
        }
    }
}
