using VoCards.Core.Models;
using VoCards.Core.Study;

namespace VoCards.Core.Gamification;

/// <summary>Checks the catalogue against the library and unlocks whatever now qualifies.</summary>
public static class AchievementEngine
{
    /// <summary>
    /// Evaluates every locked achievement and unlocks those that are satisfied,
    /// awarding their XP. Returns only the ones newly unlocked by this call.
    /// </summary>
    public static IReadOnlyList<Achievement> Evaluate(
        Library library,
        SessionSummary? session,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(library);

        var context = new AchievementContext
        {
            Library = library,
            Now = now,
            Session = session,
        };

        var unlocked = new List<Achievement>();

        foreach (Achievement achievement in AchievementCatalog.All)
        {
            if (library.Profile.HasUnlocked(achievement.Id))
            {
                continue;
            }

            // A badly written predicate must never take the app down mid-session.
            if (!SafelySatisfied(achievement, context))
            {
                continue;
            }

            library.Profile.Unlock(achievement.Id, now);
            library.Profile.AwardXp(achievement.XpReward);
            unlocked.Add(achievement);
        }

        return unlocked;
    }

    /// <summary>Progress towards a locked achievement, 0..1. Unlocked ones report 1.</summary>
    public static double ProgressOf(Achievement achievement, Library library, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(achievement);
        ArgumentNullException.ThrowIfNull(library);

        if (library.Profile.HasUnlocked(achievement.Id))
        {
            return 1d;
        }

        if (achievement.Progress is not { } progress)
        {
            return 0d;
        }

        var context = new AchievementContext { Library = library, Now = now };

        try
        {
            return Math.Clamp(progress(context), 0d, 1d);
        }
        catch (InvalidOperationException)
        {
            return 0d;
        }
    }

    private static bool SafelySatisfied(Achievement achievement, AchievementContext context)
    {
        try
        {
            return achievement.IsSatisfied(context);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
