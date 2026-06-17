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

    /// <summary>Body/face archetype the artist draws for this skin.</summary>
    public SkinStyle Style { get; init; } = SkinStyle.Claud;

    /// <summary>
    /// How far above the feet (as a multiple of body height) the usage battery/bubble
    /// should float, so it clears whatever sits on top of the character. The classic
    /// block ends near 1.0; skins with tall headgear (Nicolaia's top hat) raise this so
    /// the battery isn't drawn over the hat.
    /// </summary>
    public float HudHeadroom { get; init; } = 0.98f;

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

        // The iconic Claw'd: a deep terracotta block with pure-black square eyes.
        Palette = new SkinPalette
        {
            Body = new RgbaColor(0xC1, 0x5F, 0x3C),
            BodyShadow = new RgbaColor(0xA1, 0x4A, 0x2B),
            Pupil = new RgbaColor(0x1E, 0x1A, 0x17),
        },
    };

    /// <summary>Always-available Minecraft Creeper skin.</summary>
    public static Skin Creeper => new()
    {
        Id = "creeper",
        Name = "Creeper",
        Author = "Claude Buddy",
        Palette = new SkinPalette
        {
            Style = SkinStyle.Creeper,
            Body = new RgbaColor(0x4E, 0xA6, 0x3A),       // creeper green
            BodyShadow = new RgbaColor(0x37, 0x7C, 0x2A),
            Pupil = new RgbaColor(0x12, 0x2A, 0x12),       // dark face
            Accent = new RgbaColor(0x86, 0xD0, 0x5A),
            Blush = new RgbaColor(0x37, 0x7C, 0x2A),
        },
    };

    /// <summary>Always-available Minecraft Ghast skin.</summary>
    public static Skin Ghast => new()
    {
        Id = "ghast",
        Name = "Ghast",
        Author = "Claude Buddy",
        Palette = new SkinPalette
        {
            Style = SkinStyle.Ghast,
            Body = new RgbaColor(0xE7, 0xE6, 0xE2),        // pale ghast white
            BodyShadow = new RgbaColor(0xC2, 0xC1, 0xBC),
            Pupil = new RgbaColor(0x32, 0x32, 0x32),
            Mouth = new RgbaColor(0x88, 0x2A, 0x2A),       // red maw when it "shoots"
            Accent = new RgbaColor(0xFF, 0x6B, 0x3A),      // fireball orange
        },
    };

    /// <summary>Always-available "Nicolaia": a dapper fellow in a black top hat and suit.</summary>
    public static Skin Nicolaia => new()
    {
        Id = "nicolaia",
        Name = "Nicolaia",
        Author = "Claude Buddy",
        Palette = new SkinPalette
        {
            Style = SkinStyle.Nicolaia,
            Body = new RgbaColor(0xE8, 0xC4, 0xA0),        // warm skin tone (the face)
            BodyShadow = new RgbaColor(0x16, 0x14, 0x12),  // black suit + top hat
            Belly = new RgbaColor(0xF4, 0xF1, 0xEA),       // white shirt
            Pupil = new RgbaColor(0x2A, 0x22, 0x1C),       // dark eyes
            Accent = new RgbaColor(0x5A, 0x3E, 0x28),      // brown side-curls (peyot)
            Blush = new RgbaColor(0xD8, 0x9A, 0x80),
            HudHeadroom = 1.34f,                            // clear the tall top hat
        },
    };
}
