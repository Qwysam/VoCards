namespace VoCards.Web.Components;

/// <summary>
/// What the card editor produces. A plain value carried back to the page, which
/// decides whether it becomes a new card or an edit to an existing one.
/// </summary>
public sealed record CardDraft(
    string Front,
    string Back,
    string Example,
    string Notes,
    string Pronunciation,
    IReadOnlyList<string> Tags);
