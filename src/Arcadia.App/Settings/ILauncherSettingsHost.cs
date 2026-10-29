using Arcadia.App.Keyboard;
using Arcadia.App.Services;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;

namespace Arcadia.App.Settings;

/// <summary>What the launcher settings (#136, #137, #138) need from the library they configure.</summary>
public interface ILauncherSettingsHost
{
    LauncherRegistry Launchers { get; }

    /// <summary>The launchers' logos: the user's pictures and the launchers' own (#138).</summary>
    LauncherLogos Logos { get; }

    /// <summary>The file and folder dialogs; null until the window is there.</summary>
    ISystemDialogs? Dialogs { get; }

    /// <summary>The games of an active launcher as its tab shows them; null while it has no tab (inactive, or not read yet).</summary>
    LauncherLibrary? LibraryOf(string launcherId);

    /// <summary>When the launcher's games were last read; null if never since Arcadia started.</summary>
    DateTimeOffset? ScannedAt(string launcherId);

    /// <summary>The active launchers or their order changed: tabs follow at once, a launcher switched on is read.</summary>
    void LauncherChoiceChanged();

    /// <summary>A launcher's name, keyword or logo changed (#137, #138): tabs, covers and search show it at once.</summary>
    void LauncherLooksChanged();

    /// <summary>A switch for software, tools or soundtracks changed (#141): the tabs show what it adds or hides at once.</summary>
    void LibraryTypesChanged();

    /// <summary>X: reads every active launcher again ("Alle neu einlesen").</summary>
    void RescanLaunchers();

    /// <summary>"Neu einlesen" on a launcher's page, or its folder was chosen (#138): reads it again and says what came of it.</summary>
    void RescanLauncher(string launcherId);

    /// <summary>The on-screen keyboard for a text of the settings, in the layout last chosen.</summary>
    OnScreenKeyboardViewModel CreateKeyboard(IKeyboardTarget target);

    /// <summary>
    /// "Controller-Wächter (Pan)" is on and can run here: only then is there a Steam Controller mode (#242), so only then
    /// can a launcher's default for it change (#178).
    /// </summary>
    bool IsPanOn { get; }

    /// <summary>Pan can run on this system at all (not yet on Linux): otherwise switching it on doesn't help.</summary>
    bool IsPanSupported { get; }

    /// <summary>A short message at the bottom, e.g. why a text wasn't taken.</summary>
    void Notify(string message);
}
