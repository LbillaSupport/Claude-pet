namespace ClaudeBuddy.Skins;

/// <summary>
/// The on-disk <c>skin.json</c> schema. Everything is optional and falls back to the
/// built-in palette, so even a one-line manifest produces a valid skin. See
/// <c>docs/SKINS.md</c> for the authoring guide.
/// </summary>
public sealed class SkinManifest
{
    public string? Name { get; set; }
    public string? Author { get; set; }
    public string? Version { get; set; }
    public string? Description { get; set; }
    public string? Preview { get; set; }

    /// <summary>Optional body archetype: "claud" (default), "creeper" or "ghast".</summary>
    public string? Style { get; set; }

    public SkinColors? Colors { get; set; }

    /// <summary>Sound key → relative wav path inside the skin folder.</summary>
    public Dictionary<string, string>? Sounds { get; set; }
}

/// <summary>Hex colour strings (e.g. "#D97A5A"). Any omitted colour uses the default.</summary>
public sealed class SkinColors
{
    public string? Body { get; set; }
    public string? BodyShadow { get; set; }
    public string? Belly { get; set; }
    public string? Pupil { get; set; }
    public string? Mouth { get; set; }
    public string? Blush { get; set; }
    public string? Accent { get; set; }
}
