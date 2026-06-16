using System.Text.Json;
using ClaudeBuddy.Behaviors;
using ClaudeBuddy.Core;

namespace ClaudeBuddy.Mods;

/// <summary>A loaded mod and how many behaviours it contributed.</summary>
public sealed record LoadedMod(string Name, string Author, string Folder, int BehaviorCount);

/// <summary>
/// Loads behaviour-pack mods from <c>/Mods</c> folders and merges their behaviours
/// into the live <see cref="BehaviorCatalog"/>. Enum fields are parsed defensively so
/// a typo in one mod can never take down the app.
/// </summary>
public sealed class ModManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly List<LoadedMod> _loaded = new();

    public IReadOnlyList<LoadedMod> Loaded => _loaded;

    public void LoadAll(BehaviorCatalog catalog, string appData)
    {
        _loaded.Clear();

        foreach (string root in ModRoots(appData))
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (string folder in Directory.EnumerateDirectories(root))
            {
                TryLoad(folder, catalog);
            }
        }
    }

    private static IEnumerable<string> ModRoots(string appData)
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Mods");
        yield return Path.Combine(appData, "Mods");
    }

    private void TryLoad(string folder, BehaviorCatalog catalog)
    {
        try
        {
            string manifestPath = Path.Combine(folder, "mod.json");
            if (!File.Exists(manifestPath))
            {
                return;
            }

            ModManifest? mod = JsonSerializer.Deserialize<ModManifest>(
                File.ReadAllText(manifestPath), JsonOptions);
            if (mod is null || !mod.Enabled)
            {
                return;
            }

            int count = 0;
            if (mod.Behaviors is not null)
            {
                foreach (ModBehavior mb in mod.Behaviors)
                {
                    BehaviorDefinition? def = Convert(mb);
                    if (def is not null)
                    {
                        catalog.Add(def);
                        count++;
                    }
                }
            }

            _loaded.Add(new LoadedMod(
                mod.Name ?? Path.GetFileName(folder), mod.Author ?? "Unknown", folder, count));
        }
        catch
        {
            // Skip broken mods silently; robustness over strictness.
        }
    }

    private static BehaviorDefinition? Convert(ModBehavior mb)
    {
        if (string.IsNullOrWhiteSpace(mb.Id) ||
            !Enum.TryParse(mb.Animation, ignoreCase: true, out AnimationState anim))
        {
            return null;
        }

        Enum.TryParse(mb.Movement, true, out BehaviorMovement movement);
        Enum.TryParse(mb.Category, true, out BehaviorCategory category);
        Enum.TryParse(mb.Mood, true, out Mood mood);

        ParticleKind? particle = Enum.TryParse(mb.Particle, true, out ParticleKind pk) ? pk : null;

        return new BehaviorDefinition
        {
            Id = mb.Id!,
            DisplayName = mb.DisplayName ?? mb.Id!,
            Animation = anim,
            Movement = movement,
            Category = category,
            Mood = mood,
            Weight = mb.Weight,
            MinDuration = mb.MinDuration,
            MaxDuration = mb.MaxDuration,
            Cooldown = mb.Cooldown,
            MinHappiness = mb.MinHappiness,
            EnterParticle = particle,
            EnterSound = mb.Sound,
        };
    }
}
