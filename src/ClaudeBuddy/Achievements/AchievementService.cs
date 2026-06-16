using ClaudeBuddy.Settings;

namespace ClaudeBuddy.Achievements;

/// <summary>Read-only snapshot the achievement conditions are evaluated against.</summary>
public readonly record struct AchievementContext(AppSettings Settings, int TotalSkins, int TotalBehaviors, int LifetimeDays);

/// <summary>A single unlockable goal and the reward it grants.</summary>
public sealed record Achievement(
    string Id,
    string Title,
    string Description,
    Func<AchievementContext, bool> IsMet,
    string? RewardContentId = null);

/// <summary>
/// Tracks long-term goals. The engine feeds it a context once a second; any newly
/// satisfied achievement is persisted, may unlock bonus content, and raises an event
/// so the mascot can celebrate with a confetti toast.
/// </summary>
public sealed class AchievementService
{
    private readonly IReadOnlyList<Achievement> _achievements;

    public AchievementService() => _achievements = BuildCatalog();

    /// <summary>Raised once per newly-unlocked achievement.</summary>
    public event Action<Achievement>? Unlocked;

    public IReadOnlyList<Achievement> All => _achievements;

    public int UnlockedCount(AppSettings settings) =>
        _achievements.Count(a => settings.UnlockedAchievements.Contains(a.Id));

    /// <summary>Evaluates all goals; persists and announces anything newly earned.</summary>
    public void Evaluate(in AchievementContext context)
    {
        AppSettings settings = context.Settings;
        foreach (Achievement achievement in _achievements)
        {
            if (settings.UnlockedAchievements.Contains(achievement.Id))
            {
                continue;
            }

            if (!achievement.IsMet(context))
            {
                continue;
            }

            settings.UnlockedAchievements.Add(achievement.Id);
            if (achievement.RewardContentId is not null)
            {
                settings.UnlockedContent.Add(achievement.RewardContentId);
            }

            Unlocked?.Invoke(achievement);
        }
    }

    private static IReadOnlyList<Achievement> BuildCatalog() =>
    [
        new("first-friend", "First Friend", "Meet Claude Buddy for the first time.",
            _ => true),
        new("gentle-hand", "Gentle Hand", "Pet the mascot 10 times.",
            c => c.Settings.Stats.PetCount >= 10),
        new("best-friends", "Best Friends", "Pet the mascot 100 times.",
            c => c.Settings.Stats.PetCount >= 100, RewardContentId: "particles-rainbow"),
        new("overflowing", "Overflowing Joy", "Reach maximum happiness.",
            c => c.Settings.Happiness >= 0.999f, RewardContentId: "dance-secret"),
        new("productive", "Productive Pals", "Open Claude 10 times.",
            c => c.Settings.Stats.ClaudeOpenCount >= 10),
        new("power-user", "Power User", "Open Claude 50 times.",
            c => c.Settings.Stats.ClaudeOpenCount >= 50, RewardContentId: "skin-neon"),
        new("globetrotter", "Globetrotter", "Wander 100,000 pixels in total.",
            c => c.Settings.Stats.DistanceWalked >= 100_000),
        new("springs", "Springs For Legs", "Jump 100 times.",
            c => c.Settings.Stats.JumpCount >= 100),
        new("fashionista", "Fashionista", "Try on every installed skin.",
            c => c.TotalSkins > 0 && c.Settings.Stats.SkinsUsed.Count >= c.TotalSkins),
        new("connoisseur", "Behaviour Connoisseur", "Witness every idle behaviour.",
            c => c.TotalBehaviors > 0 && c.Settings.Stats.BehaviorsSeen.Count >= c.TotalBehaviors,
            RewardContentId: "particles-stars"),
        new("loyal", "Loyal Companion", "Keep the mascot alive for 30 days.",
            c => c.LifetimeDays >= 30, RewardContentId: "skin-gold"),
    ];
}
