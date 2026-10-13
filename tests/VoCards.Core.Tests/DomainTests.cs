using VoCards.Core.Gamification;
using VoCards.Core.Models;
using VoCards.Core.Scheduling;
using VoCards.Core.Search;
using VoCards.Core.Stats;

namespace VoCards.Core.Tests;

public class CardTests
{
    [Fact]
    public void NewCard_StartsUnstudied()
    {
        var card = new Card("  front  ", "  back  ");

        Assert.Equal("front", card.Front);
        Assert.Equal("back", card.Back);
        Assert.Equal(CardState.New, card.State);
        Assert.Equal(0, card.ReviewCount);
        Assert.Equal(0d, card.Accuracy);
        Assert.Equal(0d, card.Mastery);
        Assert.False(card.IsGraduated);
        Assert.False(card.IsLeech);
    }

    [Fact]
    public void Edit_RejectsEmptyFaces()
    {
        var card = new Card("front", "back");

        Assert.True(card.Edit(front: "  ").IsFailure);
        Assert.True(card.Edit(back: "").IsFailure);
        Assert.Equal("front", card.Front);
        Assert.Equal("back", card.Back);
    }

    [Fact]
    public void Tags_AreDeduplicatedCaseInsensitively()
    {
        var card = new Card("a", "b");

        Assert.True(card.AddTag("Verbs"));
        Assert.False(card.AddTag("verbs"));
        Assert.False(card.AddTag("  VERBS  "));
        Assert.Single(card.Tags);
        Assert.True(card.HasTag("VeRbS"));

        Assert.True(card.RemoveTag("verbs"));
        Assert.Empty(card.Tags);
    }

    [Fact]
    public void Suspend_RemembersThePreviousState()
    {
        var card = new Card("a", "b") { State = CardState.Review };

        card.Suspend();
        Assert.Equal(CardState.Suspended, card.State);
        Assert.False(card.IsDue(TestData.Epoch));

        card.Unsuspend();
        Assert.Equal(CardState.Review, card.State);
    }

    [Fact]
    public void Leech_IsFlaggedAtTheLapseThreshold()
    {
        var card = new Card("a", "b") { Lapses = Card.LeechThreshold - 1 };
        Assert.False(card.IsLeech);

        card.Lapses = Card.LeechThreshold;
        Assert.True(card.IsLeech);
    }

    [Fact]
    public void AcceptedAnswers_FollowTheDirection()
    {
        var card = new Card("big", "grande, enorme");

        Assert.Equal(2, card.AcceptedAnswers(StudyDirection.FrontToBack).Count);
        Assert.Single(card.AcceptedAnswers(StudyDirection.BackToFront));
        Assert.Equal("big", card.PromptText(StudyDirection.FrontToBack));
        Assert.Equal("big", card.AnswerText(StudyDirection.BackToFront));
    }

    [Fact]
    public void ResetProgress_KeepsContent_AndClearsSchedule()
    {
        var card = new Card("a", "b");
        card.AddTag("keep");
        card.Star();

        var deck = new Deck("d");
        deck.AddCard(card);
        CardReviewer.Apply(card, deck, Rating.Easy, TestData.Epoch);

        card.ResetProgress();

        Assert.Equal(CardState.New, card.State);
        Assert.Equal(0, card.ReviewCount);
        Assert.Null(card.DueAt);
        Assert.Equal(2.5d, card.EaseFactor);
        Assert.Contains("keep", card.Tags);
        Assert.True(card.IsStarred);
    }

    [Fact]
    public void CloneContent_CopiesContentWithANewIdentity()
    {
        var card = new Card("a", "b");
        card.AddTag("t");
        card.Star();

        Card copy = card.CloneContent();

        Assert.NotEqual(card.Id, copy.Id);
        Assert.Equal(card.Front, copy.Front);
        Assert.Contains("t", copy.Tags);
        Assert.True(copy.IsStarred);
        Assert.Equal(CardState.New, copy.State);
    }

    [Fact]
    public void NewCards_AreAlwaysDue()
    {
        var card = new Card("a", "b");

        Assert.True(card.IsDue(TestData.Epoch));
        Assert.True(card.IsDue(TestData.Epoch.AddYears(-5)));
        Assert.Equal(0d, card.DaysOverdue(TestData.Epoch));
    }
}

public class DeckTests
{
    [Fact]
    public void AddCard_RejectsEmptyFaces()
    {
        var deck = new Deck("d");

        Assert.True(deck.AddCard("", "back").IsFailure);
        Assert.True(deck.AddCard("front", "   ").IsFailure);
        Assert.Equal(0, deck.TotalCards);
    }

    [Fact]
    public void AddingTheSameCardTwice_IsIdempotent()
    {
        var deck = new Deck("d");
        var card = new Card("a", "b");

        deck.AddCard(card);
        deck.AddCard(card);

        Assert.Equal(1, deck.TotalCards);
    }

    [Fact]
    public void FindCard_IsExactAndFast()
    {
        Deck deck = TestData.Deck(count: 50);
        Guid target = deck[37].Id;

        Assert.Same(deck[37], deck.FindCard(target));
        Assert.Null(deck.FindCard(Guid.NewGuid()));
        Assert.True(deck.Contains(target));
    }

    [Fact]
    public void MoveCard_Reorders()
    {
        Deck deck = TestData.Deck(count: 4);
        Guid last = deck[3].Id;

        Assert.True(deck.MoveCard(last, 0));
        Assert.Equal(last, deck[0].Id);
        Assert.False(deck.MoveCard(last, 99));
        Assert.False(deck.MoveCard(Guid.NewGuid(), 0));
    }

    [Fact]
    public void Tags_AreAggregatedAndSorted()
    {
        Deck deck = TestData.Deck(count: 3);
        deck[0].AddTag("zeta");
        deck[1].AddTag("alpha");
        deck[2].AddTag("Alpha");

        Assert.Equal(["alpha", "zeta"], deck.Tags);
    }

    [Fact]
    public void Duplicate_CopiesContentButNotProgress()
    {
        Deck deck = TestData.Deck(count: 3);
        deck.Settings.NewCardsPerDay = 7;
        CardReviewer.Apply(deck[0], deck, Rating.Easy, TestData.Epoch);

        Deck copy = deck.Duplicate();

        Assert.NotEqual(deck.Id, copy.Id);
        Assert.Equal(3, copy.TotalCards);
        Assert.Equal(7, copy.Settings.NewCardsPerDay);
        Assert.All(copy.Cards, c => Assert.Equal(CardState.New, c.State));
    }

    [Fact]
    public void NextDueAt_IgnoresCardsAlreadyDue()
    {
        Deck deck = TestData.Deck(count: 3);
        deck[0].State = CardState.Review;
        deck[0].DueAt = TestData.Epoch.AddDays(-1);
        deck[1].State = CardState.Review;
        deck[1].DueAt = TestData.Epoch.AddDays(5);
        deck[2].State = CardState.Review;
        deck[2].DueAt = TestData.Epoch.AddDays(3);

        Assert.Equal(TestData.Epoch.AddDays(3), deck.NextDueAt(TestData.Epoch));
    }

    [Fact]
    public void MaturedCards_CountsOnlyLongIntervals()
    {
        Deck deck = TestData.Deck(count: 3);
        deck[0].State = CardState.Review;
        deck[0].IntervalDays = 30d;
        deck[1].State = CardState.Review;
        deck[1].IntervalDays = 5d;

        Assert.Equal(1, deck.MaturedCards);
    }
}

public class LibraryTests
{
    [Fact]
    public void CreateDeck_RejectsDuplicateNames()
    {
        var library = new Library();

        Assert.True(library.CreateDeck("Animals").IsSuccess);
        Assert.True(library.CreateDeck("animals").IsFailure);
        Assert.True(library.CreateDeck("  ").IsFailure);
        Assert.Equal(1, library.DeckCount);
    }

    [Fact]
    public void UniqueName_AppendsUntilFree()
    {
        var library = new Library();
        library.CreateDeck("Animals");

        Assert.Equal("Animals 2", library.UniqueName("Animals"));

        library.CreateDeck("Animals 2");
        Assert.Equal("Animals 3", library.UniqueName("Animals"));
        Assert.Equal("Fresh", library.UniqueName("Fresh"));
    }

    [Fact]
    public void RenameDeck_RejectsAClashWithAnotherDeck()
    {
        var library = new Library();
        Deck a = library.CreateDeck("Animals").Value!;
        library.CreateDeck("Jobs");

        Assert.True(library.RenameDeck(a.Id, "jobs").IsFailure);

        // Renaming to its own name, in different case, is fine.
        Assert.True(library.RenameDeck(a.Id, "ANIMALS").IsSuccess);
        Assert.Equal("ANIMALS", a.Name);
    }

    [Fact]
    public void PinnedDecks_SortFirst()
    {
        var library = new Library();
        library.CreateDeck("Zebra");
        Deck jobs = library.CreateDeck("Jobs").Value!;

        Assert.Equal("Jobs", library[0].Name);

        library.FindDeckByName("Zebra")!.Pin();
        library.Sort();

        Assert.Equal("Zebra", library[0].Name);
        Assert.Equal(jobs.Id, library[1].Id);
    }

    [Fact]
    public void LocateCard_FindsTheOwningDeck()
    {
        var library = new Library();
        Deck a = TestData.Deck("A", 2);
        Deck b = TestData.Deck("B", 2);
        library.AddDeck(a);
        library.AddDeck(b);

        var located = library.LocateCard(b[1].Id);

        Assert.NotNull(located);
        Assert.Equal(b.Id, located!.Value.Deck.Id);
        Assert.Null(library.LocateCard(Guid.NewGuid()));
    }

    [Fact]
    public void MoveCard_TransfersBetweenDecks_KeepingSchedule()
    {
        var library = new Library();
        Deck a = TestData.Deck("A", 2);
        Deck b = TestData.Deck("B", 1);
        library.AddDeck(a);
        library.AddDeck(b);

        Card card = a[0];
        CardReviewer.Apply(card, a, Rating.Easy, TestData.Epoch);
        double interval = card.IntervalDays;

        Assert.True(library.MoveCard(card.Id, b.Id).IsSuccess);
        Assert.Equal(1, a.TotalCards);
        Assert.Equal(2, b.TotalCards);
        Assert.Equal(interval, b.FindCard(card.Id)!.IntervalDays);
    }

    [Fact]
    public void RemovingADeck_PrunesItsHistory()
    {
        var library = new Library();
        Deck deck = TestData.Deck("A", 2);
        library.AddDeck(deck);

        library.History.Add(CardReviewer.Apply(deck[0], deck, Rating.Good, TestData.Epoch));
        Assert.Equal(1, library.History.Count);

        library.RemoveDeck(deck.Id);

        Assert.Equal(0, library.DeckCount);
        Assert.Equal(0, library.History.Count);
    }

    [Fact]
    public void DuplicateDeck_GetsAUniqueName()
    {
        var library = new Library();
        Deck deck = library.CreateDeck("Animals").Value!;
        deck.AddCard("a", "b");

        Deck copy = library.DuplicateDeck(deck.Id).Value!;

        Assert.Equal("Animals (copy)", copy.Name);
        Assert.Equal(2, library.DeckCount);
        Assert.True(library.DuplicateDeck(Guid.NewGuid()).IsFailure);
    }
}

public class ProfileTests
{
    [Fact]
    public void LevelCurve_MatchesItsDocumentedThresholds()
    {
        Assert.Equal(0L, UserProfile.XpForLevel(1));
        Assert.Equal(100L, UserProfile.XpForLevel(2));
        Assert.Equal(300L, UserProfile.XpForLevel(3));
        Assert.Equal(600L, UserProfile.XpForLevel(4));
        Assert.Equal(1000L, UserProfile.XpForLevel(5));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(99, 1)]
    [InlineData(100, 2)]
    [InlineData(299, 2)]
    [InlineData(300, 3)]
    [InlineData(1000, 5)]
    public void LevelForXp_IsTheInverseOfTheCurve(long xp, int expected) =>
        Assert.Equal(expected, UserProfile.LevelForXp(xp));

    [Fact]
    public void AwardXp_ReportsLevelsGained()
    {
        var profile = new UserProfile();

        Assert.Equal(0, profile.AwardXp(50));
        Assert.Equal(1, profile.Level);

        Assert.Equal(1, profile.AwardXp(50));
        Assert.Equal(2, profile.Level);

        // 1000 XP is the level-5 threshold, so this jumps three bands at once.
        Assert.Equal(3, profile.AwardXp(900));
        Assert.Equal(5, profile.Level);
    }

    [Fact]
    public void LevelProgress_IsFractionOfTheCurrentBand()
    {
        var profile = new UserProfile();
        profile.AwardXp(150);

        Assert.Equal(2, profile.Level);
        Assert.Equal(50L, profile.XpIntoLevel);
        Assert.Equal(200L, profile.XpForNextLevel);
        Assert.Equal(0.25d, profile.LevelProgress, 4);
    }

    [Fact]
    public void Streak_StartsExtendsAndBreaks()
    {
        var profile = new UserProfile();
        var day = new DateOnly(2026, 3, 1);

        Assert.Equal(StreakOutcome.Started, profile.RegisterStudyDay(day));
        Assert.Equal(1, profile.CurrentStreak);

        Assert.Equal(StreakOutcome.AlreadyCounted, profile.RegisterStudyDay(day));
        Assert.Equal(1, profile.CurrentStreak);

        Assert.Equal(StreakOutcome.Extended, profile.RegisterStudyDay(day.AddDays(1)));
        Assert.Equal(2, profile.CurrentStreak);

        // A five-day gap is past saving, even with freezes in hand.
        Assert.Equal(StreakOutcome.Broken, profile.RegisterStudyDay(day.AddDays(6)));
        Assert.Equal(1, profile.CurrentStreak);
        Assert.Equal(2, profile.LongestStreak);
    }

    [Fact]
    public void AFreeze_CoversExactlyOneMissedDay()
    {
        var profile = new UserProfile();
        var day = new DateOnly(2026, 3, 1);

        profile.RegisterStudyDay(day);
        int freezes = profile.StreakFreezes;

        Assert.Equal(StreakOutcome.Frozen, profile.RegisterStudyDay(day.AddDays(2)));
        Assert.Equal(2, profile.CurrentStreak);
        Assert.Equal(freezes - 1, profile.StreakFreezes);
    }

    [Fact]
    public void WithoutFreezes_AMissedDayBreaksTheStreak()
    {
        var profile = new UserProfile();
        var day = new DateOnly(2026, 3, 1);
        profile.RegisterStudyDay(day);

        // Burn both freezes.
        profile.RegisterStudyDay(day.AddDays(2));
        profile.RegisterStudyDay(day.AddDays(4));
        Assert.Equal(0, profile.StreakFreezes);

        Assert.Equal(StreakOutcome.Broken, profile.RegisterStudyDay(day.AddDays(6)));
    }

    [Fact]
    public void StudyingInThePast_DoesNotRewindTheStreak()
    {
        var profile = new UserProfile();
        var day = new DateOnly(2026, 3, 10);

        profile.RegisterStudyDay(day);
        Assert.Equal(StreakOutcome.AlreadyCounted, profile.RegisterStudyDay(day.AddDays(-3)));
        Assert.Equal(day, profile.LastStudyDay);
    }

    [Fact]
    public void Unlock_IsIdempotent()
    {
        var profile = new UserProfile();

        Assert.True(profile.Unlock("first-steps", TestData.Epoch));
        Assert.False(profile.Unlock("first-steps", TestData.Epoch.AddDays(1)));
        Assert.Single(profile.UnlockedAchievements);
        Assert.Equal(TestData.Epoch, profile.AchievementDates["first-steps"]);
    }
}

public class GamificationTests
{
    [Fact]
    public void ForgettingACard_StillEarnsTheBaseAward() =>
        Assert.Equal(XpRules.BaseAnswerXp, XpRules.ForAnswer(Rating.Again, CardState.Review, combo: 0));

    [Fact]
    public void ANewCard_PaysABonus()
    {
        long fresh = XpRules.ForAnswer(Rating.Good, CardState.New, 0);
        long repeat = XpRules.ForAnswer(Rating.Good, CardState.Review, 0);

        Assert.Equal(XpRules.NewCardBonus, fresh - repeat);
    }

    [Fact]
    public void ComboBonus_IsCapped()
    {
        long huge = XpRules.ForAnswer(Rating.Good, CardState.Review, combo: 1000);
        long atCap = XpRules.ForAnswer(Rating.Good, CardState.Review, combo: XpRules.MaxComboBonus);

        Assert.Equal(atCap, huge);
    }

    [Fact]
    public void ALapse_EarnsNoComboBonus() =>
        Assert.Equal(
            XpRules.ForAnswer(Rating.Again, CardState.Review, 0),
            XpRules.ForAnswer(Rating.Again, CardState.Review, 50));

    [Fact]
    public void EveryAchievement_HasAUniqueId()
    {
        var ids = AchievementCatalog.All.Select(a => a.Id).ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id)));
    }

    [Fact]
    public void EveryAchievement_IsWellFormed() =>
        Assert.All(AchievementCatalog.All, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Name));
            Assert.False(string.IsNullOrWhiteSpace(a.Description));
            Assert.False(string.IsNullOrWhiteSpace(a.Emoji));
            Assert.True(a.XpReward > 0);
        });

    [Fact]
    public void Evaluate_UnlocksFirstSteps_AfterOneReview()
    {
        (Library library, Deck deck) = TestData.Library(cards: 2);
        library.History.Add(CardReviewer.Apply(deck[0], deck, Rating.Good, TestData.Epoch));
        library.Profile.RecordReview();

        var unlocked = AchievementEngine.Evaluate(library, null, TestData.Epoch);

        Assert.Contains(unlocked, a => a.Id == "first-steps");
        Assert.True(library.Profile.HasUnlocked("first-steps"));
    }

    [Fact]
    public void Evaluate_NeverUnlocksTheSameAchievementTwice()
    {
        (Library library, Deck deck) = TestData.Library(cards: 2);
        library.History.Add(CardReviewer.Apply(deck[0], deck, Rating.Good, TestData.Epoch));
        library.Profile.RecordReview();

        AchievementEngine.Evaluate(library, null, TestData.Epoch);
        long xpAfterFirst = library.Profile.Xp;

        var second = AchievementEngine.Evaluate(library, null, TestData.Epoch);

        Assert.Empty(second);
        Assert.Equal(xpAfterFirst, library.Profile.Xp);
    }

    [Fact]
    public void ProgressOf_ReportsPartialProgress()
    {
        (Library library, _) = TestData.Library(cards: 2);
        for (int i = 0; i < 50; i++)
        {
            library.Profile.RecordReview();
        }

        Achievement century = AchievementCatalog.Find("century")!;

        Assert.Equal(0.5d, AchievementEngine.ProgressOf(century, library, TestData.Epoch), 4);
    }

    [Fact]
    public void ProgressOf_ReportsOne_ForAnUnlockedAchievement()
    {
        (Library library, _) = TestData.Library(cards: 1);
        library.Profile.Unlock("century", TestData.Epoch);

        Achievement century = AchievementCatalog.Find("century")!;

        Assert.Equal(1d, AchievementEngine.ProgressOf(century, library, TestData.Epoch));
    }
}

public class StatsTests
{
    private static Library WithHistory(int days, int perDay, double correctRate = 1d)
    {
        (Library library, Deck deck) = TestData.Library(cards: 20);

        for (int d = 0; d < days; d++)
        {
            DateTimeOffset when = TestData.Epoch.AddDays(-d);

            for (int i = 0; i < perDay; i++)
            {
                bool correct = i < (int)(perDay * correctRate);

                library.History.Add(new Review
                {
                    CardId = deck[i % deck.TotalCards].Id,
                    DeckId = deck.Id,
                    ReviewedAt = when,
                    Rating = correct ? Rating.Good : Rating.Again,
                    PreviousState = CardState.Review,
                    NewState = CardState.Review,
                    PreviousIntervalDays = 10d,
                    NewIntervalDays = 25d,
                    Elapsed = TimeSpan.FromSeconds(4),
                });
            }
        }

        return library;
    }

    [Fact]
    public void Summarize_CountsTodaysWork()
    {
        Library library = WithHistory(days: 3, perDay: 10, correctRate: 0.8d);

        LibraryStats stats = StatsService.Summarize(library, TestData.Epoch);

        Assert.Equal(10, stats.ReviewsToday);
        Assert.Equal(8, stats.CorrectToday);
        Assert.Equal(0.8d, stats.AccuracyToday, 4);
        Assert.Equal(30, stats.ReviewsThisWeek);
        Assert.Equal(TimeSpan.FromSeconds(40), stats.TimeStudiedToday);
    }

    [Fact]
    public void GoalProgress_TracksTheDailyGoal()
    {
        Library library = WithHistory(days: 1, perDay: 15);
        library.Profile.DailyGoal = 30;

        LibraryStats stats = StatsService.Summarize(library, TestData.Epoch);

        Assert.Equal(0.5d, stats.GoalProgress, 4);
        Assert.False(stats.GoalMet);

        library.Profile.DailyGoal = 10;
        Assert.True(StatsService.Summarize(library, TestData.Epoch).GoalMet);
    }

    [Fact]
    public void Activity_IncludesEmptyDays_SoTheHeatmapIsDense()
    {
        Library library = WithHistory(days: 2, perDay: 5);

        var activity = StatsService.Activity(library, TestData.Epoch, days: 30);

        Assert.Equal(30, activity.Count);
        Assert.Equal(TestData.Epoch.AddDays(-29).Date, activity[0].Day.ToDateTime(TimeOnly.MinValue).Date);
        Assert.Equal(2, activity.Count(a => a.Reviews > 0));
    }

    [Fact]
    public void Breakdown_PartitionsEveryCardExactlyOnce()
    {
        (Library library, Deck deck) = TestData.Library(cards: 10);
        deck[0].State = CardState.Review;
        deck[0].IntervalDays = 30d;
        deck[1].State = CardState.Review;
        deck[1].IntervalDays = 3d;
        deck[2].State = CardState.Learning;
        deck[3].Suspend();

        StateBreakdown breakdown = StatsService.Breakdown(library);

        Assert.Equal(10, breakdown.Total);
        Assert.Equal(1, breakdown.Mature);
        Assert.Equal(1, breakdown.Young);
        Assert.Equal(1, breakdown.Learning);
        Assert.Equal(1, breakdown.Suspended);
        Assert.Equal(6, breakdown.New);
    }

    [Fact]
    public void Forecast_BucketsUpcomingDueDates()
    {
        (Library library, Deck deck) = TestData.Library(cards: 5);

        for (int i = 0; i < 5; i++)
        {
            deck[i].State = CardState.Review;
            deck[i].DueAt = TestData.Epoch.AddDays(i);
            deck[i].IntervalDays = 10d;
        }

        var forecast = StatsService.Forecast(library, TestData.Epoch, days: 7);

        Assert.Equal(7, forecast.Count);
        Assert.All(forecast.Take(5), day => Assert.Equal(1, day.Due));
        Assert.Equal(0, forecast[6].Due);
    }

    [Fact]
    public void Forecast_RollsOverdueCardsIntoToday()
    {
        (Library library, Deck deck) = TestData.Library(cards: 3);

        foreach (Card card in deck.Cards)
        {
            card.State = CardState.Review;
            card.DueAt = TestData.Epoch.AddDays(-30);
            card.IntervalDays = 10d;
        }

        var forecast = StatsService.Forecast(library, TestData.Epoch, days: 7);

        Assert.Equal(3, forecast[0].Due);
    }

    [Fact]
    public void RetentionCurve_BucketsByPreviousInterval()
    {
        Library library = WithHistory(days: 1, perDay: 10, correctRate: 0.7d);

        var curve = StatsService.RetentionCurve(library);

        Assert.Single(curve);
        Assert.Equal("1–3 w", curve[0].Label);
        Assert.Equal(10, curve[0].Reviews);
        Assert.Equal(0.7d, curve[0].Retention, 4);
    }

    [Fact]
    public void ReviewsByHour_HasTwentyFourBuckets()
    {
        Library library = WithHistory(days: 1, perDay: 5);

        var hours = StatsService.ReviewsByHour(library);

        Assert.Equal(24, hours.Count);
        Assert.Equal(5, hours.Sum());
    }

    [Fact]
    public void BusyDayThreshold_IsZero_WithNoActivity()
    {
        var empty = StatsService.Activity(new Library(), TestData.Epoch, days: 10);

        Assert.Equal(0, StatsService.BusyDayThreshold(empty));
        Assert.All(empty, a => Assert.Equal(0, a.Intensity(0)));
    }

    [Fact]
    public void PerDeck_SortsByOutstandingWork()
    {
        var library = new Library();
        Deck quiet = TestData.Deck("Quiet", 1);
        Deck busy = TestData.Deck("Busy", 10);
        library.AddDeck(quiet);
        library.AddDeck(busy);

        var stats = StatsService.PerDeck(library, TestData.Epoch);

        Assert.Equal("Busy", stats[0].Name);
        Assert.Equal(10, stats[0].DueNow);
    }
}

public class SearchTests
{
    private static Library Sample()
    {
        var library = new Library();
        var deck = new Deck("Spanish");
        deck.SetLanguages("en-US", "es-ES");

        var casa = new Card("house", "la casa");
        casa.AddTag("nouns");
        casa.Star();

        var comer = new Card("to eat", "comer");
        comer.AddTag("verbs");

        var cafe = new Card("coffee", "el café");
        cafe.AddTag("nouns");
        cafe.Edit(notes: "masculine noun");

        deck.AddCard(casa);
        deck.AddCard(comer);
        deck.AddCard(cafe);
        library.AddDeck(deck);

        return library;
    }

    [Fact]
    public void EmptyQuery_MatchesEverything()
    {
        var hits = CardSearch.Run(Sample(), new CardQuery());

        Assert.Equal(3, hits.Count);
        Assert.True(new CardQuery().IsEmpty);
    }

    [Fact]
    public void TextSearch_MatchesEitherFace()
    {
        Library library = Sample();

        Assert.Single(CardSearch.Run(library, new CardQuery { Text = "house" }));
        Assert.Single(CardSearch.Run(library, new CardQuery { Text = "casa" }));
    }

    [Fact]
    public void TextSearch_IgnoresAccents()
    {
        var hits = CardSearch.Run(Sample(), new CardQuery { Text = "cafe" });

        Assert.Single(hits);
        Assert.Equal("coffee", hits[0].Card.Front);
    }

    [Fact]
    public void TextSearch_ReachesNotes()
    {
        var hits = CardSearch.Run(Sample(), new CardQuery { Text = "masculine" });

        Assert.Single(hits);
    }

    [Fact]
    public void ExactMatches_OutrankSubstrings()
    {
        var library = new Library();
        var deck = new Deck("d");
        deck.AddCard(new Card("greenhouse", "x"));
        deck.AddCard(new Card("house", "y"));
        library.AddDeck(deck);

        var hits = CardSearch.Run(library, new CardQuery { Text = "house" });

        Assert.Equal("house", hits[0].Card.Front);
        Assert.True(hits[0].Score > hits[1].Score);
    }

    [Fact]
    public void TagFilter_RequiresEveryTag()
    {
        Library library = Sample();

        Assert.Equal(2, CardSearch.Run(library, new CardQuery { Tags = ["nouns"] }).Count);
        Assert.Empty(CardSearch.Run(library, new CardQuery { Tags = ["nouns", "verbs"] }));
    }

    [Fact]
    public void StarredFilter_Works()
    {
        Library library = Sample();

        Assert.Single(CardSearch.Run(library, new CardQuery { Starred = true }));
        Assert.Equal(2, CardSearch.Run(library, new CardQuery { Starred = false }).Count);
    }

    [Fact]
    public void Sorting_IsHonoured()
    {
        var ascending = CardSearch.Run(Sample(), new CardQuery { Sort = CardSort.FrontAsc });
        var descending = CardSearch.Run(Sample(), new CardQuery { Sort = CardSort.FrontDesc });

        Assert.Equal("coffee", ascending[0].Card.Front);
        Assert.Equal(ascending.Select(h => h.Card.Front).Reverse(), descending.Select(h => h.Card.Front));
    }

    [Fact]
    public void Take_LimitsResults() =>
        Assert.Equal(2, CardSearch.Run(Sample(), new CardQuery { Take = 2 }).Count);

    [Fact]
    public void Suggest_ReturnsNothingForBlankInput()
    {
        Assert.Empty(CardSearch.Suggest(Sample(), ""));
        Assert.Empty(CardSearch.Suggest(Sample(), "   "));
        Assert.Single(CardSearch.Suggest(Sample(), "casa"));
    }

    [Fact]
    public void Hits_CarryTheirDeck()
    {
        var hits = CardSearch.Run(Sample(), new CardQuery { Text = "house" });

        Assert.Equal("Spanish", hits[0].Deck.Name);
    }
}
