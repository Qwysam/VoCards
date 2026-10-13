using VoCards.Core.Common;

namespace VoCards.Core.Models;

/// <summary>
/// One vocabulary item.
///
/// The 2021 version was three fields — <c>Front</c>, <c>Back</c> and a public mutable
/// <c>memorized</c> bool that callers were asked by comment not to touch. This version
/// owns its own scheduling state and exposes it through methods, so it cannot desync.
/// </summary>
public sealed class Card
{
    private readonly List<string> _tags = [];

    public Card(string front, string back)
    {
        Front = front.Trim();
        Back = back.Trim();
    }

    /// <summary>Stable identity. Survives edits, reordering and import/export.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The term being learnt, e.g. "Cat".</summary>
    public string Front { get; private set; }

    /// <summary>The meaning, e.g. "Кот". May hold comma-separated alternatives.</summary>
    public string Back { get; private set; }

    /// <summary>An optional sentence showing the word in use.</summary>
    public string? Example { get; private set; }

    /// <summary>Free-form learner notes: mnemonics, gender, irregular forms.</summary>
    public string? Notes { get; private set; }

    /// <summary>Optional phonetic hint, e.g. "/kæt/".</summary>
    public string? Pronunciation { get; private set; }

    /// <summary>Learner-assigned labels used for filtering and sub-deck study.</summary>
    public IReadOnlyList<string> Tags => _tags;

    /// <summary>Flagged for special attention.</summary>
    public bool IsStarred { get; private set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset ModifiedAt { get; private set; } = DateTimeOffset.Now;

    // ---------------------------------------------------------------- scheduling

    public CardState State { get; internal set; } = CardState.New;

    /// <summary>When this card next wants to be seen. Null while it is still new.</summary>
    public DateTimeOffset? DueAt { get; internal set; }

    /// <summary>Current spacing in days. Fractional while in learning steps.</summary>
    public double IntervalDays { get; internal set; }

    /// <summary>SM-2 ease factor. Starts at 2.5 and drifts with performance.</summary>
    public double EaseFactor { get; internal set; } = 2.5;

    /// <summary>Consecutive successful reviews since the last lapse.</summary>
    public int Repetitions { get; internal set; }

    /// <summary>How many times this card has been forgotten after graduating.</summary>
    public int Lapses { get; internal set; }

    /// <summary>Index into the deck's learning-step ladder while in Learning/Relearning.</summary>
    public int LearningStep { get; internal set; }

    /// <summary>FSRS memory stability, in days. Zero until the first review.</summary>
    public double Stability { get; internal set; }

    /// <summary>FSRS intrinsic difficulty, on a 1–10 scale.</summary>
    public double Difficulty { get; internal set; }

    public DateTimeOffset? LastReviewedAt { get; internal set; }

    /// <summary>Total gradings this card has received, across every study mode.</summary>
    public int ReviewCount { get; internal set; }

    /// <summary>Gradings of Hard or better.</summary>
    public int CorrectCount { get; internal set; }

    /// <summary>Fastest answer ever recorded for this card, for the speed leaderboard.</summary>
    public TimeSpan? BestResponseTime { get; internal set; }

    // ---------------------------------------------------------------- computed

    /// <summary>Share of reviews answered correctly, 0..1. Zero-review cards report 0.</summary>
    public double Accuracy => ReviewCount == 0 ? 0d : (double)CorrectCount / ReviewCount;

    /// <summary>True once this card has graduated onto real review intervals.</summary>
    public bool IsGraduated => State is CardState.Review or CardState.Relearning;

    /// <summary>
    /// A "leech": a card that keeps being forgotten. Surfaced so the learner can rewrite
    /// or bury it rather than grinding it forever.
    /// </summary>
    public bool IsLeech => Lapses >= LeechThreshold;

    /// <summary>Lapse count at which a card is considered a leech.</summary>
    public const int LeechThreshold = 6;

    /// <summary>
    /// Mastery on a 0..1 scale, blending interval length with accuracy. Used for the
    /// per-deck mastery bars and for "hardest first" queue ordering.
    /// </summary>
    public double Mastery
    {
        get
        {
            if (ReviewCount == 0)
            {
                return 0d;
            }

            // A 21-day interval is treated as fully spaced out.
            double spacing = Math.Clamp(IntervalDays / 21d, 0d, 1d);
            double reliability = Accuracy;
            double penalty = Math.Clamp(Lapses * 0.05d, 0d, 0.4d);

            return Math.Clamp(((spacing * 0.6d) + (reliability * 0.4d)) - penalty, 0d, 1d);
        }
    }

    /// <summary>Whether this card is ready to be studied at <paramref name="now"/>.</summary>
    public bool IsDue(DateTimeOffset now) => State switch
    {
        CardState.Suspended => false,
        CardState.New => true,
        _ => DueAt is null || DueAt <= now,
    };

    /// <summary>
    /// How many days overdue this card is. Negative when it is not due yet;
    /// zero for new cards, which are never "overdue".
    /// </summary>
    public double DaysOverdue(DateTimeOffset now) =>
        State is CardState.New or CardState.Suspended || DueAt is null
            ? 0d
            : (now - DueAt.Value).TotalDays;

    /// <summary>The accepted answers for the given direction, including alternatives.</summary>
    public IReadOnlyList<string> AcceptedAnswers(StudyDirection direction) =>
        TextTools.SplitAlternatives(AnswerText(direction)) is { Count: > 0 } parts
            ? parts
            : [AnswerText(direction)];

    /// <summary>The text shown as the prompt for the given direction.</summary>
    public string PromptText(StudyDirection direction) =>
        direction == StudyDirection.BackToFront ? Back : Front;

    /// <summary>The text expected as the answer for the given direction.</summary>
    public string AnswerText(StudyDirection direction) =>
        direction == StudyDirection.BackToFront ? Front : Back;

    // ---------------------------------------------------------------- mutation

    public Result Edit(string? front = null, string? back = null, string? example = null,
                       string? notes = null, string? pronunciation = null)
    {
        if (front is not null)
        {
            if (string.IsNullOrWhiteSpace(front))
            {
                return Result.Failure("The front of a card cannot be empty.");
            }

            Front = front.Trim();
        }

        if (back is not null)
        {
            if (string.IsNullOrWhiteSpace(back))
            {
                return Result.Failure("The back of a card cannot be empty.");
            }

            Back = back.Trim();
        }

        if (example is not null)
        {
            Example = Blank(example);
        }

        if (notes is not null)
        {
            Notes = Blank(notes);
        }

        if (pronunciation is not null)
        {
            Pronunciation = Blank(pronunciation);
        }

        Touch();
        return Result.Success();

        static string? Blank(string v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    public void Star(bool starred = true)
    {
        IsStarred = starred;
        Touch();
    }

    public void ToggleStar() => Star(!IsStarred);

    public bool AddTag(string tag)
    {
        string clean = tag.Trim();
        if (clean.Length == 0 || _tags.Contains(clean, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        _tags.Add(clean);
        Touch();
        return true;
    }

    public bool RemoveTag(string tag)
    {
        int index = _tags.FindIndex(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return false;
        }

        _tags.RemoveAt(index);
        Touch();
        return true;
    }

    public bool HasTag(string tag) => _tags.Contains(tag, StringComparer.OrdinalIgnoreCase);

    public void SetTags(IEnumerable<string> tags)
    {
        _tags.Clear();
        foreach (string tag in tags)
        {
            string clean = tag.Trim();
            if (clean.Length > 0 && !_tags.Contains(clean, StringComparer.OrdinalIgnoreCase))
            {
                _tags.Add(clean);
            }
        }

        Touch();
    }

    /// <summary>Takes the card out of rotation without deleting it.</summary>
    public void Suspend()
    {
        if (State == CardState.Suspended)
        {
            return;
        }

        StateBeforeSuspension = State;
        State = CardState.Suspended;
        Touch();
    }

    /// <summary>Returns a suspended card to whatever state it was in before.</summary>
    public void Unsuspend()
    {
        if (State != CardState.Suspended)
        {
            return;
        }

        State = StateBeforeSuspension ?? CardState.New;
        StateBeforeSuspension = null;
        Touch();
    }

    internal CardState? StateBeforeSuspension { get; set; }

    /// <summary>Wipes all scheduling progress, keeping the content. The "forget" action.</summary>
    public void ResetProgress()
    {
        State = CardState.New;
        StateBeforeSuspension = null;
        DueAt = null;
        IntervalDays = 0d;
        EaseFactor = 2.5d;
        Repetitions = 0;
        Lapses = 0;
        LearningStep = 0;
        Stability = 0d;
        Difficulty = 0d;
        LastReviewedAt = null;
        ReviewCount = 0;
        CorrectCount = 0;
        BestResponseTime = null;
        Touch();
    }

    /// <summary>A content-only copy, with fresh identity and no scheduling history.</summary>
    public Card CloneContent()
    {
        var copy = new Card(Front, Back)
        {
            Example = Example,
            Notes = Notes,
            Pronunciation = Pronunciation,
            IsStarred = IsStarred,
        };

        copy.SetTags(_tags);
        return copy;
    }

    /// <summary>
    /// Restores the fields that are otherwise write-protected. Used only by the
    /// deserializer, which is why it is internal rather than public.
    /// </summary>
    internal void RestoreContent(
        string? example,
        string? notes,
        string? pronunciation,
        bool starred,
        DateTimeOffset modifiedAt,
        IEnumerable<string> tags)
    {
        Example = example;
        Notes = notes;
        Pronunciation = pronunciation;
        IsStarred = starred;
        SetTags(tags);
        ModifiedAt = modifiedAt;
    }

    internal void Touch() => ModifiedAt = DateTimeOffset.Now;

    public override string ToString() => $"{Front} → {Back}";
}
