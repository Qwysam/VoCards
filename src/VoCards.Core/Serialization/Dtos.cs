using VoCards.Core.Models;

namespace VoCards.Core.Serialization;

/// <summary>
/// The on-disk shape of a library.
///
/// These exist so the persisted format is an explicit, versioned contract rather than
/// whatever the entity classes happen to look like today. The 2021 app serialized its
/// live object graph with <c>BinaryFormatter</c>, which meant that renaming a private
/// field broke every existing save — and which was removed from .NET entirely in 9.0
/// after a decade of deserialization vulnerabilities.
/// </summary>
public sealed record LibraryDto
{
    public int Version { get; init; } = Library.SchemaVersion;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset ExportedAt { get; init; } = DateTimeOffset.Now;

    public ProfileDto Profile { get; init; } = new();

    public IReadOnlyList<DeckDto> Decks { get; init; } = [];

    public IReadOnlyList<ReviewDto> Reviews { get; init; } = [];
}

/// <summary>A single deck, optionally shared on its own.</summary>
public sealed record DeckDto
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = "Untitled";

    public string? Description { get; init; }

    public string Emoji { get; init; } = "📚";

    public string ColorToken { get; init; } = "violet";

    public string FrontLanguage { get; init; } = "en-US";

    public string BackLanguage { get; init; } = "en-US";

    public bool IsPinned { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset ModifiedAt { get; init; } = DateTimeOffset.Now;

    public DeckSettingsDto Settings { get; init; } = new();

    public IReadOnlyList<CardDto> Cards { get; init; } = [];
}

public sealed record CardDto
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Front { get; init; } = string.Empty;

    public string Back { get; init; } = string.Empty;

    public string? Example { get; init; }

    public string? Notes { get; init; }

    public string? Pronunciation { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public bool IsStarred { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset ModifiedAt { get; init; } = DateTimeOffset.Now;

    public CardState State { get; init; } = CardState.New;

    public CardState? StateBeforeSuspension { get; init; }

    public DateTimeOffset? DueAt { get; init; }

    public double IntervalDays { get; init; }

    public double EaseFactor { get; init; } = 2.5d;

    public int Repetitions { get; init; }

    public int Lapses { get; init; }

    public int LearningStep { get; init; }

    public double Stability { get; init; }

    public double Difficulty { get; init; }

    public DateTimeOffset? LastReviewedAt { get; init; }

    public int ReviewCount { get; init; }

    public int CorrectCount { get; init; }

    public long? BestResponseTicks { get; init; }
}

public sealed record DeckSettingsDto
{
    public SchedulerKind Scheduler { get; init; } = SchedulerKind.Sm2;

    public StudyDirection Direction { get; init; } = StudyDirection.FrontToBack;

    public StudyMode DefaultMode { get; init; } = StudyMode.Flip;

    public int NewCardsPerDay { get; init; } = 20;

    public int MaxReviewsPerDay { get; init; } = 200;

    public QueueOrder Order { get; init; } = QueueOrder.DueFirst;

    public IReadOnlyList<double> LearningStepsMinutes { get; init; } = [1d, 10d];

    public IReadOnlyList<double> RelearningStepsMinutes { get; init; } = [10d];

    public double GraduatingIntervalDays { get; init; } = 1d;

    public double EasyIntervalDays { get; init; } = 4d;

    public double MaximumIntervalDays { get; init; } = 365d * 5d;

    public double IntervalModifier { get; init; } = 1d;

    public double DesiredRetention { get; init; } = 0.9d;

    public bool AutoSpeak { get; init; }

    public bool IgnoreAccents { get; init; } = true;

    public bool IgnoreCase { get; init; } = true;

    public double TypoTolerance { get; init; } = 0.85d;
}

public sealed record ProfileDto
{
    public string DisplayName { get; init; } = "Learner";

    public string Theme { get; init; } = "system";

    public string Accent { get; init; } = "violet";

    public bool ReduceMotion { get; init; }

    public bool CardFlipAnimation { get; init; } = true;

    public bool SoundEffects { get; init; } = true;

    public int DailyGoal { get; init; } = 30;

    public string? PreferredVoice { get; init; }

    public double SpeechRate { get; init; } = 0.95d;

    public double SpeechPitch { get; init; } = 1d;

    public bool ShowAlternatives { get; init; } = true;

    public bool HasOnboarded { get; init; }

    public long Xp { get; init; }

    public int CurrentStreak { get; init; }

    public int LongestStreak { get; init; }

    public DateOnly? LastStudyDay { get; init; }

    public int StreakFreezes { get; init; } = 2;

    public long LifetimeReviews { get; init; }

    public IReadOnlyDictionary<string, DateTimeOffset> Achievements { get; init; } =
        new Dictionary<string, DateTimeOffset>();
}

public sealed record ReviewDto
{
    public Guid CardId { get; init; }

    public Guid DeckId { get; init; }

    public DateTimeOffset ReviewedAt { get; init; }

    public Rating Rating { get; init; } = Rating.Good;

    public StudyMode Mode { get; init; } = StudyMode.Flip;

    public StudyDirection Direction { get; init; } = StudyDirection.FrontToBack;

    public AnswerVerdict Verdict { get; init; } = AnswerVerdict.Correct;

    public long ElapsedTicks { get; init; }

    public double PreviousIntervalDays { get; init; }

    public double NewIntervalDays { get; init; }

    public CardState PreviousState { get; init; }

    public CardState NewState { get; init; }
}
