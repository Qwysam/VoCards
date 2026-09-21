using System.Globalization;
using System.Text;

namespace VoCards.Core.Common;

/// <summary>
/// String helpers shared by answer grading and search. Kept deliberately small and
/// allocation-aware: grading runs on every keystroke in the typing study mode.
/// </summary>
public static class TextTools
{
    /// <summary>
    /// Strips diacritics so that "cafe" matches "café" and "uber" matches "über".
    /// Combining marks are removed; base characters are kept.
    /// </summary>
    public static string RemoveDiacritics(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (char ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Normalises an answer for comparison: trims, collapses internal whitespace,
    /// lowercases and optionally folds accents and strips punctuation.
    /// </summary>
    public static string Normalize(
        string value,
        bool ignoreCase = true,
        bool ignoreAccents = true,
        bool ignorePunctuation = true)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string working = ignoreAccents ? RemoveDiacritics(value) : value;

        var builder = new StringBuilder(working.Length);
        bool lastWasSpace = true; // leading whitespace is dropped

        foreach (char ch in working)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace)
                {
                    builder.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            if (ignorePunctuation && (char.IsPunctuation(ch) || char.IsSymbol(ch)))
            {
                continue;
            }

            builder.Append(ignoreCase ? char.ToLowerInvariant(ch) : ch);
            lastWasSpace = false;
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Damerau–Levenshtein edit distance (optimal string alignment), used to tell a
    /// typo ("recieve") from a wrong answer ("banana").
    ///
    /// Plain Levenshtein charges two edits for a transposition, which is the single
    /// most common typing mistake — that would score "recieve" against "receive" at
    /// 0.71 similarity and reject it. Counting an adjacent swap as one edit puts it
    /// at 0.86, comfortably inside the default tolerance.
    /// </summary>
    public static int EditDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a))
        {
            return b?.Length ?? 0;
        }

        if (string.IsNullOrEmpty(b))
        {
            return a.Length;
        }

        // Three rolling rows: the transposition rule needs the row two back.
        int[] twoBack = new int[b.Length + 1];
        int[] previous = new int[b.Length + 1];
        int[] current = new int[b.Length + 1];

        for (int j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;

            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;

                int deletion = previous[j] + 1;
                int insertion = current[j - 1] + 1;
                int substitution = previous[j - 1] + cost;

                current[j] = Math.Min(Math.Min(deletion, insertion), substitution);

                // Adjacent transposition: "ab" typed where "ba" was expected.
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    current[j] = Math.Min(current[j], twoBack[j - 2] + 1);
                }
            }

            (twoBack, previous, current) = (previous, current, twoBack);
        }

        return previous[b.Length];
    }

    /// <summary>
    /// Similarity in the range 0..1, where 1 is an exact match. Derived from
    /// <see cref="EditDistance"/> relative to the longer string.
    /// </summary>
    public static double Similarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
        {
            return 1d;
        }

        int longest = Math.Max(a?.Length ?? 0, b?.Length ?? 0);
        return longest == 0 ? 1d : 1d - ((double)EditDistance(a ?? string.Empty, b ?? string.Empty) / longest);
    }

    /// <summary>
    /// Splits a comma/semicolon separated answer field into its accepted alternatives,
    /// so a card whose back is "big, large" accepts either word.
    /// </summary>
    public static IReadOnlyList<string> SplitAlternatives(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', ';', '/', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.Length > 0)
            .ToArray();
    }

    /// <summary>Case-insensitive, accent-insensitive "does the haystack contain the needle".</summary>
    public static bool ContainsLoose(string? haystack, string needle)
    {
        if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle))
        {
            return false;
        }

        return Normalize(haystack).Contains(Normalize(needle), StringComparison.Ordinal);
    }

    /// <summary>Truncates for display without cutting mid-word where it can be helped.</summary>
    public static string Ellipsize(string? value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value ?? string.Empty;
        }

        int cut = value.LastIndexOf(' ', Math.Min(max - 1, value.Length - 1));
        return cut > max / 2
            ? string.Concat(value.AsSpan(0, cut), "…")
            : string.Concat(value.AsSpan(0, Math.Max(0, max - 1)), "…");
    }
}
