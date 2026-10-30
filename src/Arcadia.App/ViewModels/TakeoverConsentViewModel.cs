using Arcadia.App.Input;
using Arcadia.ControllerSupport.Pan;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>How the dialogs and the settings name what of the Steam Controller is connected (designs, "#242 Pan").</summary>
public static class SteamControllerPartsNames
{
    /// <summary>"Steam Controller und Puck", "Puck" or "Steam Controller".</summary>
    public static string Names(SteamControllerParts parts) => parts switch
    {
        SteamControllerParts.Puck => "Puck",
        SteamControllerParts.Controller => "Steam Controller",
        _ => "Steam Controller und Puck",
    };

    /// <summary>As the object of a sentence: "Steam Controller und Puck", "den Puck" or "den Steam Controller".</summary>
    public static string Object(SteamControllerParts parts) => parts switch
    {
        SteamControllerParts.Puck => "den Puck",
        SteamControllerParts.Controller => "den Steam Controller",
        _ => "Steam Controller und Puck",
    };

    /// <summary>The chips of what was found, with "Steam läuft".</summary>
    public static IReadOnlyList<string> Found(SteamControllerParts parts) => parts switch
    {
        SteamControllerParts.Puck => ["Puck", "Steam läuft"],
        SteamControllerParts.Controller => ["Steam Controller", "Steam läuft"],
        _ => ["Steam Controller", "Puck", "Steam läuft"],
    };
}

/// <summary>
/// "Steam Controller übernehmen?" (#242, owner's design 2026-10-28): Steam runs and the real puck was visible, so it may
/// be updating the firmware, which a hide would break off. Can't be closed: the player answers, "Ja" held 3 s, "Nein"
/// (focused first) held 1 s; [B] does nothing.
/// </summary>
public sealed partial class TakeoverConsentViewModel : ViewModelBase, IDisposable
{
    public const string AfterText = "Bei „Nein“ kannst du sie später in den Einstellungen unter Controller › Pan übernehmen lassen. Beim nächsten Start von Arcadia fragt Arcadia wieder.";

    public TakeoverConsentViewModel(SteamControllerParts parts, Action<bool> answer, TimeProvider time, Action<Action> post)
    {
        Parts = parts;
        Yes = new HoldButtonViewModel("Ja, übernehmen. Es läuft gerade kein Firmware-Update!", TimeSpan.FromSeconds(3), () => answer(true), time, post);
        No = new HoldButtonViewModel("Nein, nicht übernehmen", TimeSpan.FromSeconds(1), () => answer(false), time, post) { IsFocused = true };
    }

    /// <summary>What is connected; follows the question while it is open (#325).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(Found), nameof(Message), nameof(WarningTitle))]
    public partial SteamControllerParts Parts { get; private set; }

    /// <summary>
    /// The question names other parts now. A running hold stops: the player confirms what the dialog shows, not what it
    /// showed when the hold began.
    /// </summary>
    public void Follow(SteamControllerParts parts)
    {
        if (parts == Parts)
        {
            return;
        }

        CancelHold();
        Parts = parts;
    }

    public string Title => Parts switch
    {
        SteamControllerParts.Puck => "Puck übernehmen?",
        SteamControllerParts.Controller => "Steam Controller übernehmen?",
        _ => "Steam Controller und Puck übernehmen?",
    };

    public IReadOnlyList<string> Found => SteamControllerPartsNames.Found(Parts);

    public string Message =>
        $"Übernimmt Arcadia {SteamControllerPartsNames.Object(Parts)}, kann ein laufendes Firmware-Update dadurch unterbrochen werden und scheitern. Das kann im Zweifel {SteamControllerPartsNames.Object(Parts)} beschädigen!";

    public string WarningTitle => Parts switch
    {
        SteamControllerParts.Puck => "Erst prüfen: Führt Steam gerade ein Firmware-Update des Pucks aus?",
        SteamControllerParts.Controller => "Erst prüfen: Führt Steam gerade ein Firmware-Update des Steam Controllers aus?",
        _ => "Erst prüfen: Führt Steam gerade ein Firmware-Update des Steam Controllers oder Pucks aus?",
    };

    public string WarningText => "Dann wähle „Nein“ und lass das Update fertig laufen.";

    public string After => AfterText;

    public HoldButtonViewModel Yes { get; }

    public HoldButtonViewModel No { get; }

    [ObservableProperty]
    public partial string Legend { get; private set; } = "Halten (1 s): Nicht übernehmen";

    private HoldButtonViewModel Focused => Yes.IsFocused ? Yes : No;

    /// <summary>Always true: the dialog stays until it is answered.</summary>
    public bool Handle(UiAction action)
    {
        switch (action)
        {
            case UiAction.Left:
                Focus(Yes);
                break;
            case UiAction.Right:
                Focus(No);
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
            Yes.Release();
            No.Release();
        }
    }

    /// <summary>A click: the button under the pointer gets the focus and starts its hold.</summary>
    public void Press(HoldButtonViewModel button)
    {
        Focus(button);
        button.Press();
    }

    public void CancelHold()
    {
        Yes.Cancel();
        No.Cancel();
    }

    private void Focus(HoldButtonViewModel button)
    {
        if (Focused == button)
        {
            return;
        }

        CancelHold();
        Yes.IsFocused = button == Yes;
        No.IsFocused = button == No;
        Legend = button == Yes ? "Halten (3 s): Übernehmen" : "Halten (1 s): Nicht übernehmen";
    }

    public void Dispose()
    {
        Yes.Dispose();
        No.Dispose();
    }
}
