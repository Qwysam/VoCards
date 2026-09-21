using Microsoft.JSInterop;

namespace VoCards.Web.Services;

/// <summary>
/// The single typed wrapper around <c>wwwroot/js/vocards.js</c>.
///
/// Every interop call in the app goes through here, so there is one place that
/// knows the JavaScript function names and one place that swallows the
/// <see cref="JSException"/>s a hostile browser environment can throw — a
/// private window with storage disabled, a platform with no speech synthesis,
/// a page being torn down mid-call.
/// </summary>
public sealed class JsBridge(IJSRuntime js) : IAsyncDisposable
{
    private readonly IJSRuntime _js = js;
    private IJSObjectReference? _module;

    private async ValueTask<IJSObjectReference?> ModuleAsync()
    {
        if (_module is not null)
        {
            return _module;
        }

        try
        {
            _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/vocards.js");
            return _module;
        }
        catch (JSException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            // Prerendering, or the circuit is gone.
            return null;
        }
    }

    /// <summary>Invokes a module function, returning false if interop is unavailable.</summary>
    private async ValueTask<bool> CallAsync(string name, params object?[] args)
    {
        IJSObjectReference? module = await ModuleAsync();

        if (module is null)
        {
            return false;
        }

        try
        {
            await module.InvokeVoidAsync(name, args);
            return true;
        }
        catch (JSException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    private async ValueTask<T?> CallAsync<T>(string name, params object?[] args)
    {
        IJSObjectReference? module = await ModuleAsync();

        if (module is null)
        {
            return default;
        }

        try
        {
            return await module.InvokeAsync<T>(name, args);
        }
        catch (JSException)
        {
            return default;
        }
        catch (ObjectDisposedException)
        {
            return default;
        }
        catch (TaskCanceledException)
        {
            return default;
        }
    }

    // ---------------------------------------------------------------- storage

    public ValueTask<bool> SaveAsync(string json) => CallAsync<bool>("save", json)!;

    public ValueTask<string?> LoadAsync() => CallAsync<string?>("load");

    public ValueTask ClearStorageAsync() => new(CallAsync("clear").AsTask());

    public ValueTask<StorageEstimate?> StorageEstimateAsync() =>
        CallAsync<StorageEstimate?>("storageEstimate");

    // ------------------------------------------------------------------ theme

    public ValueTask ApplyThemeAsync(string theme, string accent, bool reduceMotion) =>
        new(CallAsync("applyTheme", theme, accent, reduceMotion).AsTask());

    public ValueTask<bool> IsApplePlatformAsync() => CallAsync<bool>("isApplePlatform");

    public ValueTask<bool> PrefersDarkAsync() => CallAsync<bool>("prefersDark");

    public ValueTask<bool> PrefersReducedMotionAsync() => CallAsync<bool>("prefersReducedMotion");

    // ----------------------------------------------------------------- speech

    public ValueTask<bool> SpeechAvailableAsync() => CallAsync<bool>("speechAvailable");

    public async ValueTask<IReadOnlyList<VoiceInfo>> VoicesAsync() =>
        await CallAsync<VoiceInfo[]>("listVoices") ?? [];

    public ValueTask SpeakAsync(string text, string lang, string? voiceUri, double rate, double pitch) =>
        new(CallAsync("speak", text, lang, voiceUri, rate, pitch).AsTask());

    public ValueTask StopSpeakingAsync() => new(CallAsync("stopSpeaking").AsTask());

    // ------------------------------------------------------------------ sound

    public ValueTask BlipAsync(string kind) => new(CallAsync("blip", kind).AsTask());

    // --------------------------------------------------------------- keyboard

    public ValueTask RegisterKeysAsync<T>(DotNetObjectReference<T> reference) where T : class =>
        new(CallAsync("registerKeys", reference).AsTask());

    public ValueTask UnregisterKeysAsync() => new(CallAsync("unregisterKeys").AsTask());

    // --------------------------------------------------------------------- ui

    public ValueTask FocusAsync(string selector) => new(CallAsync("focus", selector).AsTask());

    public ValueTask ScrollToTopAsync() => new(CallAsync("scrollToTop").AsTask());

    // ------------------------------------------------------------------ files

    public ValueTask DownloadAsync(string filename, string content, string mime) =>
        new(CallAsync("download", filename, content, mime).AsTask());

    public ValueTask<bool> CopyAsync(string text) => CallAsync<bool>("copyText", text);

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            /* the page is already gone */
        }
        catch (JSException)
        {
            /* nothing useful to do during teardown */
        }

        _module = null;
    }
}

/// <summary>A speech-synthesis voice as the browser reports it.</summary>
public sealed record VoiceInfo(string Name, string Lang, string Uri, bool IsDefault);

/// <summary>Browser storage usage, in bytes.</summary>
public sealed record StorageEstimate(long Usage, long Quota)
{
    public double Fraction => Quota <= 0 ? 0d : (double)Usage / Quota;

    public string UsageLabel => Format(Usage);

    public string QuotaLabel => Format(Quota);

    private static string Format(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024d:0.#} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024d):0.#} MB",
        _ => $"{bytes / (1024d * 1024d * 1024d):0.##} GB",
    };
}
