using VoCards.Core.Models;

namespace VoCards.Core.Scheduling;

/// <summary>
/// FSRS — the Free Spaced Repetition Scheduler, v4.5 formulation.
///
/// Where SM-2 tracks a single "ease" number, FSRS models memory with three quantities:
/// <list type="bullet">
///   <item><b>Stability</b> — how many days until recall probability falls to 90%.</item>
///   <item><b>Difficulty</b> — how resistant this particular card is, on a 1–10 scale.</item>
///   <item><b>Retrievability</b> — the probability of recall right now, derived from the
///   first two plus elapsed time.</item>
/// </list>
/// The interval is then solved directly for the learner's desired retention, rather
/// than being a fixed multiple of the last interval.
/// </summary>
public sealed class FsrsScheduler : IScheduler
{
    /// <summary>Power-law forgetting exponent.</summary>
    private const double Decay = -0.5d;

    /// <summary>Chosen so that R(t = S) == 0.9 exactly.</summary>
    private const double Factor = 19d / 81d;

    /// <summary>
    /// The 17 default FSRS-4.5 weights, fitted against a large public review dataset.
    /// Indices 0–3 are the initial stability for Again/Hard/Good/Easy.
    /// </summary>
    private static readonly double[] DefaultWeights =
    [
        0.4872, 1.4003, 3.7145, 13.8206,  // w0–w3  initial stability per rating
        5.1618, 1.2298,                   // w4–w5  initial difficulty
        0.8975, 0.0310,                   // w6–w7  difficulty update + mean reversion
        1.6474, 0.1367, 1.0461,           // w8–w10 stability growth on success
        2.1072, 0.0793, 0.3246, 1.5870,   // w11–w14 stability after a lapse
        0.2272, 2.8755,                   // w15–w16 hard penalty, easy bonus
    ];

    private readonly double[] _w;

    public FsrsScheduler()
        : this(DefaultWeights)
    {
    }

    /// <summary>Allows a caller to supply weights fitted to their own review history.</summary>
    public FsrsScheduler(IReadOnlyList<double> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        if (weights.Count != DefaultWeights.Length)
        {
            throw new ArgumentException(
                $"FSRS expects exactly {DefaultWeights.Length} weights, got {weights.Count}.",
                nameof(weights));
        }

        _w = [.. weights];
    }

    public SchedulerKind Kind => SchedulerKind.Fsrs;

    public string DisplayName => "FSRS (adaptive)";

    /// <summary>The default weight vector, exposed for the settings UI and for tests.</summary>
    public static IReadOnlyList<double> Weights => DefaultWeights;

    public SchedulingOutcome Schedule(Card card, Rating rating, DeckSettings settings, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(settings);

        bool firstEverReview = card.State == CardState.New || card.Stability <= 0d;

        (double stability, double difficulty) = firstEverReview
            ? InitialMemoryState(rating)
            : UpdatedMemoryState(card, rating, now);

        // Again always routes back through relearning, whatever the memory state says.
        if (rating == Rating.Again)
        {
            double relearnMinutes = settings.RelearningStepsMinutes.Count > 0
                ? settings.RelearningStepsMinutes[0]
                : 10d;

            return new SchedulingOutcome
            {
                State = firstEverReview ? CardState.Learning : CardState.Relearning,
                DueAt = now.AddMinutes(relearnMinutes),
                IntervalDays = relearnMinutes / 1440d,
                EaseFactor = card.EaseFactor,
                Repetitions = 0,
                Lapses = firstEverReview ? card.Lapses : card.Lapses + 1,
                LearningStep = 0,
                Stability = stability,
                Difficulty = difficulty,
                WasLapse = !firstEverReview,
            };
        }

        double days = Math.Clamp(
            IntervalForRetention(stability, settings.DesiredRetention) * settings.IntervalModifier,
            1d / 1440d,
            settings.MaximumIntervalDays);

        // A brand-new card still respects the deck's graduating interval as a floor,
        // so FSRS and SM-2 decks feel consistent on day one.
        if (firstEverReview)
        {
            double floor = rating == Rating.Easy ? settings.EasyIntervalDays : settings.GraduatingIntervalDays;
            days = Math.Max(days, Math.Min(floor, settings.MaximumIntervalDays));
        }

        return new SchedulingOutcome
        {
            State = CardState.Review,
            DueAt = now.AddDays(days),
            IntervalDays = days,
            EaseFactor = card.EaseFactor,
            Repetitions = card.Repetitions + 1,
            Lapses = card.Lapses,
            LearningStep = 0,
            Stability = stability,
            Difficulty = difficulty,
            WasGraduation = card.State is CardState.New or CardState.Learning or CardState.Relearning,
        };
    }

    /// <summary>Memory state for a card being answered for the very first time.</summary>
    private (double Stability, double Difficulty) InitialMemoryState(Rating rating)
    {
        double stability = Math.Max(_w[(int)rating - 1], 0.1d);
        double difficulty = ClampDifficulty(_w[4] - (_w[5] * ((int)rating - 3)));
        return (stability, difficulty);
    }

    /// <summary>Memory state for a card with review history.</summary>
    private (double Stability, double Difficulty) UpdatedMemoryState(Card card, Rating rating, DateTimeOffset now)
    {
        double elapsedDays = card.LastReviewedAt is { } last
            ? Math.Max((now - last).TotalDays, 0d)
            : Math.Max(card.IntervalDays, 0d);

        double retrievability = Retrievability(elapsedDays, card.Stability);
        double difficulty = NextDifficulty(card.Difficulty <= 0d ? _w[4] : card.Difficulty, rating);

        double stability = rating == Rating.Again
            ? StabilityAfterLapse(card.Stability, difficulty, retrievability)
            : StabilityAfterRecall(card.Stability, difficulty, retrievability, rating);

        return (Math.Max(stability, 0.1d), difficulty);
    }

    /// <summary>Probability of recalling a card with stability <paramref name="stability"/> after <paramref name="elapsedDays"/>.</summary>
    public static double Retrievability(double elapsedDays, double stability)
    {
        if (stability <= 0d)
        {
            return 0d;
        }

        return Math.Pow(1d + (Factor * elapsedDays / stability), Decay);
    }

    /// <summary>Solves the forgetting curve for the interval that lands on the target retention.</summary>
    public static double IntervalForRetention(double stability, double desiredRetention)
    {
        double retention = Math.Clamp(desiredRetention, 0.70d, 0.99d);
        return stability / Factor * (Math.Pow(retention, 1d / Decay) - 1d);
    }

    private double NextDifficulty(double difficulty, Rating rating)
    {
        // Linear damping towards the "Good" anchor, then mean-reversion to the easy baseline.
        double updated = difficulty - (_w[6] * ((int)rating - 3));
        double anchor = _w[4] - (_w[5] * (4 - 3)); // difficulty a fresh "Easy" card would get
        double reverted = (_w[7] * anchor) + ((1d - _w[7]) * updated);
        return ClampDifficulty(reverted);
    }

    private double StabilityAfterRecall(double stability, double difficulty, double retrievability, Rating rating)
    {
        double hardPenalty = rating == Rating.Hard ? _w[15] : 1d;
        double easyBonus = rating == Rating.Easy ? _w[16] : 1d;
        double safeStability = Math.Max(stability, 0.1d);

        double growth = Math.Exp(_w[8])
            * (11d - difficulty)
            * Math.Pow(safeStability, -_w[9])
            * (Math.Exp((1d - retrievability) * _w[10]) - 1d)
            * hardPenalty
            * easyBonus;

        return safeStability * (1d + growth);
    }

    private double StabilityAfterLapse(double stability, double difficulty, double retrievability)
    {
        double safeStability = Math.Max(stability, 0.1d);

        double lapsed = _w[11]
            * Math.Pow(difficulty, -_w[12])
            * (Math.Pow(safeStability + 1d, _w[13]) - 1d)
            * Math.Exp((1d - retrievability) * _w[14]);

        // A lapse must never increase stability.
        return Math.Min(lapsed, safeStability);
    }

    private static double ClampDifficulty(double difficulty) => Math.Clamp(difficulty, 1d, 10d);
}
