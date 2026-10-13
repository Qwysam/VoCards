using VoCards.Core.Models;

namespace VoCards.Core.Stats;

/// <summary>
/// Turns the review log into everything the stats page draws.
///
/// The 2021 app's entire statistics feature was one line:
/// <c>"{Words_Learnt} words learnt out of {Words_Total}"</c>.
/// </summary>
public static class StatsService
{
    /// <summary>A card is "mature" once it is spaced at least this far out.</summary>
    public const double MatureIntervalDays = 21d;

    /// <summary>The top-level dashboard rollup.</summary>
    public static LibraryStats Summarize(Library library, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(library);

        DateOnly today = DateOnly.FromDateTime(now.LocalDateTime);
        var todaysReviews = library.History.OnDay(today).ToArray();
        var allReviews = library.History.All;

        DateTimeOffset endOfToday = new DateTimeOffset(
            today.Year, today.Month, today.Day, 23, 59, 59, now.Offset);

        DateOnly weekStart = today.AddDays(-6);

        return new LibraryStats
        {
            TotalCards = library.TotalCards,
            DueNow = library.DueCount(now),
            DueToday = library.AllCards.Count(c => c.State != CardState.Suspended && c.IsDue(endOfToday)),
            States = Breakdown(library),
            OverallMastery = library.OverallMastery,
            ReviewsToday = todaysReviews.Length,
            CorrectToday = todaysReviews.Count(static r => r.WasCorrect),
            CurrentStreak = library.Profile.CurrentStreak,
            LongestStreak = library.Profile.LongestStreak,
            LifetimeRetention = Retention(allReviews),
            TimeStudiedToday = Sum(todaysReviews),
            TimeStudiedAllTime = Sum(allReviews),
            ReviewsThisWeek = allReviews.Count(r => r.Day >= weekStart && r.Day <= today),
            DayGoal = library.Profile.DailyGoal,
        };
    }

    /// <summary>How the library's cards are spread across the lifecycle.</summary>
    public static StateBreakdown Breakdown(Library library)
    {
        ArgumentNullException.ThrowIfNull(library);

        int newCards = 0, learning = 0, young = 0, mature = 0, suspended = 0;

        foreach (Card card in library.AllCards)
        {
            switch (card.State)
            {
                case CardState.Suspended:
                    suspended++;
                    break;
                case CardState.New:
                    newCards++;
                    break;
                case CardState.Learning:
                case CardState.Relearning:
                    learning++;
                    break;
                default:
                    if (card.IntervalDays >= MatureIntervalDays)
                    {
                        mature++;
                    }
                    else
                    {
                        young++;
                    }

                    break;
            }
        }

        return new StateBreakdown
        {
            New = newCards,
            Learning = learning,
            Young = young,
            Mature = mature,
            Suspended = suspended,
        };
    }

    /// <summary>
    /// Daily activity for the last <paramref name="days"/> days, including empty days
    /// so the heatmap grid is dense.
    /// </summary>
    public static IReadOnlyList<DailyActivity> Activity(Library library, DateTimeOffset now, int days = 365)
    {
        ArgumentNullException.ThrowIfNull(library);

        DateOnly today = DateOnly.FromDateTime(now.LocalDateTime);
        DateOnly start = today.AddDays(-(Math.Max(1, days) - 1));

        var byDay = library.History.All
            .Where(r => r.Day >= start && r.Day <= today)
            .GroupBy(static r => r.Day)
            .ToDictionary(static g => g.Key, static g => g.ToArray());

        var activity = new List<DailyActivity>(days);

        for (DateOnly day = start; day <= today; day = day.AddDays(1))
        {
            if (!byDay.TryGetValue(day, out Review[]? reviews))
            {
                activity.Add(new DailyActivity { Day = day, Reviews = 0, Correct = 0 });
                continue;
            }

            activity.Add(new DailyActivity
            {
                Day = day,
                Reviews = reviews.Length,
                Correct = reviews.Count(static r => r.WasCorrect),
                TimeSpent = Sum(reviews),
                NewCards = reviews.Count(static r => r.PreviousState == CardState.New),
            });
        }

        return activity;
    }

    /// <summary>
    /// A busy-day threshold for scaling heatmap intensity: the 90th percentile of
    /// non-empty days, so one outlier session does not flatten the whole grid.
    /// </summary>
    public static int BusyDayThreshold(IReadOnlyList<DailyActivity> activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var active = activity
            .Where(static a => a.Reviews > 0)
            .Select(static a => a.Reviews)
            .OrderBy(static r => r)
            .ToArray();

        if (active.Length == 0)
        {
            return 0;
        }

        int index = Math.Clamp((int)Math.Ceiling(active.Length * 0.9d) - 1, 0, active.Length - 1);
        return Math.Max(1, active[index]);
    }

    /// <summary>How many cards come due on each of the next <paramref name="days"/> days.</summary>
    public static IReadOnlyList<ForecastDay> Forecast(Library library, DateTimeOffset now, int days = 30)
    {
        ArgumentNullException.ThrowIfNull(library);

        DateOnly today = DateOnly.FromDateTime(now.LocalDateTime);
        var buckets = new Dictionary<DateOnly, (int Due, int Young, int Mature)>();

        foreach (Card card in library.AllCards)
        {
            if (card.State == CardState.Suspended || card.DueAt is not { } due)
            {
                continue;
            }

            // Anything already overdue is counted as due today.
            DateOnly day = DateOnly.FromDateTime(due.LocalDateTime);
            if (day < today)
            {
                day = today;
            }

            if (day > today.AddDays(days - 1))
            {
                continue;
            }

            (int d, int y, int m) = buckets.GetValueOrDefault(day);
            bool isMature = card.IntervalDays >= MatureIntervalDays;
            buckets[day] = (d + 1, y + (isMature ? 0 : 1), m + (isMature ? 1 : 0));
        }

        var forecast = new List<ForecastDay>(days);

        for (int offset = 0; offset < days; offset++)
        {
            DateOnly day = today.AddDays(offset);
            (int due, int young, int mature) = buckets.GetValueOrDefault(day);
            forecast.Add(new ForecastDay { Day = day, Due = due, Young = young, Mature = mature });
        }

        return forecast;
    }

    /// <summary>
    /// The retention curve: how accuracy holds up as intervals get longer. Reviews are
    /// bucketed by the interval the card had going in.
    /// </summary>
    public static IReadOnlyList<RetentionPoint> RetentionCurve(Library library)
    {
        ArgumentNullException.ThrowIfNull(library);

        (double Lower, double Upper, string Label)[] buckets =
        [
            (0d, 1d, "<1 d"),
            (1d, 3d, "1–3 d"),
            (3d, 7d, "3–7 d"),
            (7d, 21d, "1–3 w"),
            (21d, 60d, "3 w–2 mo"),
            (60d, 180d, "2–6 mo"),
            (180d, double.MaxValue, "6 mo+"),
        ];

        var points = new List<RetentionPoint>(buckets.Length);

        foreach ((double lower, double upper, string label) in buckets)
        {
            // Only graduated reviews say anything about retention; learning steps do not.
            var inBucket = library.History.All
                .Where(r => r.PreviousState is CardState.Review or CardState.Relearning)
                .Where(r => r.PreviousIntervalDays >= lower && r.PreviousIntervalDays < upper)
                .ToArray();

            if (inBucket.Length == 0)
            {
                continue;
            }

            points.Add(new RetentionPoint
            {
                IntervalDays = lower,
                Reviews = inBucket.Length,
                Retention = Retention(inBucket),
                Label = label,
            });
        }

        return points;
    }

    /// <summary>Per-deck rollups, ordered by how much work is waiting.</summary>
    public static IReadOnlyList<DeckStats> PerDeck(Library library, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(library);

        return library.Decks
            .Select(deck =>
            {
                var reviews = library.History.ForDeck(deck.Id).ToArray();

                return new DeckStats
                {
                    DeckId = deck.Id,
                    Name = deck.Name,
                    Emoji = deck.Emoji,
                    ColorToken = deck.ColorToken,
                    TotalCards = deck.TotalCards,
                    DueNow = deck.DueCount(now),
                    Mastery = deck.Mastery,
                    Accuracy = Retention(reviews),
                    ReviewsAllTime = reviews.Length,
                    NextDueAt = deck.NextDueAt(now),
                    LeechCount = deck.LeechCards,
                };
            })
            .OrderByDescending(static s => s.DueNow)
            .ThenBy(static s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Reviews bucketed by local hour, for the "when do you study" chart.</summary>
    public static IReadOnlyList<int> ReviewsByHour(Library library)
    {
        ArgumentNullException.ThrowIfNull(library);

        int[] hours = new int[24];

        foreach (Review review in library.History.All)
        {
            hours[review.ReviewedAt.LocalDateTime.Hour]++;
        }

        return hours;
    }

    /// <summary>The cards giving the learner the most trouble.</summary>
    public static IReadOnlyList<Card> HardestCards(Library library, int take = 10)
    {
        ArgumentNullException.ThrowIfNull(library);

        return library.AllCards
            .Where(static c => c.ReviewCount >= 3 && c.State != CardState.Suspended)
            .OrderBy(static c => c.Accuracy)
            .ThenByDescending(static c => c.Lapses)
            .Take(Math.Max(1, take))
            .ToArray();
    }

    private static double Retention(IReadOnlyCollection<Review> reviews) =>
        reviews.Count == 0 ? 0d : (double)reviews.Count(static r => r.WasCorrect) / reviews.Count;

    private static TimeSpan Sum(IEnumerable<Review> reviews) =>
        TimeSpan.FromMilliseconds(reviews.Sum(static r => r.Elapsed.TotalMilliseconds));
}
