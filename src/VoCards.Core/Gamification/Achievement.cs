using VoCards.Core.Models;
using VoCards.Core.Study;

namespace VoCards.Core.Gamification;

/// <summary>How hard an achievement is to get, which drives its colour in the UI.</summary>
public enum AchievementTier
{
    Bronze,
    Silver,
    Gold,
    Platinum,
}

/// <summary>Groups achievements on the trophy page.</summary>
public enum AchievementCategory
{
    Milestones,
    Consistency,
    Accuracy,
    Collection,
    Mastery,
    Curiosity,
}

/// <summary>
/// One unlockable badge. The predicate is the whole definition — an achievement is
/// just a name attached to a question asked of the library.
/// </summary>
public sealed record Achievement
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Emoji { get; init; }

    public required AchievementTier Tier { get; init; }

    public required AchievementCategory Category { get; init; }

    /// <summary>XP granted when this unlocks.</summary>
    public int XpReward { get; init; } = 25;

    /// <summary>Whether the library currently satisfies this achievement.</summary>
    public required Func<AchievementContext, bool> IsSatisfied { get; init; }

    /// <summary>
    /// Optional progress towards the goal, 0..1, for the partially-filled ring shown
    /// on locked badges. Null means "no meaningful progress to show".
    /// </summary>
    public Func<AchievementContext, double>? Progress { get; init; }
}

/// <summary>Everything an achievement predicate is allowed to look at.</summary>
public sealed record AchievementContext
{
    public required Library Library { get; init; }

    public required DateTimeOffset Now { get; init; }

    /// <summary>The session that just ended, when the evaluation was triggered by one.</summary>
    public SessionSummary? Session { get; init; }

    public UserProfile Profile => Library.Profile;

    public int LocalHour => Now.LocalDateTime.Hour;
}
