using System.Globalization;
using System.Text;
using VoCards.Core.Common;
using VoCards.Core.Models;

namespace VoCards.Core.Serialization;

/// <summary>What a delimited import produced.</summary>
public sealed record ImportReport
{
    public required int Imported { get; init; }

    public required int Skipped { get; init; }

    /// <summary>Row numbers and reasons, for the "3 rows could not be read" detail panel.</summary>
    public IReadOnlyList<string> Problems { get; init; } = [];

    public bool HasProblems => Problems.Count > 0;
}

/// <summary>
/// CSV and TSV import/export. Tab-separated with front and back in the first two
/// columns is what Anki, Quizlet and most word-list sites produce, so it is the
/// path of least resistance for bringing an existing vocabulary list in.
/// </summary>
public static class DelimitedPorter
{
    /// <summary>Columns written on export, in order.</summary>
    public static readonly string[] Columns =
        ["Front", "Back", "Example", "Notes", "Pronunciation", "Tags", "Starred"];

    /// <summary>Renders a deck as delimited text.</summary>
    public static string Export(Deck deck, char delimiter = ',', bool includeHeader = true)
    {
        ArgumentNullException.ThrowIfNull(deck);

        var builder = new StringBuilder();

        if (includeHeader)
        {
            builder.AppendLine(string.Join(delimiter, Columns.Select(c => Quote(c, delimiter))));
        }

        foreach (Card card in deck.Cards)
        {
            string[] fields =
            [
                card.Front,
                card.Back,
                card.Example ?? string.Empty,
                card.Notes ?? string.Empty,
                card.Pronunciation ?? string.Empty,
                string.Join(' ', card.Tags),
                card.IsStarred ? "yes" : string.Empty,
            ];

            builder.AppendLine(string.Join(delimiter, fields.Select(f => Quote(f, delimiter))));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Parses delimited text into a deck. Rows missing a front or a back are skipped
    /// and reported rather than aborting the whole import.
    /// </summary>
    public static Result<(Deck Deck, ImportReport Report)> Import(
        string content,
        string deckName,
        char? delimiter = null,
        bool? hasHeader = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Result.Failure<(Deck, ImportReport)>("The file is empty.");
        }

        string[] lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Where(static line => line.Trim().Length > 0)
            .ToArray();

        if (lines.Length == 0)
        {
            return Result.Failure<(Deck, ImportReport)>("The file has no rows.");
        }

        char separator = delimiter ?? DetectDelimiter(lines);
        bool skipHeader = hasHeader ?? LooksLikeHeader(lines[0], separator);

        var deck = new Deck(string.IsNullOrWhiteSpace(deckName) ? "Imported" : deckName.Trim());
        var problems = new List<string>();
        int imported = 0;
        int skipped = 0;

        for (int i = skipHeader ? 1 : 0; i < lines.Length; i++)
        {
            string[] fields = ParseLine(lines[i], separator);

            if (fields.Length < 2 || string.IsNullOrWhiteSpace(fields[0]) || string.IsNullOrWhiteSpace(fields[1]))
            {
                skipped++;

                if (problems.Count < 20)
                {
                    problems.Add($"Row {i + 1}: needs both a front and a back.");
                }

                continue;
            }

            var card = new Card(fields[0], fields[1]);

            card.Edit(
                example: Field(fields, 2),
                notes: Field(fields, 3),
                pronunciation: Field(fields, 4));

            if (Field(fields, 5) is { Length: > 0 } tags)
            {
                card.SetTags(tags.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }

            if (Field(fields, 6) is { Length: > 0 } starred && IsTruthy(starred))
            {
                card.Star();
            }

            deck.AddCard(card);
            imported++;
        }

        if (imported == 0)
        {
            return Result.Failure<(Deck, ImportReport)>(
                "No usable rows were found. Each row needs at least a front and a back.");
        }

        var report = new ImportReport { Imported = imported, Skipped = skipped, Problems = problems };
        return Result.Success((deck, report));

        static string? Field(string[] fields, int index) =>
            index < fields.Length ? fields[index].Trim() : null;

        static bool IsTruthy(string value) =>
            value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.Ordinal)
            || value.Equals("y", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Picks the delimiter by seeing which candidate gives the most consistent column
    /// count across the first few rows.
    /// </summary>
    public static char DetectDelimiter(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        char[] candidates = ['\t', ',', ';', '|'];
        char best = ',';
        int bestScore = 0;

        foreach (char candidate in candidates)
        {
            int sample = Math.Min(lines.Count, 10);
            var counts = new List<int>(sample);

            for (int i = 0; i < sample; i++)
            {
                counts.Add(ParseLine(lines[i], candidate).Length);
            }

            // A good delimiter yields >1 column, consistently.
            int columns = counts.Count > 0 ? counts[0] : 0;
            if (columns < 2 || counts.Any(c => c != columns))
            {
                continue;
            }

            if (columns > bestScore)
            {
                bestScore = columns;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>Heuristic: the first row is a header if it names our known columns.</summary>
    public static bool LooksLikeHeader(string line, char delimiter)
    {
        string[] fields = ParseLine(line, delimiter);

        if (fields.Length < 2)
        {
            return false;
        }

        return fields.Take(2).All(f =>
            Columns.Any(c => string.Equals(c, f.Trim(), StringComparison.OrdinalIgnoreCase))
            || string.Equals(f.Trim(), "term", StringComparison.OrdinalIgnoreCase)
            || string.Equals(f.Trim(), "definition", StringComparison.OrdinalIgnoreCase)
            || string.Equals(f.Trim(), "word", StringComparison.OrdinalIgnoreCase)
            || string.Equals(f.Trim(), "translation", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Splits one row, honouring RFC 4180 quoting so that a field containing the
    /// delimiter — or a doubled quote — survives the trip.
    /// </summary>
    public static string[] ParseLine(string line, char delimiter)
    {
        ArgumentNullException.ThrowIfNull(line);

        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];

            if (inQuotes)
            {
                // A doubled quote inside a quoted field is a literal quote.
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                if (ch == '"')
                {
                    inQuotes = false;
                    continue;
                }

                current.Append(ch);
                continue;
            }

            if (ch == '"' && current.Length == 0)
            {
                inQuotes = true;
                continue;
            }

            if (ch == delimiter)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        fields.Add(current.ToString());
        return [.. fields];
    }

    /// <summary>Quotes a field if it contains the delimiter, a quote or a newline.</summary>
    private static string Quote(string value, char delimiter)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        bool needsQuotes = value.Contains(delimiter, StringComparison.Ordinal)
            || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal);

        return needsQuotes
            ? string.Create(CultureInfo.InvariantCulture, $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"")
            : value;
    }
}
