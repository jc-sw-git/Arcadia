using Arcadia.App.Services;
using Arcadia.ControllerSupport;

namespace Arcadia.App.ViewModels;

/// <summary>
/// The Pan update question at start (#243, T243-14): this Arcadia bundles a newer Pan than the installed one, so the user
/// is asked once per start; declined, the question comes again at the next start. Over another dialog (the first run,
/// the settings) it is dropped for this start.
/// </summary>
public sealed class PanUpdateOffer(ControllerSupportService support, MainViewModel main)
{
    public const string Title = "Pan aktualisieren";
    public const string Text = "Diese Arcadia-Version bringt eine neue Version von Pan mit, dem Wächterdienst für den Steam Controller. Die Aktualisierung fragt einmal nach Administratorrechten.";
    public const string Confirm = "Aktualisieren";

    /// <summary>Off the UI thread: compares the bundled Pan with the installed one.</summary>
    public bool IsDue() => support.IsPanUpdateAvailable;

    /// <summary>On the UI thread: the question, with the update behind its [A].</summary>
    public void Ask() => main.Ask(new ConfirmationViewModel(Title, Text, Confirm, () => support.UpdatePanAsync().Forget("Pan aktualisieren")));
}
