using VoCards.Core.Common;
using VoCards.Core.Models;
using VoCards.Core.Scheduling;

namespace VoCards.Core.Tests;

/// <summary>Builders that keep the tests readable.</summary>
internal static class TestData
{
    /// <summary>A fixed point in time, so nothing in the suite depends on when it runs.</summary>
    public static readonly DateTimeOffset Epoch = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    public static FixedClock Clock() => new(Epoch);

    /// <summary>A deck of <paramref name="count"/> generated cards.</summary>
    public static Deck Deck(string name = "Test", int count = 10)
    {
        var deck = new Deck(name);

        for (int i = 1; i <= count; i++)
        {
            deck.AddCard(new Card($"front{i}", $"back{i}"));
        }

        return deck;
    }

    /// <summary>A library holding one generated deck.</summary>
    public static (Library Library, Deck Deck) Library(int cards = 10)
    {
        var library = new Library();
        Deck deck = Deck(count: cards);
        library.AddDeck(deck);
        return (library, deck);
    }

    /// <summary>A card already graduated onto a review interval.</summary>
    public static Card ReviewCard(double intervalDays = 10d, double ease = 2.5d, int lapses = 0)
    {
        var card = new Card("term", "meaning");
        var deck = new Deck("holder");
        deck.AddCard(card);

        // Drive it through the real scheduler rather than poking fields, so the card
        // ends up in a state the production code could actually produce.
        DateTimeOffset now = Epoch;
        CardReviewer.Apply(card, deck, Rating.Easy, now);

        while (card.State != CardState.Review)
        {
            now = now.AddDays(1);
            CardReviewer.Apply(card, deck, Rating.Good, now);
        }

        card.IntervalDays = intervalDays;
        card.EaseFactor = ease;
        card.Lapses = lapses;
        card.DueAt = Epoch.AddDays(intervalDays);
        return card;
    }
}
