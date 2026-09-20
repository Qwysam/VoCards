using VoCards.Core.Common;
using VoCards.Core.Gamification;
using VoCards.Core.Models;
using VoCards.Core.Scheduling;

namespace VoCards.Core.Study;

/// <summary>Where a session is in its lifecycle.</summary>
public enum SessionPhase
{
    /// <summary>Showing a prompt; the answer is hidden.</summary>
    Prompting,

    /// <summary>The answer is on screen and awaiting a grade.</summary>
    Revealed,

    /// <summary>Every card has been graded.</summary>
    Finished,
}

/// <summary>
/// A study run, as an explicit state machine.
///
/// This replaces <c>Deck.GoThroughCards()</c>, which drove the whole interaction from
/// a single method nested five levels deep — <c>else</c> → <c>for(;;)</c> → <c>if</c>
/// → <c>for(;;)</c> → <c>if</c> — reading from the console at the bottom. Here each
/// step is one public method that returns, so the same engine drives a browser, a
/// test, or anything else.
/// </summary>
public sealed class StudySession
{
    private readonly List<Card> _queue;
    private readonly List<SessionCardResult> _results = [];
    private readonly Library _library;
    private readonly IClock _clock;
    private readonly Random _random;
    private readonly DateTimeOffset _startedAt;

    private int _index;
    private DateTimeOffset _questionShownAt;

    private StudySession(Library library, Deck deck, IReadOnlyList<Card> queue, StudyOptions options, IClock clock)
    {
        _library = library;
        Deck = deck;
        Options = options;
        _clock = clock;
        _queue = [.. queue];
        _random = options.Seed is { } seed ? new Random(seed) : Random.Shared;
        _startedAt = clock.Now;
        _questionShownAt = clock.Now;

        Advance(initial: true);
    }

    /// <summary>Starts a session over one deck. Fails when nothing qualifies.</summary>
    public static Result<StudySession> Start(Library library, Deck deck, StudyOptions options, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        IReadOnlyList<Card> queue = DueSelector.Build(deck, options, clock.Now);

        return queue.Count == 0
            ? Result.Failure<StudySession>(EmptyReason(deck, options))
            : Result.Success(new StudySession(library, deck, queue, options, clock));
    }

    /// <summary>Starts a session pooling several decks. Cards keep their own deck's settings.</summary>
    public static Result<StudySession> StartAcross(
        Library library,
        IReadOnlyList<Deck> decks,
        StudyOptions options,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(decks);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        if (decks.Count == 0)
        {
            return Result.Failure<StudySession>("There are no decks to study yet.");
        }

        IReadOnlyList<Card> queue = DueSelector.BuildAcross(decks, options, clock.Now);

        return queue.Count == 0
            ? Result.Failure<StudySession>("Nothing is due right now. Enjoy the break.")
            : Result.Success(new StudySession(library, decks[0], queue, options, clock));
    }

    private static string EmptyReason(Deck deck, StudyOptions options) => true switch
    {
        _ when deck.TotalCards == 0 => "This deck has no cards yet. Add one to get started.",
        _ when options.StarredOnly => "No starred cards are waiting in this deck.",
        _ when options.LeechesOnly => "No leeches here — nothing is giving you that much trouble.",
        _ when options.Tag is { Length: > 0 } => $"No cards tagged “{options.Tag}” are due right now.",
        _ when deck.SuspendedCards == deck.TotalCards => "Every card in this deck is suspended.",
        _ => "Nothing is due in this deck yet. Come back later, or use cram mode.",
    };

    // ---------------------------------------------------------------- state

    public Deck Deck { get; }

    public StudyOptions Options { get; }

    public SessionPhase Phase { get; private set; } = SessionPhase.Prompting;

    /// <summary>The question on screen, or null once the session has finished.</summary>
    public StudyQuestion? Current { get; private set; }

    public bool IsFinished => Phase == SessionPhase.Finished;

    /// <summary>Cards graded so far.</summary>
    public int Completed => _results.Count;

    /// <summary>Cards in the queue in total.</summary>
    public int Total => _queue.Count;

    /// <summary>Cards still to grade, including the one on screen.</summary>
    public int Remaining => Math.Max(0, _queue.Count - _index);

    public double Progress => Total == 0 ? 1d : (double)Completed / Total;

    public int CorrectSoFar => _results.Count(static r => r.WasCorrect);

    /// <summary>Consecutive correct answers, for the combo indicator.</summary>
    public int Combo { get; private set; }

    public int BestCombo { get; private set; }

    /// <summary>The grade of the most recent answer, for the feedback flash.</summary>
    public GradedAnswer? LastGrade { get; private set; }

    /// <summary>XP banked so far this session.</summary>
    public long XpEarned { get; private set; }

    /// <summary>
    /// The next <paramref name="count"/> cards in queue order, starting with the one
    /// on screen, without advancing anything.
    ///
    /// Match mode needs a whole batch on screen at once. Returning them in queue
    /// order means the caller can grade them back with plain <see cref="Grade"/>
    /// calls in the same order, so the batch view needs no special grading path.
    /// </summary>
    public IReadOnlyList<Card> PeekBatch(int count)
    {
        if (count <= 0 || _index >= _queue.Count)
        {
            return [];
        }

        int take = Math.Min(count, _queue.Count - _index);
        return _queue.GetRange(_index, take);
    }

    // ---------------------------------------------------------------- actions

    /// <summary>Shows the answer. In flip mode this is what the learner does before grading.</summary>
    public void Reveal()
    {
        if (Phase == SessionPhase.Prompting)
        {
            Phase = SessionPhase.Revealed;
        }
    }

    /// <summary>
    /// Grades the current card and moves on. Safe to call at any time: once the session
    /// has finished it does nothing.
    /// </summary>
    public void Grade(Rating rating, AnswerVerdict verdict = AnswerVerdict.Correct, string? typedAnswer = null)
    {
        if (Current is not { } question)
        {
            return;
        }

        DateTimeOffset now = _clock.Now;
        TimeSpan elapsed = now - _questionShownAt;

        // Cram never touches the schedule — that is the whole point of cram.
        string nextInterval = "—";
        if (!Options.Cram)
        {
            Deck owner = _library.LocateCard(question.Card.Id)?.Deck ?? Deck;
            Review review = CardReviewer.Apply(
                question.Card, owner, rating, now, question.Mode, question.Direction, verdict, elapsed);

            _library.History.Add(review);
            _library.Profile.RecordReview();
            nextInterval = SchedulingOutcome.FormatInterval(review.NewIntervalDays);
        }

        bool correct = rating != Rating.Again;
        Combo = correct ? Combo + 1 : 0;
        BestCombo = Math.Max(BestCombo, Combo);
        XpEarned += XpRules.ForAnswer(rating, question.Card.State, Combo);

        _results.Add(new SessionCardResult
        {
            CardId = question.Card.Id,
            Front = question.Card.Front,
            Back = question.Card.Back,
            Rating = rating,
            Verdict = verdict,
            Elapsed = elapsed,
            TypedAnswer = typedAnswer,
            NextInterval = nextInterval,
        });

        _index++;
        Advance(initial: false);
    }

    /// <summary>
    /// Grades a typed answer: works out the verdict, records it, and advances. Returns
    /// the grade so the UI can show the diff before the next card appears.
    /// </summary>
    public GradedAnswer SubmitTyped(string? typed)
    {
        if (Current is not { } question)
        {
            return new GradedAnswer
            {
                Verdict = AnswerVerdict.Skipped,
                ClosestAnswer = string.Empty,
                Similarity = 0d,
                AcceptedAnswers = [],
            };
        }

        GradedAnswer grade = AnswerGrader.Grade(
            question.Card, typed, question.Deck.Settings, question.Direction);

        LastGrade = grade;
        Phase = SessionPhase.Revealed;
        return grade;
    }

    /// <summary>Commits the grade produced by <see cref="SubmitTyped"/> and moves on.</summary>
    public void CommitTyped(GradedAnswer grade, string? typed)
    {
        ArgumentNullException.ThrowIfNull(grade);
        Grade(grade.ImpliedRating, grade.Verdict, typed);
    }

    /// <summary>Answers a multiple-choice question by option index.</summary>
    public GradedAnswer Choose(int choiceIndex)
    {
        if (Current is not { } question)
        {
            return new GradedAnswer
            {
                Verdict = AnswerVerdict.Skipped,
                ClosestAnswer = string.Empty,
                Similarity = 0d,
                AcceptedAnswers = [],
            };
        }

        bool correct = choiceIndex == question.CorrectChoiceIndex;

        var grade = new GradedAnswer
        {
            Verdict = correct ? AnswerVerdict.Correct : AnswerVerdict.Incorrect,
            ClosestAnswer = question.Answer,
            Similarity = correct ? 1d : 0d,
            AcceptedAnswers = question.Card.AcceptedAnswers(question.Direction),
        };

        LastGrade = grade;
        Phase = SessionPhase.Revealed;
        return grade;
    }

    /// <summary>Skips the current card without grading it, pushing it to the back of the queue.</summary>
    public void Skip()
    {
        if (Current is not { } question || Remaining <= 1)
        {
            // Nothing to push it behind — grade it as a miss instead of looping forever.
            Grade(Rating.Again, AnswerVerdict.Skipped);
            return;
        }

        _queue.RemoveAt(_index);
        _queue.Add(question.Card);
        Advance(initial: false);
    }

    /// <summary>Ends the session early, keeping whatever has already been graded.</summary>
    public void Finish()
    {
        _index = _queue.Count;
        Current = null;
        Phase = SessionPhase.Finished;
    }

    /// <summary>
    /// Folds the session into the profile — XP, streak, achievements — and returns the
    /// recap. Call this once, after the session has finished.
    /// </summary>
    public SessionSummary BuildSummary()
    {
        DateTimeOffset now = _clock.Now;
        int levelsGained = _library.Profile.AwardXp(XpEarned);

        StreakOutcome streak = _results.Count > 0
            ? _library.Profile.RegisterStudyDay(DateOnly.FromDateTime(now.LocalDateTime))
            : StreakOutcome.AlreadyCounted;

        var summary = new SessionSummary
        {
            Results = _results,
            Duration = now - _startedAt,
            XpEarned = XpEarned,
            LevelsGained = levelsGained,
            StreakOutcome = streak,
        };

        IReadOnlyList<Achievement> unlocked = AchievementEngine.Evaluate(_library, summary, now);

        return summary with
        {
            UnlockedAchievements = unlocked.Select(static a => a.Id).ToArray(),
        };
    }

    // ---------------------------------------------------------------- queue walking

    /// <summary>
    /// Moves to the next card, or finishes. One method, one level of nesting — the
    /// original walked the deck with an index loop that mutated the count it was
    /// looping over.
    /// </summary>
    private void Advance(bool initial)
    {
        if (!initial)
        {
            LastGrade = null;
        }

        if (_index >= _queue.Count)
        {
            Current = null;
            Phase = SessionPhase.Finished;
            return;
        }

        Card card = _queue[_index];
        Deck owner = _library.LocateCard(card.Id)?.Deck ?? Deck;
        StudyDirection direction = ResolveDirection(card);

        Current = BuildQuestion(card, owner, direction);
        _questionShownAt = _clock.Now;
        Phase = SessionPhase.Prompting;
    }

    private StudyDirection ResolveDirection(Card card) => Options.Direction switch
    {
        StudyDirection.Mixed => _random.Next(2) == 0
            ? StudyDirection.FrontToBack
            : StudyDirection.BackToFront,
        _ => Options.Direction,
    };

    private StudyQuestion BuildQuestion(Card card, Deck owner, StudyDirection direction)
    {
        var question = new StudyQuestion
        {
            Card = card,
            Deck = owner,
            Direction = direction,
            Mode = Options.Mode,
            Index = _index,
            Total = _queue.Count,
        };

        if (Options.Mode is not (StudyMode.MultipleChoice or StudyMode.SpeedRound))
        {
            return question;
        }

        (IReadOnlyList<string> choices, int correctIndex) = BuildChoices(card, owner, direction);
        return question with { Choices = choices, CorrectChoiceIndex = correctIndex };
    }

    /// <summary>
    /// Picks distractors from the same deck, preferring cards the learner has actually
    /// seen so the wrong options are plausible rather than obviously filler.
    /// </summary>
    private (IReadOnlyList<string> Choices, int CorrectIndex) BuildChoices(
        Card card,
        Deck owner,
        StudyDirection direction)
    {
        string answer = card.AnswerText(direction);
        int wanted = Math.Max(2, Options.ChoiceCount);

        var distractors = owner.Cards
            .Where(c => c.Id != card.Id)
            .Select(c => c.AnswerText(direction))
            .Where(text => !string.Equals(text, answer, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(_ => _random.Next())
            .Take(wanted - 1)
            .ToList();

        // A deck too small to fill the options simply shows fewer of them.
        var choices = new List<string>(distractors.Count + 1) { answer };
        choices.AddRange(distractors);

        for (int i = choices.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (choices[i], choices[j]) = (choices[j], choices[i]);
        }

        return (choices, choices.IndexOf(answer));
    }
}
