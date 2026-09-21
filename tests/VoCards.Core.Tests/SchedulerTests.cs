using VoCards.Core.Models;
using VoCards.Core.Scheduling;

namespace VoCards.Core.Tests;

public class Sm2SchedulerTests
{
    private readonly Sm2Scheduler _scheduler = new();
    private readonly DeckSettings _settings = new();

    [Fact]
    public void NewCard_OnGood_MovesToSecondLearningStep()
    {
        var card = new Card("a", "b");

        SchedulingOutcome outcome = _scheduler.Schedule(card, Rating.Good, _settings, TestData.Epoch);

        Assert.Equal(CardState.Learning, outcome.State);
        Assert.Equal(1, outcome.LearningStep);
        Assert.Equal(10d / 1440d, outcome.IntervalDays, 6);
    }

    [Fact]
    public void NewCard_OnEasy_GraduatesImmediately()
    {
        var card = new Card("a", "b");

        SchedulingOutcome outcome = _scheduler.Schedule(card, Rating.Easy, _settings, TestData.Epoch);

        Assert.Equal(CardState.Review, outcome.State);
        Assert.True(outcome.WasGraduation);
        Assert.Equal(_settings.EasyIntervalDays, outcome.IntervalDays, 6);
    }

    [Fact]
    public void LearningCard_OnAgain_ReturnsToTheFirstStep()
    {
        var card = new Card("a", "b") { State = CardState.Learning, LearningStep = 1 };

        SchedulingOutcome outcome = _scheduler.Schedule(card, Rating.Again, _settings, TestData.Epoch);

        Assert.Equal(0, outcome.LearningStep);
        Assert.Equal(CardState.Learning, outcome.State);
    }

    [Fact]
    public void LearningCard_OnGoodAtTheLastStep_Graduates()
    {
        var card = new Card("a", "b")
        {
            State = CardState.Learning,
            LearningStep = _settings.LearningStepsMinutes.Count - 1,
        };

        SchedulingOutcome outcome = _scheduler.Schedule(card, Rating.Good, _settings, TestData.Epoch);

        Assert.Equal(CardState.Review, outcome.State);
        Assert.Equal(_settings.GraduatingIntervalDays, outcome.IntervalDays, 6);
    }

    [Fact]
    public void ReviewCard_OnGood_MultipliesIntervalByEase()
    {
        Card card = TestData.ReviewCard(intervalDays: 10d, ease: 2.5d);

        SchedulingOutcome outcome = _scheduler.Schedule(card, Rating.Good, _settings, TestData.Epoch);

        Assert.Equal(CardState.Review, outcome.State);
        Assert.Equal(25d, outcome.IntervalDays, 4);
        Assert.Equal(2.5d, outcome.EaseFactor, 4);
    }

    [Fact]
    public void ReviewCard_OnHard_GrowsSlowly_AndLosesEase()
    {
        Card card = TestData.ReviewCard(intervalDays: 10d, ease: 2.5d);

        SchedulingOutcome outcome = _scheduler.Schedule(card, Rating.Hard, _settings, TestData.Epoch);

        Assert.Equal(12d, outcome.IntervalDays, 4);
        Assert.Equal(2.35d, outcome.EaseFactor, 4);
    }

    [Fact]
    public void ReviewCard_OnEasy_JumpsFurther_AndGainsEase()
    {
        Card card = TestData.ReviewCard(intervalDays: 10d, ease: 2.5d);

        SchedulingOutcome outcome = _scheduler.Schedule(card, Rating.Easy, _settings, TestData.Epoch);

        Assert.True(outcome.IntervalDays > 25d, $"expected more than Good's 25 days, got {outcome.IntervalDays}");
        Assert.Equal(2.65d, outcome.EaseFactor, 4);
    }

    [Fact]
    public void ReviewCard_OnAgain_Lapses_IntoRelearning()
    {
        Card card = TestData.ReviewCard(intervalDays: 30d, ease: 2.5d, lapses: 1);

        SchedulingOutcome outcome = _scheduler.Schedule(card, Rating.Again, _settings, TestData.Epoch);

        Assert.Equal(CardState.Relearning, outcome.State);
        Assert.True(outcome.WasLapse);
        Assert.Equal(2, outcome.Lapses);
        Assert.Equal(2.3d, outcome.EaseFactor, 4);
        Assert.Equal(0, outcome.Repetitions);
    }

    [Fact]
    public void Ease_IsClampedToItsFloor()
    {
        Card card = TestData.ReviewCard(intervalDays: 5d, ease: Sm2Scheduler.MinimumEase);

        SchedulingOutcome outcome = _scheduler.Schedule(card, Rating.Again, _settings, TestData.Epoch);

        Assert.Equal(Sm2Scheduler.MinimumEase, outcome.EaseFactor, 4);
    }

    [Fact]
    public void Ease_IsClampedToItsCeiling()
    {
        Card card = TestData.ReviewCard(intervalDays: 5d, ease: Sm2Scheduler.MaximumEase);

        SchedulingOutcome outcome = _scheduler.Schedule(card, Rating.Easy, _settings, TestData.Epoch);

        Assert.Equal(Sm2Scheduler.MaximumEase, outcome.EaseFactor, 4);
    }

    [Fact]
    public void Interval_NeverExceedsTheConfiguredMaximum()
    {
        var settings = new DeckSettings { MaximumIntervalDays = 100d };
        Card card = TestData.ReviewCard(intervalDays: 90d, ease: 2.9d);

        SchedulingOutcome outcome = _scheduler.Schedule(card, Rating.Easy, settings, TestData.Epoch);

        Assert.True(outcome.IntervalDays <= 100d, $"interval {outcome.IntervalDays} exceeded the cap");
    }

    [Fact]
    public void CorrectAnswer_NeverShrinksTheInterval()
    {
        Card card = TestData.ReviewCard(intervalDays: 40d, ease: Sm2Scheduler.MinimumEase);

        foreach (Rating rating in (Rating[])[Rating.Hard, Rating.Good, Rating.Easy])
        {
            SchedulingOutcome outcome = _scheduler.Schedule(card, rating, _settings, TestData.Epoch);
            Assert.True(outcome.IntervalDays > 40d, $"{rating} shrank 40 d to {outcome.IntervalDays}");
        }
    }

    [Fact]
    public void OverdueCard_GetsCreditForSurvivingTheExtraDays()
    {
        Card card = TestData.ReviewCard(intervalDays: 10d, ease: 2.5d);

        SchedulingOutcome onTime = _scheduler.Schedule(card, Rating.Good, _settings, TestData.Epoch.AddDays(10));
        SchedulingOutcome late = _scheduler.Schedule(card, Rating.Good, _settings, TestData.Epoch.AddDays(30));

        Assert.True(late.IntervalDays > onTime.IntervalDays,
            $"late review {late.IntervalDays} should beat on-time {onTime.IntervalDays}");
    }

    [Fact]
    public void PreviewIntervals_AreOrdered_AgainThroughEasy()
    {
        Card card = TestData.ReviewCard(intervalDays: 10d);

        var previews = ((IScheduler)_scheduler).PreviewIntervals(card, _settings, TestData.Epoch);

        Assert.True(previews[Rating.Again] < previews[Rating.Hard]);
        Assert.True(previews[Rating.Hard] < previews[Rating.Good]);
        Assert.True(previews[Rating.Good] < previews[Rating.Easy]);
    }

    [Fact]
    public void Scheduling_DoesNotMutateTheCard()
    {
        Card card = TestData.ReviewCard(intervalDays: 10d, ease: 2.5d);
        CardState state = card.State;
        double interval = card.IntervalDays;
        double ease = card.EaseFactor;

        _scheduler.Schedule(card, Rating.Again, _settings, TestData.Epoch);

        Assert.Equal(state, card.State);
        Assert.Equal(interval, card.IntervalDays);
        Assert.Equal(ease, card.EaseFactor);
    }

    [Fact]
    public void DeckWithNoLearningSteps_GraduatesOnTheFirstAnswer()
    {
        var settings = new DeckSettings { LearningStepsMinutes = [] };
        var card = new Card("a", "b");

        SchedulingOutcome outcome = _scheduler.Schedule(card, Rating.Good, settings, TestData.Epoch);

        Assert.Equal(CardState.Review, outcome.State);
    }

    [Theory]
    [InlineData(0.0005, "<1 min")]
    [InlineData(0.007, "10 min")]
    [InlineData(0.25, "6 h")]
    [InlineData(3, "3 d")]
    [InlineData(45, "1.5 mo")]
    [InlineData(730, "2 y")]
    public void IntervalLabels_ReadTheWayAUiWants(double days, string expected) =>
        Assert.Equal(expected, SchedulingOutcome.FormatInterval(days));
}

public class FsrsSchedulerTests
{
    private readonly FsrsScheduler _scheduler = new();
    private readonly DeckSettings _settings = new() { Scheduler = SchedulerKind.Fsrs };

    [Fact]
    public void FirstReview_SetsStabilityFromTheRating()
    {
        var easyCard = new Card("a", "b");
        var hardCard = new Card("a", "b");

        SchedulingOutcome easy = _scheduler.Schedule(easyCard, Rating.Easy, _settings, TestData.Epoch);
        SchedulingOutcome hard = _scheduler.Schedule(hardCard, Rating.Hard, _settings, TestData.Epoch);

        Assert.True(easy.Stability > hard.Stability,
            $"Easy stability {easy.Stability} should exceed Hard's {hard.Stability}");
        Assert.True(easy.IntervalDays > hard.IntervalDays);
    }

    [Fact]
    public void FirstReview_SetsDifficultyInverselyToTheRating()
    {
        var easyCard = new Card("a", "b");
        var againCard = new Card("a", "b");

        SchedulingOutcome easy = _scheduler.Schedule(easyCard, Rating.Easy, _settings, TestData.Epoch);
        SchedulingOutcome again = _scheduler.Schedule(againCard, Rating.Again, _settings, TestData.Epoch);

        Assert.True(easy.Difficulty < again.Difficulty,
            $"an easy card ({easy.Difficulty}) should be less difficult than a forgotten one ({again.Difficulty})");
        Assert.InRange(easy.Difficulty, 1d, 10d);
        Assert.InRange(again.Difficulty, 1d, 10d);
    }

    [Fact]
    public void Retrievability_IsExactlyNinetyPercent_AtOneStability()
    {
        double r = FsrsScheduler.Retrievability(elapsedDays: 10d, stability: 10d);

        Assert.Equal(0.9d, r, 6);
    }

    [Fact]
    public void Retrievability_DecaysOverTime()
    {
        double fresh = FsrsScheduler.Retrievability(1d, 10d);
        double stale = FsrsScheduler.Retrievability(100d, 10d);

        Assert.True(fresh > stale);
        Assert.InRange(fresh, 0d, 1d);
        Assert.InRange(stale, 0d, 1d);
    }

    [Fact]
    public void IntervalForRetention_IsShorter_WhenMoreRetentionIsDemanded()
    {
        double relaxed = FsrsScheduler.IntervalForRetention(stability: 20d, desiredRetention: 0.80d);
        double strict = FsrsScheduler.IntervalForRetention(stability: 20d, desiredRetention: 0.95d);

        Assert.True(strict < relaxed, $"95% retention ({strict} d) should be tighter than 80% ({relaxed} d)");
    }

    [Fact]
    public void IntervalForRetention_IsStability_AtNinetyPercent()
    {
        double interval = FsrsScheduler.IntervalForRetention(stability: 15d, desiredRetention: 0.9d);

        Assert.Equal(15d, interval, 4);
    }

    [Fact]
    public void Lapse_NeverIncreasesStability()
    {
        var deck = new Deck("d") { Settings = _settings };
        var card = new Card("a", "b");
        deck.AddCard(card);

        CardReviewer.Apply(card, deck, Rating.Easy, TestData.Epoch);
        double before = card.Stability;

        CardReviewer.Apply(card, deck, Rating.Again, TestData.Epoch.AddDays(5));

        Assert.True(card.Stability <= before,
            $"stability rose from {before} to {card.Stability} on a lapse");
    }

    [Fact]
    public void SuccessfulReviews_GrowStabilityMonotonically()
    {
        var deck = new Deck("d") { Settings = _settings };
        var card = new Card("a", "b");
        deck.AddCard(card);

        DateTimeOffset now = TestData.Epoch;
        CardReviewer.Apply(card, deck, Rating.Good, now);

        for (int i = 0; i < 5; i++)
        {
            double before = card.Stability;
            now = card.DueAt ?? now.AddDays(1);
            CardReviewer.Apply(card, deck, Rating.Good, now);

            Assert.True(card.Stability >= before,
                $"review {i}: stability fell from {before} to {card.Stability}");
        }
    }

    [Fact]
    public void ConstructedWithWrongWeightCount_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new FsrsScheduler([1d, 2d, 3d]));

        Assert.Contains("17", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultWeights_AreTheSeventeenFsrsParameters() =>
        Assert.Equal(17, FsrsScheduler.Weights.Count);

    [Fact]
    public void Factory_ReturnsTheSchedulerTheDeckAskedFor()
    {
        Assert.Equal(SchedulerKind.Sm2, SchedulerFactory.For(SchedulerKind.Sm2).Kind);
        Assert.Equal(SchedulerKind.Fsrs, SchedulerFactory.For(SchedulerKind.Fsrs).Kind);
        Assert.Equal(2, SchedulerFactory.All.Count);
    }
}
