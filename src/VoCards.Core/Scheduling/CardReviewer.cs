using VoCards.Core.Models;

namespace VoCards.Core.Scheduling;

/// <summary>
/// The single place where a scheduling decision is written back onto a card and
/// recorded in history. Nothing else in the codebase mutates a card's scheduling
/// fields — which is exactly what the 2021 app could not promise, since
/// <c>Card.memorized</c> was a public field guarded only by a comment.
/// </summary>
public static class CardReviewer
{
    /// <summary>
    /// Grades a card, advances its schedule, and returns the history entry produced.
    /// </summary>
    public static Review Apply(
        Card card,
        Deck deck,
        Rating rating,
        DateTimeOffset now,
        StudyMode mode = StudyMode.Flip,
        StudyDirection direction = StudyDirection.FrontToBack,
        AnswerVerdict verdict = AnswerVerdict.Correct,
        TimeSpan elapsed = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(deck);

        IScheduler scheduler = SchedulerFactory.For(deck);
        SchedulingOutcome outcome = scheduler.Schedule(card, rating, deck.Settings, now);

        CardState previousState = card.State;
        double previousInterval = card.IntervalDays;

        card.State = outcome.State;
        card.DueAt = outcome.DueAt;
        card.IntervalDays = outcome.IntervalDays;
        card.EaseFactor = outcome.EaseFactor;
        card.Repetitions = outcome.Repetitions;
        card.Lapses = outcome.Lapses;
        card.LearningStep = outcome.LearningStep;
        card.Stability = outcome.Stability;
        card.Difficulty = outcome.Difficulty;
        card.LastReviewedAt = now;
        card.ReviewCount++;

        if (rating != Rating.Again)
        {
            card.CorrectCount++;

            if (elapsed > TimeSpan.Zero && (card.BestResponseTime is null || elapsed < card.BestResponseTime))
            {
                card.BestResponseTime = elapsed;
            }
        }

        card.Touch();

        return new Review
        {
            CardId = card.Id,
            DeckId = deck.Id,
            ReviewedAt = now,
            Rating = rating,
            Mode = mode,
            Direction = direction,
            Verdict = verdict,
            Elapsed = elapsed,
            PreviousIntervalDays = previousInterval,
            NewIntervalDays = outcome.IntervalDays,
            PreviousState = previousState,
            NewState = outcome.State,
        };
    }

    /// <summary>
    /// Previews the four intervals without touching the card, for the rating buttons.
    /// </summary>
    public static IReadOnlyDictionary<Rating, string> PreviewLabels(Card card, Deck deck, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(deck);

        IScheduler scheduler = SchedulerFactory.For(deck);

        return scheduler
            .PreviewIntervals(card, deck.Settings, now)
            .ToDictionary(static pair => pair.Key, static pair => SchedulingOutcome.FormatInterval(pair.Value));
    }
}
