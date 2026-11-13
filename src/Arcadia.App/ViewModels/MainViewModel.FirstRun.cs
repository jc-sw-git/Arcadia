using Arcadia.App.Services;
using Arcadia.App.Settings;
using Arcadia.App.Setup;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.ViewModels;

/// <summary>
/// Main's part of the first start (#139, #497): the launcher page, then the step "Datenquellen" in the settings' frame,
/// then the library. It continues on the page it stopped on, with what was chosen, also after closing or a crash
/// (<see cref="FirstRunSetup.Progress"/>).
/// </summary>
public partial class MainViewModel
{
    // The launcher page, kept while "Datenquellen" shows, so B comes back to it with the switches as left.
    private FirstRunViewModel? _launcherPage;

    // B on "Datenquellen" while the first read still ran: the launchers switched off are read once it is over.
    private bool _firstRunReadsAllLaunchers;

    /// <summary>The first start shows (a page of it): the library isn't there yet for the player.</summary>
    public bool IsInFirstRun => _firstRun.IsPending;

    /// <summary>
    /// Shown from the start, so the library never flashes up before it; the launcher page's launchers follow once they are
    /// read. A first start that stopped on "Datenquellen" opens there.
    /// </summary>
    private void StartFirstRun(LauncherRegistry launchers, Func<LauncherInfo, LauncherBadge> badgeOf)
    {
        _launcherPage = new FirstRunViewModel(launchers, badgeOf, _firstRun.Progress.SwitchedOff, _firstRun.SwitchedOff, ChooseFirstRunLaunchers);
        if (_firstRun.Progress.Step == FirstRunStep.DataSources && ShowFirstRunDataSources())
        {
            AppLog.Write("Ersteinrichtung: weiter bei „Datenquellen“");
            return;
        }

        FirstRun = _launcherPage;
    }

    /// <summary>The launcher page counts the games read; on "Datenquellen" the library holds only the launchers chosen.</summary>
    private void ShowLaunchersInFirstRun(IReadOnlyList<LauncherLibrary> libraries)
    {
        if (FirstRun is null)
        {
            return;
        }

        FirstRun.Show(libraries);
        if (_firstRunReadsAllLaunchers && !Session.IsLoading)
        {
            _firstRunReadsAllLaunchers = false;
            Session.ShowChosenLaunchers();
        }
    }

    /// <summary>"Fertig" on the launcher page: on to "Datenquellen", the library already with only these launchers.</summary>
    private void ChooseFirstRunLaunchers(IReadOnlyList<string> active)
    {
        _firstRun.ChooseLaunchers(active);
        AppLog.Write($"Ersteinrichtung: Launcher gewählt: {string.Join(", ", active)}");
        if (!ShowFirstRunDataSources())
        {
            CompleteFirstRun(active);
            return;
        }

        FirstRun = null;
        Session.ShowChosenLaunchers();
    }

    /// <returns>False if there is no step to show (no artwork service wired, as in some tests).</returns>
    private bool ShowFirstRunDataSources()
    {
        if (_artwork is not { SteamGridDb: { } key } artwork)
        {
            return false;
        }

        var changes = new SettingsChanges(_settings);
        var step = new FirstRunDataSourcesViewModel(key, artwork, this, changes, _time, KeepsSteamGridDbKey, _metadata, BackToFirstRunLaunchers, FinishFirstRun);
        Settings = new SettingsPageViewModel([step], changes, this) { IsFirstRun = true };
        return true;
    }

    /// <summary>B on "Datenquellen": the launcher page again, with every launcher found read for its counts.</summary>
    private void BackToFirstRunLaunchers()
    {
        _firstRun.BackToLaunchers();
        FirstRun = _launcherPage;
        Settings = null;
        if (Session.IsLoading)
        {
            _firstRunReadsAllLaunchers = true;
        }
        else
        {
            Session.ShowChosenLaunchers();
        }
    }

    /// <summary>≡, Enter or "Überspringen" / "Fertig" on "Datenquellen": the library, with the launchers chosen.</summary>
    private void FinishFirstRun()
    {
        if (_firstRun.Progress.Chosen is not { } active)
        {
            return;
        }

        Settings = null;
        CompleteFirstRun(active);
    }

    /// <summary>The first start ends: only the launchers switched on keep their tab, and it doesn't come again.</summary>
    private void CompleteFirstRun(IReadOnlyList<string> active)
    {
        try
        {
            _firstRun.Complete(active);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Launcherauswahl nicht gespeichert: {ex.Message}");
            Toast.Show(HealthSeverity.Error, AppTexts.FirstRunNotSaved);
        }

        AppLog.Write($"Ersteinrichtung abgeschlossen, aktive Launcher: {string.Join(", ", active)}");
        FirstRun = null;
        _launcherPage = null;
        OnPropertyChanged(nameof(IsInFirstRun));
        Session.ShowChosenLaunchers();
    }
}
