using ClaudeBuddy.Core;

namespace ClaudeBuddy.Routine;

/// <summary>
/// Bundle of multipliers that flavour the mascot's behaviour for the current part
/// of the day. Produced by <see cref="DailyRoutine"/> and consumed by the behaviour
/// selector and emotion engine.
/// </summary>
public readonly record struct RoutineProfile(
    DayPhase Phase,
    float EnergyTarget,       // 0–1: how lively the mascot should feel
    float SleepBias,          // multiplier on the weight of sleepy behaviours
    float ExploreBias,        // multiplier on walk/run/inspect behaviours
    float MovementSpeedScale, // global locomotion speed scale
    bool CarriesCoffee);      // morning prop

/// <summary>
/// Maps real wall-clock time onto a <see cref="RoutineProfile"/>. This is what makes
/// Claude Buddy feel like it has a life: energetic mornings, exploratory afternoons,
/// yawny evenings, and deep sleep in the small hours.
/// </summary>
public sealed class DailyRoutine
{
    /// <summary>Allows tests / Photo Mode to override "now".</summary>
    public Func<DateTime> Clock { get; set; } = () => DateTime.Now;

    public DayPhase CurrentPhase => Evaluate().Phase;

    public RoutineProfile Evaluate()
    {
        int hour = Clock().Hour;

        return hour switch
        {
            >= 6 and < 12 => new RoutineProfile(
                DayPhase.Morning, EnergyTarget: 0.95f, SleepBias: 0.15f,
                ExploreBias: 1.0f, MovementSpeedScale: 1.05f, CarriesCoffee: true),

            >= 12 and < 18 => new RoutineProfile(
                DayPhase.Afternoon, EnergyTarget: 0.85f, SleepBias: 0.4f,
                ExploreBias: 1.4f, MovementSpeedScale: 1.0f, CarriesCoffee: false),

            >= 18 and < 22 => new RoutineProfile(
                DayPhase.Evening, EnergyTarget: 0.6f, SleepBias: 1.0f,
                ExploreBias: 0.8f, MovementSpeedScale: 0.9f, CarriesCoffee: false),

            >= 22 or < 1 => new RoutineProfile(
                DayPhase.Night, EnergyTarget: 0.3f, SleepBias: 2.4f,
                ExploreBias: 0.45f, MovementSpeedScale: 0.75f, CarriesCoffee: false),

            _ => new RoutineProfile(
                DayPhase.LateNight, EnergyTarget: 0.12f, SleepBias: 4.5f,
                ExploreBias: 0.2f, MovementSpeedScale: 0.6f, CarriesCoffee: false),
        };
    }
}
