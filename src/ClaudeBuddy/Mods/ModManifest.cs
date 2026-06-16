namespace ClaudeBuddy.Mods;

/// <summary>
/// The on-disk <c>mod.json</c> schema. A mod is a folder under <c>/Mods</c> that can
/// contribute extra behaviours (and, in future, particle/sound packs) without any
/// recompilation. See <c>docs/MODS.md</c>.
/// </summary>
public sealed class ModManifest
{
    public string? Name { get; set; }
    public string? Author { get; set; }
    public string? Version { get; set; }
    public string? Description { get; set; }
    public bool Enabled { get; set; } = true;

    /// <summary>Extra autonomous behaviours added to the catalogue.</summary>
    public List<ModBehavior>? Behaviors { get; set; }
}

/// <summary>A behaviour contributed by a mod. Enum fields are parsed by name.</summary>
public sealed class ModBehavior
{
    public string? Id { get; set; }
    public string? DisplayName { get; set; }
    public string? Animation { get; set; }
    public string? Movement { get; set; }
    public string? Category { get; set; }
    public string? Mood { get; set; }
    public float Weight { get; set; } = 1f;
    public float MinDuration { get; set; } = 2f;
    public float MaxDuration { get; set; } = 4f;
    public float Cooldown { get; set; } = 6f;
    public float MinHappiness { get; set; }
    public string? Particle { get; set; }
    public string? Sound { get; set; }
}
