using Arcadia.App.Keyboard;
using Arcadia.App.Services;
using Arcadia.App.Settings;
using Arcadia.ControllerSupport;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;

namespace Arcadia.App.ViewModels;

/// <summary>
/// Main's part of "Datenquellen" (#489): the category's host (toasts, keyboard, clipboard, the key page) and the warning
/// when a search the player started fails as a whole.
/// </summary>
public partial class MainViewModel : IDataSourcesHost
{
    private static readonly TimeSpan BrowserOpenTimeout = TimeSpan.FromSeconds(10);

    // The player's own search is going on: its end may need a warning.
    private bool _playerSearchRunning;

    /// <summary>The default browser, for SteamGridDB's key page; the tests put a fake in.</summary>
    public IWebBrowser Browser { get; set; } = new SystemWebBrowser();

    /// <summary>The clipboard, for "Einfügen"; the window gives its own, the tests a fake.</summary>
    public IClipboardText Clipboard { get; set; } = new NoClipboard();

    /// <summary>The key is kept across a restart (Windows); else only until Arcadia closes, which its info text says.</summary>
    public bool KeepsSteamGridDbKey { get; set; } = OperatingSystem.IsWindows();

    /// <summary>"Datenquellen" exists only with artwork and SteamGridDB wired (the App does both).</summary>
    private DataSourcesSettingsViewModel? CreateDataSources(SettingsChanges changes) =>
        _artwork is { SteamGridDb: { } key } artwork ? new DataSourcesSettingsViewModel(key, artwork, this, changes, _time, KeepsSteamGridDbKey) : null;

    /// <summary>
    /// Opens "Datenquellen" on the key, e.g. from the status modal or "Artwork ändern" without a key (#490), or on another
    /// row, e.g. a source switched off (#506).
    /// </summary>
    public void OpenDataSources(string rowKey = SteamGridDbKeyRows.KeyRowKey)
    {
        ShowSettings(typeof(DataSourcesSettingsViewModel));
        Settings?.Selected?.FocusRow(rowKey);
    }

    /// <summary>The key's state and the search are followed from the start, for the header and the warning at a search's end.</summary>
    private void WatchSteamGridDb(ArtworkService artwork)
    {
        if (artwork.SteamGridDb is { } key)
        {
            Status.WatchSteamGridDb(key);
        }

        artwork.SearchChanged += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => OnArtworkSearchChanged(artwork.Search));
    }

    /// <summary>
    /// A run the player started that SteamGridDB failed as a whole: one warning (#13); a background run says nothing. A
    /// turned-down key also shows as a status message.
    /// </summary>
    private void OnArtworkSearchChanged(ArtworkSearch search)
    {
        if (search.Running is { ByPlayer: true })
        {
            _playerSearchRunning = true;
            return;
        }

        if (search.Running is not null || !_playerSearchRunning)
        {
            return;
        }

        _playerSearchRunning = false;
        switch (search.Result?.SteamGridDbFailure)
        {
            case SteamGridDbOutcome.NoConnection:
                Toast.Show(HealthSeverity.Warning, AppTexts.DataSourcesSearchNoConnection);
                break;
            case SteamGridDbOutcome.KeyRejected:
                Toast.Show(HealthSeverity.Warning, AppTexts.DataSourcesKeyRejectedToast);
                break;
        }
    }

    void IDataSourcesHost.Toast(HealthSeverity level, string text) => Toast.Show(level, text);

    OnScreenKeyboardViewModel IDataSourcesHost.TakeKeyboard(IKeyboardTarget editor) => SettingsKeyboard.Take(editor, Input.UsesController);

    void IDataSourcesHost.GiveBackKeyboard(IKeyboardTarget editor) => SettingsKeyboard.GiveBack(editor);

    async Task<string?> IDataSourcesHost.ReadClipboardAsync()
    {
        try
        {
            return await Clipboard.ReadAsync();
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            // Another program holds the clipboard: the same as no key there.
            AppLog.Write($"Zwischenablage nicht lesbar: {e.GetType().Name}");
            return null;
        }
    }

    async Task IDataSourcesHost.OpenKeyPageAsync()
    {
        var byController = Input.UsesController;
        Toast.Show(HealthSeverity.Info, AppTexts.DataSourcesKeyPageOpening);
        try
        {
            // Off the UI thread: the shell may take a moment to start the browser.
            await Task.Run(() => Browser.Open(SteamGridDbClient.KeyPage)).WaitAsync(BrowserOpenTimeout, _time);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException)
        {
            AppLog.Write($"SteamGridDB-Seite nicht geöffnet: {ex.GetType().Name}: {ex.Message}");
            Toast.Show(HealthSeverity.Error, AppTexts.DataSourcesKeyPageFailed);
            return;
        }

        // The browser is used with the mouse: opened by controller, the mouse mode comes on for it, as for a log (#433).
        if (byController)
        {
            OpenedForMouseByController?.Invoke(this, EventArgs.Empty);
        }
    }
}
