using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

public enum PanUpdateState
{
    /// <summary>Shown at start; the update begins once Pan's own dialogs (a firmware hold) are done.</summary>
    Waiting,
    Updating,

    /// <summary>Declined, timed out or failed: [A] tries again, [X] removes Pan, [B] ends Arcadia.</summary>
    Failed,
    Removing,
}

/// <summary>
/// The start page while the installed Pan isn't the one this Arcadia brings (#297, owner 2026-10-28): no question, a
/// spinner and one Windows prompt, then the library. Until the update succeeded or Pan is removed the library can't be
/// reached, so a broken update never leaves Arcadia half working, and "Pan entfernen" never locks Arcadia out for good.
/// </summary>
public sealed partial class PanUpdatePageViewModel(ControllerSupportService support, Action<string> done, Action quit) : ViewModelBase
{
    public const string UpdatingTitle = "Pan wird aktualisiert";
    public const string UpdatingText = "Diese Arcadia-Version braucht eine neue Version von Pan, deinem Controller-Wächter. Der Start dauert deshalb etwas länger.";
    public const string PromptHint = "Bitte bestätige die Windows-Abfrage. Sie lässt sich bisher nur mit Maus oder Tastatur beantworten.";
    public const string FailedTitle = "Pan muss aktualisiert werden";
    public const string FailedText = "Arcadia läuft nur mit der Pan-Version, die zu ihm gehört. Entfernst du Pan, startet Arcadia sofort, verwaltet dann aber keine Controller.";
    public const string RemovingTitle = "Pan wird entfernt";
    public const string UpdateDeclined = "Die Aktualisierung wurde in der Windows-Abfrage abgelehnt.";
    public const string RemoveDeclined = "Das Entfernen wurde in der Windows-Abfrage abgelehnt.";
    public const string UpdatedToast = "Pan: aktualisiert";
    public const string RemovedToast = "Pan: entfernt und ausgeschaltet";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(Text), nameof(IsBusy), nameof(IsFailed), nameof(IsAskingWindows))]
    public partial PanUpdateState State { get; private set; }

    /// <summary>Why the last try failed, shown on the page; the details are in <c>pan.log</c> or <c>arcadia.log</c>.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    public string Title => State switch
    {
        PanUpdateState.Failed => FailedTitle,
        PanUpdateState.Removing => RemovingTitle,
        _ => UpdatingTitle,
    };

    public string Text => State == PanUpdateState.Failed ? FailedText : UpdatingText;

    public bool IsBusy => State != PanUpdateState.Failed;

    public bool IsFailed => State == PanUpdateState.Failed;

    /// <summary>The Windows prompt is up (or about to be): the page says to answer it.</summary>
    public bool IsAskingWindows => State is PanUpdateState.Updating or PanUpdateState.Removing;

    /// <summary>Starts the update; on the UI thread, once Pan's own dialogs are done.</summary>
    public void Run()
    {
        if (State is PanUpdateState.Waiting or PanUpdateState.Failed)
        {
            RunAsync(support.UpdatePanAsync, UpdateDeclined, UpdatedToast, PanUpdateState.Updating).Forget("Pan aktualisieren");
        }
    }

    /// <summary>[A] tries again, [X] removes Pan, [B] ends Arcadia; while busy every press is ignored (the prompt is up).</summary>
    public void Handle(UiAction action)
    {
        switch (action)
        {
            case UiAction.Accept:
                Retry();
                break;
            case UiAction.Search:
                Remove();
                break;
            case UiAction.Back:
                Quit();
                break;
        }
    }

    [RelayCommand]
    private void Retry()
    {
        if (IsFailed)
        {
            Run();
        }
    }

    [RelayCommand]
    private void Remove()
    {
        if (IsFailed)
        {
            RunAsync(support.RemovePanAsync, RemoveDeclined, RemovedToast, PanUpdateState.Removing).Forget("Pan entfernen");
        }
    }

    [RelayCommand]
    private void Quit()
    {
        if (IsFailed)
        {
            quit();
        }
    }

    private async Task RunAsync(Func<CancellationToken, Task<bool>> step, string declined, string toast, PanUpdateState busy)
    {
        State = busy;
        Error = null;
        try
        {
            // Off the UI thread: the elevated start waits for the Windows prompt.
            if (await Task.Run(() => step(CancellationToken.None)))
            {
                done(toast);
                return;
            }

            Error = declined;
        }
        catch (Exception ex)
        {
            if (ex is not (InvalidOperationException or TimeoutException))
            {
                // Pan's own failures come as these with their text; anything else is a bug, logged with its stack.
                AppErrors.Report(ex, busy == PanUpdateState.Removing ? "Pan entfernen" : "Pan aktualisieren");
            }

            AppLog.Write($"Pan beim Start: {ex.Message}");
            Error = ex.Message;
        }

        State = PanUpdateState.Failed;
    }
}
