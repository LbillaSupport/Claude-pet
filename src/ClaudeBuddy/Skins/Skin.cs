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

    /// <summary>Always-available "Among Us" crewmate.</summary>
    public static Skin AmongUs => new()
    {
        Id = "amongus",
        Name = "Among Us",
        Author = "Claude Buddy",
        Palette = new SkinPalette
        {
            Style = SkinStyle.AmongUs,
            Body = new RgbaColor(0xC5, 0x1F, 0x24),        // classic red crewmate
            BodyShadow = new RgbaColor(0x9A, 0x16, 0x1B),  // darker red (shadow + legs)
            Belly = new RgbaColor(0x9C, 0xD3, 0xE6),       // visor glass (light blue)
            Pupil = new RgbaColor(0x2A, 0x44, 0x55),       // visor frame / dark
            Accent = new RgbaColor(0xEF, 0x5A, 0x5F),       // backpack highlight
            HudHeadroom = 1.18f,
        },
    };

    /// <summary>Always-available "Pikachu".</summary>
    public static Skin Pikachu => new()
    {
        Id = "pikachu",
        Name = "Pikachu",
        Author = "Claude Buddy",
        Palette = new SkinPalette
        {
            Style = SkinStyle.Pikachu,
            Body = new RgbaColor(0xF6, 0xD0, 0x2F),        // pikachu yellow
            BodyShadow = new RgbaColor(0xD9, 0xA4, 0x1A),  // ear tips / tan shadow
            Belly = new RgbaColor(0xFF, 0xE2, 0x66),       // lighter yellow sheen
            Pupil = new RgbaColor(0x20, 0x18, 0x14),       // black eyes
            Mouth = new RgbaColor(0x6A, 0x32, 0x22),
            Blush = new RgbaColor(0xE8, 0x4C, 0x3D),        // red cheeks
            Accent = new RgbaColor(0xB0, 0x4A, 0x16),       // brown back stripes
            HudHeadroom = 1.42f,                            // clear the tall ears
        },
    };

    /// <summary>Always-available "Mate": a big friendly Argentine mate gourd with a bombilla.</summary>
    public static Skin Mate => new()
    {
        Id = "mate",
        Name = "Mate",
        Author = "Claude Buddy",
        Palette = new SkinPalette
        {
            Style = SkinStyle.Mate,
            Body = new RgbaColor(0x6E, 0x44, 0x22),        // gourd brown
            BodyShadow = new RgbaColor(0x53, 0x31, 0x18),  // darker brown
            Belly = new RgbaColor(0x3E, 0x8E, 0x4F),        // green yerba on top
            Pupil = new RgbaColor(0x20, 0x16, 0x10),
            Mouth = new RgbaColor(0x3A, 0x24, 0x16),
            Blush = new RgbaColor(0xC9, 0x86, 0x52),
            Accent = new RgbaColor(0xC9, 0xCD, 0xD2),       // metal bombilla
            HudHeadroom = 1.30f,                            // clear the bombilla sticking up
        },
    };

    /// <summary>Always-available "Ghost": a friendly Pac-Man-style ghost.</summary>
    public static Skin Ghost => new()
    {
        Id = "ghost",
        Name = "Ghost",
        Author = "Claude Buddy",
        Palette = new SkinPalette
        {
            Style = SkinStyle.Ghost,
            Body = new RgbaColor(0xF2, 0x4B, 0xC2),        // pinky-style pink
            BodyShadow = new RgbaColor(0xCB, 0x36, 0xA1),
            Belly = new RgbaColor(0xFF, 0xFF, 0xFF),        // white eye whites
            Pupil = new RgbaColor(0x21, 0x2C, 0x84),        // classic blue pupils
            Accent = new RgbaColor(0xFF, 0xC0, 0xE8),
            HudHeadroom = 1.06f,
        },
    };

    /// <summary>Always-available "Galgo": the smiley line-34 city bus in a Vélez hat.</summary>
    public static Skin Galgo => new()
    {
        Id = "galgo",
        Name = "Galgo (Bondi 34)",
        Author = "Claude Buddy",
        Palette = new SkinPalette
        {
            Style = SkinStyle.Galgo,
            Body = new RgbaColor(0xF4, 0xF6, 0xF8),        // white shell
            BodyShadow = new RgbaColor(0x1F, 0x3A, 0x8A),  // Vélez navy (skirt + hat)
            Belly = new RgbaColor(0xBF, 0xDD, 0xEC),        // light-blue glass
            Pupil = new RgbaColor(0x16, 0x16, 0x18),        // black outlines / tyres / pupils
            Accent = new RgbaColor(0xC8, 0x2A, 0x2A),       // red stripe
            Mouth = new RgbaColor(0xC0, 0x33, 0x3A),        // red mouth
            HudHeadroom = 1.55f,                            // clear the bucket hat
        },
    };
}
