using VoCards.Core.Models;

namespace VoCards.Core.Gamification;

/// <summary>
/// How experience is earned. Deliberately weighted so that <i>showing up</i> pays more
/// than <i>getting everything right</i> — a learner who only ever reviews easy cards
/// should not out-earn one who is genuinely struggling through new material.
/// </summary>
public static class XpRules
{
    /// <summary>Base award for answering a card at all.</summary>
    public const int BaseAnswerXp = 5;

    /// <summary>Extra for seeing a brand-new card for the first time.</summary>
    public const int NewCardBonus = 5;

    /// <summary>Extra per consecutive correct answer, capped by <see cref="MaxComboBonus"/>.</summary>
    public const int ComboStep = 1;

    /// <summary>Ceiling on the combo bonus, so a long easy streak cannot run away.</summary>
    public const int MaxComboBonus = 10;

    /// <summary>Awarded once per day for meeting the daily goal.</summary>
    public const int DailyGoalXp = 50;

    /// <summary>Awarded per day of streak when a streak is extended, capped.</summary>
    public const int StreakStepXp = 5;

    public const int MaxStreakXp = 100;

    /// <summary>XP for one graded answer.</summary>
    public static long ForAnswer(Rating rating, CardState stateBefore, int combo)
    {
        // An honest "Again" still earns the base award: forgetting is part of learning.
        int quality = rating switch
        {
            Rating.Again => 0,
            Rating.Hard => 2,
            Rating.Good => 4,
            _ => 3, // Easy pays slightly less than Good: it was not much of a workout
        };

        int newBonus = stateBefore == CardState.New ? NewCardBonus : 0;
        int comboBonus = rating == Rating.Again
            ? 0
            : Math.Min(combo * ComboStep, MaxComboBonus);

        return BaseAnswerXp + quality + newBonus + comboBonus;
    }

    /// <summary>Bonus XP for a streak of the given length.</summary>
    public static long ForStreak(int streakDays) =>
        Math.Min(Math.Max(0, streakDays) * StreakStepXp, MaxStreakXp);

    /// <summary>Total XP a session of <paramref name="reviews"/> perfect answers would yield.</summary>
    public static long PerfectSessionEstimate(int reviews)
    {
        long total = 0;
        for (int i = 1; i <= Math.Max(0, reviews); i++)
        {
            total += ForAnswer(Rating.Good, CardState.Review, i);
        }

        return total;
    }
}
