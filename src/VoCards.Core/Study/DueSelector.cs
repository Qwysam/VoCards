using VoCards.Core.Models;

namespace VoCards.Core.Study;

/// <summary>
/// Builds the ordered queue of cards a session will walk. Pulled out of the session
/// itself so the dashboard can ask "what would I study?" without starting anything.
/// </summary>
public static class DueSelector
{
    /// <summary>Selects and orders the cards for a session over a single deck.</summary>
    public static IReadOnlyList<Card> Build(Deck deck, StudyOptions options, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(options);

        IEnumerable<Card> candidates = deck.Cards.Where(card => Qualifies(card, options, now));

        // Daily caps only apply to real scheduled study, never to cram.
        if (!options.Cram)
        {
            candidates = ApplyDailyCaps(candidates, deck.Settings);
        }

        var ordered = Order(candidates, options, now).ToList();

        return options.MaxCards is { } max && max > 0 && ordered.Count > max
            ? ordered.GetRange(0, max)
            : ordered;
    }

    /// <summary>Selects across several decks at once, for the "study everything" button.</summary>
    public static IReadOnlyList<Card> BuildAcross(
        IEnumerable<Deck> decks,
        StudyOptions options,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(decks);
        ArgumentNullException.ThrowIfNull(options);

        var pooled = decks.SelectMany(deck => Build(deck, options with { MaxCards = null }, now));
        var ordered = Order(pooled, options, now).ToList();

        return options.MaxCards is { } max && max > 0 && ordered.Count > max
            ? ordered.GetRange(0, max)
            : ordered;
    }

    /// <summary>
    /// Whether a card belongs in this session at all. Written as a flat chain of guard
    /// clauses rather than nested conditionals.
    /// </summary>
    private static bool Qualifies(Card card, StudyOptions options, DateTimeOffset now)
    {
        if (card.State == CardState.Suspended)
        {
            return false;
        }

        if (options.StarredOnly && !card.IsStarred)
        {
            return false;
        }

        if (options.LeechesOnly && !card.IsLeech)
        {
            return false;
        }

        if (options.Tag is { Length: > 0 } tag && !card.HasTag(tag))
        {
            return false;
        }

        bool stateAllowed = card.State switch
        {
            CardState.New => options.IncludeNew,
            CardState.Learning or CardState.Relearning => options.IncludeLearning,
            CardState.Review => options.IncludeReview,
            _ => false,
        };

        if (!stateAllowed)
        {
            return false;
        }

        // Cram ignores the calendar entirely.
        return options.Cram || card.IsDue(now);
    }

    /// <summary>Enforces the deck's new-card and review ceilings.</summary>
    private static IEnumerable<Card> ApplyDailyCaps(IEnumerable<Card> cards, DeckSettings settings)
    {
        int newBudget = Math.Max(0, settings.NewCardsPerDay);
        int reviewBudget = Math.Max(0, settings.MaxReviewsPerDay);

        foreach (Card card in cards)
        {
            // Learning cards already in flight are never withheld; dropping them
            // mid-ladder is how a card gets forgotten.
            if (card.State is CardState.Learning or CardState.Relearning)
            {
                yield return card;
                continue;
            }

            if (card.State == CardState.New)
            {
                if (newBudget <= 0)
                {
                    continue;
                }

                newBudget--;
                yield return card;
                continue;
            }

            if (reviewBudget <= 0)
            {
                continue;
            }

            reviewBudget--;
            yield return card;
        }
    }

    private static IEnumerable<Card> Order(IEnumerable<Card> cards, StudyOptions options, DateTimeOffset now)
    {
        return options.Order switch
        {
            QueueOrder.Random => Shuffle(cards, options.Seed),
            QueueOrder.Added => cards.OrderBy(static c => c.CreatedAt),
            QueueOrder.HardestFirst => cards
                .OrderBy(static c => c.Mastery)
                .ThenByDescending(static c => c.Lapses),

            // DueFirst: learning cards first (they are on minute-scale timers and go
            // stale fastest), then the most overdue, then new cards last.
            _ => cards
                .OrderBy(static c => c.State switch
                {
                    CardState.Learning or CardState.Relearning => 0,
                    CardState.Review => 1,
                    _ => 2,
                })
                .ThenByDescending(c => c.DaysOverdue(now))
                .ThenBy(static c => c.CreatedAt),
        };
    }

    /// <summary>Deterministic Fisher–Yates when a seed is supplied, random otherwise.</summary>
    private static Card[] Shuffle(IEnumerable<Card> cards, int? seed)
    {
        var array = cards.ToArray();
        Random random = seed is { } s ? new Random(s) : Random.Shared;

        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }

        return array;
    }
}
