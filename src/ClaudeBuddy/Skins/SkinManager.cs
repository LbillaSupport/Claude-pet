using System.Text.Json;
using ClaudeBuddy.Core;

namespace ClaudeBuddy.Skins;

/// <summary>
/// Discovers and resolves skins. Users add a skin simply by dropping a folder
/// containing a <c>skin.json</c> into <c>/Skins</c> (next to the exe) or into
/// <c>%AppData%\ClaudeBuddy\Skins</c>. The built-in "Classic Claude" skin is always
/// available, so the list is never empty.
/// </summary>
public sealed class SkinManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly List<Skin> _skins = new();

    public IReadOnlyList<Skin> Skins => _skins;

    public Skin Current { get; private set; } = Skin.BuiltIn;

    /// <summary>(Re)scans all skin folders. Safe to call when the menu opens.</summary>
    public void Discover(string appData)
    {
        _skins.Clear();
        _skins.Add(Skin.BuiltIn);
        _skins.Add(Skin.Creeper);
        _skins.Add(Skin.Ghast);
        _skins.Add(Skin.Nicolaia);
        _skins.Add(Skin.Galgo);

        foreach (string root in SkinRoots(appData))
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (string folder in Directory.EnumerateDirectories(root))
            {
                Skin? skin = TryLoad(folder);
                if (skin is not null && !_skins.Any(s => s.Id == skin.Id))
                {
                    _skins.Add(skin);
                }
            }
        }
    }

    public void SetCurrent(string id)
    {
        Skin? match = _skins.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        Current = match ?? Skin.BuiltIn;
    }

    private static IEnumerable<string> SkinRoots(string appData)
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Skins");
        yield return Path.Combine(appData, "Skins");
    }

    private static Skin? TryLoad(string folder)
    {
        try
        {
            string manifestPath = Path.Combine(folder, "skin.json");
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            SkinManifest? manifest = JsonSerializer.Deserialize<SkinManifest>(
                File.ReadAllText(manifestPath), JsonOptions);
            if (manifest is null)
            {
                return null;
            }

            string id = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));

            return new Skin
            {
                Id = id,
                Name = manifest.Name ?? id,
                Author = manifest.Author ?? "Unknown",
                Palette = ResolvePalette(manifest.Colors, manifest.Style),
                SourceFolder = folder,
                SoundOverrides = ResolveSounds(folder, manifest.Sounds),
            };
        }
        catch
        {
            // A broken skin folder must never prevent other skins from loading.
            return null;
        }
    }

    private static SkinPalette ResolvePalette(SkinColors? c, string? style)
    {
        SkinPalette d = SkinPalette.Default;
        SkinStyle resolvedStyle = style?.Trim().ToLowerInvariant() switch
        {
            "creeper" => SkinStyle.Creeper,
            "ghast" => SkinStyle.Ghast,
            "nicolaia" => SkinStyle.Nicolaia,
            "galgo" => SkinStyle.Galgo,
            _ => SkinStyle.Claud,
        };

        if (c is null)
        {
            return new SkinPalette { Style = resolvedStyle };
        }

        return new SkinPalette
        {
            Style = resolvedStyle,
            Body = Parse(c.Body, d.Body),
            BodyShadow = Parse(c.BodyShadow, d.BodyShadow),
            Belly = Parse(c.Belly, d.Belly),
            Pupil = Parse(c.Pupil, d.Pupil),
            Mouth = Parse(c.Mouth, d.Mouth),
            Blush = Parse(c.Blush, d.Blush),
            Accent = Parse(c.Accent, d.Accent),
        };
    }

    private static RgbaColor Parse(string? hex, RgbaColor fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        try
        {
            return RgbaColor.FromHex(hex);
        }
        catch
        {
            return fallback;
        }
    }

    private static IReadOnlyDictionary<string, string> ResolveSounds(
        string folder, Dictionary<string, string>? sounds)
    {
        if (sounds is null || sounds.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, string rel) in sounds)
        {
            resolved[key] = Path.Combine(folder, rel);
        }

        return resolved;
    }
}
