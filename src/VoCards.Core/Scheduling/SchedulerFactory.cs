using VoCards.Core.Models;

namespace VoCards.Core.Scheduling;

/// <summary>Hands out the scheduler a deck asked for. Both implementations are stateless, so they are shared.</summary>
public static class SchedulerFactory
{
    private static readonly Sm2Scheduler Sm2 = new();
    private static readonly FsrsScheduler Fsrs = new();

    public static IScheduler For(SchedulerKind kind) => kind switch
    {
        SchedulerKind.Fsrs => Fsrs,
        _ => Sm2,
    };

    public static IScheduler For(DeckSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return For(settings.Scheduler);
    }

    public static IScheduler For(Deck deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        return For(deck.Settings);
    }

    /// <summary>Every scheduler, for the settings picker.</summary>
    public static IReadOnlyList<IScheduler> All => [Sm2, Fsrs];
}
