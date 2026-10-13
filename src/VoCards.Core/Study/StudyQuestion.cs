using VoCards.Core.Models;

namespace VoCards.Core.Study;

/// <summary>
/// One question as the UI needs it: the prompt, the answer, and — for the choice-based
/// modes — the shuffled options. Built once when the card is presented so that the
/// distractors do not reshuffle on every re-render.
/// </summary>
public sealed record StudyQuestion
{
    public required Card Card { get; init; }

    public required Deck Deck { get; init; }

    public required StudyDirection Direction { get; init; }

    public required StudyMode Mode { get; init; }

    /// <summary>Zero-based position in the queue.</summary>
    public required int Index { get; init; }

    /// <summary>Total cards in the queue.</summary>
    public required int Total { get; init; }

    /// <summary>Options for multiple-choice and speed-round modes, already shuffled.</summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>Index into <see cref="Choices"/> holding the correct answer, or -1.</summary>
    public int CorrectChoiceIndex { get; init; } = -1;

    public string Prompt => Card.PromptText(Direction);

    public string Answer => Card.AnswerText(Direction);

    /// <summary>BCP-47 tag for speaking the prompt.</summary>
    public string PromptLanguage => Deck.LanguageFor(Direction);

    /// <summary>BCP-47 tag for speaking the answer.</summary>
    public string AnswerLanguage => Direction == StudyDirection.BackToFront
        ? Deck.FrontLanguage
        : Deck.BackLanguage;

    /// <summary>One-based position, for "3 of 20" style labels.</summary>
    public int Position => Index + 1;

    /// <summary>Progress through the queue, 0..1.</summary>
    public double Progress => Total == 0 ? 1d : (double)Index / Total;
}
