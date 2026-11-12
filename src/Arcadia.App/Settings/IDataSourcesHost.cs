using Arcadia.App.Keyboard;
using Arcadia.ControllerSupport;

namespace Arcadia.App.Settings;

/// <summary>What the category "Datenquellen" (#489) needs of the App around it.</summary>
public interface IDataSourcesHost
{
    void Toast(HealthSeverity level, string text);

    /// <summary>The settings' on-screen keyboard, now typing into <paramref name="editor"/>.</summary>
    OnScreenKeyboardViewModel TakeKeyboard(IKeyboardTarget editor);

    void GiveBackKeyboard(IKeyboardTarget editor);

    /// <summary>The clipboard's text; null if it holds none (empty, an image) or can't be read.</summary>
    Task<string?> ReadClipboardAsync();

    /// <summary>
    /// "Schlüssel-Seite öffnen" (SteamGridDB's key page, #489) or "Zugang anlegen" (Twitch's developer console, #494): a page
    /// fixed in code in the default browser, the mouse mode on for it when opened by controller; says itself (a toast)
    /// whether that worked.
    /// </summary>
    /// <param name="opening">The info toast while it opens.</param>
    /// <param name="failed">The error toast when it didn't.</param>
    Task OpenPageAsync(Uri page, string opening, string failed);

    /// <summary>"Warten auf deine Auswahl" (#491): the waiting list over the settings; B there comes back to the row.</summary>
    void OpenWaitingList();
}
