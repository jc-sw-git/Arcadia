using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.ControllerSupport.Copy;
using Arcadia.ControllerSupport.Pan;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>
/// "Steam will die Firmware aktualisieren" (#242, owner's design 2026-10-28): Steam tries an update through the copy,
/// which never reaches the real puck. Releasing the puck while Steam still sends could hand it the rest of the update, so
/// "Controller freigeben" and "Arcadia beenden" (held 1 s) wait until Steam gave up (<see cref="FirmwareAttemptWatch"/>).
/// "Steam hat das Update abgebrochen" (held 3 s) is the player's own fallback, so a misread message never traps them.
/// "Schließen" (no hold) then closes it and keeps them taken over. Can't be closed before; [B] does nothing. After the
/// release it says how the update goes on, and closes with [A] or [B].
/// </summary>
public sealed partial class FirmwareAttemptViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromMilliseconds(250);

    private readonly FirmwareAttemptWatch _watch;
    private readonly Action<Action> _post;
    private readonly ITimer _refresh;
    private readonly HoldButtonViewModel[] _order;
    private bool _skipped;

    // Ready as last refreshed: the focus moves to "Controller freigeben" once, when it becomes ready.
    private bool _wasReady;

    public FirmwareAttemptViewModel(
        FirmwareAttemptWatch watch,
        SteamControllerParts parts,
        Func<Task> release,
        Action quit,
        TimeProvider time,
        Action<Action> post)
    {
        _watch = watch;
        _post = post;
        Parts = parts;
        ReleaseButton = new HoldButtonViewModel("Controller freigeben", TimeSpan.FromSeconds(1), () => ReleaseAsync(release).Forget("Controller für das Firmware-Update freigeben"), time, post);
        QuitButton = new HoldButtonViewModel("Arcadia beenden", TimeSpan.FromSeconds(1), () =>
        {
            _watch.End(_skipped ? "vom Spieler übersprungen, Arcadia beendet" : "Steam hat aufgegeben, Arcadia beendet");
            quit();
        }, time, post);
        CloseButton = new HoldButtonViewModel("Schließen", TimeSpan.Zero, () =>
        {
            _watch.End(_skipped ? "vom Spieler übersprungen, geschlossen, Controller bleiben übernommen" : "Steam hat aufgegeben, geschlossen, Controller bleiben übernommen");
            Closed?.Invoke();
        }, time, post);
        SkipButton = new HoldButtonViewModel("Steam hat das Update abgebrochen", TimeSpan.FromSeconds(3), Skip, time, post) { IsFocused = true };
        _order = [ReleaseButton, QuitButton, CloseButton, SkipButton];
        Refresh();
        _refresh = time.CreateTimer(_ => post(Refresh), null, RefreshEvery, RefreshEvery);
    }

    public SteamControllerParts Parts { get; }

    public string Title => "Steam will die Firmware aktualisieren";

    public string Message => "Arcadia hat festgestellt, dass Steam ein Firmware-Update deines Steam Controllers oder Pucks gestartet hat. Solange Arcadia Controller und Puck verwaltet, sind keine Firmware-Updates möglich. Das laufende Update muss in Steam erst abbrechen oder fehlschlagen, bevor Arcadia Controller und Puck sicher freigeben kann. Bis dahin kann es sie nicht beschädigen.";

    /// <summary>Above "Steam hat das Update abgebrochen": the player confirms it on their own responsibility.</summary>
    public string SkipWarning => "Vergewissere dich, dass Steam das Firmware-Update wirklich abgebrochen hat, bevor du den Abbruch selbst bestätigst! Am besten beendest du dafür Steam.";

    /// <summary>"Schließen": the dialog goes, the controllers stay taken over.</summary>
    public event Action? Closed;

    public HoldButtonViewModel ReleaseButton { get; }

    public HoldButtonViewModel QuitButton { get; }

    public HoldButtonViewModel CloseButton { get; }

    public HoldButtonViewModel SkipButton { get; }

    [ObservableProperty]
    public partial FirmwareAttemptState State { get; private set; }

    /// <summary>Released: the dialog says how the update goes on.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWaiting))]
    public partial bool IsReleased { get; private set; }

    [ObservableProperty]
    public partial int SecondsLeft { get; private set; }

    /// <summary>How much of the countdown has passed, 0 to 1, for the bar.</summary>
    [ObservableProperty]
    public partial double CountdownPassed { get; private set; }

    [ObservableProperty]
    public partial string FocusText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Legend { get; private set; } = string.Empty;

    public bool IsTrying => !IsReady && State == FirmwareAttemptState.Trying;

    public bool IsCountingDown => !IsReady && State == FirmwareAttemptState.CountingDown;

    /// <summary>Steam gave up, or the player said it did: release and quit act now.</summary>
    public bool IsReady => _skipped || State == FirmwareAttemptState.GaveUp;

    public bool IsWaiting => !IsReleased;

    public string StatusTitle => _skipped ? "Du hast bestätigt, dass Steam abgebrochen hat"
        : State switch
        {
            FirmwareAttemptState.CountingDown => "Es gab seit einigen Sekunden keinen Versuch mehr von Steam",
            FirmwareAttemptState.GaveUp => "Steam hat aufgegeben",
            _ => "Steam versucht es noch",
        };

    public string StatusText => _skipped
        ? "Arcadia gibt die Wahl frei, ohne weiter zu warten. Gib die Controller nur frei, wenn Steam wirklich nicht mehr versucht, das Update zu starten."
        : State switch
        {
            FirmwareAttemptState.CountingDown => "Kommt bis dahin kein neuer Versuch, gilt das Update als abgebrochen. Ein neuer Versuch startet die Wartezeit neu.",
            FirmwareAttemptState.GaveUp => "30 Sekunden lang kam kein Update-Versuch mehr. Du kannst die Controller jetzt freigeben, Arcadia beenden oder einfach weiterspielen.",
            _ => "Arcadia wartet, bis 30 Sekunden lang kein Update-Versuch mehr kommt. Erst dann ist es sicher, die Controller freizugeben.",
        };

    public string ReleasedTitle => $"{SteamControllerPartsNames.Names(Parts)} freigegeben";

    public string ReleasedText => "Steam sieht sie jetzt direkt. Starte das Firmware-Update in Steam erneut und warte, bis es fertig ist.";

    public string ReleasedNext => $"Damit Arcadia sie wieder übernimmt: Einstellungen › Controller › Pan › „{SteamControllerPartsNames.Names(Parts)} übernehmen“. Oder starte Arcadia neu.";

    /// <summary>True while the dialog stays; false once the release note is closed.</summary>
    public bool Handle(UiAction action)
    {
        if (IsReleased)
        {
            return action is not (UiAction.Accept or UiAction.Back);
        }

        switch (action)
        {
            case UiAction.Left or UiAction.Up:
                Move(-1);
                break;
            case UiAction.Right or UiAction.Down:
                Move(1);
                break;
            case UiAction.Accept:
                Focused.Press();
                break;
        }

        return true;
    }

    public void Release(UiAction action)
    {
        if (action == UiAction.Accept)
        {
            foreach (var button in _order)
            {
                button.Release();
            }
        }
    }

    public void Press(HoldButtonViewModel button)
    {
        if (button.IsEnabled)
        {
            Focus(button);
            button.Press();
        }
    }

    public void CancelHold()
    {
        foreach (var button in _order)
        {
            button.Cancel();
        }
    }

    private HoldButtonViewModel Focused => _order.First(button => button.IsFocused);

    private IEnumerable<HoldButtonViewModel> Shown => IsReady ? [ReleaseButton, QuitButton, CloseButton] : _order;

    private void Move(int step)
    {
        var shown = Shown.ToList();
        var at = shown.IndexOf(Focused);
        Focus(shown[Math.Clamp(at + step, 0, shown.Count - 1)]);
    }

    private void Focus(HoldButtonViewModel button)
    {
        if (!button.IsFocused)
        {
            CancelHold();
        }

        foreach (var other in _order)
        {
            other.IsFocused = other == button;
        }

        UpdateTexts();
    }

    private void Skip()
    {
        _skipped = true;
        _watch.PlayerSaysAborted();
        Refresh();
    }

    private async Task ReleaseAsync(Func<Task> release)
    {
        _watch.End(_skipped ? "vom Spieler übersprungen, Controller freigegeben" : "Steam hat aufgegeben, Controller freigegeben");
        await release();
        _post(() => IsReleased = true);
    }

    private void Refresh()
    {
        var (state, left) = _watch.Now();
        State = state == FirmwareAttemptState.None ? FirmwareAttemptState.GaveUp : state;
        SecondsLeft = (int)Math.Ceiling(left.TotalSeconds);
        CountdownPassed = State == FirmwareAttemptState.CountingDown ? 1 - (left / FirmwareAttemptWatch.Countdown) : 0;
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(IsTrying));
        OnPropertyChanged(nameof(IsCountingDown));
        OnPropertyChanged(nameof(StatusTitle));
        OnPropertyChanged(nameof(StatusText));
        ReleaseButton.IsEnabled = QuitButton.IsEnabled = CloseButton.IsEnabled = IsReady;
        SkipButton.IsShown = !IsReady;
        if (IsReady && !_wasReady)
        {
            Focus(CloseButton);
        }

        _wasReady = IsReady;

        UpdateTexts();
    }

    private void UpdateTexts()
    {
        var focused = Focused;
        FocusText = focused == ReleaseButton ? $"Controller freigeben: Steam sieht {SteamControllerPartsNames.Names(Parts)} wieder direkt, Arcadia läuft weiter."
            : focused == QuitButton ? "Arcadia beenden: Arcadia gibt alle Controller frei und beendet sich."
            : focused == CloseButton ? $"Schließen: Arcadia behält {SteamControllerPartsNames.Names(Parts)}. Das Firmware-Update ist dann erst möglich, wenn du Arcadia beendest."
            : "Erst wenn 30 Sekunden lang kein Versuch mehr kommt, lassen sich die Controller freigeben.";
        Legend = focused == SkipButton ? "Halten (3 s): Abgebrochen"
            : !IsReady ? "Noch gesperrt"
            : focused == CloseButton ? "Schließen"
            : "Halten (1 s): Auswählen";
    }

    public void Dispose()
    {
        _refresh.Dispose();
        foreach (var button in _order)
        {
            button.Dispose();
        }
    }
}
