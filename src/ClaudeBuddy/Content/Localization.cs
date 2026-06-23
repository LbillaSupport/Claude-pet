using System.Globalization;
using ClaudeBuddy.Core;

namespace ClaudeBuddy.Content;

/// <summary>
/// Resolves and holds the active <see cref="Language"/>. <see cref="Language.Auto"/> is mapped
/// to one of the supported languages from the OS UI culture at startup (English for anything we
/// don't ship). Everything that shows text — the <see cref="Phrasebook"/>, the
/// <see cref="Strings"/> UI table, the engine's inline lines — reads <see cref="Current"/> so a
/// single setting flips the whole app.
///
/// DI singleton. The engine sets it from settings on startup and whenever the menu changes it.
/// </summary>
public sealed class Localization
{
    /// <summary>The resolved language actually in use (never <see cref="Language.Auto"/>).</summary>
    public Language Current { get; private set; } = Language.English;

    /// <summary>Sets the active language, mapping <see cref="Language.Auto"/> to the OS culture.</summary>
    public void Set(Language requested) => Current = Resolve(requested);

    /// <summary>Maps a (possibly <see cref="Language.Auto"/>) setting to a concrete language.</summary>
    public static Language Resolve(Language requested) =>
        requested == Language.Auto ? FromCulture(CultureInfo.CurrentUICulture) : requested;

    /// <summary>Best-effort map from a culture (e.g. "pt-BR") to a supported language.</summary>
    public static Language FromCulture(CultureInfo culture)
    {
        // The two-letter ISO language name is stable across regions ("es-AR" and "es-ES" → "es").
        string tag = culture.TwoLetterISOLanguageName.ToLowerInvariant();
        return tag switch
        {
            "es" => Language.Spanish,
            "pt" => Language.Portuguese,
            "fr" => Language.French,
            "de" => Language.German,
            "it" => Language.Italian,
            "en" => Language.English,
            _ => Language.English, // anything we don't ship falls back to the global default
        };
    }
}
