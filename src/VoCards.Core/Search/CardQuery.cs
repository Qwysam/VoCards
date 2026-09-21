using VoCards.Core.Common;
using VoCards.Core.Models;

namespace VoCards.Core.Search;

/// <summary>How to sort search results.</summary>
public enum CardSort
{
    Relevance,
    FrontAsc,
    FrontDesc,
    RecentlyAdded,
    RecentlyModified,
    DueSoonest,
    HardestFirst,
    MostLapses,
}

/// <summary>
/// A filter over the library. Every field is optional; an empty query matches
/// everything. Combined as AND across fields, OR within a field's values.
/// </summary>
public sealed record CardQuery
{
    /// <summary>Free text matched against front, back, example, notes and tags.</summary>
    public string? Text { get; init; }

    /// <summary>Restrict to these decks. Empty means all decks.</summary>
    public IReadOnlyList<Guid> DeckIds { get; init; } = [];

    /// <summary>Card must carry every one of these tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Restrict to these lifecycle states. Empty means all states.</summary>
    public IReadOnlyList<CardState> States { get; init; } = [];

    public bool? Starred { get; init; }

    public bool? Leech { get; init; }

    /// <summary>Only cards due at or before this moment.</summary>
    public DateTimeOffset? DueBefore { get; init; }

    /// <summary>Only cards whose accuracy is at or below this, 0..1.</summary>
    public double? MaxAccuracy { get; init; }

    /// <summary>Only cards reviewed at least this many times.</summary>
    public int? MinReviews { get; init; }

    public CardSort Sort { get; init; } = CardSort.Relevance;

    public int? Take { get; init; }

    /// <summary>True when this query would match everything.</summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Text)
        && DeckIds.Count == 0
        && Tags.Count == 0
        && States.Count == 0
        && Starred is null
        && Leech is null
        && DueBefore is null
        && MaxAccuracy is null
        && MinReviews is null;
}

/// <summary>One hit, carrying the deck it came from so the UI can show context.</summary>
public sealed record CardHit
{
    public required Card Card { get; init; }

    public required Deck Deck { get; init; }

    /// <summary>Higher is a better match. Zero when the query had no text.</summary>
    public double Score { get; init; }
}

/// <summary>Runs a <see cref="CardQuery"/> over a library.</summary>
public static class CardSearch
{
    /// <summary>Finds every card matching the query, sorted as asked.</summary>
    public static IReadOnlyList<CardHit> Run(Library library, CardQuery query)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(query);

        var deckFilter = query.DeckIds.Count == 0 ? null : query.DeckIds.ToHashSet();
        var stateFilter = query.States.Count == 0 ? null : query.States.ToHashSet();
        string? needle = string.IsNullOrWhiteSpace(query.Text) ? null : TextTools.Normalize(query.Text);

        var hits = new List<CardHit>();

        foreach (Deck deck in library.Decks)
        {
            if (deckFilter is not null && !deckFilter.Contains(deck.Id))
            {
                continue;
            }

            foreach (Card card in deck.Cards)
            {
                if (!Matches(card, query, stateFilter))
                {
                    continue;
                }

                double score = needle is null ? 0d : Score(card, needle);
                if (needle is not null && score <= 0d)
                {
                    continue;
                }

                hits.Add(new CardHit { Card = card, Deck = deck, Score = score });
            }
        }

        var sorted = SortHits(hits, query.Sort);

        return query.Take is { } take && take > 0
            ? sorted.Take(take).ToArray()
            : sorted.ToArray();
    }

    /// <summary>
    /// Every non-text predicate, as a flat run of guard clauses. Adding a filter means
    /// adding one clause here, not another nesting level.
    /// </summary>
    private static bool Matches(Card card, CardQuery query, HashSet<CardState>? states)
    {
        if (states is not null && !states.Contains(card.State))
        {
            return false;
        }

        if (query.Starred is { } starred && card.IsStarred != starred)
        {
            return false;
        }

        if (query.Leech is { } leech && card.IsLeech != leech)
        {
            return false;
        }

        if (query.DueBefore is { } before && !card.IsDue(before))
        {
            return false;
        }

        if (query.MinReviews is { } minReviews && card.ReviewCount < minReviews)
        {
            return false;
        }

        if (query.MaxAccuracy is { } maxAccuracy && card.ReviewCount > 0 && card.Accuracy > maxAccuracy)
        {
            return false;
        }

        foreach (string tag in query.Tags)
        {
            if (!card.HasTag(tag))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Relevance scoring: an exact front match beats a prefix, which beats a
    /// substring, which beats a hit in the notes.
    /// </summary>
    private static double Score(Card card, string needle)
    {
        string front = TextTools.Normalize(card.Front);
        string back = TextTools.Normalize(card.Back);

        if (string.Equals(front, needle, StringComparison.Ordinal))
        {
            return 100d;
        }

        if (string.Equals(back, needle, StringComparison.Ordinal))
        {
            return 90d;
        }

        if (front.StartsWith(needle, StringComparison.Ordinal))
        {
            return 80d;
        }

        if (back.StartsWith(needle, StringComparison.Ordinal))
        {
            return 70d;
        }

        if (front.Contains(needle, StringComparison.Ordinal))
        {
            return 60d;
        }

        if (back.Contains(needle, StringComparison.Ordinal))
        {
            return 50d;
        }

        if (card.Tags.Any(tag => TextTools.ContainsLoose(tag, needle)))
        {
            return 40d;
        }

        if (TextTools.ContainsLoose(card.Example, needle))
        {
            return 30d;
        }

        if (TextTools.ContainsLoose(card.Notes, needle))
        {
            return 20d;
        }

        // A close-but-not-exact front match still counts, so typos in the search box work.
        double similarity = TextTools.Similarity(front, needle);
        return similarity >= 0.8d ? similarity * 10d : 0d;
    }

    private static IEnumerable<CardHit> SortHits(List<CardHit> hits, CardSort sort) => sort switch
    {
        CardSort.FrontAsc => hits.OrderBy(static h => h.Card.Front, StringComparer.OrdinalIgnoreCase),
        CardSort.FrontDesc => hits.OrderByDescending(static h => h.Card.Front, StringComparer.OrdinalIgnoreCase),
        CardSort.RecentlyAdded => hits.OrderByDescending(static h => h.Card.CreatedAt),
        CardSort.RecentlyModified => hits.OrderByDescending(static h => h.Card.ModifiedAt),
        CardSort.DueSoonest => hits
            .OrderBy(static h => h.Card.DueAt ?? DateTimeOffset.MaxValue)
            .ThenBy(static h => h.Card.Front, StringComparer.OrdinalIgnoreCase),
        CardSort.HardestFirst => hits.OrderBy(static h => h.Card.Mastery),
        CardSort.MostLapses => hits.OrderByDescending(static h => h.Card.Lapses),
        _ => hits
            .OrderByDescending(static h => h.Score)
            .ThenBy(static h => h.Card.Front, StringComparer.OrdinalIgnoreCase),
    };

    /// <summary>
    /// Suggestions for the command palette: decks and cards whose text starts with or
    /// contains what has been typed so far.
    /// </summary>
    public static IReadOnlyList<CardHit> Suggest(Library library, string text, int take = 8)
    {
        ArgumentNullException.ThrowIfNull(library);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return Run(library, new CardQuery { Text = text, Sort = CardSort.Relevance, Take = take });
    }
}
