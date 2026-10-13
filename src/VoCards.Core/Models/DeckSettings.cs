namespace VoCards.Core.Models;

/// <summary>
/// Per-deck study configuration. The 2021 app had exactly one knob — "how many cards
/// do you want to go through" — asked on every single session.
/// </summary>
public sealed class DeckSettings
{
    /// <summary>Which algorithm schedules this deck.</summary>
    public SchedulerKind Scheduler { get; set; } = SchedulerKind.Sm2;

    /// <summary>Default prompt direction for this deck.</summary>
    public StudyDirection Direction { get; set; } = StudyDirection.FrontToBack;

    /// <summary>Default mode when the learner hits "Study".</summary>
    public StudyMode DefaultMode { get; set; } = StudyMode.Flip;

    /// <summary>Ceiling on brand-new cards introduced per day. Keeps tomorrow survivable.</summary>
    public int NewCardsPerDay { get; set; } = 20;

    /// <summary>Ceiling on review cards per day.</summary>
    public int MaxReviewsPerDay { get; set; } = 200;

    /// <summary>Ordering of the built queue.</summary>
    public QueueOrder Order { get; set; } = QueueOrder.DueFirst;

    /// <summary>
    /// Sub-day learning steps, in minutes, walked before a new card graduates.
    /// </summary>
    public IList<double> LearningStepsMinutes { get; set; } = [1d, 10d];

    /// <summary>Steps walked after a lapse, in minutes.</summary>
    public IList<double> RelearningStepsMinutes { get; set; } = [10d];

    /// <summary>Interval, in days, granted when a card graduates on Good.</summary>
    public double GraduatingIntervalDays { get; set; } = 1d;

    /// <summary>Interval, in days, granted when a card graduates on Easy.</summary>
    public double EasyIntervalDays { get; set; } = 4d;

    /// <summary>Hard ceiling on any interval, so cards do not vanish for a decade.</summary>
    public double MaximumIntervalDays { get; set; } = 365d * 5d;

    /// <summary>Global multiplier on computed intervals. Below 1 means more frequent review.</summary>
    public double IntervalModifier { get; set; } = 1d;

    /// <summary>Target recall probability used by the FSRS scheduler.</summary>
    public double DesiredRetention { get; set; } = 0.9d;

    /// <summary>Speak the prompt automatically in modes that support it.</summary>
    public bool AutoSpeak { get; set; }

    /// <summary>Typing mode: fold accents before comparing.</summary>
    public bool IgnoreAccents { get; set; } = true;

    /// <summary>Typing mode: ignore letter case.</summary>
    public bool IgnoreCase { get; set; } = true;

    /// <summary>Typing mode: how close a near-miss must be to count, 0..1.</summary>
    public double TypoTolerance { get; set; } = 0.85d;

    /// <summary>A deep copy, used when a deck is duplicated.</summary>
    public DeckSettings Clone() => new()
    {
        Scheduler = Scheduler,
        Direction = Direction,
        DefaultMode = DefaultMode,
        NewCardsPerDay = NewCardsPerDay,
        MaxReviewsPerDay = MaxReviewsPerDay,
        Order = Order,
        LearningStepsMinutes = [.. LearningStepsMinutes],
        RelearningStepsMinutes = [.. RelearningStepsMinutes],
        GraduatingIntervalDays = GraduatingIntervalDays,
        EasyIntervalDays = EasyIntervalDays,
        MaximumIntervalDays = MaximumIntervalDays,
        IntervalModifier = IntervalModifier,
        DesiredRetention = DesiredRetention,
        AutoSpeak = AutoSpeak,
        IgnoreAccents = IgnoreAccents,
        IgnoreCase = IgnoreCase,
        TypoTolerance = TypoTolerance,
    };
}
