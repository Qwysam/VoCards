using VoCards.Core.Common;

namespace VoCards.Core.Models;

/// <summary>
/// A themed set of cards.
///
/// Replaces the 2021 <c>Deck</c>, which kept <c>words_total_deck</c> and
/// <c>words_learnt_deck</c> as fields incremented by hand. Those two counters
/// desynchronised from the list on any removal path; here every count is derived.
/// </summary>
public sealed class Deck
{
    private readonly List<Card> _cards = [];
    private readonly Dictionary<Guid, Card> _index = [];

    public Deck(string name)
    {
        Name = name.Trim();
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; private set; }

    public string? Description { get; private set; }

    /// <summary>A single emoji used as the deck's glyph in the UI.</summary>
    public string Emoji { get; private set; } = "📚";

    /// <summary>Accent token (e.g. "violet") that the UI maps to a colour ramp.</summary>
    public string ColorToken { get; private set; } = "violet";

    /// <summary>BCP-47 tag for the front face, used to pick a speech-synthesis voice.</summary>
    public string FrontLanguage { get; private set; } = "en-US";

    /// <summary>BCP-47 tag for the back face.</summary>
    public string BackLanguage { get; private set; } = "en-US";

    public DeckSettings Settings { get; init; } = new();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset ModifiedAt { get; private set; } = DateTimeOffset.Now;

    /// <summary>Whether this deck is pinned to the top of the deck grid.</summary>
    public bool IsPinned { get; private set; }

    /// <summary>Cards in insertion order. Read-only: mutate through the deck's methods.</summary>
    public IReadOnlyList<Card> Cards => _cards;

    // ---------------------------------------------------------------- derived counts

    public int TotalCards => _cards.Count;

    public int NewCards => CountWhere(static c => c.State == CardState.New);

    public int LearningCards => CountWhere(static c => c.State is CardState.Learning or CardState.Relearning);

    public int ReviewCards => CountWhere(static c => c.State == CardState.Review);

    public int SuspendedCards => CountWhere(static c => c.State == CardState.Suspended);

    public int StarredCards => CountWhere(static c => c.IsStarred);

    public int LeechCards => CountWhere(static c => c.IsLeech);

    /// <summary>
    /// Cards considered "learnt": graduated and spaced at least three weeks out.
    /// This is the honest successor to the old <c>words_learnt_deck</c> counter.
    /// </summary>
    public int MaturedCards => CountWhere(static c => c.IsGraduated && c.IntervalDays >= 21d);

    /// <summary>Distinct tags used anywhere in this deck, alphabetically.</summary>
    public IReadOnlyList<string> Tags => _cards
        .SelectMany(static c => c.Tags)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(static t => t, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>Average mastery across every non-suspended card, 0..1.</summary>
    public double Mastery
    {
        get
        {
            var active = _cards.Where(static c => c.State != CardState.Suspended).ToArray();
            return active.Length == 0 ? 0d : active.Average(static c => c.Mastery);
        }
    }

    public int DueCount(DateTimeOffset now) => CountWhere(c => c.IsDue(now));

    /// <summary>The earliest upcoming due time, ignoring cards that are already due.</summary>
    public DateTimeOffset? NextDueAt(DateTimeOffset now) => _cards
        .Where(c => c.State != CardState.Suspended && c.DueAt is { } due && due > now)
        .Select(static c => c.DueAt!.Value)
        .DefaultIfEmpty()
        .Min() is { } min && min != default ? min : null;

    private int CountWhere(Func<Card, bool> predicate)
    {
        int count = 0;
        foreach (Card card in _cards)
        {
            if (predicate(card))
            {
                count++;
            }
        }

        return count;
    }

    // ---------------------------------------------------------------- lookup

    /// <summary>
    /// O(1) lookup by id. The 2021 code scanned the whole list for every lookup and
    /// did not even break out of the loop on a hit.
    /// </summary>
    public Card? FindCard(Guid id) => _index.GetValueOrDefault(id);

    public bool Contains(Guid id) => _index.ContainsKey(id);

    /// <summary>Indexer by position, preserved from the original API shape.</summary>
    public Card this[int index] => _cards[index];

    // ---------------------------------------------------------------- mutation

    public Result<Card> AddCard(string front, string back)
    {
        if (string.IsNullOrWhiteSpace(front))
        {
            return Result.Failure<Card>("The front of a card cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(back))
        {
            return Result.Failure<Card>("The back of a card cannot be empty.");
        }

        var card = new Card(front, back);
        AddCard(card);
        return Result.Success(card);
    }

    public void AddCard(Card card)
    {
        if (_index.ContainsKey(card.Id))
        {
            return;
        }

        _cards.Add(card);
        _index[card.Id] = card;
        Touch();
    }

    public void AddCards(IEnumerable<Card> cards)
    {
        foreach (Card card in cards)
        {
            AddCard(card);
        }
    }

    /// <summary>
    /// Removes a card. Returns false when the id is unknown — the 2021 version threw
    /// <c>ArgumentOutOfRangeException</c> at the console instead.
    /// </summary>
    public bool RemoveCard(Guid id)
    {
        if (!_index.Remove(id, out Card? card))
        {
            return false;
        }

        _cards.Remove(card);
        Touch();
        return true;
    }

    public int RemoveCards(IEnumerable<Guid> ids)
    {
        int removed = 0;
        foreach (Guid id in ids)
        {
            if (RemoveCard(id))
            {
                removed++;
            }
        }

        return removed;
    }

    /// <summary>Moves a card to a new position, for manual ordering in the browser.</summary>
    public bool MoveCard(Guid id, int newIndex)
    {
        int current = _cards.FindIndex(c => c.Id == id);
        if (current < 0 || newIndex < 0 || newIndex >= _cards.Count)
        {
            return false;
        }

        Card card = _cards[current];
        _cards.RemoveAt(current);
        _cards.Insert(newIndex, card);
        Touch();
        return true;
    }

    public Result Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("A deck needs a name.");
        }

        Name = name.Trim();
        Touch();
        return Result.Success();
    }

    public void Describe(string? description)
    {
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Touch();
    }

    public void SetAppearance(string? emoji = null, string? colorToken = null)
    {
        if (!string.IsNullOrWhiteSpace(emoji))
        {
            Emoji = emoji.Trim();
        }

        if (!string.IsNullOrWhiteSpace(colorToken))
        {
            ColorToken = colorToken.Trim();
        }

        Touch();
    }

    public void SetLanguages(string? front = null, string? back = null)
    {
        if (!string.IsNullOrWhiteSpace(front))
        {
            FrontLanguage = front.Trim();
        }

        if (!string.IsNullOrWhiteSpace(back))
        {
            BackLanguage = back.Trim();
        }

        Touch();
    }

    /// <summary>The BCP-47 tag to speak a given face in.</summary>
    public string LanguageFor(StudyDirection direction) =>
        direction == StudyDirection.BackToFront ? BackLanguage : FrontLanguage;

    public void Pin(bool pinned = true)
    {
        IsPinned = pinned;
        Touch();
    }

    public void TogglePin() => Pin(!IsPinned);

    /// <summary>Resets scheduling on every card, keeping the content.</summary>
    public void ResetProgress()
    {
        foreach (Card card in _cards)
        {
            card.ResetProgress();
        }

        Touch();
    }

    /// <summary>A copy with fresh ids and no study history.</summary>
    public Deck Duplicate(string? newName = null)
    {
        var copy = new Deck(newName ?? $"{Name} (copy)")
        {
            Settings = Settings.Clone(),
        };

        copy.Describe(Description);
        copy.SetAppearance(Emoji, ColorToken);
        copy.SetLanguages(FrontLanguage, BackLanguage);
        copy.AddCards(_cards.Select(static c => c.CloneContent()));
        return copy;
    }

    internal void Touch() => ModifiedAt = DateTimeOffset.Now;

    public override string ToString() => $"{Emoji} {Name} ({TotalCards} cards)";
}
