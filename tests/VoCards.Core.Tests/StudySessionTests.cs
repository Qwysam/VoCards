using VoCards.Core.Common;
using VoCards.Core.Models;
using VoCards.Core.Study;

namespace VoCards.Core.Tests;

public class StudySessionTests
{
    private static Result<StudySession> Start(Library library, Deck deck, IClock clock, StudyOptions? options = null) =>
        StudySession.Start(library, deck, options ?? new StudyOptions { Seed = 42 }, clock);

    [Fact]
    public void Session_WalksEveryCard_ThenFinishes()
    {
        (Library library, Deck deck) = TestData.Library(cards: 5);
        FixedClock clock = TestData.Clock();

        StudySession session = Start(library, deck, clock).Value!;

        Assert.Equal(5, session.Total);

        for (int i = 0; i < 5; i++)
        {
            Assert.False(session.IsFinished);
            Assert.NotNull(session.Current);
            Assert.Equal(i, session.Current!.Index);
            session.Grade(Rating.Good);
        }

        Assert.True(session.IsFinished);
        Assert.Null(session.Current);
        Assert.Equal(5, session.Completed);
    }

    [Fact]
    public void Reveal_MovesFromPromptingToRevealed()
    {
        (Library library, Deck deck) = TestData.Library(cards: 2);
        StudySession session = Start(library, deck, TestData.Clock()).Value!;

        Assert.Equal(SessionPhase.Prompting, session.Phase);
        session.Reveal();
        Assert.Equal(SessionPhase.Revealed, session.Phase);

        session.Grade(Rating.Good);
        Assert.Equal(SessionPhase.Prompting, session.Phase);
    }

    [Fact]
    public void GradingAfterTheSessionEnds_IsANoOp()
    {
        (Library library, Deck deck) = TestData.Library(cards: 1);
        StudySession session = Start(library, deck, TestData.Clock()).Value!;

        session.Grade(Rating.Good);
        Assert.True(session.IsFinished);

        // The original would have walked off the end of the list here.
        session.Grade(Rating.Good);
        session.Grade(Rating.Again);

        Assert.Equal(1, session.Completed);
    }

    [Fact]
    public void Combo_GrowsOnCorrect_AndResetsOnAgain()
    {
        (Library library, Deck deck) = TestData.Library(cards: 5);
        StudySession session = Start(library, deck, TestData.Clock()).Value!;

        session.Grade(Rating.Good);
        session.Grade(Rating.Good);
        Assert.Equal(2, session.Combo);

        session.Grade(Rating.Again);
        Assert.Equal(0, session.Combo);
        Assert.Equal(2, session.BestCombo);

        session.Grade(Rating.Easy);
        Assert.Equal(1, session.Combo);
    }

    [Fact]
    public void Skip_PushesTheCardToTheBack_WithoutGradingIt()
    {
        (Library library, Deck deck) = TestData.Library(cards: 3);
        StudySession session = Start(library, deck, TestData.Clock()).Value!;

        Guid first = session.Current!.Card.Id;
        session.Skip();

        Assert.Equal(0, session.Completed);
        Assert.NotEqual(first, session.Current!.Card.Id);

        session.Grade(Rating.Good);
        session.Grade(Rating.Good);

        Assert.Equal(first, session.Current!.Card.Id);
    }

    [Fact]
    public void Skip_OnTheLastCard_GradesItRatherThanLoopingForever()
    {
        (Library library, Deck deck) = TestData.Library(cards: 1);
        StudySession session = Start(library, deck, TestData.Clock()).Value!;

        session.Skip();

        Assert.True(session.IsFinished);
        Assert.Equal(1, session.Completed);
    }

    [Fact]
    public void Finish_EndsEarly_KeepingWhatWasAlreadyGraded()
    {
        (Library library, Deck deck) = TestData.Library(cards: 10);
        StudySession session = Start(library, deck, TestData.Clock()).Value!;

        session.Grade(Rating.Good);
        session.Grade(Rating.Good);
        session.Finish();

        Assert.True(session.IsFinished);
        Assert.Equal(2, session.BuildSummary().Total);
    }

    [Fact]
    public void CramMode_DoesNotTouchTheSchedule()
    {
        (Library library, Deck deck) = TestData.Library(cards: 3);
        FixedClock clock = TestData.Clock();

        StudySession session = Start(library, deck, clock,
            new StudyOptions { Cram = true, Seed = 1 }).Value!;

        while (!session.IsFinished)
        {
            session.Grade(Rating.Good);
        }

        Assert.All(deck.Cards, card => Assert.Equal(CardState.New, card.State));
        Assert.Empty(library.History.All);
    }

    [Fact]
    public void CramMode_PullsCardsThatAreNotDue()
    {
        (Library library, Deck deck) = TestData.Library(cards: 3);
        FixedClock clock = TestData.Clock();

        // Push every card far into the future.
        foreach (Card card in deck.Cards)
        {
            card.State = CardState.Review;
            card.DueAt = clock.Now.AddDays(100);
            card.IntervalDays = 100d;
        }

        Assert.True(Start(library, deck, clock).IsFailure);
        Assert.True(Start(library, deck, clock, new StudyOptions { Cram = true }).IsSuccess);
    }

    [Fact]
    public void NormalStudy_WritesReviewsToHistory()
    {
        (Library library, Deck deck) = TestData.Library(cards: 3);
        StudySession session = Start(library, deck, TestData.Clock()).Value!;

        while (!session.IsFinished)
        {
            session.Grade(Rating.Good);
        }

        Assert.Equal(3, library.History.Count);
        Assert.All(library.History.All, r => Assert.Equal(deck.Id, r.DeckId));
        Assert.Equal(3, library.Profile.LifetimeReviews);
    }

    [Fact]
    public void MultipleChoice_BuildsOptionsIncludingTheRightOne()
    {
        (Library library, Deck deck) = TestData.Library(cards: 10);

        StudySession session = Start(library, deck, TestData.Clock(),
            new StudyOptions { Mode = StudyMode.MultipleChoice, ChoiceCount = 4, Seed = 7 }).Value!;

        StudyQuestion question = session.Current!;

        Assert.Equal(4, question.Choices.Count);
        Assert.InRange(question.CorrectChoiceIndex, 0, 3);
        Assert.Equal(question.Answer, question.Choices[question.CorrectChoiceIndex]);
        Assert.Equal(question.Choices.Count, question.Choices.Distinct().Count());
    }

    [Fact]
    public void MultipleChoice_InATinyDeck_ShowsFewerOptions_RatherThanFailing()
    {
        (Library library, Deck deck) = TestData.Library(cards: 2);

        StudySession session = Start(library, deck, TestData.Clock(),
            new StudyOptions { Mode = StudyMode.MultipleChoice, ChoiceCount = 4, Seed = 3 }).Value!;

        Assert.Equal(2, session.Current!.Choices.Count);
        Assert.InRange(session.Current.CorrectChoiceIndex, 0, 1);
    }

    [Fact]
    public void Choose_GradesTheSelectedOption()
    {
        (Library library, Deck deck) = TestData.Library(cards: 6);

        StudySession session = Start(library, deck, TestData.Clock(),
            new StudyOptions { Mode = StudyMode.MultipleChoice, Seed = 11 }).Value!;

        int correct = session.Current!.CorrectChoiceIndex;

        GradedAnswer right = session.Choose(correct);
        Assert.Equal(AnswerVerdict.Correct, right.Verdict);

        session.Grade(right.ImpliedRating, right.Verdict);

        int wrong = (session.Current!.CorrectChoiceIndex + 1) % session.Current.Choices.Count;
        GradedAnswer miss = session.Choose(wrong);
        Assert.Equal(AnswerVerdict.Incorrect, miss.Verdict);
        Assert.Equal(Rating.Again, miss.ImpliedRating);
    }

    [Fact]
    public void BackToFront_SwapsPromptAndAnswer()
    {
        (Library library, Deck deck) = TestData.Library(cards: 1);

        StudySession session = Start(library, deck, TestData.Clock(),
            new StudyOptions { Direction = StudyDirection.BackToFront }).Value!;

        Assert.Equal("back1", session.Current!.Prompt);
        Assert.Equal("front1", session.Current.Answer);
    }

    [Fact]
    public void MixedDirection_IsDeterministicForAGivenSeed()
    {
        static IReadOnlyList<StudyDirection> Run(int seed)
        {
            (Library library, Deck deck) = TestData.Library(cards: 12);
            StudySession session = StudySession.Start(
                library, deck,
                new StudyOptions { Direction = StudyDirection.Mixed, Seed = seed },
                TestData.Clock()).Value!;

            var directions = new List<StudyDirection>();
            while (!session.IsFinished)
            {
                directions.Add(session.Current!.Direction);
                session.Grade(Rating.Good);
            }

            return directions;
        }

        Assert.Equal(Run(99), Run(99));
        Assert.Contains(StudyDirection.BackToFront, Run(99));
        Assert.Contains(StudyDirection.FrontToBack, Run(99));
    }

    [Fact]
    public void SubmitTyped_GradesWithoutAdvancing_UntilCommitted()
    {
        (Library library, Deck deck) = TestData.Library(cards: 3);

        StudySession session = Start(library, deck, TestData.Clock(),
            new StudyOptions { Mode = StudyMode.Typing }).Value!;

        GradedAnswer grade = session.SubmitTyped("back1");

        Assert.Equal(AnswerVerdict.Correct, grade.Verdict);
        Assert.Equal(SessionPhase.Revealed, session.Phase);
        Assert.Equal(0, session.Completed);

        session.CommitTyped(grade, "back1");
        Assert.Equal(1, session.Completed);
    }

    [Fact]
    public void StarredOnly_FiltersTheQueue()
    {
        (Library library, Deck deck) = TestData.Library(cards: 5);
        deck[0].Star();
        deck[3].Star();

        StudySession session = Start(library, deck, TestData.Clock(),
            new StudyOptions { StarredOnly = true }).Value!;

        Assert.Equal(2, session.Total);
    }

    [Fact]
    public void TagFilter_FiltersTheQueue()
    {
        (Library library, Deck deck) = TestData.Library(cards: 5);
        deck[1].AddTag("verbs");
        deck[2].AddTag("VERBS");

        StudySession session = Start(library, deck, TestData.Clock(),
            new StudyOptions { Tag = "verbs" }).Value!;

        Assert.Equal(2, session.Total);
    }

    [Fact]
    public void SuspendedCards_AreNeverQueued()
    {
        (Library library, Deck deck) = TestData.Library(cards: 3);
        deck[0].Suspend();

        StudySession session = Start(library, deck, TestData.Clock()).Value!;

        Assert.Equal(2, session.Total);
    }

    [Fact]
    public void NewCardsPerDay_CapsHowManyNewCardsAppear()
    {
        (Library library, Deck deck) = TestData.Library(cards: 20);
        deck.Settings.NewCardsPerDay = 5;

        StudySession session = Start(library, deck, TestData.Clock()).Value!;

        Assert.Equal(5, session.Total);
    }

    [Fact]
    public void Summary_TalliesRatingsAndAccuracy()
    {
        (Library library, Deck deck) = TestData.Library(cards: 4);
        StudySession session = Start(library, deck, TestData.Clock()).Value!;

        session.Grade(Rating.Good);
        session.Grade(Rating.Easy);
        session.Grade(Rating.Again);
        session.Grade(Rating.Hard);

        SessionSummary summary = session.BuildSummary();

        Assert.Equal(4, summary.Total);
        Assert.Equal(3, summary.Correct);
        Assert.Equal(1, summary.Incorrect);
        Assert.Equal(1, summary.Again);
        Assert.Equal(1, summary.Hard);
        Assert.Equal(1, summary.Good);
        Assert.Equal(1, summary.Easy);
        Assert.Equal(0.75d, summary.Accuracy, 4);
        Assert.Single(summary.Missed);
    }

    [Fact]
    public void Summary_AwardsXpAndRegistersTheStreak()
    {
        (Library library, Deck deck) = TestData.Library(cards: 3);
        StudySession session = Start(library, deck, TestData.Clock()).Value!;

        while (!session.IsFinished)
        {
            session.Grade(Rating.Good);
        }

        SessionSummary summary = session.BuildSummary();

        Assert.True(summary.XpEarned > 0);
        Assert.True(library.Profile.Xp >= summary.XpEarned);
        Assert.Equal(StreakOutcome.Started, summary.StreakOutcome);
        Assert.Equal(1, library.Profile.CurrentStreak);
    }

    [Fact]
    public void StartingWithNothingDue_ExplainsWhy()
    {
        var library = new Library();
        var deck = new Deck("Starred");
        deck.AddCard(new Card("a", "b"));
        library.AddDeck(deck);

        var result = StudySession.Start(
            library, deck, new StudyOptions { StarredOnly = true }, TestData.Clock());

        Assert.True(result.IsFailure);
        Assert.Contains("starred", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StudyingAcrossDecks_PoolsTheQueue()
    {
        var library = new Library();
        Deck a = TestData.Deck("A", 3);
        Deck b = TestData.Deck("B", 4);
        library.AddDeck(a);
        library.AddDeck(b);

        StudySession session = StudySession.StartAcross(
            library, [a, b], new StudyOptions { Seed = 5 }, TestData.Clock()).Value!;

        Assert.Equal(7, session.Total);
    }
}
