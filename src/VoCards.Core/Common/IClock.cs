namespace VoCards.Core.Common;

/// <summary>
/// Supplies the current time. Everything that schedules, streaks or stamps a review
/// takes one of these, so tests can drive the calendar instead of waiting for it.
/// </summary>
public interface IClock
{
    DateTimeOffset Now { get; }

    DateOnly Today => DateOnly.FromDateTime(Now.LocalDateTime);
}

/// <summary>The real clock.</summary>
public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    public DateTimeOffset Now => DateTimeOffset.Now;
}

/// <summary>A clock the caller winds by hand. Used by the test-suite and the demo seeder.</summary>
public sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset Now { get; private set; } = now;

    public void Advance(TimeSpan by) => Now = Now.Add(by);

    public void AdvanceDays(int days) => Advance(TimeSpan.FromDays(days));

    public void Set(DateTimeOffset to) => Now = to;
}
