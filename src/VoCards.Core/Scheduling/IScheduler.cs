using VoCards.Core.Models;

namespace VoCards.Core.Scheduling;

/// <summary>
/// A spaced-repetition algorithm. Implementations must be pure: given the same card,
/// rating, settings and clock reading they must always produce the same outcome, and
/// they must never mutate the card they are handed.
/// </summary>
public interface IScheduler
{
    SchedulerKind Kind { get; }

    /// <summary>Human-readable name for the settings UI.</summary>
    string DisplayName { get; }

    /// <summary>Decides where a card goes next after being graded.</summary>
    SchedulingOutcome Schedule(Card card, Rating rating, DeckSettings settings, DateTimeOffset now);

    /// <summary>
    /// The interval each of the four ratings would produce, for the previews shown on
    /// the rating buttons. Keys are ordered Again, Hard, Good, Easy.
    /// </summary>
    IReadOnlyDictionary<Rating, double> PreviewIntervals(Card card, DeckSettings settings, DateTimeOffset now)
    {
        var previews = new Dictionary<Rating, double>(4);
        foreach (Rating rating in (ReadOnlySpan<Rating>)[Rating.Again, Rating.Hard, Rating.Good, Rating.Easy])
        {
            previews[rating] = Schedule(card, rating, settings, now).IntervalDays;
        }

        return previews;
    }
}
