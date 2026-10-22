using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.Settings;

/// <summary>
/// Changes from the settings page: they apply at once, without a save button (#15). If settings.json can't be written
/// (locked, read-only), the change still applies until Arcadia closes and <see cref="Error"/> says so.
/// </summary>
public sealed partial class SettingsChanges(SettingsService settings) : ViewModelBase
{
    public AppSettings Current => settings.Current;

    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <param name="what">The setting's name for the log, e.g. "Mausmodus".</param>
    public void Apply(Func<AppSettings, AppSettings> change, string what)
    {
        try
        {
            settings.Update(change);
            Error = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Einstellung „{what}“ nicht gespeichert: {ex.Message}");
            Error = "Die Änderung konnte nicht gespeichert werden und gilt nur bis zum Beenden – Details stehen in arcadia.log.";
        }
    }
}
