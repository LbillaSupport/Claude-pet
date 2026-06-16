using ClaudeBuddy.Core;

namespace ClaudeBuddy.Skins;

/// <summary>
/// The colours the procedural artist uses to paint the character. Because the mascot
/// is drawn from vectors, a whole new look is just a palette — which is why skins are
/// tiny JSON files rather than megabytes of sprites.
/// </summary>
public sealed class SkinPalette
{
    public RgbaColor Body { get; init; } = RgbaColor.ClaudeClay;
    public RgbaColor BodyShadow { get; init; } = RgbaColor.ClaudeClayDark;
    public RgbaColor Belly { get; init; } = RgbaColor.Cream;
    public RgbaColor Pupil { get; init; } = RgbaColor.Ink;
    public RgbaColor Mouth { get; init; } = new(0x7A, 0x39, 0x2C);
    public RgbaColor Blush { get; init; } = RgbaColor.Blush;
    public RgbaColor Accent { get; init; } = RgbaColor.StarGold;

    public static SkinPalette Default => new();
}

/// <summary>A fully-resolved skin ready for the renderer.</summary>
public sealed class Skin
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string Author { get; init; } = "Claude Buddy";

    public SkinPalette Palette { get; init; } = SkinPalette.Default;

    /// <summary>Optional per-skin sound overrides (key → absolute wav path).</summary>
    public IReadOnlyDictionary<string, string> SoundOverrides { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Folder the skin was loaded from (empty for the built-in skin).</summary>
    public string SourceFolder { get; init; } = string.Empty;

    public static Skin BuiltIn => new()
    {
        Id = "classic",
        Name = "Classic Claude",
        Palette = SkinPalette.Default,
    };
}
