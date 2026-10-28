using Arcadia.App.Services;
using Arcadia.ControllerSupport;

namespace Arcadia.App.ViewModels;

/// <summary>
/// The Pan update question at start (#243, T243-14): this Arcadia bundles a newer Pan than the installed one, so the user
/// is asked once per start; declined, the question comes again at the next start. Over another dialog (the first run,
/// the settings) it is dropped for this start. An update that installs HidHide too restarts the puck, so Steam closes for
/// it (#296): the question says so, and with a Steam game running it isn't asked at this start.
/// </summary>
public sealed class PanUpdateOffer(ControllerSupportService support, MainViewModel main)
{
    public const string Title = "Pan aktualisieren";
    public const string Text = "Diese Arcadia-Version bringt eine neue Version von Pan mit, dem Wächterdienst für den Steam Controller. Die Aktualisierung fragt einmal nach Administratorrechten.";
    public const string SteamStopText = "Steam wird dafür kurz beendet und danach wieder gestartet.";
    public const string Confirm = "Aktualisieren";

    /// <summary>Set by <see cref="IsDue"/> off the UI thread, read by <see cref="Ask"/> on it.</summary>
    private bool _stopsSteam;

    /// <summary>Off the UI thread: compares the bundled Pan with the installed one, and looks for a Steam game in the way.</summary>
    public bool IsDue()
    {
        if (!support.IsPanUpdateAvailable)
        {
            return false;
        }

        _stopsSteam = support.PanUpdateStopsSteam;
        if (_stopsSteam && support.SteamGameBlockingInstall() is { } game)
        {
            AppLog.Write($"Pan-Aktualisierung nicht angeboten: sie beendet Steam, und „{game}“ läuft");
            return false;
        }

        return true;
    }

    /// <summary>On the UI thread: the question, with the update behind its [A].</summary>
    public void Ask() => main.Ask(new ConfirmationViewModel(Title, _stopsSteam ? $"{Text} {SteamStopText}" : Text, Confirm,
        () => support.UpdatePanAsync().Forget("Pan aktualisieren")));
}
