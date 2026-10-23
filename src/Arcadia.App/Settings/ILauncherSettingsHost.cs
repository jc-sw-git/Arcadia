using Arcadia.Core.Launchers;
using Arcadia.Core.Library;

namespace Arcadia.App.Settings;

/// <summary>What the launcher settings (#136) need from the library they configure.</summary>
public interface ILauncherSettingsHost
{
    LauncherRegistry Launchers { get; }

    /// <summary>The launcher of the library's selected tab, or null.</summary>
    string? SelectedLauncherId { get; }

    /// <summary>The games of an active launcher as its tab shows them; null while it has no tab (inactive, or not read yet).</summary>
    LauncherLibrary? LibraryOf(string launcherId);

    /// <summary>When the launcher's games were last read; null if never since Arcadia started.</summary>
    DateTimeOffset? ScannedAt(string launcherId);

    /// <summary>The active launchers or their order changed: tabs follow at once, a launcher switched on is read.</summary>
    void LauncherChoiceChanged();

    /// <summary>X: reads every active launcher again ("Alle neu einlesen").</summary>
    void RescanLaunchers();
}
