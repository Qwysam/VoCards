namespace VoCards.Core.Models;

/// <summary>
/// One immutable record of one answer. The 2021 app kept no history at all, which is
/// why it could not show a streak, a retention rate or a forecast.
/// </summary>
public sealed record Review
{
    public required Guid CardId { get; init; }

    public required Guid DeckId { get; init; }

    public required DateTimeOffset ReviewedAt { get; init; }

    public required Rating Rating { get; init; }

    public StudyMode Mode { get; init; } = StudyMode.Flip;

    public StudyDirection Direction { get; init; } = StudyDirection.FrontToBack;

    public AnswerVerdict Verdict { get; init; } = AnswerVerdict.Correct;

    /// <summary>How long the learner took to answer.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>Interval in days before this review.</summary>
    public double PreviousIntervalDays { get; init; }

    /// <summary>Interval in days granted by this review.</summary>
    public double NewIntervalDays { get; init; }

    public CardState PreviousState { get; init; }

    public CardState NewState { get; init; }

    /// <summary>The day this review belongs to, for streak and heatmap bucketing.</summary>
    public DateOnly Day => DateOnly.FromDateTime(ReviewedAt.LocalDateTime);

    /// <summary>Hard or better counts as recall.</summary>
    public bool WasCorrect => Rating != Rating.Again;
}

/// <summary>
/// The append-only history of every review, with the query helpers the stats page needs.
/// Trimmed to a rolling window so a long-lived library does not grow without bound.
/// </summary>
public sealed class ReviewLog
{
    /// <summary>Reviews older than this are dropped on <see cref="Trim"/>.</summary>
    public const int RetentionDays = 730;

    private readonly List<Review> _reviews = [];

    public IReadOnlyList<Review> All => _reviews;

    public int Count => _reviews.Count;

    public void Add(Review review) => _reviews.Add(review);

    public void AddRange(IEnumerable<Review> reviews) => _reviews.AddRange(reviews);

    public void Clear() => _reviews.Clear();

    /// <summary>Drops history beyond <see cref="RetentionDays"/>.</summary>
    public int Trim(DateTimeOffset now)
    {
        DateTimeOffset cutoff = now.AddDays(-RetentionDays);
        return _reviews.RemoveAll(r => r.ReviewedAt < cutoff);
    }

    public IEnumerable<Review> Since(DateTimeOffset from) => _reviews.Where(r => r.ReviewedAt >= from);

    public IEnumerable<Review> Between(DateTimeOffset from, DateTimeOffset to) =>
        _reviews.Where(r => r.ReviewedAt >= from && r.ReviewedAt <= to);

    public IEnumerable<Review> OnDay(DateOnly day) => _reviews.Where(r => r.Day == day);

    public IEnumerable<Review> ForDeck(Guid deckId) => _reviews.Where(r => r.DeckId == deckId);

    public IEnumerable<Review> ForCard(Guid cardId) => _reviews.Where(r => r.CardId == cardId);

    /// <summary>Every distinct day on which at least one review happened.</summary>
    public IReadOnlySet<DateOnly> ActiveDays() => _reviews.Select(static r => r.Day).ToHashSet();

    /// <summary>Removes history belonging to cards that no longer exist.</summary>
    public int Prune(IReadOnlySet<Guid> liveCardIds) => _reviews.RemoveAll(r => !liveCardIds.Contains(r.CardId));
}
