using VoCards.Core.Models;

namespace VoCards.Core.Scheduling;

/// <summary>
/// What a scheduler decided for a card, before it is written back. Keeping this
/// separate from <see cref="Card"/> means a scheduler is a pure function: it can be
/// unit-tested and it can be asked "what would happen if I pressed Easy?" without
/// actually mutating anything.
/// </summary>
public sealed record SchedulingOutcome
{
    public required CardState State { get; init; }

    public required DateTimeOffset DueAt { get; init; }

    /// <summary>The new spacing in days. Fractional while on sub-day learning steps.</summary>
    public required double IntervalDays { get; init; }

    public double EaseFactor { get; init; } = 2.5d;

    public int Repetitions { get; init; }

    public int Lapses { get; init; }

    public int LearningStep { get; init; }

    public double Stability { get; init; }

    public double Difficulty { get; init; }

    /// <summary>True when this review knocked a graduated card back into relearning.</summary>
    public bool WasLapse { get; init; }

    /// <summary>True when this review promoted a card out of learning for the first time.</summary>
    public bool WasGraduation { get; init; }

    /// <summary>A short human-readable interval, e.g. "10 min", "3 d", "2.4 mo".</summary>
    public string IntervalLabel => FormatInterval(IntervalDays);

    /// <summary>Renders a day count the way a study UI wants to show it on a rating button.</summary>
    public static string FormatInterval(double days) => days switch
    {
        < 0d => "—",
        < 1d / 1440d => "<1 min",
        < 1d / 24d => $"{Math.Round(days * 1440d):0} min",
        < 1d => $"{Math.Round(days * 24d):0} h",
        < 30d => $"{Math.Round(days, days < 10d ? 1 : 0):0.#} d",
        < 365d => $"{Math.Round(days / 30.4375d, 1):0.#} mo",
        _ => $"{Math.Round(days / 365.25d, 1):0.#} y",
    };
}
