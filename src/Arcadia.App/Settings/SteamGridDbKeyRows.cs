using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;

namespace Arcadia.App.Settings;

/// <summary>
/// The group "STEAMGRIDDB" of "Datenquellen" (#489): the key (on-screen keyboard), indented below it "Einfügen" from the
/// clipboard, "Schlüssel-Seite öffnen" and "Verbindung testen". A key is checked once it is taken; the row shows only its
/// last four characters. An invalid key is a warning on the row (and through <see cref="SteamGridDbKey"/> on the tab and
/// the status chip).
/// </summary>
public sealed class SteamGridDbKeyRows
{
    public const string KeyRowKey = "steamgriddb-key";

    private static string Group => AppTexts.DataSourcesSteamGridDbGroup;

    private readonly SteamGridDbKey _key;
    private readonly ArtworkService _artwork;
    private readonly IDataSourcesHost _host;
    private readonly Action<TextEditorViewModel?> _showEditor;
    private readonly TimeProvider _time;
    private readonly bool _keyPersists;
    private readonly TextRowViewModel _keyRow;
    private readonly ActionRowViewModel _paste;
    private readonly ActionRowViewModel _page;
    private readonly ActionRowViewModel _test;
    private bool _checking;
    private bool _testing;

    /// <param name="showEditor">Shows the editor of the key over the page (the category's), or null when it closes.</param>
    /// <param name="keyPersists">The key is kept across a restart (Windows); else only until Arcadia closes.</param>
    public SteamGridDbKeyRows(SteamGridDbKey key, ArtworkService artwork, IDataSourcesHost host, Action<TextEditorViewModel?> showEditor, TimeProvider time, bool keyPersists)
    {
        _key = key;
        _artwork = artwork;
        _host = host;
        _showEditor = showEditor;
        _time = time;
        _keyPersists = keyPersists;
        _keyRow = new TextRowViewModel(KeyRowKey, AppTexts.DataSourcesKey, () => key.Ending is { } ending ? "…" + ending : string.Empty, _ => Edit())
        {
            Group = Group,
            ActionLabel = () => _key.State == SteamGridDbKeyState.None ? AppTexts.DataSourcesKeyEnter : AppTexts.Edit,
        };
        _paste = new ActionRowViewModel("steamgriddb-paste", AppTexts.DataSourcesPaste, () => PasteAsync().Forget(AppTexts.ErrorContextSteamGridDb))
        {
            Group = Group,
            Indent = 1,
            Subtitle = AppTexts.DataSourcesPasteSubtitle,
            ActionText = AppTexts.DataSourcesPasteAction,
            Info = [new InfoText(AppTexts.DataSourcesPasteInfo), new InfoText(AppTexts.DataSourcesPasteInfoNoKey)],
        };
        _page = new ActionRowViewModel("steamgriddb-page", AppTexts.DataSourcesKeyPage, () => host.OpenKeyPageAsync().Forget(AppTexts.ErrorContextSteamGridDb))
        {
            Group = Group,
            Indent = 1,
            Subtitle = AppTexts.DataSourcesKeyPageSubtitle,
            ActionText = AppTexts.DataSourcesKeyPageAction,
            Info = [new InfoText(AppTexts.DataSourcesKeyPageInfo), new InfoText(AppTexts.DataSourcesKeyPageMouse)],
        };
        _test = new ActionRowViewModel("steamgriddb-test", AppTexts.DataSourcesTest, () => TestAsync().Forget(AppTexts.ErrorContextSteamGridDb))
        {
            Group = Group,
            Indent = 1,
            ActionText = AppTexts.DataSourcesTestAction,
            Info = [new InfoText(AppTexts.DataSourcesTestInfo), new InfoText(AppTexts.DataSourcesTestInfoTimeout)],
        };
        Refresh();
    }

    public IReadOnlyList<SettingsRowViewModel> Rows => [_keyRow, _paste, _page, _test];

    /// <summary>The key or what SteamGridDB said about it changed; UI thread.</summary>
    public void Refresh()
    {
        var state = _key.State;
        var ending = _key.Ending ?? string.Empty;
        _keyRow.Subtitle = _checking
            ? AppTexts.DataSourcesKeyChecking
            : state switch
            {
                SteamGridDbKeyState.None => AppTexts.DataSourcesKeyNone,
                SteamGridDbKeyState.Valid => AppTexts.DataSourcesKeyConnected(ending),
                SteamGridDbKeyState.Rejected => AppTexts.DataSourcesKeyRejected,
                _ => AppTexts.DataSourcesKeyUnchecked(ending),
            };
        _keyRow.Severity = state == SteamGridDbKeyState.Rejected ? HealthSeverity.Warning : null;
        _keyRow.ShowLamp(state == SteamGridDbKeyState.Valid ? RowLamp.Working : null);
        _keyRow.Info =
        [
            new InfoText(AppTexts.DataSourcesKeyInfo),
            new InfoText(AppTexts.DataSourcesKeyHowTo),
            .. state == SteamGridDbKeyState.Rejected
                ? [new InfoSection(AppTexts.DataSourcesNextStepsHeading, [new InfoText(AppTexts.DataSourcesKeyRejectedInfo)])]
                : Array.Empty<InfoBlock>(),
            new InfoSection(AppTexts.DataSourcesSendsHeading, [new InfoText(_keyPersists ? AppTexts.DataSourcesSendsInfo : AppTexts.DataSourcesSendsInfoInMemory)]),
        ];
        _keyRow.Refresh();

        var hasKey = state != SteamGridDbKeyState.None;
        _test.IsDimmed = !hasKey;
        _test.IsEnabled = hasKey && !_testing && !_checking;
        _test.IsWorking = _testing;
        _test.Subtitle = (hasKey, _key.LastCheck) switch
        {
            (false, _) => AppTexts.DataSourcesTestNeedsKey,
            (true, { Outcome: SteamGridDbOutcome.KeyRejected } check) => AppTexts.DataSourcesTestRejected(check.At.ToOffset(_time.GetLocalNow().Offset)),
            (true, { Outcome: SteamGridDbOutcome.NoConnection }) => AppTexts.DataSourcesTestNoConnection,
            (true, { } check) => AppTexts.DataSourcesTestOk(check.At.ToOffset(_time.GetLocalNow().Offset)),
            _ => AppTexts.DataSourcesTestNotYet,
        };
        _paste.IsEnabled = !_checking;
    }

    /// <summary>A on "Schlüssel": the on-screen keyboard over an empty field; an empty field taken deletes the key.</summary>
    private void Edit()
    {
        if (_keyRow.Editor is not null || _checking)
        {
            return;
        }

        TextEditorViewModel? editor = null;
        editor = new TextEditorViewModel(
            string.Empty,
            text => text.Length == 0 || SteamGridDbKey.Normalize(text) is not null ? null : AppTexts.DataSourcesKeyProblem,
            text =>
            {
                if (text.Length == 0)
                {
                    Delete();
                }
                else
                {
                    TakeAsync(text, pasted: false).Forget(AppTexts.ErrorContextSteamGridDb);
                }

                return true;
            },
            () => CloseEditor(editor!),
            _host.TakeKeyboard,
            _host.GiveBackKeyboard,
            char.IsAsciiLetterOrDigit);
        _keyRow.Editor = editor;
        _showEditor(editor);
    }

    private void CloseEditor(TextEditorViewModel editor)
    {
        _keyRow.Editor = null;
        _showEditor(null);
        if (editor.Rejection is { } reason)
        {
            _host.Toast(HealthSeverity.Warning, AppTexts.SettingNotApplied(row: _keyRow.Title, reason: reason));
        }

        editor.Dispose();
    }

    private void Delete()
    {
        if (_key.State == SteamGridDbKeyState.None)
        {
            return;
        }

        _key.Clear();
        AppLog.Write("SteamGridDB: Schlüssel gelöscht");
        _host.Toast(HealthSeverity.Info, AppTexts.DataSourcesKeyDeleted);
        Refresh();
    }

    private async Task PasteAsync()
    {
        if (_checking)
        {
            return;
        }

        if (SteamGridDbKey.Normalize(await _host.ReadClipboardAsync()) is not { } key)
        {
            _host.Toast(HealthSeverity.Warning, AppTexts.DataSourcesPasteNoKey);
            return;
        }

        await TakeAsync(key, pasted: true);
    }

    /// <summary>Saves the key and checks it; a key that holds starts the search for what is missing at once.</summary>
    private async Task TakeAsync(string key, bool pasted)
    {
        _checking = true;
        Refresh();
        SteamGridDbOutcome outcome;
        try
        {
            outcome = await _key.SetAsync(key);
        }
        finally
        {
            _checking = false;
            Refresh();
        }

        AppLog.Write($"SteamGridDB: Schlüssel …{SteamGridDbClient.Ending(key)} {(pasted ? "eingefügt" : "eingegeben")}: {outcome}");
        switch (outcome)
        {
            case SteamGridDbOutcome.KeyRejected:
                _host.Toast(HealthSeverity.Warning, AppTexts.DataSourcesKeyRejectedToast);
                break;
            case SteamGridDbOutcome.NoConnection:
                _host.Toast(HealthSeverity.Warning, AppTexts.DataSourcesKeyKeptNoConnection);
                break;
            default:
                _host.Toast(HealthSeverity.Info, pasted ? AppTexts.DataSourcesKeyPasted : AppTexts.DataSourcesKeySaved);
                _artwork.SearchAgain(byPlayer: false);
                break;
        }
    }

    private async Task TestAsync()
    {
        if (_testing || _checking)
        {
            return;
        }

        _testing = true;
        Refresh();
        SteamGridDbOutcome? outcome;
        try
        {
            outcome = await _key.TestAsync();
        }
        finally
        {
            _testing = false;
            Refresh();
        }

        switch (outcome)
        {
            case SteamGridDbOutcome.KeyRejected:
                _host.Toast(HealthSeverity.Warning, AppTexts.DataSourcesKeyRejectedToast);
                break;
            case SteamGridDbOutcome.NoConnection:
                _host.Toast(HealthSeverity.Warning, AppTexts.DataSourcesTestNoConnectionToast);
                break;
            case not null:
                _host.Toast(HealthSeverity.Info, AppTexts.DataSourcesTestOkToast);
                break;
        }
    }
}
