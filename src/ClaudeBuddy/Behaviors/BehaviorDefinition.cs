using ClaudeBuddy.Core;

namespace ClaudeBuddy.Behaviors;

/// <summary>How a behaviour moves the mascot through the world while it runs.</summary>
public enum BehaviorMovement
{
    None,            // stationary
    Wander,          // stroll to a random spot
    Run,             // dash to a random spot
    ApproachCursor,  // walk toward the mouse
    FleeCursor,      // run away from the mouse
    EdgePeek,        // walk to the nearest screen edge and peek
    ApproachPoint,   // walk to a specific world X set by the engine (e.g. the real clock)
}

/// <summary>
/// Coarse grouping used by the selector to bias behaviour by time-of-day and mood
/// (e.g. "Sleepy" behaviours get heavier at night).
/// </summary>
public enum BehaviorCategory
{
    Idle,
    Active,
    Explore,
    Sleepy,
    Social,
    Playful,
    Special,
}

/// <summary>
/// An immutable, data-driven description of one thing the mascot can choose to do.
/// The breadth of the character lives in the <see cref="BehaviorCatalog"/> list of
/// these records — adding a behaviour is data, not code, which keeps the system
/// open for extension (mods/behaviour packs) without modifying the controller.
/// </summary>
public sealed record BehaviorDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required AnimationState Animation { get; init; }

    public BehaviorMovement Movement { get; init; } = BehaviorMovement.None;

    public BehaviorCategory Category { get; init; } = BehaviorCategory.Idle;

    /// <summary>Relative likelihood before routine/mood biasing.</summary>
    public float Weight { get; init; } = 1f;

    public float MinDuration { get; init; } = 2f;

    public float MaxDuration { get; init; } = 4f;

    /// <summary>Seconds this behaviour can't be re-selected after it finishes.</summary>
    public float Cooldown { get; init; } = 6f;

    /// <summary>Mood the behaviour nudges the mascot toward on entry.</summary>
    public Mood Mood { get; init; } = Mood.Content;

    public float MoodIntensity { get; init; } = 0.4f;

    /// <summary>Gate: only selectable once happiness reaches this (rare unlockables).</summary>
    public float MinHappiness { get; init; }

    /// <summary>Optional one-shot particle burst when the behaviour starts.</summary>
    public ParticleKind? EnterParticle { get; init; }

    /// <summary>
    /// Optional particle burst fired once, partway through the behaviour (its "climax")
    /// — used by build-up moves like the power-up charge so the explosion lands on the
    /// release rather than at the start.
    /// </summary>
    public ParticleKind? ClimaxParticle { get; init; }

    /// <summary>Optional sound key (resolved by the audio service) on entry.</summary>
    public string? EnterSound { get; init; }

    /// <summary>
    /// Optional imaginary prop to show off. Only meaningful for behaviours whose
    /// <see cref="Animation"/> is <see cref="AnimationState.HoldProp"/>; the engine feeds it
    /// to the animator on entry (and clears it on the next behaviour).
    /// </summary>
    public HeldPropKind? HeldProp { get; init; }
}
