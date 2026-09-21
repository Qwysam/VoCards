namespace VoCards.Web.Services;

/// <summary>Severity of a toast, which selects its accent stripe.</summary>
public enum ToastKind
{
    Neutral,
    Good,
    Warning,
    Critical,
}

/// <summary>One transient message.</summary>
public sealed record Toast
{
    public Guid Id { get; } = Guid.NewGuid();

    public required string Message { get; init; }

    public ToastKind Kind { get; init; } = ToastKind.Neutral;

    public string? Icon { get; init; }

    /// <summary>Label for the inline action, e.g. "Undo".</summary>
    public string? ActionLabel { get; init; }

    /// <summary>Invoked when the action is pressed. The toast dismisses afterwards.</summary>
    public Func<Task>? Action { get; init; }

    public int DurationMs { get; init; } = 3600;
}

/// <summary>
/// A small queue of transient messages. Undo lives here too: a destructive
/// action posts a toast carrying the callback that reverses it.
/// </summary>
public sealed class ToastService
{
    private readonly List<Toast> _toasts = [];

    public event Action? Changed;

    /// <summary>At most this many are on screen at once; the oldest is dropped.</summary>
    public const int MaxVisible = 4;

    public IReadOnlyList<Toast> Toasts => _toasts;

    public void Show(string message, ToastKind kind = ToastKind.Neutral, string? icon = null) =>
        Push(new Toast { Message = message, Kind = kind, Icon = icon });

    public void Success(string message) => Show(message, ToastKind.Good, "check");

    public void Warn(string message) => Show(message, ToastKind.Warning, "alert");

    public void Error(string message) => Show(message, ToastKind.Critical, "alert");

    /// <summary>Posts a message with an Undo action that stays up a little longer.</summary>
    public void Undoable(string message, Func<Task> undo) =>
        Push(new Toast
        {
            Message = message,
            Kind = ToastKind.Neutral,
            ActionLabel = "Undo",
            Action = undo,
            DurationMs = 6500,
        });

    private void Push(Toast toast)
    {
        _toasts.Add(toast);

        while (_toasts.Count > MaxVisible)
        {
            _toasts.RemoveAt(0);
        }

        Changed?.Invoke();
        _ = ExpireAsync(toast);
    }

    private async Task ExpireAsync(Toast toast)
    {
        await Task.Delay(toast.DurationMs);
        Dismiss(toast.Id);
    }

    public void Dismiss(Guid id)
    {
        int index = _toasts.FindIndex(t => t.Id == id);

        if (index < 0)
        {
            return;
        }

        _toasts.RemoveAt(index);
        Changed?.Invoke();
    }

    /// <summary>Runs a toast's action, then dismisses it.</summary>
    public async Task InvokeAsync(Toast toast)
    {
        ArgumentNullException.ThrowIfNull(toast);

        if (toast.Action is { } action)
        {
            await action();
        }

        Dismiss(toast.Id);
    }
}
