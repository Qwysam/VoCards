namespace VoCards.Core.Stats;

/// <summary>One day's worth of activity, as the heatmap and the bar chart need it.</summary>
public sealed record DailyActivity
{
    public required DateOnly Day { get; init; }

    public required int Reviews { get; init; }

    public required int Correct { get; init; }

    public TimeSpan TimeSpent { get; init; }

    public int NewCards { get; init; }

    public double Accuracy => Reviews == 0 ? 0d : (double)Correct / Reviews;

    /// <summary>Heatmap intensity 0–4, relative to a busy day.</summary>
    public int Intensity(int busyDayThreshold) => Reviews switch
    {
        0 => 0,
        _ when busyDayThreshold <= 0 => 1,
        _ => Math.Clamp((int)Math.Ceiling(4d * Reviews / busyDayThreshold), 1, 4),
    };
}

/// <summary>How many cards fall due on a given future day.</summary>
public sealed record ForecastDay
{
    public required DateOnly Day { get; init; }

    public required int Due { get; init; }

    public int Young { get; init; }

    public int Mature { get; init; }
}

/// <summary>How cards are distributed across the learning lifecycle.</summary>
public sealed record StateBreakdown
{
    public int New { get; init; }

    public int Learning { get; init; }

    public int Young { get; init; }

    public int Mature { get; init; }

    public int Suspended { get; init; }

    public int Total => New + Learning + Young + Mature + Suspended;

    public double FractionOf(int count) => Total == 0 ? 0d : (double)count / Total;
}

/// <summary>Per-deck rollup for the stats page and the deck cards.</summary>
public sealed record DeckStats
{
    public required Guid DeckId { get; init; }

    public required string Name { get; init; }

    public required string Emoji { get; init; }

    public required string ColorToken { get; init; }

    public required int TotalCards { get; init; }

    public required int DueNow { get; init; }

    public required double Mastery { get; init; }

    public required double Accuracy { get; init; }

    public int ReviewsAllTime { get; init; }

    public DateTimeOffset? NextDueAt { get; init; }

    public int LeechCount { get; init; }
}

/// <summary>Accuracy bucketed by how long a card's interval was, i.e. the retention curve.</summary>
public sealed record RetentionPoint
{
    /// <summary>Lower bound of the interval bucket, in days.</summary>
    public required double IntervalDays { get; init; }

    public required int Reviews { get; init; }

    public required double Retention { get; init; }

    public required string Label { get; init; }
}

/// <summary>The whole dashboard in one object, so the UI makes a single call.</summary>
public sealed record LibraryStats
{
    public required int TotalCards { get; init; }

    public required int DueNow { get; init; }

    public required int DueToday { get; init; }

    public required StateBreakdown States { get; init; }

    public required double OverallMastery { get; init; }

    public required int ReviewsToday { get; init; }

    public required int CorrectToday { get; init; }

    public required int CurrentStreak { get; init; }

    public required int LongestStreak { get; init; }

    public required double LifetimeRetention { get; init; }

    public required TimeSpan TimeStudiedToday { get; init; }

    public required TimeSpan TimeStudiedAllTime { get; init; }

    public required int ReviewsThisWeek { get; init; }

    public required int DayGoal { get; init; }

    public double AccuracyToday => ReviewsToday == 0 ? 0d : (double)CorrectToday / ReviewsToday;

    /// <summary>Progress towards today's goal, 0..1.</summary>
    public double GoalProgress => DayGoal <= 0 ? 1d : Math.Clamp((double)ReviewsToday / DayGoal, 0d, 1d);

    public bool GoalMet => ReviewsToday >= DayGoal;
}
