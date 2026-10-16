using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.App.Services;
using System.Globalization;
using Arcadia.ControllerSupport;
using Arcadia.Core.Updates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;

namespace Arcadia.App.ViewModels;

/// <summary>A focusable control in the settings dialog.</summary>
public partial class SettingsItemViewModel(Action activate) : ViewModelBase
{
    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    public void Activate() => activate();
}

/// <summary>A question over the settings that has to be answered before "SISR installieren" can go on.</summary>
public enum SettingsPrompt
{
    None,

    /// <summary>A Steam game runs; installing would end it, because Steam has to close.</summary>
    SteamGameRunning,

    /// <summary>Steam runs; asking whether Arcadia may close it for the install.</summary>
    ConfirmSteamStop,

    /// <summary>Switching on started Steam, but it wasn't up within a minute; the setting is off again.</summary>
    SteamStartFailed,
}

/// <summary>A problem found by the controller-support monitoring, with an optional fix button.</summary>
public sealed class HealthIssueViewModel(HealthIssue issue, Action<HealthFix> fix) : SettingsItemViewModel(() => fix(issue.Fix))
{
    public string Title => issue.Title;

    public string Detail => issue.Detail;

    public bool IsError => issue.Severity == HealthSeverity.Error;

    public bool HasFix => issue.Fix != HealthFix.None;

    public string FixLabel => issue.FixLabel ?? "Beheben";

    public IRelayCommand FixCommand { get; } = new RelayCommand(() => fix(issue.Fix));
}

/// <summary>One version in the "Was ist neu" box.</summary>
public sealed class ChangelogEntryViewModel(ChangelogEntry entry)
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    public string VersionText => $"Version {entry.Version.ToString(3)}";

    public string DateText => entry.PublishedAt is { } date ? date.ToLocalTime().ToString("d. MMMM yyyy", German) : string.Empty;

    public IReadOnlyList<string> Changes => entry.Changes.Count > 0 ? entry.Changes : ["Keine Änderungen aufgeführt."];
}

/// <summary>
/// The settings dialog. For now it holds "Steam Controller als Xbox 360 Gamepad verwenden" and "Über Arcadia"; the
/// other sections from the designs follow.
/// </summary>
public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly ControllerSupportService _support;
    private readonly ArcadiaUpdateService _updates;
    private readonly SettingsItemViewModel _toggle;
    private readonly SettingsItemViewModel _retryInstall;
    private readonly SettingsItemViewModel _closeGamePrompt;
    private readonly SettingsItemViewModel _confirmSteamStop;
    private readonly SettingsItemViewModel _cancelSteamStop;
    private readonly SettingsItemViewModel _retrySteamStart;
    private readonly SettingsItemViewModel _cancelSteamStart;
    private readonly DispatcherTimer _promptCheck;
    private readonly SettingsItemViewModel _update;
    private readonly SettingsItemViewModel _arcadiaUpdate;
    private readonly SettingsItemViewModel _checkUpdates;
    private int _focus;

    public SettingsViewModel(ControllerSupportService support, ArcadiaUpdateService updates, Func<Task> installArcadiaUpdate)
    {
        _checkUpdates = new SettingsItemViewModel(() => updates.CheckNowAsync().Forget("Update-Prüfung"));
        CheckUpdatesCommand = new RelayCommand(() => updates.CheckNowAsync().Forget("Update-Prüfung"));
        _support = support;
        _updates = updates;
        _toggle = new SettingsItemViewModel(ActivateMainRow);
        _retryInstall = new SettingsItemViewModel(BeginInstall);
        _closeGamePrompt = new SettingsItemViewModel(ClosePrompt);
        _confirmSteamStop = new SettingsItemViewModel(ConfirmSteamStop);
        _cancelSteamStop = new SettingsItemViewModel(ClosePrompt);
        _retrySteamStart = new SettingsItemViewModel(SwitchOn);
        _cancelSteamStart = new SettingsItemViewModel(ClosePrompt);
        _update = new SettingsItemViewModel(() => _support.InstallUpdateAsync().Forget("SISR aktualisieren"));
        _arcadiaUpdate = new SettingsItemViewModel(() => installArcadiaUpdate().Forget("Arcadia aktualisieren"));
        ToggleCommand = new RelayCommand(ActivateMainRow);
        RetryInstallCommand = new RelayCommand(BeginInstall);
        RetrySteamStartCommand = new RelayCommand(SwitchOn);
        ConfirmSteamStopCommand = new RelayCommand(ConfirmSteamStop);
        ClosePromptCommand = new RelayCommand(ClosePrompt);

        // The game can end (or start) while a prompt is open; nothing else would tell us.
        _promptCheck = new DispatcherTimer(PromptCheckInterval, DispatcherPriority.Background,
            async (_, _) => await AppErrors.Guard(CheckPromptAsync, "Laufendes Steam-Spiel prüfen"));

        // An old failure describes a moment that is over; the dialog opens without it.
        _support.ClearError();
        UpdateCommand = new RelayCommand(() => _support.InstallUpdateAsync().Forget("SISR aktualisieren"));
        ArcadiaUpdateCommand = new RelayCommand(() => installArcadiaUpdate().Forget("Arcadia aktualisieren"));
        _support.Changed += OnSupportChanged;
        _updates.Changed += OnSupportChanged;
        Refresh();
        _toggle.IsFocused = true;
    }

    public SettingsItemViewModel ToggleItem => _toggle;

    private static readonly TimeSpan PromptCheckInterval = TimeSpan.FromSeconds(1);

    public SettingsItemViewModel RetryInstallItem => _retryInstall;

    public SettingsItemViewModel CloseGamePromptItem => _closeGamePrompt;

    public SettingsItemViewModel ConfirmSteamStopItem => _confirmSteamStop;

    public SettingsItemViewModel CancelSteamStopItem => _cancelSteamStop;

    public SettingsItemViewModel RetrySteamStartItem => _retrySteamStart;

    public SettingsItemViewModel CancelSteamStartItem => _cancelSteamStart;

    public IRelayCommand RetryInstallCommand { get; }

    public IRelayCommand RetrySteamStartCommand { get; }

    public IRelayCommand ConfirmSteamStopCommand { get; }

    /// <summary>"Schließen" / "Abbrechen", B, or a click beside the prompt.</summary>
    public IRelayCommand ClosePromptCommand { get; }

    /// <summary>Everything is in place (or the setting is already on): the row is a switch.</summary>
    [ObservableProperty]
    public partial bool ShowSwitch { get; private set; }

    /// <summary>Something is missing: the row offers "SISR installieren" instead of the switch.</summary>
    [ObservableProperty]
    public partial bool ShowInstall { get; private set; }

    /// <summary>Not on this PC (no Windows, no Steam): the row is greyed out and says why.</summary>
    [ObservableProperty]
    public partial bool IsUnavailable { get; private set; }

    [ObservableProperty]
    public partial string UnavailableText { get; private set; } = string.Empty;

    /// <summary>The modal over the settings, if any.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPromptOpen), nameof(IsGamePrompt), nameof(IsSteamStopPrompt), nameof(IsSteamStartFailedPrompt), nameof(IsWarningPrompt), nameof(PromptBackText))]
    public partial SettingsPrompt Prompt { get; private set; }

    public bool IsPromptOpen => Prompt != SettingsPrompt.None;

    public bool IsGamePrompt => Prompt == SettingsPrompt.SteamGameRunning;

    public bool IsSteamStopPrompt => Prompt == SettingsPrompt.ConfirmSteamStop;

    public bool IsSteamStartFailedPrompt => Prompt == SettingsPrompt.SteamStartFailed;

    /// <summary>The prompt reports a problem ("!") rather than asking ("?").</summary>
    public bool IsWarningPrompt => Prompt is SettingsPrompt.SteamGameRunning or SettingsPrompt.SteamStartFailed;

    /// <summary>The prompts that "SISR installieren" opens; they end when the install becomes moot.</summary>
    private bool IsInstallPrompt => Prompt is SettingsPrompt.SteamGameRunning or SettingsPrompt.ConfirmSteamStop;

    public string PromptBackText => IsGamePrompt ? "Schließen" : "Abbrechen";

    /// <summary>The Steam game named in the game prompt.</summary>
    [ObservableProperty]
    public partial string RunningGame { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowStatus { get; private set; }

    public SettingsItemViewModel UpdateItem => _update;

    public SettingsItemViewModel ArcadiaUpdateItem => _arcadiaUpdate;

    public IRelayCommand ArcadiaUpdateCommand { get; }

    public SettingsItemViewModel CheckUpdatesItem => _checkUpdates;

    public IRelayCommand CheckUpdatesCommand { get; }

    public ObservableCollection<ChangelogEntryViewModel> Changelog { get; } = [];

    [ObservableProperty]
    public partial bool HasChangelog { get; private set; }

    /// <summary>"Nach Updates suchen" is shown for an installed Arcadia.</summary>
    public bool CanSearchForUpdates => _updates.IsInstalled;

    [ObservableProperty]
    public partial bool IsCheckIdle { get; private set; }

    [ObservableProperty]
    public partial string CheckButtonText { get; private set; } = "Nach Updates suchen";


    public string ArcadiaVersionText => $"Arcadia {_updates.CurrentVersion}";

    public string SourceText => $"Freie Software unter der GPL-3.0 · Quelltext: {ArcadiaUpdateService.RepositoryUrl.Replace("https://", string.Empty, StringComparison.Ordinal)}";

    /// <summary>"Jetzt aktualisieren" is offered (an update is available and not yet downloaded).</summary>
    [ObservableProperty]
    public partial bool HasArcadiaUpdate { get; private set; }

    /// <summary>Update state below the version: available, downloading, waiting for exit, dev build.</summary>
    [ObservableProperty]
    public partial string ArcadiaUpdateText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string? ArcadiaUpdateError { get; private set; }

    public ObservableCollection<HealthIssueViewModel> Issues { get; } = [];

    public IRelayCommand ToggleCommand { get; }

    public IRelayCommand UpdateCommand { get; }

    [ObservableProperty]
    public partial bool IsEnabled { get; private set; }

    [ObservableProperty]
    public partial string StatusText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsHealthy { get; private set; }

    /// <summary>SISR is starting (doesn't emulate the controller yet) or stopping; shown with a spinner.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStatusDot))]
    public partial bool IsInTransition { get; private set; }

    public bool ShowStatusDot => !IsInTransition;

    [ObservableProperty]
    public partial bool HasIssues { get; private set; }

    [ObservableProperty]
    public partial string VersionText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasUpdate { get; private set; }

    [ObservableProperty]
    public partial string UpdateText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string? BusyText { get; private set; }

    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    public bool IsBusy => BusyText is not null;

    /// <summary>On, but Steam's "SISR Marker" is missing; its fix goes through the install prompts.</summary>
    private bool NeedsMarker { get; set; }

    /// <summary>Steam, SISR or VIIPER are starting or stopping (<see cref="ControllerSupportService.IsSettling"/>): the dialog takes no input but B until it is done.</summary>
    [ObservableProperty]
    public partial bool IsWorking { get; private set; }

    /// <returns>False if the action closes the dialog.</returns>
    public bool Handle(UiAction action)
    {
        if (IsWorking && action is not (UiAction.Back or UiAction.Settings))
        {
            return true;
        }

        switch (action)
        {
            case UiAction.Left when IsPromptOpen:
            case UiAction.Up:
                MoveFocus(-1);
                return true;
            case UiAction.Right when IsPromptOpen:
            case UiAction.Down:
                MoveFocus(1);
                return true;
            case UiAction.Accept:
                if (!IsBusy)
                {
                    FocusableItems()[_focus].Activate();
                }

                return true;
            case UiAction.Back when IsPromptOpen:
                ClosePrompt();
                return true;
            case UiAction.Back:
            case UiAction.Settings:
                return false;
            default:
                return true;
        }
    }

    public void Dispose()
    {
        _promptCheck.Stop();
        _support.Changed -= OnSupportChanged;
        _updates.Changed -= OnSupportChanged;
        _support.ClearError();
    }

    private void ActivateMainRow()
    {
        if (IsBusy)
        {
            return;
        }

        if (ShowSwitch && _support.Enabled)
        {
            _support.SetEnabledAsync(false).Forget("Steam Controller (SISR) ausschalten");
        }
        else if (ShowSwitch)
        {
            SwitchOn();
        }
        else if (ShowInstall)
        {
            BeginInstall();
        }
    }

    /// <summary>
    /// Installing closes Steam, so it can't run beside a Steam game, and the user is asked before
    /// Steam closes. Without Steam running it just installs. Also "Erneut versuchen".
    /// </summary>
    private void BeginInstall()
    {
        if (_support.SteamGameBlockingInstall() is { } game)
        {
            RunningGame = game;
            ShowPrompt(SettingsPrompt.SteamGameRunning);
        }
        else if (_support.InstallStopsSteam)
        {
            ShowPrompt(SettingsPrompt.ConfirmSteamStop);
        }
        else
        {
            ShowPrompt(SettingsPrompt.None);
            _support.InstallAsync().Forget("SISR installieren");
        }
    }

    /// <summary>Switching on starts Steam first; if Steam doesn't come up, the setting is off again and a prompt says so.</summary>
    private void SwitchOn()
    {
        ShowPrompt(SettingsPrompt.None);
        SwitchOnAsync().Forget("Steam Controller (SISR) einschalten");
    }

    private async Task SwitchOnAsync()
    {
        if (!await _support.SetEnabledAsync(true))
        {
            ShowPrompt(SettingsPrompt.SteamStartFailed);
        }
    }

    /// <summary>The marker can only be written while Steam is closed, so it goes through the install prompts.</summary>
    private void ApplyFix(HealthFix fix)
    {
        if (fix == HealthFix.CreateMarker)
        {
            BeginInstall();
            return;
        }

        _support.ApplyFixAsync(fix).Forget("Problem beheben");
    }

    private void ConfirmSteamStop()
    {
        // A game may have started since the question came up.
        if (_support.SteamGameBlockingInstall() is not null)
        {
            BeginInstall();
            return;
        }

        ShowPrompt(SettingsPrompt.None);
        _support.InstallAsync().Forget("SISR installieren");
    }

    private void ClosePrompt() => ShowPrompt(SettingsPrompt.None);

    private void ShowPrompt(SettingsPrompt prompt)
    {
        Prompt = prompt;
        if (!IsInstallPrompt)
        {
            _promptCheck.Stop();
        }
        else
        {
            _promptCheck.Start();
        }

        Refresh();
    }

    /// <summary>
    /// Keeps an open prompt true: when the game ends it goes on with "Steam beenden?" (or closes
    /// if Steam is gone too); when a game starts during "Steam beenden?" it says so.
    /// </summary>
    private Task CheckPromptAsync()
    {
        var game = _support.SteamGameBlockingInstall();
        switch (Prompt)
        {
            case SettingsPrompt.SteamGameRunning when game is not null:
                RunningGame = game;
                break;
            case SettingsPrompt.SteamGameRunning:
                ShowPrompt(_support.InstallStopsSteam ? SettingsPrompt.ConfirmSteamStop : SettingsPrompt.None);
                break;
            case SettingsPrompt.ConfirmSteamStop when game is not null:
                RunningGame = game;
                ShowPrompt(SettingsPrompt.SteamGameRunning);
                break;
        }

        return Task.CompletedTask;
    }

    private void OnSupportChanged(object? sender, EventArgs e) => Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);

    private void Refresh()
    {
        var focused = FocusableItems().ElementAtOrDefault(_focus);

        IsEnabled = _support.Enabled;
        BusyText = _support.BusyText;
        OnPropertyChanged(nameof(IsBusy));
        IsWorking = _support.IsSettling;
        ErrorText = _support.LastError;

        Issues.Clear();
        foreach (var issue in _support.Issues)
        {
            Issues.Add(new HealthIssueViewModel(issue, ApplyFix));
        }

        HasIssues = Issues.Count > 0;

        // Once on, the switch stays even if something breaks later; the problem shows below it.
        var setup = _support.Setup;
        NeedsMarker = _support.Issues.Any(i => i.Fix == HealthFix.CreateMarker);
        IsUnavailable = !IsEnabled && setup is ControllerSupportSetup.UnsupportedPlatform or ControllerSupportSetup.SteamMissing;
        UnavailableText = setup == ControllerSupportSetup.SteamMissing ? "Benötigt Steam." : "Momentan nur unter Windows verfügbar.";
        ShowSwitch = IsEnabled || setup == ControllerSupportSetup.Ready;
        ShowInstall = !IsEnabled && setup == ControllerSupportSetup.NeedsInstall;
        if (IsInstallPrompt && ((!ShowInstall && !NeedsMarker) || IsBusy))
        {
            // Installed some other way, or something else started: the question is moot.
            Prompt = SettingsPrompt.None;
            _promptCheck.Stop();
        }

        ShowStatus = !IsUnavailable;

        var state = _support.State;
        IsInTransition = IsEnabled && state is SisrState.Starting or SisrState.Stopping;
        IsHealthy = IsEnabled && !HasIssues && BusyText is null && state is not (SisrState.Starting or SisrState.NoController);
        StatusText = (IsEnabled, state == SisrState.Paused, HasIssues) switch
        {
            (false, _, _) when ShowInstall => "Nicht eingerichtet – „SISR installieren“ richtet alles Nötige ein und schaltet die Option ein.",
            (false, _, _) => "Aus – außerhalb von Steam läuft der Steam Controller im Desktop-Modus (Maus und Tastatur).",
            (true, true, _) => "Pausiert – ein Spiel mit „Steam Input“ läuft.",
            (true, false, true) => "Eingeschaltet, aber nicht voll funktionsfähig – siehe unten.",
            _ => state switch
            {
                SisrState.Starting => "SISR startet – der Steam Controller wird gleich als Xbox 360 Controller eingerichtet …",
                SisrState.NoController => "Bereit, aber kein Steam Controller gefunden – schalte ihn ein.",
                SisrState.Running => "Aktiv – der Steam Controller wird als Xbox 360 Controller erkannt.",
                SisrState.Stopping => "SISR wird beendet …",
                _ => "Wird gestartet …",
            },
        };

        VersionText = _support.Sisr.InstalledVersion is { } version ? $"SISR {version}" : "SISR ist noch nicht installiert";
        HasUpdate = _support.AvailableUpdate is not null;
        UpdateText = _support.AvailableUpdate is { } update ? $"Neue Version verfügbar: {update.Tag}" : string.Empty;

        HasArcadiaUpdate = _updates.AvailableVersion is not null && _updates.Progress is null && !_updates.IsRestartingForUpdate;
        ArcadiaUpdateError = _updates.LastError;
        IsCheckIdle = !_updates.IsChecking && _updates.Progress is null && !_updates.IsRestartingForUpdate;
        CheckButtonText = _updates.IsChecking ? "Suche läuft …" : "Nach Updates suchen";
        ArcadiaUpdateText = _updates switch
        {
            { IsInstalled: false } => "Entwicklungs-Build – Updates gibt es nur für das installierte Arcadia.",
            { IsChecking: true } => "Suche nach Updates …",
            { AvailableVersion: { } next, IsRestartingForUpdate: true } => $"Version {next} ist geladen – Arcadia startet neu …",
            { AvailableVersion: { } next, Progress: { } percent } => $"Version {next} wird geladen … {percent} %",
            { AvailableVersion: { } next } => $"Version {next} ist verfügbar.",
            { LastChecked: { } checkedAt } => $"Arcadia ist auf dem neuesten Stand. Zuletzt gesucht: {checkedAt.ToLocalTime():HH:mm} Uhr.",
            _ => "Arcadia ist auf dem neuesten Stand.",
        };

        if (!Changelog.Select(c => c.VersionText).SequenceEqual(_updates.Changelog.Select(e => $"Version {e.Version.ToString(3)}")))
        {
            Changelog.Clear();
            foreach (var entry in _updates.Changelog)
            {
                Changelog.Add(new ChangelogEntryViewModel(entry));
            }
        }

        HasChangelog = _updates.AvailableVersion is not null && Changelog.Count > 0;

        // Keep the controller focus on the same item where possible. All items lose it first, so
        // the row behind an open prompt doesn't keep its ring.
        foreach (var item in new[] { _toggle, _update, _checkUpdates, _arcadiaUpdate, _retryInstall, _closeGamePrompt, _confirmSteamStop, _cancelSteamStop, _retrySteamStart, _cancelSteamStart })
        {
            item.IsFocused = false;
        }

        var items = FocusableItems();
        // A prompt opening focuses its first button; one closing hands focus back to the main row (first).
        _focus = focused is not null && items.Contains(focused) ? items.IndexOf(focused)
            : IsPromptOpen || IsPromptItem(focused) ? 0
            : Math.Min(_focus, items.Count - 1);
        items[_focus].IsFocused = true;
    }

    private List<SettingsItemViewModel> FocusableItems()
    {
        // While a prompt is open, only its buttons can be focused.
        switch (Prompt)
        {
            case SettingsPrompt.SteamGameRunning:
                return [_retryInstall, _closeGamePrompt];
            case SettingsPrompt.ConfirmSteamStop:
                return [_confirmSteamStop, _cancelSteamStop];
            case SettingsPrompt.SteamStartFailed:
                return [_retrySteamStart, _cancelSteamStart];
        }

        var items = new List<SettingsItemViewModel> { _toggle };
        if (HasUpdate)
        {
            items.Add(_update);
        }

        items.AddRange(Issues.Where(i => i.HasFix));
        if (CanSearchForUpdates)
        {
            items.Add(_checkUpdates);
        }

        if (HasArcadiaUpdate)
        {
            items.Add(_arcadiaUpdate);
        }

        return items;
    }

    private bool IsPromptItem(SettingsItemViewModel? item) =>
        item == _retryInstall || item == _closeGamePrompt || item == _confirmSteamStop || item == _cancelSteamStop
        || item == _retrySteamStart || item == _cancelSteamStart;

    private void MoveFocus(int delta)
    {
        var items = FocusableItems();
        var next = Math.Clamp(_focus + delta, 0, items.Count - 1);
        items[_focus].IsFocused = false;
        _focus = next;
        items[_focus].IsFocused = true;
    }
}
