using System.Text.Json;
using ClaudeBuddy.Settings;

namespace ClaudeBuddy.Services;

/// <summary>Loads and persists the user's <see cref="AppSettings"/>.</summary>
public interface ISettingsService
{
    AppSettings Current { get; }

    void Load();

    void Save();

    string SettingsDirectory { get; }
}

/// <summary>
/// JSON-backed settings store. The live <see cref="AppSettings"/> object is shared
/// across the app and written back atomically (temp file + replace) so a crash mid-
/// write can never corrupt the user's configuration.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _filePath;

    public SettingsService()
    {
        SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClaudeBuddy");
        _filePath = Path.Combine(SettingsDirectory, "settings.json");
        Current = new AppSettings();
    }

    public AppSettings Current { get; private set; }

    public string SettingsDirectory { get; }

    public void Load()
    {
        Directory.CreateDirectory(SettingsDirectory);

        if (!File.Exists(_filePath))
        {
            // First ever run: seed from the shipped defaults if present.
            Current = LoadShippedDefaults() ?? new AppSettings();
            Current.IsFirstEverRun = true;
            Current.FirstRunUtc = DateTimeOffset.UtcNow;
            Save();
            return;
        }

        try
        {
            string json = File.ReadAllText(_filePath);
            Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception)
        {
            // A malformed file should never stop the mascot from appearing.
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            string json = JsonSerializer.Serialize(Current, JsonOptions);
            string tmp = _filePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _filePath, overwrite: true);
        }
        catch (Exception)
        {
            // Best-effort persistence; never throw out of a save.
        }
    }

    private static AppSettings? LoadShippedDefaults()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "appsettings.default.json");
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
