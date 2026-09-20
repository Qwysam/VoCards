using Microsoft.AspNetCore.Components;
using VoCards.Core.Models;

namespace VoCards.Web.Services;

/// <summary>
/// UI-only state that outlives a single page: whether the command palette or the
/// shortcut sheet is open, and the theme currently applied.
/// </summary>
public sealed class AppState(LibraryStore store, JsBridge js, NavigationManager nav)
{
    private readonly LibraryStore _store = store;
    private readonly JsBridge _js = js;
    private readonly NavigationManager _nav = nav;

    public event Action? Changed;

    public bool PaletteOpen { get; private set; }

    public bool ShortcutsOpen { get; private set; }

    /// <summary>True while a study session owns the screen, which hides the shell chrome.</summary>
    public bool Immersive { get; private set; }

    public bool SpeechAvailable { get; private set; }

    public IReadOnlyList<VoiceInfo> Voices { get; private set; } = [];

    // --------------------------------------------------------------- palette

    public void OpenPalette()
    {
        PaletteOpen = true;
        ShortcutsOpen = false;
        Changed?.Invoke();
    }

    public void ClosePalette()
    {
        PaletteOpen = false;
        Changed?.Invoke();
    }

    public void TogglePalette()
    {
        if (PaletteOpen)
        {
            ClosePalette();
        }
        else
        {
            OpenPalette();
        }
    }

    public void ToggleShortcuts()
    {
        ShortcutsOpen = !ShortcutsOpen;
        PaletteOpen = false;
        Changed?.Invoke();
    }

    public void CloseOverlays()
    {
        if (!PaletteOpen && !ShortcutsOpen)
        {
            return;
        }

        PaletteOpen = false;
        ShortcutsOpen = false;
        Changed?.Invoke();
    }

    public void SetImmersive(bool immersive)
    {
        if (Immersive == immersive)
        {
            return;
        }

        Immersive = immersive;
        Changed?.Invoke();
    }

    // ----------------------------------------------------------------- theme

    /// <summary>Pushes the profile's theme settings into the DOM.</summary>
    public async Task ApplyThemeAsync()
    {
        UserProfile profile = _store.Profile;
        await _js.ApplyThemeAsync(profile.Theme, profile.Accent, profile.ReduceMotion);
    }

    /// <summary>Discovers speech support once, at startup.</summary>
    public async Task DetectCapabilitiesAsync()
    {
        SpeechAvailable = await _js.SpeechAvailableAsync();

        if (SpeechAvailable)
        {
            Voices = await _js.VoicesAsync();
        }

        Changed?.Invoke();
    }

    /// <summary>Re-reads the voice list; the browser populates it asynchronously.</summary>
    public async Task RefreshVoicesAsync()
    {
        if (!SpeechAvailable)
        {
            return;
        }

        Voices = await _js.VoicesAsync();
        Changed?.Invoke();
    }

    // ------------------------------------------------------------ navigation

    public void Go(string uri)
    {
        CloseOverlays();
        _nav.NavigateTo(uri);
    }

    /// <summary>Speaks a card face using the learner's voice preferences.</summary>
    public async Task SpeakAsync(string text, string language)
    {
        if (!SpeechAvailable || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        UserProfile profile = _store.Profile;
        await _js.SpeakAsync(text, language, profile.PreferredVoice, profile.SpeechRate, profile.SpeechPitch);
    }

    /// <summary>Plays a UI sound, if the learner has them switched on.</summary>
    public async Task BlipAsync(string kind)
    {
        if (_store.Profile.SoundEffects)
        {
            await _js.BlipAsync(kind);
        }
    }
}
