using ClaudeBuddy.Core;
using ClaudeBuddy.Emotions;
using ClaudeBuddy.Routine;

namespace ClaudeBuddy.Behaviors;

/// <summary>
/// Chooses the next autonomous behaviour with a weighted roulette. Weights are the
/// catalogue base values multiplied by biases for the time of day, the current mood,
/// energy, and an anti-repetition penalty — so the mascot feels purposeful rather
/// than random, and rarely does the same thing twice in a row.
/// </summary>
public sealed class BehaviorSelector
{
    private readonly Rng _rng;

    public BehaviorSelector(Rng rng) => _rng = rng;

    public BehaviorDefinition Select(
        BehaviorCatalog catalog,
        IReadOnlyDictionary<string, float> cooldowns,
        in RoutineProfile routine,
        EmotionState emotion,
        string? currentId)
    {
        float totalWeight = 0f;

        // First pass: total weight (the catalogue is small, so two passes is fine).
        foreach (BehaviorDefinition def in catalog.Selectable)
        {
            totalWeight += Weigh(def, cooldowns, routine, emotion, currentId);
        }

        if (totalWeight <= 0f)
        {
            return catalog["idle"];
        }

        // Second pass: roulette.
        float roll = _rng.Range(0f, totalWeight);
        foreach (BehaviorDefinition def in catalog.Selectable)
        {
            float w = Weigh(def, cooldowns, routine, emotion, currentId);
            if (w <= 0f)
            {
                continue;
            }

            roll -= w;
            if (roll <= 0f)
            {
                return def;
            }
        }

        return catalog["idle"];
    }

    private static float Weigh(
        BehaviorDefinition def,
        IReadOnlyDictionary<string, float> cooldowns,
        in RoutineProfile routine,
        EmotionState emotion,
        string? currentId)
    {
        // Hard gates first.
        if (cooldowns.TryGetValue(def.Id, out float remaining) && remaining > 0f)
        {
            return 0f;
        }

        if (emotion.Happiness < def.MinHappiness)
        {
            return 0f;
        }

        float w = def.Weight;

        // Time-of-day biasing by category.
        w *= def.Category switch
        {
            BehaviorCategory.Sleepy => routine.SleepBias,
            BehaviorCategory.Explore => routine.ExploreBias,
            BehaviorCategory.Active => MathUtil.Lerp(0.35f, 1.5f, routine.EnergyTarget),
            BehaviorCategory.Playful => MathUtil.Lerp(0.5f, 1.4f, routine.EnergyTarget),
            _ => 1f,
        };

        // Energy biasing: tired mascots favour rest, lively ones favour motion.
        if (def.Category is BehaviorCategory.Sleepy)
        {
            w *= MathUtil.Lerp(2.0f, 0.3f, emotion.Energy);
        }
        else if (def.Category is BehaviorCategory.Active or BehaviorCategory.Playful)
        {
            w *= MathUtil.Lerp(0.4f, 1.3f, emotion.Energy);
        }

        // Mood affinity: a behaviour that matches the current mood is more likely.
        if (def.Mood == emotion.Mood)
        {
            w *= 1.4f;
        }

        // Anti-repetition: strongly discourage repeating the same behaviour.
        if (currentId is not null && def.Id.Equals(currentId, StringComparison.OrdinalIgnoreCase))
        {
            w *= 0.12f;
        }

        return MathF.Max(0f, w);
    }
}
