using VoCards.Core.Models;

namespace VoCards.Core.Study;

/// <summary>What one card did during one session, for the recap screen.</summary>
public sealed record SessionCardResult
{
    public required Guid CardId { get; init; }

    public required string Front { get; init; }

    public required string Back { get; init; }

    public required Rating Rating { get; init; }

    public required AnswerVerdict Verdict { get; init; }

    public TimeSpan Elapsed { get; init; }

    /// <summary>What the learner actually typed, in typing mode.</summary>
    public string? TypedAnswer { get; init; }

    /// <summary>The interval the card was granted, formatted for display.</summary>
    public string NextInterval { get; init; } = "—";

    public bool WasCorrect => Rating != Rating.Again;
}

/// <summary>The tally shown when a session ends.</summary>
public sealed record SessionSummary
{
    public required IReadOnlyList<SessionCardResult> Results { get; init; }

    public required TimeSpan Duration { get; init; }

    public required long XpEarned { get; init; }

    /// <summary>Levels gained during this session.</summary>
    public int LevelsGained { get; init; }

    /// <summary>What this session did to the streak.</summary>
    public StreakOutcome StreakOutcome { get; init; } = StreakOutcome.AlreadyCounted;

    /// <summary>Achievements unlocked by this session.</summary>
    public IReadOnlyList<string> UnlockedAchievements { get; init; } = [];

    public int Total => Results.Count;

    public int Correct => Results.Count(static r => r.WasCorrect);

    public int Incorrect => Total - Correct;

    public int Again => CountRating(Rating.Again);

    public int Hard => CountRating(Rating.Hard);

    public int Good => CountRating(Rating.Good);

    public int Easy => CountRating(Rating.Easy);

    /// <summary>Share answered correctly, 0..1.</summary>
    public double Accuracy => Total == 0 ? 0d : (double)Correct / Total;

    public TimeSpan AverageTime => Total == 0
        ? TimeSpan.Zero
        : TimeSpan.FromMilliseconds(Results.Average(static r => r.Elapsed.TotalMilliseconds));

    /// <summary>Cards that were missed, for the "review these again" action.</summary>
    public IReadOnlyList<SessionCardResult> Missed =>
        Results.Where(static r => !r.WasCorrect).ToArray();

    private int CountRating(Rating rating) => Results.Count(r => r.Rating == rating);

    /// <summary>A short verdict used as the headline on the recap screen.</summary>
    public string Headline => Accuracy switch
    {
        _ when Total == 0 => "Nothing to review",
        >= 0.95d => "Flawless",
        >= 0.80d => "Strong session",
        >= 0.60d => "Solid progress",
        >= 0.40d => "Keep going",
        _ => "Tough round",
    };
}
