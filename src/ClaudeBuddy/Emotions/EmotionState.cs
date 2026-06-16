using ClaudeBuddy.Core;
using ClaudeBuddy.Routine;

namespace ClaudeBuddy.Emotions;

/// <summary>
/// The mascot's evolving inner life. Mood is the headline emotion; intensity is how
/// strongly it is felt; energy tracks tiredness across the day; happiness is the
/// long-lived affection meter that petting fills and neglect drains.
/// </summary>
public sealed class EmotionState
{
    private float _moodHold;

    public Mood Mood { get; private set; } = Mood.Content;

    /// <summary>How strongly the current mood is expressed (0–1). Decays toward calm.</summary>
    public float Intensity { get; private set; } = 0.4f;

    /// <summary>Tiredness inverse (0 = exhausted, 1 = wide awake).</summary>
    public float Energy { get; private set; } = 0.9f;

    /// <summary>Affection meter (0–1). Drives unlockable rare reactions.</summary>
    public float Happiness { get; set; } = 0.5f;

    /// <summary>True once happiness crosses the unlock threshold this session.</summary>
    public bool RareContentUnlocked => Happiness >= EngineConstants.HappinessUnlockThreshold;

    /// <summary>
    /// Pushes the mascot toward a mood. Stronger triggers (a pet, a scare) override
    /// weaker ambient ones and "hold" for a short while so moods don't flicker.
    /// </summary>
    public void Nudge(Mood mood, float intensity, float holdSeconds = 1.5f)
    {
        if (intensity >= Intensity || _moodHold <= 0f)
        {
            Mood = mood;
            Intensity = MathUtil.Clamp01(intensity);
            _moodHold = holdSeconds;
        }
    }

    public void Update(float dt, in RoutineProfile routine)
    {
        // Energy eases toward whatever the time of day wants it to be.
        Energy = MathUtil.Damp(Energy, routine.EnergyTarget, 0.25f, dt);

        // Mood intensity relaxes back to a gentle baseline.
        _moodHold = MathF.Max(0f, _moodHold - dt);
        if (_moodHold <= 0f)
        {
            Intensity = MathUtil.Damp(Intensity, 0.35f, 0.6f, dt);
            if (Intensity <= 0.4f && Mood is not (Mood.Content or Mood.Sleepy))
            {
                // Settle back to a resting mood appropriate to energy.
                Mood = Energy < 0.35f ? Mood.Sleepy : Mood.Content;
            }
        }

        // Affection slowly fades when the mascot is left alone.
        Happiness = MathUtil.Clamp01(Happiness - (EngineConstants.HappinessDecayPerSecond * dt));
    }

    public void AddHappiness(float amount) =>
        Happiness = MathUtil.Clamp01(Happiness + amount);

    /// <summary>Locomotion speed multiplier implied by the current mood.</summary>
    public float SpeedMultiplier => Mood switch
    {
        Mood.Excited => 1.35f,
        Mood.Playful => 1.2f,
        Mood.Scared => 1.5f,
        Mood.Sleepy => 0.65f,
        Mood.Lazy => 0.7f,
        Mood.Sad => 0.8f,
        _ => 1.0f,
    };
}
