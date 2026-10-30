using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

public enum PanUpdateState
{
    /// <summary>"Neue Pan-Version": [A] updates, [B] goes on without Pan; nothing runs yet (#326).</summary>
    Notice,

    /// <summary>[A] was pressed; the update begins once Pan's own dialogs (a firmware hold) are done.</summary>
    Waiting,
    Updating,

    /// <summary>Not updated, Pan is off: Arcadia waits until the old Pan, left alone, has ended itself.</summary>
    Stopping,

    /// <summary>The old Pan still runs: [A] restores the controllers, [B] ends Arcadia.</summary>
    Stuck,
    Restoring,

    /// <summary>The restore didn't help: steps for HidHide by hand, [A] checks again, [B] ends Arcadia.</summary>
    ManualCleanup,
}

/// <summary>
/// The start page while the installed Pan isn't the one this Arcadia brings and Pan is on (#297, #326, artboards
/// T326F01–F09): it says the update comes and that Windows asks, and runs it only on [A]. Declined or failed, Pan goes
/// off and the page waits until the old Pan has given the controllers back and ended, since Arcadia isn't meant to run
/// beside it; if it doesn't, the page offers the restore and then the steps by hand.
/// </summary>
/// <param name="requestStart">[A] on the notice: the owner runs <see cref="Run"/> once Pan's own dialogs are done.</param>
public sealed partial class PanUpdatePageViewModel(ControllerSupportService support, TimeProvider time, Action requestStart, Action<string> done, Action quit)
    : ViewModelBase
{
    public const string NoticeTitle = "Neue Pan-Version";
    public const string NoticeText = "Diese Arcadia-Version bringt eine neue Version von Pan mit, deinem Controller-Wächter. Ohne Aktualisierung schaltet Arcadia den Controller-Wächter aus.";
    public const string NoticeHint = "Danach fragt Windows, ob Pan installiert werden darf.";
    public const string UpdatingTitle = "Pan wird aktualisiert";
    public const string UpdatingText = "Diese Arcadia-Version braucht eine neue Version von Pan, deinem Controller-Wächter.";
    public const string PromptHint = "Bitte bestätige die Windows-Abfrage. Sie lässt sich bisher nur mit Maus oder Tastatur beantworten.";
    public const string StoppingTitle = "Pan wird beendet";
    public const string StoppingText = "Arcadia wartet, bis das alte Pan deine Controller freigegeben hat. Das dauert höchstens 45 Sekunden.";
    public const string StuckTitle = "Controller freigeben";
    public const string StuckText = "Das alte Pan hat deine Controller nicht freigegeben. Steam Controller oder Puck sind für Spiele womöglich noch unsichtbar.";
    public const string RestoreHint = "Beim Wiederherstellen fragt Windows nach deiner Erlaubnis.";
    public const string RestoringTitle = "Controller werden wiederhergestellt";
    public const string ManualTitle = "Controller von Hand freigeben";
    public const string ManualText = "Das Wiederherstellen hat nicht geklappt. So gibst du die Controller in HidHide frei (nur mit Maus oder Tastatur):";
    public const string RestoreDeclined = "Das Wiederherstellen wurde in der Windows-Abfrage abgelehnt.";
    public const string UpdatedToast = "Pan: aktualisiert";
    public const string DeclinedToast = "Pan: nicht aktualisiert, Controller-Wächter ausgeschaltet";
    public const string FailedToast = "Pan: Aktualisierung fehlgeschlagen, Controller-Wächter ausgeschaltet";
    public const string RestoredToast = "Pan: Controller wiederhergestellt, Controller-Wächter ausgeschaltet";

    /// <summary>An old Pan without a client ends itself after 30 s (<c>ServiceHost.IdleTimeout</c>), after giving everything back.</summary>
    public static readonly TimeSpan StopWait = TimeSpan.FromSeconds(45);

    private static readonly TimeSpan CountdownStep = TimeSpan.FromSeconds(1);

    public static IReadOnlyList<string> ManualSteps { get; } =
    [
        "1. Öffne im Startmenü „HidHide Configuration Client“ und erlaube die Windows-Abfrage.",
        "2. Reiter „Devices“: Entferne die Haken bei Steam Controller und Puck.",
        "3. Reiter „Applications“: Entferne Arcadia aus der Liste.",
        "4. Schalte „Enable device hiding“ aus und schließe das Programm.",
        "5. Wähle hier „Erneut prüfen“.",
    ];

    /// <summary>The toast once the old Pan has ended: why Pan is off.</summary>
    private string _offToast = DeclinedToast;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(Text), nameof(Hint), nameof(HasHint), nameof(IsBusy), nameof(IsCountingDown),
        nameof(IsNotice), nameof(IsWarning), nameof(HasButtons), nameof(ConfirmText), nameof(CancelText), nameof(ShowsSteps))]
    public partial PanUpdateState State { get; private set; }

    /// <summary>Why the restore failed, shown on the page; the details are in <c>pan.log</c> or <c>arcadia.log</c>.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>The countdown while the old Pan ends.</summary>
    [ObservableProperty]
    public partial int SecondsLeft { get; private set; }

    public string Title => State switch
    {
        PanUpdateState.Notice => NoticeTitle,
        PanUpdateState.Stopping => StoppingTitle,
        PanUpdateState.Stuck => StuckTitle,
        PanUpdateState.Restoring => RestoringTitle,
        PanUpdateState.ManualCleanup => ManualTitle,
        _ => UpdatingTitle,
    };

    public string Text => State switch
    {
        PanUpdateState.Notice => NoticeText,
        PanUpdateState.Stopping => StoppingText,
        PanUpdateState.Stuck or PanUpdateState.Restoring => StuckText,
        PanUpdateState.ManualCleanup => ManualText,
        _ => UpdatingText,
    };

    public string? Hint => State switch
    {
        PanUpdateState.Notice => NoticeHint,
        PanUpdateState.Updating or PanUpdateState.Restoring => PromptHint,
        PanUpdateState.Stuck => RestoreHint,
        _ => null,
    };

    public bool HasHint => Hint is not null;

    /// <summary>The small spinner beside the title: something runs and every press is ignored.</summary>
    public bool IsBusy => State is PanUpdateState.Waiting or PanUpdateState.Updating or PanUpdateState.Restoring;

    public bool IsCountingDown => State == PanUpdateState.Stopping;

    public bool IsNotice => State == PanUpdateState.Notice;

    public bool IsWarning => State is PanUpdateState.Stuck or PanUpdateState.ManualCleanup;

    public bool HasButtons => IsNotice || IsWarning;

    public bool ShowsSteps => State == PanUpdateState.ManualCleanup;

    public string ConfirmText => State switch
    {
        PanUpdateState.Notice => "Aktualisieren",
        PanUpdateState.Stuck => "Controller wiederherstellen",
        _ => "Erneut prüfen",
    };

    public string CancelText => IsNotice ? "Ohne Pan weiter" : "Arcadia beenden";

    /// <summary>Starts the update; on the UI thread, once [A] was pressed and Pan's own dialogs are done.</summary>
    public void Run()
    {
        if (State == PanUpdateState.Waiting)
        {
            UpdateAsync().Forget("Pan aktualisieren");
        }
    }

    /// <summary>[A] and [B] act on what the page offers; anything else, and every press while something runs, is ignored.</summary>
    public void Handle(UiAction action)
    {
        switch (action)
        {
            case UiAction.Accept:
                Confirm();
                break;
            case UiAction.Back:
                Cancel();
                break;
        }
    }

    [RelayCommand]
    private void Confirm()
    {
        switch (State)
        {
            case PanUpdateState.Notice:
                State = PanUpdateState.Waiting;
                requestStart();
                break;
            case PanUpdateState.Stuck:
                RestoreAsync().Forget("Controller wiederherstellen");
                break;
            case PanUpdateState.ManualCleanup:
                Error = null;
                WaitForOldPanAsync().Forget("Auf das Ende des alten Pan warten");
                break;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        switch (State)
        {
            case PanUpdateState.Notice:
                SwitchOff(null, DeclinedToast);
                break;
            case PanUpdateState.Stuck or PanUpdateState.ManualCleanup:
                quit();
                break;
        }
    }

    private async Task UpdateAsync()
    {
        State = PanUpdateState.Updating;
        try
        {
            // Off the UI thread: the elevated start waits for the Windows prompt.
            if (await Task.Run(() => support.UpdatePanAsync(CancellationToken.None)))
            {
                done(UpdatedToast);
                return;
            }

            SwitchOff(null, DeclinedToast);
        }
        catch (Exception ex)
        {
            Report(ex, "Pan aktualisieren");
            SwitchOff(ex.Message, FailedToast);
        }
    }

    private void SwitchOff(string? error, string toast)
    {
        support.SwitchPanOffForUpdate(error);
        _offToast = toast;
        WaitForOldPanAsync().Forget("Auf das Ende des alten Pan warten");
    }

    /// <summary>Pan is off, so Arcadia no longer talks to the old one: it gives everything back and ends, or it is stuck.</summary>
    private async Task WaitForOldPanAsync()
    {
        State = PanUpdateState.Stopping;
        SecondsLeft = (int)StopWait.TotalSeconds;
        var started = time.GetTimestamp();
        while (true)
        {
            if (!await Task.Run(() => support.IsPanRunning))
            {
                done(_offToast);
                return;
            }

            // From the clock, not by counting steps: a slow check never stretches the wait.
            var left = StopWait - time.GetElapsedTime(started);
            SecondsLeft = Math.Max(0, (int)Math.Ceiling(left.TotalSeconds));
            if (left <= TimeSpan.Zero)
            {
                break;
            }

            await Task.Delay(CountdownStep, time);
        }

        AppLog.Write($"Pan: das alte Pan läuft nach {StopWait.TotalSeconds:0} s noch – es hat die Controller wohl nicht freigegeben");
        State = PanUpdateState.Stuck;
    }

    private async Task RestoreAsync()
    {
        State = PanUpdateState.Restoring;
        Error = null;
        try
        {
            if (await Task.Run(() => support.RestoreAfterOldPanAsync(CancellationToken.None)))
            {
                done(RestoredToast);
                return;
            }

            Error = RestoreDeclined;
        }
        catch (Exception ex)
        {
            Report(ex, "Controller wiederherstellen");
            Error = ex.Message;
        }

        State = PanUpdateState.ManualCleanup;
    }

    private static void Report(Exception ex, string context)
    {
        if (ex is not (InvalidOperationException or TimeoutException))
        {
            // Pan's own failures come as these with their text; anything else is a bug, logged with its stack.
            AppErrors.Report(ex, context);
        }

        AppLog.Write($"{context} beim Start: {ex.Message}");
    }
}
