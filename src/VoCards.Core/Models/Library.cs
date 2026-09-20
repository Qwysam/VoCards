using VoCards.Core.Common;

namespace VoCards.Core.Models;

/// <summary>
/// The aggregate root: every deck, the review history and the learner's profile.
///
/// This is the successor to the 2021 <c>Progress</c> class. The differences that matter:
/// lookups are dictionary-backed rather than full linear scans that never early-exit,
/// all totals are computed rather than stored in hand-maintained counters, and nothing
/// here writes to the console.
/// </summary>
public sealed class Library
{
    private readonly List<Deck> _decks = [];
    private readonly Dictionary<Guid, Deck> _byId = [];

    /// <summary>Schema version, bumped when the persisted shape changes.</summary>
    public const int SchemaVersion = 1;

    public UserProfile Profile { get; init; } = new();

    public ReviewLog History { get; init; } = new();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    /// <summary>Decks in display order: pinned first, then by name.</summary>
    public IReadOnlyList<Deck> Decks => _decks;

    public int DeckCount => _decks.Count;

    public int TotalCards => _decks.Sum(static d => d.TotalCards);

    /// <summary>
    /// Cards that have graduated and reached a three-week interval. The honest
    /// replacement for the old <c>words_learnt</c> field.
    /// </summary>
    public int MaturedCards => _decks.Sum(static d => d.MaturedCards);

    public int NewCards => _decks.Sum(static d => d.NewCards);

    public int SuspendedCards => _decks.Sum(static d => d.SuspendedCards);

    public int LeechCards => _decks.Sum(static d => d.LeechCards);

    public int StarredCards => _decks.Sum(static d => d.StarredCards);

    /// <summary>Share of the whole library that has matured, 0..1.</summary>
    public double OverallMastery
    {
        get
        {
            int total = TotalCards;
            return total == 0 ? 0d : _decks.Sum(static d => d.Mastery * d.TotalCards) / total;
        }
    }

    /// <summary>Every tag in use anywhere, alphabetically.</summary>
    public IReadOnlyList<string> AllTags => _decks
        .SelectMany(static d => d.Cards)
        .SelectMany(static c => c.Tags)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(static t => t, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>Every card in the library, flattened.</summary>
    public IEnumerable<Card> AllCards => _decks.SelectMany(static d => d.Cards);

    // ---------------------------------------------------------------- lookup

    /// <summary>O(1) lookup by deck id.</summary>
    public Deck? FindDeck(Guid id) => _byId.GetValueOrDefault(id);

    /// <summary>
    /// Case-insensitive lookup by name. The 2021 <c>FindByTopic</c> scanned every deck
    /// even after finding a match, and returned -1 that the caller then used as an index.
    /// </summary>
    public Deck? FindDeckByName(string name) =>
        _decks.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));

    public bool HasDeckNamed(string name) => FindDeckByName(name) is not null;

    /// <summary>Finds the card with this id, and the deck holding it.</summary>
    public (Deck Deck, Card Card)? LocateCard(Guid cardId)
    {
        foreach (Deck deck in _decks)
        {
            if (deck.FindCard(cardId) is { } card)
            {
                return (deck, card);
            }
        }

        return null;
    }

    public Deck this[int index] => _decks[index];

    // ---------------------------------------------------------------- deck mutation

    public Result<Deck> CreateDeck(string name, string? description = null,
                                   string? emoji = null, string? colorToken = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Deck>("A deck needs a name.");
        }

        string clean = name.Trim();
        if (HasDeckNamed(clean))
        {
            return Result.Failure<Deck>($"A deck called “{clean}” already exists.");
        }

        var deck = new Deck(clean);
        deck.Describe(description);
        deck.SetAppearance(emoji, colorToken);
        AddDeck(deck);
        return Result.Success(deck);
    }

    public void AddDeck(Deck deck)
    {
        if (_byId.ContainsKey(deck.Id))
        {
            return;
        }

        _decks.Add(deck);
        _byId[deck.Id] = deck;
        Sort();
    }

    public bool RemoveDeck(Guid id)
    {
        if (!_byId.Remove(id, out Deck? deck))
        {
            return false;
        }

        _decks.Remove(deck);
        History.Prune(AllCards.Select(static c => c.Id).ToHashSet());
        return true;
    }

    public Result<Deck> DuplicateDeck(Guid id)
    {
        if (FindDeck(id) is not { } source)
        {
            return Result.Failure<Deck>("That deck no longer exists.");
        }

        string name = UniqueName($"{source.Name} (copy)");
        Deck copy = source.Duplicate(name);
        AddDeck(copy);
        return Result.Success(copy);
    }

    public Result RenameDeck(Guid id, string name)
    {
        if (FindDeck(id) is not { } deck)
        {
            return Result.Failure("That deck no longer exists.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("A deck needs a name.");
        }

        string clean = name.Trim();
        if (FindDeckByName(clean) is { } clash && clash.Id != id)
        {
            return Result.Failure($"A deck called “{clean}” already exists.");
        }

        Result result = deck.Rename(clean);
        Sort();
        return result;
    }

    /// <summary>Moves a card between decks, keeping its scheduling state intact.</summary>
    public Result MoveCard(Guid cardId, Guid toDeckId)
    {
        if (LocateCard(cardId) is not { } located)
        {
            return Result.Failure("That card no longer exists.");
        }

        (Deck from, Card card) = located;

        if (from.Id == toDeckId)
        {
            return Result.Success();
        }

        if (FindDeck(toDeckId) is not { } target)
        {
            return Result.Failure("The destination deck no longer exists.");
        }

        from.RemoveCard(cardId);
        target.AddCard(card);
        return Result.Success();
    }

    /// <summary>Appends a numeric suffix until the name is free.</summary>
    public string UniqueName(string desired)
    {
        string candidate = desired.Trim();
        if (!HasDeckNamed(candidate))
        {
            return candidate;
        }

        for (int n = 2; n < 1000; n++)
        {
            string next = $"{candidate} {n}";
            if (!HasDeckNamed(next))
            {
                return next;
            }
        }

        return $"{candidate} {Guid.NewGuid():N}";
    }

    /// <summary>Re-sorts decks: pinned first, then alphabetically.</summary>
    public void Sort()
    {
        _decks.Sort(static (a, b) => a.IsPinned != b.IsPinned
            ? b.IsPinned.CompareTo(a.IsPinned)
            : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    }

    // ---------------------------------------------------------------- aggregate queries

    /// <summary>Total cards due across every deck at <paramref name="now"/>.</summary>
    public int DueCount(DateTimeOffset now) => _decks.Sum(d => d.DueCount(now));

    /// <summary>Decks that have at least one card waiting.</summary>
    public IEnumerable<Deck> DecksWithWork(DateTimeOffset now) => _decks.Where(d => d.DueCount(now) > 0);

    /// <summary>The soonest moment any card becomes due.</summary>
    public DateTimeOffset? NextDueAt(DateTimeOffset now) => _decks
        .Select(d => d.NextDueAt(now))
        .Where(static d => d is not null)
        .Select(static d => d!.Value)
        .DefaultIfEmpty()
        .Min() is { } min && min != default ? min : null;

    /// <summary>Drops every deck and all history. Used by "reset all data".</summary>
    public void Clear()
    {
        _decks.Clear();
        _byId.Clear();
        History.Clear();
    }
}
