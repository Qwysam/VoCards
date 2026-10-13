using VoCards.Core.Models;

namespace VoCards.Core.Study;

/// <summary>
/// Everything the learner can choose before a session starts.
///
/// The 2021 app asked exactly one question here — "how many cards?" — in a
/// <c>for(;;)</c> loop that re-asked until the number was in range.
/// </summary>
public sealed record StudyOptions
{
    public StudyMode Mode { get; init; } = StudyMode.Flip;

    public StudyDirection Direction { get; init; } = StudyDirection.FrontToBack;

    /// <summary>Cap on the session length. Null means "everything available".</summary>
    public int? MaxCards { get; init; }

    public bool IncludeNew { get; init; } = true;

    public bool IncludeLearning { get; init; } = true;

    public bool IncludeReview { get; init; } = true;

    /// <summary>
    /// Cram mode: ignore due dates and pull from the whole deck, and do not write the
    /// results back to the schedule. For the night before an exam.
    /// </summary>
    public bool Cram { get; init; }

    public QueueOrder Order { get; init; } = QueueOrder.DueFirst;

    /// <summary>Restrict to cards carrying this tag.</summary>
    public string? Tag { get; init; }

    /// <summary>Restrict to starred cards.</summary>
    public bool StarredOnly { get; init; }

    /// <summary>Restrict to cards flagged as leeches, for a targeted repair session.</summary>
    public bool LeechesOnly { get; init; }

    /// <summary>Seed for shuffling. Fixing it makes a session reproducible in tests.</summary>
    public int? Seed { get; init; }

    /// <summary>Seconds allowed per card in <see cref="StudyMode.SpeedRound"/>.</summary>
    public int SpeedRoundSeconds { get; init; } = 10;

    /// <summary>Number of options shown in multiple-choice modes, including the right one.</summary>
    public int ChoiceCount { get; init; } = 4;

    /// <summary>The defaults a deck's own settings imply.</summary>
    public static StudyOptions FromDeck(Deck deck)
    {
        ArgumentNullException.ThrowIfNull(deck);

        return new StudyOptions
        {
            Mode = deck.Settings.DefaultMode,
            Direction = deck.Settings.Direction,
            Order = deck.Settings.Order,
        };
    }
}
