using VoCards.Core.Common;
using VoCards.Core.Models;
using VoCards.Core.Study;

namespace VoCards.Core.Tests;

public class AnswerGraderTests
{
    private readonly DeckSettings _settings = new();

    private GradedAnswer Grade(string back, string? typed, DeckSettings? settings = null) =>
        AnswerGrader.Grade(new Card("term", back), typed, settings ?? _settings, StudyDirection.FrontToBack);

    [Fact]
    public void ExactMatch_IsCorrect()
    {
        GradedAnswer grade = Grade("house", "house");

        Assert.Equal(AnswerVerdict.Correct, grade.Verdict);
        Assert.Equal(1d, grade.Similarity);
        Assert.Equal(Rating.Good, grade.ImpliedRating);
    }

    [Fact]
    public void CaseDiffersOnly_IsStillCorrect() =>
        Assert.Equal(AnswerVerdict.Correct, Grade("House", "hOUSE").Verdict);

    [Fact]
    public void SurroundingWhitespace_IsIgnored() =>
        Assert.Equal(AnswerVerdict.Correct, Grade("house", "   house  ").Verdict);

    [Fact]
    public void InternalWhitespace_IsCollapsed() =>
        Assert.Equal(AnswerVerdict.Correct, Grade("good morning", "good    morning").Verdict);

    [Theory]
    [InlineData("café", "cafe")]
    [InlineData("cafe", "café")]
    [InlineData("über", "uber")]
    [InlineData("pequeño", "pequeno")]
    [InlineData("Être", "etre")]
    public void Accents_AreFoldedByDefault(string expected, string typed) =>
        Assert.Equal(AnswerVerdict.Correct, Grade(expected, typed).Verdict);

    [Fact]
    public void Accents_CanBeRequired()
    {
        var strict = new DeckSettings { IgnoreAccents = false };

        Assert.NotEqual(AnswerVerdict.Correct, Grade("café", "cafe", strict).Verdict);
    }

    [Fact]
    public void Case_CanBeRequired()
    {
        var strict = new DeckSettings { IgnoreCase = false, TypoTolerance = 1d };

        Assert.NotEqual(AnswerVerdict.Correct, Grade("Haus", "haus", strict).Verdict);
    }

    [Fact]
    public void ATypo_IsAlmostCorrect()
    {
        GradedAnswer grade = Grade("receive", "recieve");

        Assert.Equal(AnswerVerdict.AlmostCorrect, grade.Verdict);
        Assert.True(grade.IsAcceptable);
        Assert.Equal(Rating.Hard, grade.ImpliedRating);
    }

    [Fact]
    public void ADifferentWord_IsIncorrect()
    {
        GradedAnswer grade = Grade("receive", "banana");

        Assert.Equal(AnswerVerdict.Incorrect, grade.Verdict);
        Assert.False(grade.IsAcceptable);
        Assert.Equal(Rating.Again, grade.ImpliedRating);
    }

    [Fact]
    public void EmptyAnswer_IsSkipped()
    {
        Assert.Equal(AnswerVerdict.Skipped, Grade("house", "").Verdict);
        Assert.Equal(AnswerVerdict.Skipped, Grade("house", null).Verdict);
        Assert.Equal(AnswerVerdict.Skipped, Grade("house", "   ").Verdict);
    }

    [Fact]
    public void AnyListedAlternative_IsAccepted()
    {
        Assert.Equal(AnswerVerdict.Correct, Grade("big, large, huge", "large").Verdict);
        Assert.Equal(AnswerVerdict.Correct, Grade("big, large, huge", "huge").Verdict);
        Assert.Equal(AnswerVerdict.Correct, Grade("big, large, huge", "big").Verdict);
        Assert.Equal(AnswerVerdict.Incorrect, Grade("big, large, huge", "tiny").Verdict);
    }

    [Fact]
    public void AcceptedAnswers_AreReportedBack()
    {
        GradedAnswer grade = Grade("big, large", "tiny");

        Assert.Equal(2, grade.AcceptedAnswers.Count);
        Assert.Contains("big", grade.AcceptedAnswers);
        Assert.Contains("large", grade.AcceptedAnswers);
    }

    [Fact]
    public void TypoTolerance_IsConfigurable()
    {
        var strict = new DeckSettings { TypoTolerance = 1d };
        var loose = new DeckSettings { TypoTolerance = 0.6d };

        Assert.Equal(AnswerVerdict.Incorrect, Grade("receive", "recieve", strict).Verdict);
        Assert.Equal(AnswerVerdict.AlmostCorrect, Grade("receive", "recieve", loose).Verdict);
    }

    [Fact]
    public void Punctuation_IsIgnored() =>
        Assert.Equal(AnswerVerdict.Correct, Grade("¿Dónde?", "donde").Verdict);

    [Fact]
    public void Diff_MarksAWrongCharacter()
    {
        var segments = AnswerGrader.Diff("hoyse", "house");

        Assert.Contains(segments, s => s.Kind == DiffKind.Wrong);
        Assert.Equal("house".Length, segments.Sum(s => s.Kind == DiffKind.Missing ? 0 : s.Text.Length));
    }

    [Fact]
    public void Diff_MarksAMissingCharacter()
    {
        var segments = AnswerGrader.Diff("hose", "house");

        Assert.Contains(segments, s => s.Kind == DiffKind.Missing);
    }

    [Fact]
    public void Diff_MarksAnExtraCharacter()
    {
        var segments = AnswerGrader.Diff("houuse", "house");

        Assert.Contains(segments, s => s.Kind == DiffKind.Extra);
    }

    [Fact]
    public void Diff_OfAnExactMatch_IsAllMatch()
    {
        var segments = AnswerGrader.Diff("house", "house");

        Assert.Single(segments);
        Assert.Equal(DiffKind.Match, segments[0].Kind);
        Assert.Equal("house", segments[0].Text);
    }
}

public class TextToolsTests
{
    [Theory]
    [InlineData("café", "cafe")]
    [InlineData("naïve", "naive")]
    [InlineData("Ünïcödé", "Unicode")]
    [InlineData("plain", "plain")]
    [InlineData("", "")]
    public void RemoveDiacritics_StripsCombiningMarks(string input, string expected) =>
        Assert.Equal(expected, TextTools.RemoveDiacritics(input));

    [Theory]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("", "abc", 3)]
    [InlineData("abc", "", 3)]
    [InlineData("same", "same", 0)]
    [InlineData("flaw", "lawn", 2)]
    [InlineData("receive", "recieve", 1)]
    [InlineData("ab", "ba", 1)]
    public void EditDistance_MatchesKnownValues(string a, string b, int expected) =>
        Assert.Equal(expected, TextTools.EditDistance(a, b));

    [Fact]
    public void EditDistance_IsSymmetric() =>
        Assert.Equal(TextTools.EditDistance("kitten", "sitting"), TextTools.EditDistance("sitting", "kitten"));

    [Fact]
    public void Similarity_IsOneForIdenticalStrings() =>
        Assert.Equal(1d, TextTools.Similarity("house", "house"));

    [Fact]
    public void Similarity_IsZeroForCompletelyDifferentStrings() =>
        Assert.Equal(0d, TextTools.Similarity("abc", "xyz"));

    [Theory]
    [InlineData("big, large", 2)]
    [InlineData("a; b; c", 3)]
    [InlineData("one/two", 2)]
    [InlineData("solo", 1)]
    [InlineData("", 0)]
    public void SplitAlternatives_HandlesEverySeparator(string input, int expected) =>
        Assert.Equal(expected, TextTools.SplitAlternatives(input).Count);

    [Fact]
    public void Ellipsize_LeavesShortStringsAlone() =>
        Assert.Equal("short", TextTools.Ellipsize("short", 20));

    [Fact]
    public void Ellipsize_TruncatesLongStrings()
    {
        string result = TextTools.Ellipsize("a rather long sentence that keeps going", 20);

        Assert.True(result.Length <= 20);
        Assert.EndsWith("…", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ContainsLoose_IgnoresCaseAndAccents()
    {
        Assert.True(TextTools.ContainsLoose("Le café noir", "cafe"));
        Assert.True(TextTools.ContainsLoose("Le café noir", "NOIR"));
        Assert.False(TextTools.ContainsLoose("Le café noir", "tea"));
        Assert.False(TextTools.ContainsLoose(null, "tea"));
    }
}
