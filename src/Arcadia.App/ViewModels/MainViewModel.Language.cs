using System.Globalization;
using Arcadia.App.Services;
using Arcadia.App.Settings;

namespace Arcadia.App.ViewModels;

/// <summary>
/// Main's part of the UI language (#554): what "Sprache anwenden" waits for, the restart it asks the App for, and the
/// page the restarted Arcadia opens on.
/// </summary>
public partial class MainViewModel
{
    /// <summary>The language this Arcadia started in and the system's display language: the App's, or a test's own.</summary>
    public (CultureInfo InUse, CultureInfo System) Languages { get; init; } = (UiLanguage.Culture, UiLanguage.System);

    /// <summary>
    /// Starts Arcadia again in the saved language, opening the settings on "Sprache" in the same window mode (the App);
    /// on failure it shows a toast and this Arcadia stays. Null: nothing restarts (a test that doesn't set it).
    /// </summary>
    public Action? RestartForLanguage { get; set; }

    /// <summary>The restarted Arcadia: the settings, category "Lokalisierung", focus on "Sprache". Not over the first start.</summary>
    public void ShowLanguageSetting()
    {
        if (FirstRun is not null)
        {
            return;
        }

        ShowSettings(typeof(LocalizationSettingsViewModel));
        FocusRowOrTop(Settings?.Selected, UiLanguageRows.LanguageKey);
    }

    private LanguageRestart CreateLanguageRestart() => new(
        Languages.InUse,
        Languages.System,
        GameInTheWay,
        () => _controllerSupport.Installation is { IsOver: false },
        () => _updates.Progress is not null || _updates.IsRestartingForUpdate,
        () => _artwork?.Search.Running is { Reload: true } || _metadata?.Progress is { Running: true, Reload: true },
        () => RestartForLanguage?.Invoke());

    /// <summary>Everything "Sprache anwenden" waits for or warns of, followed from the start; the events may come off the UI thread.</summary>
    private void WatchLanguageRestart()
    {
        _controllerSupport.Changed += OnLanguageRestartStateChanged;
        _updates.Changed += OnLanguageRestartStateChanged;
        if (_artwork is not null)
        {
            _artwork.SearchChanged += OnLanguageRestartStateChanged;
        }

        if (_metadata is not null)
        {
            _metadata.ProgressChanged += OnLanguageRestartStateChanged;
        }
    }

    /// <summary>A closed window stops listening: the services outlive it in the tests.</summary>
    private void StopWatchingLanguageRestart()
    {
        _controllerSupport.Changed -= OnLanguageRestartStateChanged;
        _updates.Changed -= OnLanguageRestartStateChanged;
        if (_artwork is not null)
        {
            _artwork.SearchChanged -= OnLanguageRestartStateChanged;
        }

        if (_metadata is not null)
        {
            _metadata.ProgressChanged -= OnLanguageRestartStateChanged;
        }
    }

    private void OnLanguageRestartStateChanged(object? sender, EventArgs e) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(RefreshLanguageRestart);

    private void RefreshLanguageRestart() =>
        Settings?.Categories.OfType<LocalizationSettingsViewModel>().FirstOrDefault()?.RestartStateChanged();
}
