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

/// <summary>A question over the settings that has to be answered before an action that closes Steam can go on.</summary>
public enum SettingsPrompt
{
    None,

    /// <summary>A Steam game runs; the action would end it, because Steam has to close.</summary>
    SteamGameRunning,

    /// <summary>Steam runs; asking whether Arcadia may close it for the action.</summary>
    ConfirmSteamStop,

    /// <summary>"SISR entfernen" runs (#214): what it does right now.</summary>
    RemovingSisr,
}

/// <summary>
/// The category "Controller" (#15): the group "Pan" (#242) with the emulation's parts, the group "Steam" with Steam's
/// desktop layout (#169), the group "SISR" while the old SISR is there (#214), then the mouse mode and "Zurück zu
/// Arcadia", as rows of the page. A driver fix that restarts the puck (#296) and "SISR entfernen" with its Steam marker
/// close Steam, so they ask first in dialogs over the page.
/// </summary>
public sealed partial class ControllerSettingsViewModel : SettingsCategoryViewModel
{
    /// <summary>Everything Steam does to the controllers, in one group (owner, 2026-10-24).</summary>
    private const string SteamGroup = "STEAM";

    private const string SteamDesktopLayoutKey = "steam-desktop-layout";

    private static readonly TimeSpan PromptCheckInterval = TimeSpan.FromSeconds(1);

    private readonly ControllerSupportService _support;
    private readonly SettingsChanges _changes;
    private readonly Action<string> _notify;
    private readonly IPanEmulationView _pan;
    private readonly PanRows _panRows;
    private readonly SisrRows _sisrRows;
    private readonly MouseModeRows _mouseMode;
    private readonly ReturnWithViewRows _returnWithView;
    private readonly SwitchRowViewModel _steamDesktopLayout;

    private readonly PromptButtonViewModel _retryInstall;
    private readonly PromptButtonViewModel _closeGamePrompt;
    private readonly PromptButtonViewModel _confirmSteamStop;
    private readonly PromptButtonViewModel _cancelSteamStop;

    /// <summary>The driver fix the Steam prompts are open for (#296): it restarts the puck, so Steam closes for it.</summary>
    private HealthFix? _steamStopFix;

    /// <summary>The Steam prompts are open for "SISR entfernen", whose marker needs Steam closed (#214).</summary>
    private bool _forSisr;

    /// <summary>"SISR entfernen" closes Steam: its dialog says Steam comes back.</summary>
    private bool _sisrRemovalStopsSteam;

    private readonly DispatcherTimer _promptCheck;
    private bool _disposed;
    private int _promptFocus;

    /// <param name="pan">Pan's emulation (#242).</param>
    /// <param name="gameInTheWay">The title of a game that runs or starts: the switch "Controller-Wächter (Pan)" waits for its end.</param>
    /// <param name="notify">Shows a toast, e.g. "SISR ist vollständig entfernt."</param>
    /// <param name="time">Times the held row "Steam Controller und Puck übernehmen"; tests pass a fake.</param>
    public ControllerSettingsViewModel(ControllerSupportService support, SettingsChanges changes, IPanEmulationView pan, Func<string?> gameInTheWay,
        Action<string> notify, TimeProvider? time = null)
        : base("Controller", "M7 7h10a5 5 0 0 1 4.6 6.9l-1.3 3.2a2.3 2.3 0 0 1-3.9.6L14.6 16H9.4l-1.8 1.7a2.3 2.3 0 0 1-3.9-.6l-1.3-3.2A5 5 0 0 1 7 7z M8 10v3 M6.5 11.5h3 M16 10.5v.01 M17.5 12.5v.01")
    {
        _support = support;
        _changes = changes;
        _notify = notify;
        _pan = pan;
        _panRows = new PanRows(support, changes, pan, gameInTheWay, ApplyFix, time ?? TimeProvider.System, action => Dispatcher.UIThread.Post(action));
        _panRows.Switched += Refresh;
        _panRows.TakenOver += () =>
        {
            Refresh();
            FocusRow(PanRows.MainKey);
        };
        _sisrRows = new SisrRows(support, RemoveSisr, KeepSisr);
        _mouseMode = new MouseModeRows(changes);
        _returnWithView = new ReturnWithViewRows(changes);
        _steamDesktopLayout = new SwitchRowViewModel(
            SteamDesktopLayoutKey,
            "Steams Desktop-Belegung ignorieren",
            () => changes.Current.IgnoreSteamDesktopLayout,
            value => changes.Apply(s => s with { IgnoreSteamDesktopLayout = value }, "Steams Desktop-Belegung ignorieren"))
        {
            Group = SteamGroup,
            Subtitle = "Alle Controller: Maus und Tasten, die Steam simuliert, kommen nicht an",
            Info =
            [
                new InfoText("Läuft Steam, macht es außerhalb von Spielen aus manchen Controller-Eingaben selbst Tasten („Desktop-Belegung“): Hältst du ⧉ + ≡, löst Steam mit jedem Controller ein Alt+Tab aus."),
                new InfoText("Ist die Option an, verwirft Arcadia solche Tasten und Mausbewegungen von Steam, solange ein Controller benutzt wird. Echte Maus und Tastatur wirken weiter. Neben einem Spiel aus der Bibliothek verwirft Arcadia nur Tasten, und nur solange ⧉ + ≡ gehalten werden, damit das Spiel Steams Belegung behält."),
                new InfoText("Empfohlen, wenn du den Mausmodus benutzt: ⧉ + ≡ schaltet ihn um, und ohne diese Option wechselt Steam dabei zugleich mit Alt+Tab das Fenster."),
            ],
        };

        _retryInstall = new PromptButtonViewModel(ContinueSteamStop);
        _closeGamePrompt = new PromptButtonViewModel(ClosePrompt);
        _confirmSteamStop = new PromptButtonViewModel(ConfirmSteamStop);
        _cancelSteamStop = new PromptButtonViewModel(ClosePrompt);
        RetryInstallCommand = new RelayCommand(ContinueSteamStop);
        ConfirmSteamStopCommand = new RelayCommand(ConfirmSteamStop);
        ClosePromptCommand = new RelayCommand(ClosePrompt);

        // The game can end (or start) while a prompt is open; nothing else would tell us.
        _promptCheck = new DispatcherTimer(PromptCheckInterval, DispatcherPriority.Background,
            async (_, _) => await AppErrors.Guard(CheckPromptAsync, "Laufendes Steam-Spiel prüfen"));

        // This DispatcherTimer constructor starts the timer; it only runs while a Steam prompt is open.
        _promptCheck.Stop();

        // An old failure describes a moment that is over; the page opens without it.
        _support.ClearError();
        _support.Changed += OnSupportChanged;
        _support.SisrRemoved += OnSisrRemoved;
        _pan.Changed += OnPanChanged;
        _pan.Takeover.Changed += OnPanChanged;
        Refresh();

        // Only while the settings are open does the tick recheck the parts, HidHide's state and SISR.
        _support.WatchSetup = true;
        Task.Run(() => _support.RefreshAsync()).Forget("Einstellungen aktualisieren");
    }

    /// <summary>Steam's page points here too: Steam decides how the controllers behave outside its games (#169).</summary>
    public override IReadOnlyList<SettingsLink> LauncherPageLinks => PanLinks;

    private static readonly IReadOnlyList<SettingsLink> PanLinks =
    [
        new(KnownLaunchers.Steam.Id, "Controller-Einstellungen für Steam", "Controller-Wächter (Pan) und Steams Desktop-Belegung", PanRows.MainKey,
        [
            new InfoText("Was Steam mit deinen Controllern macht, stellst du unter Controller ein: ob Pan den Steam Controller vor Steam versteckt und Steam eine Kopie zeigt, damit er in jedem Spiel funktioniert, und ob Arcadia Steams Desktop-Belegung für alle Controller ignoriert."),
            new InfoText("[A] springt dorthin, [B] kommt hierher zurück."),
        ]),
    ];

    public PromptButtonViewModel RetryInstallItem => _retryInstall;

    public PromptButtonViewModel CloseGamePromptItem => _closeGamePrompt;

    public PromptButtonViewModel ConfirmSteamStopItem => _confirmSteamStop;

    public PromptButtonViewModel CancelSteamStopItem => _cancelSteamStop;

    public IRelayCommand RetryInstallCommand { get; }

    public IRelayCommand ConfirmSteamStopCommand { get; }

    /// <summary>"Schließen" / "Abbrechen", B, or a click beside the prompt.</summary>
    public IRelayCommand ClosePromptCommand { get; }

    /// <summary>The dialog over the page, if any.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGamePrompt), nameof(IsSteamStopPrompt), nameof(IsRemovingSisrPrompt), nameof(IsQuestionPrompt), nameof(IsWarningPrompt),
        nameof(PromptBackText), nameof(SteamStopText), nameof(SteamStopDetail), nameof(GamePromptText), nameof(CanRetryGamePrompt), nameof(ConfirmSteamStopText), nameof(RemovingSisrText))]
    public partial SettingsPrompt Prompt { get; private set; }

    partial void OnPromptChanged(SettingsPrompt value) => Dialog = value == SettingsPrompt.None ? null : this;

    public bool IsGamePrompt => Prompt == SettingsPrompt.SteamGameRunning;

    public bool IsSteamStopPrompt => Prompt == SettingsPrompt.ConfirmSteamStop;

    public bool IsRemovingSisrPrompt => Prompt == SettingsPrompt.RemovingSisr;

    /// <summary>The prompt asks a question, or reports what runs ("?").</summary>
    public bool IsQuestionPrompt => Prompt is SettingsPrompt.ConfirmSteamStop or SettingsPrompt.RemovingSisr;

    /// <summary>The prompt reports a problem ("!") rather than asking ("?").</summary>
    public bool IsWarningPrompt => Prompt is SettingsPrompt.SteamGameRunning;

    /// <summary>The prompts that wait for Steam: they end when the action becomes moot.</summary>
    private bool IsSteamPrompt => Prompt is SettingsPrompt.SteamGameRunning or SettingsPrompt.ConfirmSteamStop;

    public string PromptBackText => Prompt is SettingsPrompt.ConfirmSteamStop ? "Abbrechen" : "Schließen";

    /// <summary>Why Steam has to close.</summary>
    public string SteamStopText => _forSisr
        ? "Den Eintrag „SISR Marker“ kann Arcadia nur entfernen, solange Steam geschlossen ist. Steam wird dafür kurz beendet und danach wieder gestartet."
        : "Für die Installation muss Steam beendet werden. Arcadia startet Steam danach wieder im Hintergrund.";

    /// <summary>For a driver fix, why it restarts the puck; nothing more for "SISR entfernen".</summary>
    public string SteamStopDetail => _steamStopFix switch
    {
        HealthFix.ReinstallHidHide => "Die Neuinstallation von HidHide startet den Puck des Steam Controllers neu. Steam darf ihn in dem Moment nicht aktualisieren. Windows fragt dabei einmal nach Administratorrechten.",
        HealthFix.InstallDrivers => "Die Treiber-Installation startet den Puck des Steam Controllers neu. Steam darf ihn in dem Moment nicht aktualisieren. Windows fragt dabei einmal nach Administratorrechten.",
        _ => string.Empty,
    };

    public bool HasSteamStopDetail => SteamStopDetail.Length > 0;

    public string ConfirmSteamStopText => _forSisr ? "Weiter" : "Steam beenden und installieren";

    /// <summary>What the game prompt asks for, below the game's name.</summary>
    public string GamePromptText => _forSisr
        ? "Beende das Spiel zuerst, dann kann Arcadia SISR entfernen."
        : "Für die Installation muss Steam beendet und neu gestartet werden. Beende zuerst das Spiel und versuche es dann erneut.";

    /// <summary>The design for "SISR entfernen" has only "Schließen": the prompt goes on by itself when the game ends.</summary>
    public bool CanRetryGamePrompt => !_forSisr;

    public string RemovingSisrText => _sisrRemovalStopsSteam
        ? "Das dauert nur einen Moment. Steam startet danach von selbst wieder."
        : "Das dauert nur einen Moment.";

    /// <summary>The step "SISR entfernen" is at, e.g. "Steam wird beendet …".</summary>
    public string RemovingSisrStep => _support.BusyText ?? ControllerSupportService.SisrRemovingText;

    /// <summary>The Steam game named in the game prompt.</summary>
    [ObservableProperty]
    public partial string RunningGame { get; private set; } = string.Empty;

    private bool IsBusy => _support.BusyText is not null;

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
            case UiAction.Accept when !IsBusy && PromptButtons() is { Count: > 0 } buttons:
                buttons[_promptFocus].Activate();
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
        _support.SisrRemoved -= OnSisrRemoved;
        _pan.Changed -= OnPanChanged;
        _pan.Takeover.Changed -= OnPanChanged;
        _panRows.TakeOver.Dispose();
        _support.ClearError();
    }

    /// <summary>The first row below "Controller-Wächter (Pan)" that says why it doesn't run; [A] on a greyed mode leads there (#242).</summary>
    public string? PanWarningKey => _panRows.FirstWarningKey;

    private void OnPanChanged() => Dispatcher.UIThread.Post(() =>
    {
        if (!_disposed)
        {
            Refresh();
        }
    });

    /// <summary>[Entfernen]: its Steam marker needs Steam closed, so a running Steam is asked about (or a game named) first.</summary>
    private void RemoveSisr()
    {
        if (IsBusy)
        {
            return;
        }

        _steamStopFix = null;
        _forSisr = true;
        ContinueSteamStop();
    }

    /// <summary>[Ausblenden]: the group goes, the focus moves to the row above it.</summary>
    private void KeepSisr()
    {
        if (IsBusy)
        {
            return;
        }

        _changes.Apply(settings => settings with { KeepSisr = true }, "SISR behalten");
        AppLog.Write("SISR behalten: Bereich ausgeblendet");
        Refresh();
        FocusRow(SteamDesktopLayoutKey);
    }

    private void OnSisrRemoved(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        if (_disposed)
        {
            return;
        }

        _notify("SISR ist vollständig entfernt.");
        Refresh();
        FocusRow(SteamDesktopLayoutKey);
    });

    /// <summary>Whether the action the prompts are open for closes Steam right now.</summary>
    private bool StopsSteam => _forSisr ? _support.SisrRemovalStopsSteam : _steamStopFix is { } fix && _support.StopsSteamFor(fix);

    /// <summary>
    /// The action closes Steam, so it can't run beside a Steam game; with Steam running it asks first, without it just
    /// runs. Also "Erneut versuchen".
    /// </summary>
    private void ContinueSteamStop()
    {
        if (!StopsSteam)
        {
            // Steam was closed meanwhile (or never ran): nothing to ask, no game in the way.
            Run();
        }
        else if (_support.SteamGameBlockingInstall() is { } game)
        {
            RunningGame = game;
            ShowPrompt(SettingsPrompt.SteamGameRunning);
        }
        else
        {
            ShowPrompt(SettingsPrompt.ConfirmSteamStop);
        }
    }

    private void Run()
    {
        if (_forSisr)
        {
            _sisrRemovalStopsSteam = _support.SisrRemovalStopsSteam;
            ShowPrompt(SettingsPrompt.RemovingSisr);
            _support.RemoveSisrAsync().Forget("SISR entfernen");
            return;
        }

        ShowPrompt(SettingsPrompt.None);
        if (_steamStopFix is { } fix)
        {
            _support.ApplyFixAsync(fix).Forget("Problem beheben");
        }
    }

    /// <summary>A driver fix that restarts the puck while Steam runs (#296) asks "Steam beenden?" (or names a running Steam game).</summary>
    private void ApplyFix(HealthFix fix)
    {
        if (IsBusy)
        {
            return;
        }

        if (_support.StopsSteamFor(fix))
        {
            _steamStopFix = fix;
            _forSisr = false;
            ContinueSteamStop();
            return;
        }

        _support.ApplyFixAsync(fix).Forget("Problem beheben");
    }

    private void ConfirmSteamStop()
    {
        // A game may have started since the question came up.
        if (_support.SteamGameBlockingInstall() is not null)
        {
            ContinueSteamStop();
            return;
        }

        Run();
    }

    /// <summary>The removal can't be called off halfway, so its dialog stays until it is done.</summary>
    private void ClosePrompt()
    {
        if (Prompt != SettingsPrompt.RemovingSisr)
        {
            ShowPrompt(SettingsPrompt.None);
        }
    }

    private void ShowPrompt(SettingsPrompt prompt)
    {
        var opens = prompt != SettingsPrompt.None && prompt != Prompt;
        Prompt = prompt;
        OnPropertyChanged(nameof(HasSteamStopDetail));
        if (IsSteamPrompt)
        {
            _promptCheck.Start();
        }
        else
        {
            _promptCheck.Stop();
        }

        // A prompt opening focuses its first button.
        if (opens)
        {
            _promptFocus = 0;
        }

        Refresh();
    }

    /// <summary>
    /// Keeps an open prompt true: when the game ends it goes on with "Steam beenden?" (or closes if Steam is gone too);
    /// when a game starts during "Steam beenden?" it says so.
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
                ShowPrompt(StopsSteam ? SettingsPrompt.ConfirmSteamStop : SettingsPrompt.None);
                break;
            case SettingsPrompt.ConfirmSteamStop when game is not null:
                RunningGame = game;
                ShowPrompt(SettingsPrompt.SteamGameRunning);
                break;
        }

        return Task.CompletedTask;
    }

    private void OnSupportChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        if (!_disposed)
        {
            Refresh();
        }
    });

    private void Refresh()
    {
        var rows = _panRows.Build();
        Marker = _panRows.Marker;
        if (IsSteamPrompt && IsBusy)
        {
            // Something else started: the question is moot.
            Prompt = SettingsPrompt.None;
            _promptCheck.Stop();
        }
        else if (Prompt == SettingsPrompt.RemovingSisr && !IsBusy && (_support.SisrRemovalCause is not null || _support.LastError is not null || !_support.ShowsSisrGroup))
        {
            // Done: the toast says so, or the group row says what is left and why.
            Prompt = SettingsPrompt.None;
        }

        OnPropertyChanged(nameof(RemovingSisrStep));
        SetRows(
        [
            .. rows,
            _steamDesktopLayout,
            .. _sisrRows.Build(),
            .. _mouseMode.Rows,
            .. _returnWithView.Rows,
        ]);

        FocusPromptButton();
    }

    private void FocusPromptButton()
    {
        var buttons = PromptButtons();
        _promptFocus = Math.Clamp(_promptFocus, 0, Math.Max(0, buttons.Count - 1));
        foreach (var button in AllPromptButtons())
        {
            button.IsFocused = Prompt != SettingsPrompt.None && buttons.Count > 0 && button == buttons[_promptFocus];
        }
    }

    /// <summary>While a prompt is open, only its buttons can be focused.</summary>
    private List<PromptButtonViewModel> PromptButtons() => Prompt switch
    {
        SettingsPrompt.SteamGameRunning when CanRetryGamePrompt => [_retryInstall, _closeGamePrompt],
        SettingsPrompt.SteamGameRunning => [_closeGamePrompt],
        SettingsPrompt.ConfirmSteamStop => [_confirmSteamStop, _cancelSteamStop],
        _ => [],
    };

    private IEnumerable<PromptButtonViewModel> AllPromptButtons() => [_retryInstall, _closeGamePrompt, _confirmSteamStop, _cancelSteamStop];

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
