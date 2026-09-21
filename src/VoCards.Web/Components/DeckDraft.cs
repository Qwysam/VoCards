namespace VoCards.Web.Components;

/// <summary>What the deck editor produces, for the page to apply.</summary>
public sealed record DeckDraft(
    string Name,
    string Description,
    string Emoji,
    string Color,
    string FrontLanguage,
    string BackLanguage);
