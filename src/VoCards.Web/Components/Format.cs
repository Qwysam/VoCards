using System.Globalization;

namespace VoCards.Web.Components;

/// <summary>
/// Formatting helpers shared by the components.
///
/// Every number written into an SVG attribute or a CSS value must use the
/// invariant culture: a browser will not accept "0,75" as a stroke-dashoffset,
/// and the app would silently break for anyone with a comma decimal separator.
/// </summary>
public static class Format
{
    /// <summary>A number safe to interpolate into SVG or CSS.</summary>
    public static string Number(double value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>A CSS percentage, clamped to 0–100.</summary>
    public static string Percent(double fraction) =>
        string.Create(CultureInfo.InvariantCulture, $"{Math.Clamp(fraction, 0d, 1d) * 100:0.##}%");

    /// <summary>A rounded percentage for display, e.g. "87%".</summary>
    public static string PercentLabel(double fraction) =>
        string.Create(CultureInfo.InvariantCulture, $"{Math.Clamp(fraction, 0d, 1d) * 100:0}%");

    /// <summary>Thousands-separated integer.</summary>
    public static string Count(long value) => value.ToString("N0", CultureInfo.CurrentCulture);

    /// <summary>A duration as a human phrase: "12 min", "1 h 05".</summary>
    public static string Duration(TimeSpan span) => span.TotalSeconds switch
    {
        < 1 => "0 s",
        < 60 => $"{span.TotalSeconds:0} s",
        < 3600 => $"{span.TotalMinutes:0} min",
        _ => $"{(int)span.TotalHours} h {span.Minutes:00}",
    };

    /// <summary>A compact duration for tight spaces: "4.2 s".</summary>
    public static string Seconds(TimeSpan span) =>
        string.Create(CultureInfo.InvariantCulture, $"{span.TotalSeconds:0.0} s");

    /// <summary>"in 3 days" / "2 hours ago" / "just now".</summary>
    public static string Relative(DateTimeOffset moment, DateTimeOffset now)
    {
        TimeSpan delta = moment - now;
        bool future = delta > TimeSpan.Zero;
        TimeSpan magnitude = delta.Duration();

        string phrase = magnitude switch
        {
            { TotalSeconds: < 45 } => "just now",
            { TotalMinutes: < 60 } => Plural((int)magnitude.TotalMinutes, "minute"),
            { TotalHours: < 24 } => Plural((int)magnitude.TotalHours, "hour"),
            { TotalDays: < 30 } => Plural((int)magnitude.TotalDays, "day"),
            { TotalDays: < 365 } => Plural((int)(magnitude.TotalDays / 30), "month"),
            _ => Plural((int)(magnitude.TotalDays / 365), "year"),
        };

        if (phrase == "just now")
        {
            return phrase;
        }

        return future ? $"in {phrase}" : $"{phrase} ago";
    }

    /// <summary>A day label for an axis: "14 Mar".</summary>
    public static string Day(DateOnly day) =>
        day.ToString("d MMM", CultureInfo.CurrentCulture);

    /// <summary>A full date for a tooltip: "Saturday, 14 March".</summary>
    public static string LongDay(DateOnly day) =>
        day.ToString("dddd, d MMMM", CultureInfo.CurrentCulture);

    private static string Plural(int count, string unit) =>
        count <= 1 ? $"1 {unit}" : $"{count} {unit}s";

    /// <summary>The human name of a card state.</summary>
    public static string StateName(VoCards.Core.Models.CardState state) => state switch
    {
        VoCards.Core.Models.CardState.New => "New",
        VoCards.Core.Models.CardState.Learning => "Learning",
        VoCards.Core.Models.CardState.Relearning => "Relearning",
        VoCards.Core.Models.CardState.Review => "Review",
        _ => "Suspended",
    };

    /// <summary>The badge class matching a card state.</summary>
    public static string StateBadge(VoCards.Core.Models.CardState state) => state switch
    {
        VoCards.Core.Models.CardState.New => "badge-info",
        VoCards.Core.Models.CardState.Learning or VoCards.Core.Models.CardState.Relearning => "badge-warning",
        VoCards.Core.Models.CardState.Review => "badge-good",
        _ => string.Empty,
    };
}
