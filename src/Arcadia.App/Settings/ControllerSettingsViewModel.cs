using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Pan;
using Arcadia.ControllerSupport.Pan;
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
    private static readonly TimeSpan PromptCheckInterval = TimeSpan.FromSeconds(1);

    private readonly ControllerSupportService _support;
    private readonly SettingsChanges _changes;
    private readonly Action<string> _notify;
    private readonly IPanEmulationView _pan;
    private readonly PanRows _panRows;
    private readonly SisrRows _sisrRows;
    private readonly MouseModeRows _mouseMode;
    private readonly ReturnWithViewRows _returnWithView;
    /// <summary>Steam's controller settings are on its launcher page (#178, #315); the last group leads there.</summary>
    public const string SteamLauncherKey = "steam-launcher-settings";

    private const string SteamGroup = "STEAM";

    private readonly Func<bool> _steamInstalled;
    private readonly ActionRowViewModel _steamLauncherRow;

    // The row above SISR's group, as of the last Refresh: the focus goes there when the group goes.
    private string? _rowAboveSisr;

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
    /// <param name="steam">Steam is installed, and opening its launcher page: the group "Steam" at the end leads there.</param>
    public ControllerSettingsViewModel(ControllerSupportService support, SettingsChanges changes, IPanEmulationView pan, Func<string?> gameInTheWay,
        Action<string> notify, (Func<bool> IsInstalled, Action OpenPage)? steam = null)
        : base("Controller", "M7 7h10a5 5 0 0 1 4.6 6.9l-1.3 3.2a2.3 2.3 0 0 1-3.9.6L14.6 16H9.4l-1.8 1.7a2.3 2.3 0 0 1-3.9-.6l-1.3-3.2A5 5 0 0 1 7 7z M8 10v3 M6.5 11.5h3 M16 10.5v.01 M17.5 12.5v.01")
    {
        _support = support;
        _changes = changes;
        _notify = notify;
        _pan = pan;
        _panRows = new PanRows(support, changes, pan, gameInTheWay, ApplyFix, notify);
        _panRows.Switched += Refresh;
        _panRows.LayoutsRequested += OpenLayouts;
        _sisrRows = new SisrRows(support, RemoveSisr, KeepSisr);
        _mouseMode = new MouseModeRows(changes);
        _returnWithView = new ReturnWithViewRows(changes);
        _steamInstalled = steam?.IsInstalled ?? (() => false);
        _steamLauncherRow = new ActionRowViewModel(SteamLauncherKey, "Controller-Einstellungen für Steam", steam?.OpenPage ?? (() => { }))
        {
            Group = SteamGroup,
            Subtitle = "Weitere Controller-Einstellungen für Steam.",
            ActionText = "Steam-Einstellungen",
            Info =
            [
                new InfoText("Arcadia hat erkannt, dass du Steam installiert hast. In den Launcher-Einstellungen für Steam findest du noch weitere Einstellungsmöglichkeiten für Controller in Zusammenhang mit Steam. Unter anderem zum Steam Controller und ob die Steam Desktop-Steuerung wieder zugelassen werden soll."),
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
        Refresh();

        // Only while the settings are open does the tick recheck the parts, HidHide's state and SISR.
        _support.WatchSetup = true;
        Task.Run(() => _support.RefreshAsync()).Forget("Einstellungen aktualisieren");
    }

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
        nameof(PromptBackText), nameof(SteamStopTitle), nameof(SteamStopText), nameof(SteamStopDetail), nameof(GamePromptText), nameof(CanRetryGamePrompt), nameof(ConfirmSteamStopText), nameof(RemovingSisrText))]
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

    /// <summary>The fix the prompt is open for runs HidHide's installer: Arcadia restarts after it (#396).</summary>
    private bool InstallsHidHide => !_forSisr && _steamStopFix is { } fix && _support.InstallsHidHide(fix);

    public const string HidHideInstallTitle = "HidHide installieren?";
    public const string HidHideRestartText = "Nach der Installation von HidHide startet Arcadia einmal neu. Erst dann sieht Arcadia die Controller, die Pan vor anderen Programmen versteckt.";
    private const string SteamClosesText = "Für die Installation muss Steam beendet werden. Arcadia startet Steam danach wieder im Hintergrund.";

    /// <summary>"HidHide installieren?" before HidHide's installer runs, with Steam running or not; else "Steam beenden?".</summary>
    public string SteamStopTitle => InstallsHidHide ? HidHideInstallTitle : "Steam beenden?";

    /// <summary>Why Steam has to close, or for HidHide's installer that Arcadia restarts afterwards.</summary>
    public string SteamStopText => _forSisr
        ? "Den Eintrag „SISR Marker“ kann Arcadia nur entfernen, solange Steam geschlossen ist. Steam wird dafür kurz beendet und danach wieder gestartet."
        : InstallsHidHide ? HidHideRestartText : SteamClosesText;

    /// <summary>For a driver fix, why it restarts the puck (and, for HidHide's installer, that Steam closes); nothing more for "SISR entfernen".</summary>
    public string SteamStopDetail => (InstallsHidHide && StopsSteam ? SteamClosesText + " " : string.Empty) + _steamStopFix switch
    {
        HealthFix.ReinstallHidHide => "Die Neuinstallation von HidHide startet den Puck des Steam Controllers neu. Steam darf ihn in dem Moment nicht aktualisieren. Windows fragt dabei einmal nach Administratorrechten.",
        HealthFix.InstallDrivers => "Die Treiber-Installation startet den Puck des Steam Controllers neu. Steam darf ihn in dem Moment nicht aktualisieren. Windows fragt dabei einmal nach Administratorrechten.",
        _ => string.Empty,
    };

    public bool HasSteamStopDetail => SteamStopDetail.Length > 0;

    public string ConfirmSteamStopText => _forSisr ? "Weiter" : StopsSteam ? "Steam beenden und installieren" : "Installieren";

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

    /// <summary>"Controller zuordnen" while it is open (#238 D3a); null otherwise.</summary>
    public ControllerLayoutsViewModel? Layouts { get; private set; }

    public override void HandleDialog(UiAction action)
    {
        if (Layouts is { } layouts)
        {
            layouts.Handle(action);
            return;
        }

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
        _panRows.LayoutsRequested -= OpenLayouts;
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

    private void OpenLayouts()
    {
        if (IsBusy || Prompt != SettingsPrompt.None)
        {
            return;
        }

        Layouts = new ControllerLayoutsViewModel(
            () => _pan.ConnectedModels,
            () => _pan.SteamControllerConnected,
            model => ControllerLayouts.Find(_changes.Current.ControllerLayouts, model),
            ApplyLayouts,
            CloseLayouts);
        Dialog = Layouts;
        AppLog.Write("Controller zuordnen: geöffnet");
    }

    /// <summary>B, ✕ or a click beside it; also when the row goes (Pan off, no controller left). The focus stays on the row.</summary>
    private void CloseLayouts()
    {
        if (Layouts is null)
        {
            return;
        }

        Layouts = null;
        Dialog = Prompt == SettingsPrompt.None ? null : this;
    }

    /// <summary>
    /// A in the modal: all changes in one save, so the emulation's next tick (every 2 s) applies them together; the row's
    /// info panel shows them at once.
    /// </summary>
    private void ApplyLayouts(IReadOnlyList<(ControllerModel Model, ControllerLayout? Layout)> changes)
    {
        _changes.Apply(settings => changes.Aggregate(settings, (current, change) =>
            current.WithControllerLayout(ControllerLayouts.Key(change.Model), change.Layout is { } chosen ? ControllerLayouts.Value(chosen) : null)), "Controller zuordnen");
        foreach (var (model, layout) in changes)
        {
            AppLog.Write($"Controller zuordnen: {model} → {(layout is { } shown ? ControllerLayouts.DisplayName(shown) : "Vorgabe")}");
        }

        Refresh();
    }

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
        FocusRowAboveSisr();
    }

    private void OnSisrRemoved(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        if (_disposed)
        {
            return;
        }

        _notify("SISR ist vollständig entfernt.");
        Refresh();
        FocusRowAboveSisr();
    });

    private void FocusRowAboveSisr()
    {
        if (_rowAboveSisr is { } key)
        {
            FocusRow(key);
        }
    }

    /// <summary>Whether the action the prompts are open for closes Steam right now.</summary>
    private bool StopsSteam => _forSisr ? _support.SisrRemovalStopsSteam : _steamStopFix is { } fix && _support.StopsSteamFor(fix);

    /// <summary>The action asks first: it closes Steam, or runs HidHide's installer, after which Arcadia restarts (#396).</summary>
    private bool AsksFirst => StopsSteam || InstallsHidHide;

    /// <summary>
    /// The action closes Steam, so it can't run beside a Steam game; with Steam running it asks first, without it just
    /// runs, unless HidHide's installer runs (it says that Arcadia restarts). Also "Erneut versuchen".
    /// </summary>
    private void ContinueSteamStop()
    {
        if (!AsksFirst)
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

    /// <summary>
    /// A driver fix that restarts the puck while Steam runs (#296) asks "Steam beenden?" (or names a running Steam game);
    /// one that runs HidHide's installer asks "HidHide installieren?", as Arcadia restarts after it (#396).
    /// </summary>
    private void ApplyFix(HealthFix fix)
    {
        if (IsBusy)
        {
            return;
        }

        if (_support.StopsSteamFor(fix) || _support.InstallsHidHide(fix))
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
                ShowPrompt(AsksFirst ? SettingsPrompt.ConfirmSteamStop : SettingsPrompt.None);
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
        _rowAboveSisr = rows.LastOrDefault()?.Key;
        if (Layouts is { } layouts)
        {
            if (rows.Contains(_panRows.Layouts))
            {
                layouts.Refresh();
            }
            else
            {
                CloseLayouts();
            }
        }

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
            .. _sisrRows.Build(),
            .. _mouseMode.Rows,
            .. _returnWithView.Rows,
            .. _steamInstalled() ? [_steamLauncherRow] : Array.Empty<SettingsRowViewModel>(),
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
