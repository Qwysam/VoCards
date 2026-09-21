using VoCards.Core.Models;
using VoCards.Core.Scheduling;
using VoCards.Core.Study;

namespace VoCards.Core.Tests;

/// <summary>
/// One test per defect catalogued in PLAN.md's problem table. These exist so that the
/// specific failures of the 2021 implementation can never quietly return.
/// </summary>
public class LegacyRegressionTests
{
    /// <summary>
    /// P5 — the original kept <c>words_total_deck</c> as a field it incremented by hand.
    /// <c>RemoveCard</c> decremented it, but <c>words_learnt_deck</c> only decremented
    /// when the removed card happened to be memorised, and nothing recomputed either
    /// after a failed removal. Counts are now derived, so they cannot drift.
    /// </summary>
    [Fact]
    public void Counts_StayConsistent_AfterRemovals()
    {
        Deck deck = TestData.Deck(count: 5);
        Guid first = deck[0].Id;

        Assert.Equal(5, deck.TotalCards);
        Assert.True(deck.RemoveCard(first));
        Assert.Equal(4, deck.TotalCards);

        // Removing the same card twice must not move the count again.
        Assert.False(deck.RemoveCard(first));
        Assert.Equal(4, deck.TotalCards);
        Assert.Equal(deck.Cards.Count, deck.TotalCards);
    }

    /// <summary>
    /// P14 — <c>Deck.RemoveCard</c> threw <c>ArgumentOutOfRangeException</c> straight at
    /// the console for an out-of-range index. Removal now reports failure instead.
    /// </summary>
    [Fact]
    public void RemovingUnknownCard_ReturnsFalse_RatherThanThrowing()
    {
        Deck deck = TestData.Deck(count: 3);

        bool removed = deck.RemoveCard(Guid.NewGuid());

        Assert.False(removed);
        Assert.Equal(3, deck.TotalCards);
    }

    /// <summary>
    /// P6 — <c>HasTopic</c> scanned every deck and never broke out of the loop, and
    /// <c>FindByTopic</c> returned the *last* match rather than the first. Lookup is now
    /// dictionary-backed and name lookup is case-insensitive.
    /// </summary>
    [Fact]
    public void DeckLookup_IsCaseInsensitive_AndFindsTheRightDeck()
    {
        var library = new Library();
        library.CreateDeck("Animals");
        library.CreateDeck("Jobs");

        Assert.NotNull(library.FindDeckByName("animals"));
        Assert.NotNull(library.FindDeckByName("ANIMALS"));
        Assert.Equal("Animals", library.FindDeckByName("animals")!.Name);
        Assert.Null(library.FindDeckByName("Food"));
    }

    /// <summary>
    /// P14 — the original did <c>index = progress.FindByTopic(input)</c> and then indexed
    /// with the result. A miss returned -1, which threw on the next line. There is no
    /// index to get wrong now, and a miss is a null the caller must handle.
    /// </summary>
    [Fact]
    public void MissingDeck_ReturnsNull_InsteadOfNegativeIndex()
    {
        var library = new Library();

        Assert.Null(library.FindDeckByName("nope"));
        Assert.Null(library.FindDeck(Guid.NewGuid()));
        Assert.False(library.HasDeckNamed("nope"));
    }

    /// <summary>
    /// P9 — a card marked memorised in the 2021 app was excluded from study forever:
    /// <c>CardMemorized</c> set a bool that nothing ever cleared. A card can now lapse
    /// back into relearning, which is the entire point of spaced repetition.
    /// </summary>
    [Fact]
    public void ForgettingACard_ReturnsItToTheQueue()
    {
        Deck deck = TestData.Deck(count: 1);
        Card card = deck[0];
        DateTimeOffset now = TestData.Epoch;

        // Learn it all the way to a real review interval.
        CardReviewer.Apply(card, deck, Rating.Easy, now);
        Assert.Equal(CardState.Review, card.State);

        // Then forget it.
        now = now.AddDays(5);
        CardReviewer.Apply(card, deck, Rating.Again, now);

        Assert.Equal(CardState.Relearning, card.State);
        Assert.Equal(1, card.Lapses);
        Assert.True(card.IsDue(now.AddMinutes(30)));
    }

    /// <summary>
    /// P4 — the original's "Learn" branch was
    /// <c>if (input == "Add") {...} else { GoThroughCards(); break; }</c>, so *any*
    /// input that was not "Add" silently started a study session. Study is now started
    /// explicitly and reports why it could not, instead of guessing.
    /// </summary>
    [Fact]
    public void StartingStudy_OnAnEmptyDeck_ExplainsItselfInsteadOfLooping()
    {
        var library = new Library();
        var deck = new Deck("Empty");
        library.AddDeck(deck);

        var result = StudySession.Start(library, deck, new StudyOptions(), TestData.Clock());

        Assert.True(result.IsFailure);
        Assert.Contains("no cards", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// P3/P4 — <c>GoThroughCards</c> asked for a card count inside a <c>for(;;)</c> that
    /// re-prompted until the number was in range, then walked the deck with an index
    /// loop nested three levels deeper. The session is now a flat state machine, and a
    /// requested count larger than what is available is simply clamped.
    /// </summary>
    [Fact]
    public void RequestingMoreCardsThanExist_ClampsInsteadOfRejecting()
    {
        (Library library, Deck deck) = TestData.Library(cards: 3);

        var result = StudySession.Start(
            library, deck, new StudyOptions { MaxCards = 999 }, TestData.Clock());

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Total);
    }

    /// <summary>
    /// P7 — <c>Card.memorized</c> was a public field, mutable by anyone, guarded only by
    /// a comment asking callers not to touch it. Scheduling state is now settable only
    /// from inside the assembly, so CardReviewer is the single writer.
    /// </summary>
    [Fact]
    public void CardSchedulingState_IsNotPubliclyWritable()
    {
        System.Reflection.PropertyInfo state = typeof(Card).GetProperty(nameof(Card.State))!;

        Assert.NotNull(state.GetMethod);
        Assert.True(state.GetMethod!.IsPublic);

        // The setter exists, but is internal rather than public.
        Assert.NotNull(state.SetMethod);
        Assert.False(state.SetMethod!.IsPublic);
    }

    /// <summary>
    /// P12 — the seeded library was nine hard-coded English→Russian words across three
    /// decks. The three original decks survive, with their original cards, inside a
    /// much larger starter library.
    /// </summary>
    [Fact]
    public void SeedLibrary_StillContainsTheOriginalNineWords()
    {
        Library library = Seed.SeedLibrary.Create();

        (string Deck, string Front, string Back)[] originals =
        [
            ("Animals", "Cat", "Кот"),
            ("Animals", "Horse", "Лошадь"),
            ("Animals", "Dog", "Собака"),
            ("Jobs", "Firefighter", "Пожарный"),
            ("Jobs", "Lawyer", "Адвокат"),
            ("Jobs", "Teacher", "Учитель"),
            ("Food", "Pizza", "Пицца"),
            ("Food", "Soup", "Суп"),
            ("Food", "Bread", "Хлеб"),
        ];

        foreach ((string deckName, string front, string back) in originals)
        {
            Deck? deck = library.FindDeckByName(deckName);
            Assert.NotNull(deck);

            Card? card = deck!.Cards.FirstOrDefault(c => c.Front == front);
            Assert.NotNull(card);
            Assert.Equal(back, card!.Back);
        }

        // And it has grown well past the original nine.
        Assert.True(library.TotalCards > 150, $"expected a substantial starter library, got {library.TotalCards}");
    }
}
