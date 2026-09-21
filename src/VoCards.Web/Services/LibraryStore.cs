using VoCards.Core.Common;
using VoCards.Core.Models;
using VoCards.Core.Seed;
using VoCards.Core.Serialization;

namespace VoCards.Web.Services;

/// <summary>
/// Owns the one <see cref="Library"/> the app is editing, persists it, and tells
/// components when it changed.
///
/// Saving is debounced: a study session grades a card every couple of seconds and
/// serialising the whole library on each keystroke would be wasteful, so writes
/// coalesce into one every <see cref="SaveDelayMs"/> milliseconds.
/// </summary>
public sealed class LibraryStore(JsBridge js) : IAsyncDisposable
{
    private const int SaveDelayMs = 700;

    private readonly JsBridge _js = js;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private CancellationTokenSource? _pendingSave;
    private bool _loaded;

    /// <summary>Raised whenever the library changes, so pages can re-render.</summary>
    public event Action? Changed;

    /// <summary>Raised when a save completes or fails, for the sync indicator.</summary>
    public event Action<bool>? Saved;

    public Library Library { get; private set; } = new();

    public UserProfile Profile => Library.Profile;

    /// <summary>True once the initial load has finished, successfully or not.</summary>
    public bool IsReady { get; private set; }

    /// <summary>True when this is a first run and the starter library was seeded.</summary>
    public bool WasSeeded { get; private set; }

    /// <summary>Set when the stored library could not be read, for a recovery banner.</summary>
    public string? LoadError { get; private set; }

    /// <summary>The clock the whole app schedules against.</summary>
    public IClock Clock { get; } = SystemClock.Instance;

    public DateTimeOffset Now => Clock.Now;

    // ---------------------------------------------------------------- loading

    /// <summary>Loads from browser storage, seeding a starter library on first run.</summary>
    public async Task InitializeAsync()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        string? json = await _js.LoadAsync();

        if (string.IsNullOrWhiteSpace(json))
        {
            Library = SeedLibrary.Create();
            WasSeeded = true;
            IsReady = true;
            Changed?.Invoke();
            await SaveNowAsync();
            return;
        }

        Result<Library> restored = VoCardsJson.Deserialize(json);

        if (restored.IsFailure)
        {
            // Never destroy a file we could not read: keep the app usable with a
            // fresh library and tell the learner their data is still in storage.
            LoadError = restored.Error;
            Library = SeedLibrary.Create();
            WasSeeded = true;
        }
        else
        {
            Library = restored.Value!;
            Library.History.Trim(Now);
        }

        IsReady = true;
        Changed?.Invoke();
    }

    // ---------------------------------------------------------------- saving

    /// <summary>
    /// Signals that the library changed: notifies subscribers immediately and
    /// schedules a debounced save.
    /// </summary>
    public void MarkChanged()
    {
        Changed?.Invoke();
        QueueSave();
    }

    private void QueueSave()
    {
        _pendingSave?.Cancel();
        _pendingSave?.Dispose();

        var cts = new CancellationTokenSource();
        _pendingSave = cts;

        _ = DebouncedSaveAsync(cts.Token);
    }

    private async Task DebouncedSaveAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(SaveDelayMs, token);
        }
        catch (TaskCanceledException)
        {
            return; // superseded by a newer change
        }

        if (!token.IsCancellationRequested)
        {
            await SaveNowAsync();
        }
    }

    /// <summary>Writes immediately, bypassing the debounce.</summary>
    public async Task<bool> SaveNowAsync()
    {
        await _saveLock.WaitAsync();

        try
        {
            string json = VoCardsJson.Serialize(Library);
            bool ok = await _js.SaveAsync(json);
            Saved?.Invoke(ok);
            return ok;
        }
        finally
        {
            _saveLock.Release();
        }
    }

    // ------------------------------------------------------------ whole-library

    /// <summary>Replaces the library wholesale, as an import or a restore does.</summary>
    public async Task ReplaceAsync(Library library)
    {
        ArgumentNullException.ThrowIfNull(library);

        Library = library;
        LoadError = null;
        Changed?.Invoke();
        await SaveNowAsync();
    }

    /// <summary>Wipes everything and re-seeds the starter library.</summary>
    public async Task ResetAsync()
    {
        await _js.ClearStorageAsync();
        Library = SeedLibrary.Create();
        WasSeeded = true;
        LoadError = null;
        Changed?.Invoke();
        await SaveNowAsync();
    }

    /// <summary>Clears every deck without re-seeding, for a from-scratch start.</summary>
    public async Task ClearAsync()
    {
        Library.Clear();
        WasSeeded = false;
        Changed?.Invoke();
        await SaveNowAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _pendingSave?.Cancel();
        _pendingSave?.Dispose();
        _pendingSave = null;

        // A pending change must not be lost because the page is closing.
        await SaveNowAsync();

        _saveLock.Dispose();
    }
}
