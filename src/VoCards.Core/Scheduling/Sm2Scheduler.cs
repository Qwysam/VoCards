using VoCards.Core.Models;

namespace VoCards.Core.Scheduling;

/// <summary>
/// SuperMemo 2, with the learning-step and lapse handling that modern implementations add.
///
/// The card's ease factor rises and falls with performance, and the interval is the
/// previous interval multiplied by that ease. New cards walk a ladder of short steps
/// (1 min, 10 min by default) before graduating onto day-scale intervals.
/// </summary>
public sealed class Sm2Scheduler : IScheduler
{
    /// <summary>Ease never drops below this, or a hard card would be seen forever.</summary>
    public const double MinimumEase = 1.3d;

    /// <summary>Ease never rises above this, or an easy card would vanish.</summary>
    public const double MaximumEase = 3.0d;

    public SchedulerKind Kind => SchedulerKind.Sm2;

    public string DisplayName => "SM-2 (classic)";

    public SchedulingOutcome Schedule(Card card, Rating rating, DeckSettings settings, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(settings);

        // Learning and relearning share the same ladder-walking logic; only the ladder differs.
        return card.State switch
        {
            CardState.New or CardState.Learning =>
                WalkSteps(card, rating, settings, now, settings.LearningStepsMinutes, relearning: false),

            CardState.Relearning =>
                WalkSteps(card, rating, settings, now, settings.RelearningStepsMinutes, relearning: true),

            CardState.Review => ScheduleReview(card, rating, settings, now),

            // A suspended card that somehow gets graded is treated as a review card.
            _ => ScheduleReview(card, rating, settings, now),
        };
    }

    /// <summary>Handles a card still on sub-day steps.</summary>
    private static SchedulingOutcome WalkSteps(
        Card card,
        Rating rating,
        DeckSettings settings,
        DateTimeOffset now,
        IList<double> steps,
        bool relearning)
    {
        // A deck configured with no steps graduates immediately.
        if (steps.Count == 0)
        {
            return Graduate(card, rating, settings, now, relearning);
        }

        int step = Math.Clamp(card.LearningStep, 0, steps.Count - 1);

        switch (rating)
        {
            case Rating.Again:
                // Back to the bottom of the ladder.
                return StepOutcome(card, settings, now, steps, 0, relearning);

            case Rating.Hard:
                // Repeat the current step; if there is a next one, average the two.
                double minutes = step + 1 < steps.Count
                    ? (steps[step] + steps[step + 1]) / 2d
                    : steps[step] * 1.5d;
                return StepOutcome(card, settings, now, steps, step, relearning, overrideMinutes: minutes);

            case Rating.Good when step + 1 < steps.Count:
                return StepOutcome(card, settings, now, steps, step + 1, relearning);

            case Rating.Good:
            case Rating.Easy:
            default:
                return Graduate(card, rating, settings, now, relearning);
        }
    }

    private static SchedulingOutcome StepOutcome(
        Card card,
        DeckSettings settings,
        DateTimeOffset now,
        IList<double> steps,
        int step,
        bool relearning,
        double? overrideMinutes = null)
    {
        double minutes = overrideMinutes ?? steps[step];
        double days = minutes / 1440d;

        return new SchedulingOutcome
        {
            State = relearning ? CardState.Relearning : CardState.Learning,
            DueAt = now.AddMinutes(minutes),
            IntervalDays = days,
            EaseFactor = card.EaseFactor,
            Repetitions = card.Repetitions,
            Lapses = card.Lapses,
            LearningStep = step,
            Stability = card.Stability,
            Difficulty = card.Difficulty,
        };
    }

    /// <summary>Promotes a card off the learning ladder onto a day-scale interval.</summary>
    private static SchedulingOutcome Graduate(
        Card card,
        Rating rating,
        DeckSettings settings,
        DateTimeOffset now,
        bool relearning)
    {
        double baseDays = rating == Rating.Easy
            ? settings.EasyIntervalDays
            : settings.GraduatingIntervalDays;

        // A relearning card keeps some credit for the interval it had before lapsing.
        if (relearning && card.IntervalDays > baseDays)
        {
            baseDays = Math.Max(baseDays, card.IntervalDays * 0.4d);
        }

        double days = Clamp(baseDays * settings.IntervalModifier, settings);

        return new SchedulingOutcome
        {
            State = CardState.Review,
            DueAt = now.AddDays(days),
            IntervalDays = days,
            EaseFactor = card.EaseFactor,
            Repetitions = card.Repetitions + 1,
            Lapses = card.Lapses,
            LearningStep = 0,
            Stability = card.Stability,
            Difficulty = card.Difficulty,
            WasGraduation = true,
        };
    }

    /// <summary>Handles a card already in day-scale review.</summary>
    private static SchedulingOutcome ScheduleReview(
        Card card,
        Rating rating,
        DeckSettings settings,
        DateTimeOffset now)
    {
        double ease = card.EaseFactor;

        if (rating == Rating.Again)
        {
            // A lapse: lose ease, count the lapse, drop back onto the relearning ladder.
            double lapsedEase = ClampEase(ease - 0.20d);
            double relearnMinutes = settings.RelearningStepsMinutes.Count > 0
                ? settings.RelearningStepsMinutes[0]
                : 10d;

            return new SchedulingOutcome
            {
                State = CardState.Relearning,
                DueAt = now.AddMinutes(relearnMinutes),
                IntervalDays = relearnMinutes / 1440d,
                EaseFactor = lapsedEase,
                Repetitions = 0,
                Lapses = card.Lapses + 1,
                LearningStep = 0,
                Stability = card.Stability,
                Difficulty = card.Difficulty,
                WasLapse = true,
            };
        }

        // The interval a card earns is based on how long it actually survived, so a
        // card answered late gets credit for the extra days it was remembered.
        double elapsed = Math.Max(card.IntervalDays, card.IntervalDays + card.DaysOverdue(now));
        double previous = Math.Max(card.IntervalDays, 1d);

        (double multiplier, double easeDelta) = rating switch
        {
            Rating.Hard => (1.2d, -0.15d),
            Rating.Easy => (ease * 1.3d, +0.15d),
            _ => (ease, 0d),
        };

        // Overdue credit only applies when the answer was Good or Easy.
        double basis = rating == Rating.Hard ? previous : Math.Max(previous, elapsed * 0.9d);
        double days = Clamp(basis * multiplier * settings.IntervalModifier, settings);

        // Never let an interval shrink below its previous value on a correct answer.
        days = Math.Max(days, previous + (rating == Rating.Hard ? 0.5d : 1d));
        days = Clamp(days, settings);

        return new SchedulingOutcome
        {
            State = CardState.Review,
            DueAt = now.AddDays(days),
            IntervalDays = days,
            EaseFactor = ClampEase(ease + easeDelta),
            Repetitions = card.Repetitions + 1,
            Lapses = card.Lapses,
            LearningStep = 0,
            Stability = card.Stability,
            Difficulty = card.Difficulty,
        };
    }

    private static double ClampEase(double ease) => Math.Clamp(ease, MinimumEase, MaximumEase);

    private static double Clamp(double days, DeckSettings settings) =>
        Math.Clamp(days, 1d / 1440d, settings.MaximumIntervalDays);
}
