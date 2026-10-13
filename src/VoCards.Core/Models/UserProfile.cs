namespace VoCards.Core.Models;

/// <summary>Application-wide preferences and the learner's progression state.</summary>
public sealed class UserProfile
{
    private readonly HashSet<string> _unlockedAchievements = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _achievementDates = new(StringComparer.Ordinal);

    public string DisplayName { get; set; } = "Learner";

    // ---------------------------------------------------------------- appearance

    /// <summary>"light", "dark" or "system".</summary>
    public string Theme { get; set; } = "system";

    /// <summary>Accent palette token, e.g. "violet", "aurora", "ember".</summary>
    public string Accent { get; set; } = "violet";

    /// <summary>Honour the OS reduced-motion preference, or force animations off.</summary>
    public bool ReduceMotion { get; set; }

    /// <summary>Show the 3D flip animation on the study card.</summary>
    public bool CardFlipAnimation { get; set; } = true;

    /// <summary>Play short sounds on correct/incorrect answers.</summary>
    public bool SoundEffects { get; set; } = true;

    // ---------------------------------------------------------------- study

    /// <summary>Reviews per day that count as hitting the goal.</summary>
    public int DailyGoal { get; set; } = 30;

    /// <summary>Speech-synthesis voice URI chosen by the learner, if any.</summary>
    public string? PreferredVoice { get; set; }

    public double SpeechRate { get; set; } = 0.95d;

    public double SpeechPitch { get; set; } = 1d;

    /// <summary>Show the answer's accepted alternatives after a typing miss.</summary>
    public bool ShowAlternatives { get; set; } = true;

    /// <summary>Whether the first-run tour has been completed or dismissed.</summary>
    public bool HasOnboarded { get; set; }

    // ---------------------------------------------------------------- progression

    /// <summary>Total experience earned. Never decreases.</summary>
    public long Xp { get; private set; }

    /// <summary>Consecutive days with at least one review.</summary>
    public int CurrentStreak { get; private set; }

    /// <summary>The best streak ever reached.</summary>
    public int LongestStreak { get; private set; }

    /// <summary>The most recent day on which a review happened.</summary>
    public DateOnly? LastStudyDay { get; private set; }

    /// <summary>
    /// Streak freezes in hand. One is spent automatically to cover a single missed day.
    /// </summary>
    public int StreakFreezes { get; private set; } = 2;

    /// <summary>Lifetime review count, kept even when the review log is trimmed.</summary>
    public long LifetimeReviews { get; private set; }

    public IReadOnlyCollection<string> UnlockedAchievements => _unlockedAchievements;

    public IReadOnlyDictionary<string, DateTimeOffset> AchievementDates => _achievementDates;

    // ---------------------------------------------------------------- level curve

    /// <summary>
    /// Levels get progressively more expensive: level <c>n</c> begins at
    /// <c>50 * n * (n - 1)</c> XP, so 0 / 100 / 300 / 600 / 1000 …
    /// </summary>
    public static long XpForLevel(int level) => level <= 1 ? 0L : 50L * level * (level - 1);

    public static int LevelForXp(long xp)
    {
        int level = 1;
        while (XpForLevel(level + 1) <= xp)
        {
            level++;
        }

        return level;
    }

    public int Level => LevelForXp(Xp);

    public long XpIntoLevel => Xp - XpForLevel(Level);

    public long XpForNextLevel => XpForLevel(Level + 1) - XpForLevel(Level);

    /// <summary>Progress through the current level, 0..1.</summary>
    public double LevelProgress => XpForNextLevel == 0 ? 1d : (double)XpIntoLevel / XpForNextLevel;

    // ---------------------------------------------------------------- mutation

    public int AwardXp(long amount)
    {
        if (amount <= 0)
        {
            return Level;
        }

        int before = Level;
        Xp += amount;
        return Level - before; // number of levels gained
    }

    public void RecordReview() => LifetimeReviews++;

    /// <summary>
    /// Folds a study day into the streak. Same-day repeats are idempotent; a single
    /// missed day is covered by a freeze if one is available.
    /// </summary>
    public StreakOutcome RegisterStudyDay(DateOnly day)
    {
        if (LastStudyDay is null)
        {
            LastStudyDay = day;
            CurrentStreak = 1;
            LongestStreak = Math.Max(LongestStreak, 1);
            return StreakOutcome.Started;
        }

        DateOnly last = LastStudyDay.Value;

        if (day <= last)
        {
            return StreakOutcome.AlreadyCounted;
        }

        int gap = day.DayNumber - last.DayNumber;
        LastStudyDay = day;

        if (gap == 1)
        {
            CurrentStreak++;
            LongestStreak = Math.Max(LongestStreak, CurrentStreak);
            return StreakOutcome.Extended;
        }

        if (gap == 2 && StreakFreezes > 0)
        {
            StreakFreezes--;
            CurrentStreak++;
            LongestStreak = Math.Max(LongestStreak, CurrentStreak);
            return StreakOutcome.Frozen;
        }

        CurrentStreak = 1;
        LongestStreak = Math.Max(LongestStreak, 1);
        return StreakOutcome.Broken;
    }

    public void GrantStreakFreeze(int count = 1) => StreakFreezes = Math.Min(StreakFreezes + count, 5);

    public bool Unlock(string achievementId, DateTimeOffset at)
    {
        if (!_unlockedAchievements.Add(achievementId))
        {
            return false;
        }

        _achievementDates[achievementId] = at;
        return true;
    }

    public bool HasUnlocked(string achievementId) => _unlockedAchievements.Contains(achievementId);

    /// <summary>Used by the deserializer and by "reset all data".</summary>
    internal void Restore(long xp, int currentStreak, int longestStreak, DateOnly? lastStudyDay,
                          int streakFreezes, long lifetimeReviews,
                          IEnumerable<KeyValuePair<string, DateTimeOffset>> achievements)
    {
        Xp = Math.Max(0, xp);
        CurrentStreak = Math.Max(0, currentStreak);
        LongestStreak = Math.Max(0, longestStreak);
        LastStudyDay = lastStudyDay;
        StreakFreezes = Math.Clamp(streakFreezes, 0, 5);
        LifetimeReviews = Math.Max(0, lifetimeReviews);

        _unlockedAchievements.Clear();
        _achievementDates.Clear();
        foreach ((string id, DateTimeOffset at) in achievements)
        {
            _unlockedAchievements.Add(id);
            _achievementDates[id] = at;
        }
    }
}

/// <summary>What happened to the streak when a study day was registered.</summary>
public enum StreakOutcome
{
    /// <summary>First ever study day.</summary>
    Started,

    /// <summary>Another review on a day already counted.</summary>
    AlreadyCounted,

    /// <summary>Studied the very next day; streak grew.</summary>
    Extended,

    /// <summary>One day was missed, and a freeze covered it.</summary>
    Frozen,

    /// <summary>Too many days missed; the streak restarted at one.</summary>
    Broken,
}
