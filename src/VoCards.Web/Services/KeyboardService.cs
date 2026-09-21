namespace VoCards.Web.Services;

/// <summary>A key press relayed from the browser.</summary>
public sealed record KeyPress
{
    public string Key { get; init; } = string.Empty;

    public bool Ctrl { get; init; }

    public bool Shift { get; init; }

    /// <summary>True when the press originated inside a text field.</summary>
    public bool FromField { get; init; }

    public string Lower => Key.ToLowerInvariant();

    public bool Is(string key) => string.Equals(Key, key, StringComparison.OrdinalIgnoreCase);

    public bool IsDigit(out int digit) => int.TryParse(Key, out digit) && Key.Length == 1;
}

/// <summary>
/// Fans global key presses out to whichever component currently wants them.
///
/// Handlers are a stack: the study page pushes one while it is on screen, an open
/// modal pushes one on top, and only the topmost handler sees a press. That keeps
/// "Escape closes the thing in front" working without any component knowing what
/// else is open.
/// </summary>
public sealed class KeyboardService
{
    private readonly List<(object Owner, Func<KeyPress, bool> Handler)> _stack = [];

    /// <summary>Pushes a handler. It receives presses until the owner unsubscribes.</summary>
    public void Push(object owner, Func<KeyPress, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(handler);

        Remove(owner);
        _stack.Add((owner, handler));
    }

    public void Remove(object owner)
    {
        int index = _stack.FindIndex(entry => ReferenceEquals(entry.Owner, owner));

        if (index >= 0)
        {
            _stack.RemoveAt(index);
        }
    }

    /// <summary>
    /// Offers the press to handlers from the top down, stopping at the first that
    /// claims it. Returns true when something handled it.
    /// </summary>
    public bool Dispatch(KeyPress press)
    {
        for (int i = _stack.Count - 1; i >= 0; i--)
        {
            if (_stack[i].Handler(press))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The shortcut reference shown by the "?" overlay.</summary>
    public static IReadOnlyList<ShortcutGroup> Reference { get; } =
    [
        new("Anywhere",
        [
            new("Ctrl / ⌘ + K", "Open the command palette"),
            new("?", "Show this sheet"),
            new("G then D", "Go to decks"),
            new("G then S", "Go to stats"),
            new("G then H", "Go to the dashboard"),
            new("Esc", "Close whatever is in front"),
        ]),

        new("Studying",
        [
            new("Space / Enter", "Flip the card, then grade it Good"),
            new("1 – 4", "Again · Hard · Good · Easy"),
            new("S", "Speak the prompt"),
            new("F", "Star this card"),
            new("E", "Edit this card"),
            new("U", "Suspend this card"),
            new("→", "Skip to the next card"),
            new("Esc", "End the session"),
        ]),

        new("Browsing",
        [
            new("/", "Focus the search box"),
            new("N", "New card or deck"),
            new("Enter", "Open the highlighted result"),
        ]),
    ];
}

/// <summary>A titled block of shortcuts in the reference sheet.</summary>
public sealed record ShortcutGroup(string Title, IReadOnlyList<Shortcut> Shortcuts);

/// <summary>One key combination and what it does.</summary>
public sealed record Shortcut(string Keys, string Description);
