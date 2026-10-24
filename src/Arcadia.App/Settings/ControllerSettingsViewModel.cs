using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Settings;

/// <summary>A button in a dialog over the settings page, e.g. "Erneut versuchen".</summary>
public partial class PromptButtonViewModel(Action activate) : ViewModels.ViewModelBase
{
    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    public virtual void Activate() => activate();
}

/// <summary>A Steam account in the marker prompt; accounts that already have the marker stay ticked.</summary>
public sealed partial class MarkerAccountViewModel(MarkerAccount account) : PromptButtonViewModel(static () => { })
{
    public uint Id => account.Id;

    public string Name => account.Name;

    public bool HasMarker => account.HasMarker;

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = account.HasMarker;

    public IRelayCommand ToggleCommand => field ??= new RelayCommand(Activate);

    public override void Activate()
    {
        if (!HasMarker)
        {
            IsChecked = !IsChecked;
        }
    }
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

    /// <summary>Several Steam accounts: which of them get SISR's marker in their Steam library.</summary>
    ChooseMarkerAccounts,
}

/// <summary>
/// The category "Controller" (#15): the group "Steam" with Steam Controller Support (switch, problems with their fix,
/// SISR's update) and Steam's desktop layout (#169), then the mouse mode and "Zurück zu Arcadia", as rows of the page. Status, how it works, drawbacks and SISR's
/// version are nothing to set, so they are boxes in the switch's info panel (owner, 2026-10-22). Installing SISR asks its
/// questions in dialogs over the page.
/// </summary>
public sealed partial class ControllerSettingsViewModel : SettingsCategoryViewModel
{
    /// <summary>
    /// Everything Steam does to the controllers, in one group (owner, 2026-10-24). The desktop layout isn't a
    /// dependent of the SISR switch: it covers every controller, SISR only the Steam Controller.
    /// </summary>
    private const string SteamGroup = "STEAM";

    private static readonly TimeSpan PromptCheckInterval = TimeSpan.FromSeconds(1);

    private readonly ControllerSupportService _support;
    private readonly SisrRowViewModel _sisr;
    private readonly ActionRowViewModel _update;
    private readonly MouseModeRows _mouseMode;
    private readonly ReturnWithViewRows _returnWithView;
    private readonly SwitchRowViewModel _steamDesktopLayout;
    private readonly Dictionary<string, ActionRowViewModel> _issueRows = [];

    private readonly PromptButtonViewModel _retryInstall;
    private readonly PromptButtonViewModel _closeGamePrompt;
    private readonly PromptButtonViewModel _confirmSteamStop;
    private readonly PromptButtonViewModel _cancelSteamStop;
    private readonly PromptButtonViewModel _retrySteamStart;
    private readonly PromptButtonViewModel _cancelSteamStart;
    private readonly PromptButtonViewModel _confirmMarkerAccounts;
    private readonly PromptButtonViewModel _cancelMarkerAccounts;

    /// <summary>The accounts chosen in the marker prompt for the install under way; null until chosen (or not asked).</summary>
    private IReadOnlyCollection<uint>? _markerChoice;
    private readonly DispatcherTimer _promptCheck;
    private bool _disposed;
    private int _promptFocus;

    public ControllerSettingsViewModel(ControllerSupportService support, SettingsChanges changes)
        : base("Controller", "M7 7h10a5 5 0 0 1 4.6 6.9l-1.3 3.2a2.3 2.3 0 0 1-3.9.6L14.6 16H9.4l-1.8 1.7a2.3 2.3 0 0 1-3.9-.6l-1.3-3.2A5 5 0 0 1 7 7z M8 10v3 M6.5 11.5h3 M16 10.5v.01 M17.5 12.5v.01")
    {
        _support = support;
        _sisr = new SisrRowViewModel(ActivateSisr)
        {
            Group = SteamGroup,
            Subtitle = "Nur Steam Controller: für Spiele außerhalb von Steam und für Arcadia",
        };
        _update = new ActionRowViewModel("sisr-update", "SISR aktualisieren", () => _support.InstallUpdateAsync().Forget("SISR aktualisieren"))
        {
            Group = SteamGroup,
            ActionText = "Aktualisieren",
            IsPrimary = true,
        };
        _mouseMode = new MouseModeRows(changes);
        _returnWithView = new ReturnWithViewRows(changes);
        _steamDesktopLayout = new SwitchRowViewModel(
            "steam-desktop-layout",
            "Steams Desktop-Belegung ignorieren",
            () => changes.Current.IgnoreSteamDesktopLayout,
            value => changes.Apply(s => s with { IgnoreSteamDesktopLayout = value }, "Steams Desktop-Belegung ignorieren"))
        {
            Group = SteamGroup,
            Subtitle = "Alle Controller: z. B. Steams Alt+Tab bei ⧉ + ≡ verwerfen",
            Info =
            [
                new InfoText("Läuft Steam, macht es außerhalb von Spielen aus manchen Controller-Eingaben selbst Tasten („Desktop-Belegung“): Hältst du ⧉ + ≡, löst Steam mit jedem Controller ein Alt+Tab aus."),
                new InfoText("Ist die Option an, verwirft Arcadia solche Tasten und Mausbewegungen von Steam, solange ein Controller benutzt wird. Echte Maus und Tastatur wirken weiter. Neben einem Spiel aus der Bibliothek verwirft Arcadia nur Tasten, und nur solange ⧉ + ≡ gehalten werden, damit das Spiel Steams Belegung behält."),
                new InfoText("Empfohlen, wenn du den Mausmodus benutzt: ⧉ + ≡ schaltet ihn um, und ohne diese Option wechselt Steam dabei zugleich mit Alt+Tab das Fenster."),
            ],
        };

        _retryInstall = new PromptButtonViewModel(ContinueInstall);
        _closeGamePrompt = new PromptButtonViewModel(ClosePrompt);
        _confirmSteamStop = new PromptButtonViewModel(ConfirmSteamStop);
        _cancelSteamStop = new PromptButtonViewModel(ClosePrompt);
        _retrySteamStart = new PromptButtonViewModel(RetrySteamStart);
        _cancelSteamStart = new PromptButtonViewModel(ClosePrompt);
        _confirmMarkerAccounts = new PromptButtonViewModel(ConfirmMarkerAccounts);
        _cancelMarkerAccounts = new PromptButtonViewModel(ClosePrompt);
        RetryInstallCommand = new RelayCommand(ContinueInstall);
        ConfirmMarkerAccountsCommand = new RelayCommand(ConfirmMarkerAccounts);
        RetrySteamStartCommand = new RelayCommand(RetrySteamStart);
        ConfirmSteamStopCommand = new RelayCommand(ConfirmSteamStop);
        ClosePromptCommand = new RelayCommand(ClosePrompt);

        // The game can end (or start) while a prompt is open; nothing else would tell us.
        _promptCheck = new DispatcherTimer(PromptCheckInterval, DispatcherPriority.Background,
            async (_, _) => await AppErrors.Guard(CheckPromptAsync, "Laufendes Steam-Spiel prüfen"));

        // This DispatcherTimer constructor starts the timer; it only runs while an install prompt is open.
        _promptCheck.Stop();

        // An old failure describes a moment that is over; the page opens without it.
        _support.ClearError();
        _support.Changed += OnSupportChanged;
        Refresh();
        if (_support.UnseenSteamStartFailure)
        {
            ShowSteamStartFailure();
        }

        // Only while the settings are open does the tick recheck what they offer (SISR installed elsewhere).
        _support.WatchSetup = true;
        Task.Run(() => _support.RefreshAsync()).Forget("Einstellungen aktualisieren");
    }

    /// <summary>Steam's page points here too: Steam decides how the controllers behave outside its games (#169).</summary>
    public override IReadOnlyList<SettingsLink> LauncherPageLinks { get; } =
    [
        new(KnownLaunchers.Steam.Id, "Controller-Einstellungen für Steam", "Steam Controller Kompatibilität und Steams Desktop-Belegung", "sisr",
        [
            new InfoText("Was Steam mit deinen Controllern macht, stellst du unter Controller → Steam ein: ob Arcadia den Steam Controller mit SISR auch außerhalb von Steam nutzbar macht, und ob es Steams Desktop-Belegung für alle Controller ignoriert."),
            new InfoText("[A] springt dorthin, [B] kommt hierher zurück."),
        ]),
    ];

    public PromptButtonViewModel RetryInstallItem => _retryInstall;

    public PromptButtonViewModel CloseGamePromptItem => _closeGamePrompt;

    public PromptButtonViewModel ConfirmSteamStopItem => _confirmSteamStop;

    public PromptButtonViewModel CancelSteamStopItem => _cancelSteamStop;

    public PromptButtonViewModel RetrySteamStartItem => _retrySteamStart;

    public PromptButtonViewModel CancelSteamStartItem => _cancelSteamStart;

    public PromptButtonViewModel ConfirmMarkerAccountsItem => _confirmMarkerAccounts;

    public PromptButtonViewModel CancelMarkerAccountsItem => _cancelMarkerAccounts;

    public IRelayCommand ConfirmMarkerAccountsCommand { get; }

    /// <summary>The Steam accounts in the marker prompt.</summary>
    public ObservableCollection<MarkerAccountViewModel> MarkerAccounts { get; } = [];

    /// <summary>"Weiter" in the marker prompt needs at least one account ticked.</summary>
    [ObservableProperty]
    public partial bool CanConfirmMarkerAccounts { get; private set; }

    public IRelayCommand RetryInstallCommand { get; }

    public IRelayCommand RetrySteamStartCommand { get; }

    public IRelayCommand ConfirmSteamStopCommand { get; }

    /// <summary>"Schließen" / "Abbrechen", B, or a click beside the prompt.</summary>
    public IRelayCommand ClosePromptCommand { get; }

    /// <summary>The dialog over the page, if any.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGamePrompt), nameof(IsSteamStopPrompt), nameof(IsSteamStartFailedPrompt), nameof(IsMarkerPrompt), nameof(IsQuestionPrompt), nameof(IsWarningPrompt), nameof(PromptBackText))]
    public partial SettingsPrompt Prompt { get; private set; }

    partial void OnPromptChanged(SettingsPrompt value) => Dialog = value == SettingsPrompt.None ? null : this;

    public bool IsGamePrompt => Prompt == SettingsPrompt.SteamGameRunning;

    public bool IsSteamStopPrompt => Prompt == SettingsPrompt.ConfirmSteamStop;

    public bool IsSteamStartFailedPrompt => Prompt == SettingsPrompt.SteamStartFailed;

    public bool IsMarkerPrompt => Prompt == SettingsPrompt.ChooseMarkerAccounts;

    /// <summary>The prompt asks a question ("?").</summary>
    public bool IsQuestionPrompt => Prompt is SettingsPrompt.ConfirmSteamStop or SettingsPrompt.ChooseMarkerAccounts;

    /// <summary>The prompt reports a problem ("!") rather than asking ("?").</summary>
    public bool IsWarningPrompt => Prompt is SettingsPrompt.SteamGameRunning or SettingsPrompt.SteamStartFailed;

    /// <summary>The prompts that "SISR installieren" opens; they end when the install becomes moot.</summary>
    private bool IsInstallPrompt => Prompt is SettingsPrompt.SteamGameRunning or SettingsPrompt.ConfirmSteamStop or SettingsPrompt.ChooseMarkerAccounts;

    public string PromptBackText => IsGamePrompt ? "Schließen" : "Abbrechen";

    /// <summary>The Steam game named in the game prompt.</summary>
    [ObservableProperty]
    public partial string RunningGame { get; private set; } = string.Empty;

    /// <summary>Steam runs but never finished logging in: restarting it helps, waiting again doesn't.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SteamStartFailedTitle), nameof(SteamStartFailedText), nameof(SteamStartFailedHint), nameof(SteamStartFailedAction))]
    public partial bool SteamLoginHangs { get; private set; }

    public string SteamStartFailedTitle => SteamLoginHangs ? "Steam meldet sich nicht an" : "Steam startet nicht";

    public string SteamStartFailedText => SteamLoginHangs
        ? "Steam läuft, hat sich aber nicht innerhalb von einer Minute fertig angemeldet. Erst danach kann SISR den Steam Controller als Xbox Controller weiterreichen."
        : "Steam konnte nicht innerhalb von einer Minute gestartet werden. Ohne Steam kann SISR den Steam Controller nicht als Xbox Controller weiterreichen.";

    public string SteamStartFailedHint => SteamLoginHangs
        ? "Die Einstellung bleibt deshalb aus. Ein Neustart von Steam hilft meist; danach wird sie eingeschaltet."
        : "Die Einstellung bleibt deshalb aus. Du kannst es erneut versuchen oder Steam selbst starten.";

    public string SteamStartFailedAction => SteamLoginHangs ? "Steam neu starten" : "Erneut versuchen";

    private bool IsBusy => _support.BusyText is not null;

    /// <summary>Steam, SISR or VIIPER start or stop: what depends on them can't be changed now, the rest can.</summary>
    private bool IsSettling => _support.IsSettling;

    public override void HandleDialog(UiAction action)
    {
        switch (action)
        {
            case UiAction.Left or UiAction.Up:
                MovePromptFocus(-1);
                break;
            case UiAction.Right or UiAction.Down:
                MovePromptFocus(1);
                break;
            case UiAction.Accept when !IsBusy:
                PromptButtons()[_promptFocus].Activate();
                break;
            case UiAction.Back:
                ClosePrompt();
                break;
        }
    }

    public override void Dispose()
    {
        _disposed = true;
        _support.WatchSetup = false;
        _promptCheck.Stop();
        _support.Changed -= OnSupportChanged;
        _support.ClearError();
    }

    /// <summary>A, ◀ ▶ or a click on "Steam Controller Kompatibilität": switch, or install first.</summary>
    private void ActivateSisr(bool? on)
    {
        if (IsBusy || IsSettling)
        {
            return;
        }

        if (_sisr.Mode == SisrRowMode.Switch && _support.Enabled && on != true)
        {
            _support.SetEnabledAsync(false).Forget("Steam Controller (SISR) ausschalten");
        }
        else if (_sisr.Mode == SisrRowMode.Switch && !_support.Enabled && on != false)
        {
            SwitchOn();
        }
        else if (_sisr.Mode == SisrRowMode.Install && on != false)
        {
            BeginInstall();
        }
    }

    private void BeginInstall()
    {
        _markerChoice = null;
        ContinueInstall();
    }

    /// <summary>
    /// Installing closes Steam, so it can't run beside a Steam game. With several Steam accounts the
    /// user chooses who gets SISR's marker, and is asked before Steam closes. Without Steam running it
    /// just installs. Also "Erneut versuchen".
    /// </summary>
    private void ContinueInstall()
    {
        if (_support.SteamGameBlockingInstall() is { } game)
        {
            RunningGame = game;
            ShowPrompt(SettingsPrompt.SteamGameRunning);
        }
        else if (_markerChoice is null && _support.MarkerAccountsToChoose() is { Count: > 0 } accounts)
        {
            ShowMarkerPrompt(accounts);
        }
        else if (_support.InstallStopsSteam)
        {
            ShowPrompt(SettingsPrompt.ConfirmSteamStop);
        }
        else
        {
            Install();
        }
    }

    private void Install()
    {
        ShowPrompt(SettingsPrompt.None);
        _support.InstallAsync(_markerChoice).Forget("SISR installieren");
    }

    private void ShowMarkerPrompt(IReadOnlyList<MarkerAccount> accounts)
    {
        foreach (var item in MarkerAccounts)
        {
            item.PropertyChanged -= OnMarkerAccountChanged;
        }

        MarkerAccounts.Clear();
        foreach (var account in accounts)
        {
            var item = new MarkerAccountViewModel(account);
            item.PropertyChanged += OnMarkerAccountChanged;
            MarkerAccounts.Add(item);
        }

        // The account that logged in last (listed first) is the likely one to play with.
        MarkerAccounts[0].IsChecked = true;
        CanConfirmMarkerAccounts = true;
        ShowPrompt(SettingsPrompt.ChooseMarkerAccounts);
    }

    private void OnMarkerAccountChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        CanConfirmMarkerAccounts = MarkerAccounts.Any(a => a.IsChecked);

    private void ConfirmMarkerAccounts()
    {
        if (!CanConfirmMarkerAccounts)
        {
            return;
        }

        _markerChoice = MarkerAccounts.Where(a => a.IsChecked && !a.HasMarker).Select(a => a.Id).ToList();
        ContinueInstall();
    }

    /// <summary>Switching on starts Steam first; if Steam doesn't come up, the setting is off again and a prompt says so.</summary>
    private void SwitchOn()
    {
        ShowPrompt(SettingsPrompt.None);
        SwitchOnAsync().Forget("Steam Controller (SISR) einschalten");
    }

    private async Task SwitchOnAsync()
    {
        if (!await _support.SetEnabledAsync(true) && !_disposed)
        {
            ShowSteamStartFailure();
        }
    }

    /// <summary>Shown where it happened, or when the settings open next if they were closed meanwhile.</summary>
    private void ShowSteamStartFailure()
    {
        _support.AcknowledgeSteamStartFailure();
        SteamLoginHangs = _support.InstallStopsSteam;
        ShowPrompt(SettingsPrompt.SteamStartFailed);
    }

    private void RetrySteamStart()
    {
        if (!SteamLoginHangs)
        {
            SwitchOn();
            return;
        }

        ShowPrompt(SettingsPrompt.None);
        RestartSteamAndSwitchOnAsync().Forget("Steam neu starten");
    }

    private async Task RestartSteamAndSwitchOnAsync()
    {
        await _support.ApplyFixAsync(HealthFix.RestartSteam);
        if (_support.LastError is null)
        {
            await SwitchOnAsync();
        }
    }

    /// <summary>The marker can only be written while Steam is closed, so it goes through the install prompts.</summary>
    private void ApplyFix(HealthFix fix)
    {
        if (IsBusy || IsSettling)
        {
            return;
        }

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
            ContinueInstall();
            return;
        }

        Install();
    }

    private void ClosePrompt() => ShowPrompt(SettingsPrompt.None);

    private void ShowPrompt(SettingsPrompt prompt)
    {
        var opens = prompt != SettingsPrompt.None && prompt != Prompt;
        Prompt = prompt;
        if (!IsInstallPrompt)
        {
            _promptCheck.Stop();
        }
        else
        {
            _promptCheck.Start();
        }

        // A prompt opening focuses its first button.
        if (opens)
        {
            _promptFocus = 0;
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
            case SettingsPrompt.SteamGameRunning when _markerChoice is null && _support.MarkerAccountsToChoose() is { Count: > 0 } accounts:
                ShowMarkerPrompt(accounts);
                break;
            case SettingsPrompt.SteamGameRunning:
                ShowPrompt(_support.InstallStopsSteam ? SettingsPrompt.ConfirmSteamStop : SettingsPrompt.None);
                break;
            case SettingsPrompt.ConfirmSteamStop or SettingsPrompt.ChooseMarkerAccounts when game is not null:
                RunningGame = game;
                ShowPrompt(SettingsPrompt.SteamGameRunning);
                break;
        }

        return Task.CompletedTask;
    }

    private void OnSupportChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        if (_disposed)
        {
            return;
        }

        // A start that failed while the page was closed and opened again shows here, not one opening late.
        if (_support.UnseenSteamStartFailure && Prompt == SettingsPrompt.None)
        {
            ShowSteamStartFailure();
        }
        else
        {
            Refresh();
        }
    });

    private void Refresh()
    {
        var enabled = _support.Enabled;
        var setup = _support.Setup;

        // Information (e.g. SISR paused for a game) is intended behaviour, not a problem.
        var hasProblems = _support.Issues.Any(i => i.Severity > HealthSeverity.Info);
        Marker = HealthIssues.WorstProblem(_support.Issues);

        // Once on, the switch stays even if something breaks later; the problem shows below it.
        var needsMarker = _support.Issues.Any(i => i.Fix == HealthFix.CreateMarker);
        var unavailable = !enabled && setup is ControllerSupportSetup.UnsupportedPlatform or ControllerSupportSetup.SteamMissing;
        var showInstall = !enabled && setup == ControllerSupportSetup.NeedsInstall;
        _sisr.Update(
            unavailable ? SisrRowMode.Unavailable : showInstall ? SisrRowMode.Install : SisrRowMode.Switch,
            enabled,
            IsSettling,
            setup == ControllerSupportSetup.SteamMissing ? "Benötigt Steam." : "Momentan nur unter Windows verfügbar.");
        if (IsInstallPrompt && ((!showInstall && !needsMarker) || IsBusy))
        {
            // Installed some other way, or something else started: the question is moot.
            Prompt = SettingsPrompt.None;
            _promptCheck.Stop();
        }

        var state = _support.State;
        var statusText = (enabled, state == SisrState.Paused, hasProblems) switch
        {
            (false, _, _) when showInstall => "Nicht eingerichtet – „SISR installieren“ richtet alles Nötige ein und schaltet die Option ein.",
            (false, _, _) => "Aus – außerhalb von Steam läuft der Steam Controller im Desktop-Modus (Maus und Tastatur).",
            (true, true, true) => "Pausiert – die Meldungen unter der Einstellung sagen, warum.",
            (true, true, _) => "Pausiert – ein Spiel mit „Steam Input“ läuft.",
            (true, false, true) => "Eingeschaltet, aber nicht voll funktionsfähig – die Meldungen unter der Einstellung sagen, was fehlt.",
            _ => state switch
            {
                SisrState.Starting => "SISR startet – der Steam Controller wird gleich als Xbox Controller eingerichtet …",
                SisrState.NoController => "Bereit, aber kein Steam Controller gefunden – schalte ihn ein.",
                SisrState.Running => "Aktiv – der Steam Controller wird als Xbox Controller erkannt.",
                SisrState.Stopping => "SISR wird beendet …",
                _ => "Wird gestartet …",
            },
        };
        var healthy = enabled && !hasProblems && _support.BusyText is null && state is not (SisrState.Starting or SisrState.NoController);
        var settlingNow = enabled && state is SisrState.Starting or SisrState.Stopping;
        _sisr.ShowRunning(enabled ? state == SisrState.Running && !hasProblems : null);
        var status = new InfoSection("STATUS",
        [
            new InfoStatus(statusText, healthy, settlingNow),
            .. _support.BusyText is { } busy ? [new InfoText(busy)] : Array.Empty<InfoBlock>(),
            .. _support.LastError is { } error ? [new InfoError(error)] : Array.Empty<InfoBlock>(),
        ]);
        var version = new InfoSection("SISR-VERSION",
        [
            new InfoText(_support.Sisr.InstalledVersion is { } installed ? $"SISR {installed}" : "SISR ist noch nicht installiert."),
            .. _support.AvailableUpdate is { } available ? [new InfoText($"Neue Version verfügbar: {available.Tag}")] : Array.Empty<InfoBlock>(),
            SisrCredit,
        ]);
        _sisr.Info = unavailable
            ? [.. SisrDescription, HowItWorks, Drawbacks]
            : [.. SisrDescription, status, HowItWorks, Drawbacks, version];

        var hasUpdate = !unavailable && _support.AvailableUpdate is not null;
        if (_support.AvailableUpdate is { } update)
        {
            _update.Subtitle = $"Neue Version verfügbar: {update.Tag}";
            _update.Info = [new InfoText($"SISR {update.Tag} ersetzt die installierte Version {_support.Sisr.InstalledVersion}."), SisrCredit];
        }

        _update.IsEnabled = !IsBusy && !IsSettling;

        SetRows(
        [
            _sisr,
            .. IssueRows(),
            .. hasUpdate ? [_update] : Array.Empty<SettingsRowViewModel>(),
            _steamDesktopLayout,
            .. _mouseMode.Rows,
            .. _returnWithView.Rows,
        ]);

        var buttons = PromptButtons();
        _promptFocus = Math.Clamp(_promptFocus, 0, Math.Max(0, buttons.Count - 1));
        foreach (var button in AllPromptButtons())
        {
            button.IsFocused = Prompt != SettingsPrompt.None && buttons.Count > 0 && button == buttons[_promptFocus];
        }
    }

    /// <summary>What "Steam Controller Kompatibilität" does, at the top of its info panel.</summary>
    private static readonly InfoBlock[] SisrDescription =
    [
        new InfoText("Mit dieser Einstellung wird der Steam Controller als Xbox Controller erkannt. Das ist nützlich, wenn du den Steam Controller auch in Spielen außerhalb von Steam (oder in Arcadia) benutzen möchtest, da er dort sonst im „Desktop-Modus“ läuft. Dies ist eine Besonderheit des Steam Controllers."),
        new InfoText("Dafür wird die Drittanbieter-Software SISR benötigt und bei Bedarf automatisch installiert. Arcadia startet SISR unsichtbar im Hintergrund."),
    ];

    private static readonly InfoSection HowItWorks = new("SO FUNKTIONIERT ES",
    [
        new InfoText("Ist diese Option aktiv (und ein Steam Controller verbunden), kann in Arcadia pro Spiel zwischen zwei Modi für den Steam Controller gewechselt werden:"),
        new InfoCard(ViewModels.ControllerModes.Sisr, "Arcadia lässt den Steam Controller in diesem Spiel als Xbox Controller erkennen.", "Vorauswahl für Spiele außerhalb von Steam"),
        new InfoCard(ViewModels.ControllerModes.Steam, "Das Standardverhalten des Steam Controllers. Empfohlen für Steam-Spiele oder wenn du außerhalb von Steam den Desktop-Modus nutzen möchtest; der Controller simuliert dann Maus und Tastatur.", "Vorauswahl für Spiele aus Steam"),
    ]);

    private static readonly InfoSection Drawbacks = new("NACHTEILE",
    [
        new InfoBullet("Steam muss im Hintergrund laufen."),
        new InfoBullet("Arcadia erkennt den Steam Controller nur, solange er als Xbox Controller läuft. In Spielen auf „Steam Input“ wirken mit ihm deshalb weder ⧉ + ≡ für den Mausmodus noch ⧉ halten für „Zurück zu Arcadia“."),
    ]);

    private static readonly InfoFootnote SisrCredit = new("SISR von Peter Repukat (Alia5) · GPL-3.0 · github.com/Alia5/SISR");

    /// <summary>One row per problem, with its fix as the row's action; a row stays the same while its problem lasts.</summary>
    private List<SettingsRowViewModel> IssueRows()
    {
        var rows = new List<SettingsRowViewModel>();
        var ids = new HashSet<string>();
        foreach (var issue in _support.Issues)
        {
            ids.Add(issue.Id);
            if (!_issueRows.TryGetValue(issue.Id, out var row))
            {
                var fix = issue.Fix;
                row = new ActionRowViewModel($"issue:{issue.Id}", issue.Title, () => ApplyFix(fix)) { Group = SteamGroup };
                _issueRows[issue.Id] = row;
            }

            row.Title = issue.Title;
            row.Subtitle = issue.ShortText;
            row.Severity = issue.Severity;
            row.ActionText = issue.Fix == HealthFix.None ? string.Empty : issue.FixLabel ?? "Beheben";
            row.IsEnabled = !IsBusy && !IsSettling;
            row.Info = [new InfoText(issue.Detail)];
            rows.Add(row);
        }

        foreach (var gone in _issueRows.Keys.Where(id => !ids.Contains(id)).ToList())
        {
            _issueRows.Remove(gone);
        }

        return rows;
    }

    /// <summary>While a prompt is open, only its buttons can be focused.</summary>
    private List<PromptButtonViewModel> PromptButtons() => Prompt switch
    {
        SettingsPrompt.SteamGameRunning => [_retryInstall, _closeGamePrompt],
        SettingsPrompt.ConfirmSteamStop => [_confirmSteamStop, _cancelSteamStop],
        SettingsPrompt.SteamStartFailed => [_retrySteamStart, _cancelSteamStart],
        SettingsPrompt.ChooseMarkerAccounts => [.. MarkerAccounts.Where(a => !a.HasMarker), _confirmMarkerAccounts, _cancelMarkerAccounts],
        _ => [],
    };

    private IEnumerable<PromptButtonViewModel> AllPromptButtons() =>
        new[] { _retryInstall, _closeGamePrompt, _confirmSteamStop, _cancelSteamStop, _retrySteamStart, _cancelSteamStart, _confirmMarkerAccounts, _cancelMarkerAccounts }
            .Concat(MarkerAccounts);

    private void MovePromptFocus(int delta)
    {
        var buttons = PromptButtons();
        if (buttons.Count == 0)
        {
            return;
        }

        buttons[_promptFocus].IsFocused = false;
        _promptFocus = Math.Clamp(_promptFocus + delta, 0, buttons.Count - 1);
        buttons[_promptFocus].IsFocused = true;
    }
}

/// <summary>What "Steam Controller Kompatibilität" offers on this PC.</summary>
public enum SisrRowMode
{
    /// <summary>Everything is in place (or the setting is already on): a switch.</summary>
    Switch,

    /// <summary>Something is missing: "SISR installieren" instead of the switch.</summary>
    Install,

    /// <summary>Not on this PC (no Windows, no Steam): greyed out, saying why.</summary>
    Unavailable,
}

/// <summary>"Steam Controller Kompatibilität": a switch, "SISR installieren", or why neither is possible.</summary>
public sealed partial class SisrRowViewModel(Action<bool?> activate)
    : SettingsRowViewModel("sisr", "Steam Controller Kompatibilität")
{
    private string _subtitle = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSwitch), nameof(IsInstall))]
    public partial SisrRowMode Mode { get; private set; }

    public bool IsSwitch => Mode == SisrRowMode.Switch;

    public bool IsInstall => Mode == SisrRowMode.Install;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValueText))]
    public partial bool IsOn { get; private set; }

    public string ValueText => IsOn ? "An" : "Aus";

    /// <summary>Steam, SISR or VIIPER are starting or stopping: the switch keeps its position but can't be flipped.</summary>
    [ObservableProperty]
    public partial bool IsLocked { get; private set; }

    public override bool CanChange => IsSwitch && !IsLocked;

    public override bool CanActivate => Mode != SisrRowMode.Unavailable && !IsLocked;

    public override string ActivateText => IsInstall ? "SISR installieren" : "Umschalten";

    /// <summary>◀ switches off, ▶ on (or installs first).</summary>
    public override void Change(int delta) => activate(delta > 0);

    public override void Activate() => activate(null);

    /// <summary>Whether SISR really runs: null while the option is off, the switch says so.</summary>
    public void ShowRunning(bool? isRunning) => Lamp = isRunning;

    public void Update(SisrRowMode mode, bool isOn, bool isLocked, string unavailableText)
    {
        if (_subtitle.Length == 0)
        {
            _subtitle = Subtitle ?? string.Empty;
        }

        Mode = mode;
        IsOn = isOn;
        IsLocked = isLocked;
        IsDimmed = mode == SisrRowMode.Unavailable;
        Subtitle = mode == SisrRowMode.Unavailable ? unavailableText : _subtitle;
        NotifyActionsChanged();
    }
}
