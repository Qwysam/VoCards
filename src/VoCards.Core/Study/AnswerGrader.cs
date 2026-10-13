using VoCards.Core.Common;
using VoCards.Core.Models;

namespace VoCards.Core.Study;

/// <summary>The outcome of grading a typed answer.</summary>
public sealed record GradedAnswer
{
    public required AnswerVerdict Verdict { get; init; }

    /// <summary>The accepted answer that came closest to what was typed.</summary>
    public required string ClosestAnswer { get; init; }

    /// <summary>Similarity to <see cref="ClosestAnswer"/>, 0..1.</summary>
    public required double Similarity { get; init; }

    /// <summary>Every answer that would have been accepted.</summary>
    public required IReadOnlyList<string> AcceptedAnswers { get; init; }

    /// <summary>Correct and almost-correct both count as recall.</summary>
    public bool IsAcceptable => Verdict is AnswerVerdict.Correct or AnswerVerdict.AlmostCorrect;

    /// <summary>
    /// The rating a typed answer implies. An exact hit is Good rather than Easy —
    /// "Easy" stays a deliberate choice the learner makes.
    /// </summary>
    public Rating ImpliedRating => Verdict switch
    {
        AnswerVerdict.Correct => Rating.Good,
        AnswerVerdict.AlmostCorrect => Rating.Hard,
        _ => Rating.Again,
    };
}

/// <summary>
/// Grades free-text answers with configurable tolerance, so that "recieve" is a typo
/// and "banana" is not.
/// </summary>
public static class AnswerGrader
{
    /// <summary>Grades <paramref name="typed"/> against every accepted answer for the card.</summary>
    public static GradedAnswer Grade(Card card, string? typed, DeckSettings settings, StudyDirection direction)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(settings);

        IReadOnlyList<string> accepted = card.AcceptedAnswers(direction);

        if (string.IsNullOrWhiteSpace(typed))
        {
            return new GradedAnswer
            {
                Verdict = AnswerVerdict.Skipped,
                ClosestAnswer = accepted.Count > 0 ? accepted[0] : card.AnswerText(direction),
                Similarity = 0d,
                AcceptedAnswers = accepted,
            };
        }

        string candidate = TextTools.Normalize(
            typed,
            ignoreCase: settings.IgnoreCase,
            ignoreAccents: settings.IgnoreAccents);

        string bestAnswer = accepted.Count > 0 ? accepted[0] : card.AnswerText(direction);
        double bestSimilarity = 0d;

        foreach (string answer in accepted)
        {
            string normalized = TextTools.Normalize(
                answer,
                ignoreCase: settings.IgnoreCase,
                ignoreAccents: settings.IgnoreAccents);

            if (string.Equals(candidate, normalized, StringComparison.Ordinal))
            {
                return new GradedAnswer
                {
                    Verdict = AnswerVerdict.Correct,
                    ClosestAnswer = answer,
                    Similarity = 1d,
                    AcceptedAnswers = accepted,
                };
            }

            double similarity = TextTools.Similarity(candidate, normalized);
            if (similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                bestAnswer = answer;
            }
        }

        AnswerVerdict verdict = bestSimilarity >= Math.Clamp(settings.TypoTolerance, 0.5d, 1d)
            ? AnswerVerdict.AlmostCorrect
            : AnswerVerdict.Incorrect;

        return new GradedAnswer
        {
            Verdict = verdict,
            ClosestAnswer = bestAnswer,
            Similarity = bestSimilarity,
            AcceptedAnswers = accepted,
        };
    }

    /// <summary>
    /// A character-level diff between what was typed and the expected answer, used by
    /// the typing mode to highlight exactly where it went wrong.
    /// </summary>
    public static IReadOnlyList<DiffSegment> Diff(string typed, string expected)
    {
        typed ??= string.Empty;
        expected ??= string.Empty;

        var segments = new List<DiffSegment>();
        int i = 0;
        int j = 0;

        while (i < typed.Length || j < expected.Length)
        {
            bool bothLeft = i < typed.Length && j < expected.Length;

            if (bothLeft && char.ToLowerInvariant(typed[i]) == char.ToLowerInvariant(expected[j]))
            {
                Append(segments, DiffKind.Match, typed[i]);
                i++;
                j++;
                continue;
            }

            // Look one character ahead to tell an insertion from a deletion.
            bool nextMatchesExpected = i + 1 < typed.Length && j < expected.Length
                && char.ToLowerInvariant(typed[i + 1]) == char.ToLowerInvariant(expected[j]);

            bool nextMatchesTyped = j + 1 < expected.Length && i < typed.Length
                && char.ToLowerInvariant(typed[i]) == char.ToLowerInvariant(expected[j + 1]);

            if (i < typed.Length && (nextMatchesExpected || j >= expected.Length))
            {
                Append(segments, DiffKind.Extra, typed[i]);
                i++;
            }
            else if (j < expected.Length && (nextMatchesTyped || i >= typed.Length))
            {
                Append(segments, DiffKind.Missing, expected[j]);
                j++;
            }
            else
            {
                Append(segments, DiffKind.Wrong, bothLeft ? typed[i] : expected[j]);
                i++;
                j++;
            }
        }

        return segments;

        static void Append(List<DiffSegment> into, DiffKind kind, char ch)
        {
            if (into.Count > 0 && into[^1].Kind == kind)
            {
                into[^1] = into[^1] with { Text = into[^1].Text + ch };
                return;
            }

            into.Add(new DiffSegment(kind, ch.ToString()));
        }
    }
}

/// <summary>How one run of characters in a typed answer compares to the expected text.</summary>
public enum DiffKind
{
    /// <summary>Typed correctly.</summary>
    Match,

    /// <summary>Typed, but wrong.</summary>
    Wrong,

    /// <summary>Typed, but should not be there.</summary>
    Extra,

    /// <summary>Expected, but not typed.</summary>
    Missing,
}

/// <summary>A run of characters sharing one <see cref="DiffKind"/>.</summary>
public sealed record DiffSegment(DiffKind Kind, string Text);
