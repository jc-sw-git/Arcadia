using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.Core.Metadata;
using Arcadia.Core.Metadata.Igdb;

namespace Arcadia.App.Settings;

/// <summary>
/// The group "IGDB" of "Datenquellen" (#494): "Client-ID" and "Client-Secret" (on-screen keyboard), each with an indented
/// "Aus der Zwischenablage einfügen", then "Zugang anlegen" (Twitch's developer console), "Verbindung testen" and "Zugang
/// löschen". The access is checked as soon as both values are there and after each change; the rows show only the
/// values' last four characters. A rejected access is a warning on the row (and through <see cref="IgdbAccess"/> on the
/// tab and the status chip).
/// </summary>
public sealed class IgdbAccessRows
{
    public const string ClientIdRowKey = "igdb-client-id";

    public const string SecretRowKey = "igdb-secret";

    private static string Group => AppTexts.DataSourcesIgdbGroup;

    private readonly IgdbAccess _access;
    private readonly MetadataService _metadata;
    private readonly IDataSourcesHost _host;
    private readonly Action<TextEditorViewModel?> _showEditor;
    private readonly TimeProvider _time;
    private readonly TextRowViewModel _clientId;
    private readonly ActionRowViewModel _pasteClientId;
    private readonly TextRowViewModel _secret;
    private readonly ActionRowViewModel _pasteSecret;
    private readonly ActionRowViewModel _console;
    private readonly ActionRowViewModel _test;
    private readonly ActionRowViewModel _delete;
    private bool _checking;
    private bool _testing;

    /// <param name="showEditor">Shows the editor over the page (the category's), or null when it closes.</param>
    public IgdbAccessRows(IgdbAccess access, MetadataService metadata, IDataSourcesHost host, Action<TextEditorViewModel?> showEditor, TimeProvider time)
    {
        _access = access;
        _metadata = metadata;
        _host = host;
        _showEditor = showEditor;
        _time = time;
        _clientId = new TextRowViewModel(ClientIdRowKey, AppTexts.DataSourcesIgdbClientId, () => access.ClientIdEnding is { } ending ? "…" + ending : string.Empty, row => Edit(row, isSecret: false))
        {
            Group = Group,
            TintsSubtitle = true,
            ActionLabel = () => _access.ClientIdEnding is null ? AppTexts.DataSourcesKeyEnter : AppTexts.Edit,
        };
        _pasteClientId = PasteRow("igdb-client-id-paste", isSecret: false);
        _secret = new TextRowViewModel(SecretRowKey, AppTexts.DataSourcesIgdbSecret, () => access.SecretEnding is { } ending ? "…" + ending : string.Empty, row => Edit(row, isSecret: true))
        {
            Group = Group,
            TintsSubtitle = true,
            ActionLabel = () => _access.SecretEnding is null ? AppTexts.DataSourcesKeyEnter : AppTexts.Edit,
            Info = [new InfoText(AppTexts.DataSourcesIgdbSecretInfo), new InfoText(AppTexts.DataSourcesIgdbHowTo)],
        };
        _pasteSecret = PasteRow("igdb-secret-paste", isSecret: true);
        _console = new ActionRowViewModel("igdb-console", AppTexts.DataSourcesIgdbConsole, () => host.OpenPageAsync(IgdbClient.ConsolePage, AppTexts.DataSourcesIgdbConsoleOpening, AppTexts.DataSourcesIgdbConsoleFailed).Forget(AppTexts.ErrorContextIgdb))
        {
            Group = Group,
            Subtitle = AppTexts.DataSourcesIgdbConsoleSubtitle,
            ActionText = AppTexts.DataSourcesKeyPageAction,
            Info = [new InfoText(AppTexts.DataSourcesIgdbConsoleInfo), new InfoText(AppTexts.DataSourcesKeyPageMouse)],
        };
        _test = new ActionRowViewModel("igdb-test", AppTexts.DataSourcesTest, () => TestAsync().Forget(AppTexts.ErrorContextIgdb))
        {
            Group = Group,
            ActionText = AppTexts.DataSourcesTestAction,
            Info = [new InfoText(AppTexts.DataSourcesIgdbTestInfo), new InfoText(AppTexts.DataSourcesIgdbTestInfoTimeout)],
        };
        _delete = new ActionRowViewModel("igdb-delete", AppTexts.DataSourcesIgdbDelete, Delete)
        {
            Group = Group,
            ActionText = AppTexts.DataSourcesKeyDeleteAction,
            Info = [new InfoText(AppTexts.DataSourcesIgdbDeleteInfo)],
        };
        Refresh();
    }

    public IReadOnlyList<SettingsRowViewModel> Rows => [_clientId, _pasteClientId, _secret, _pasteSecret, _console, _test, _delete];

    /// <summary>The access or what Twitch and IGDB said about it changed; UI thread.</summary>
    public void Refresh()
    {
        var state = _access.State;

        // The fields beside them show the values' ends, so the subtitles say only the state, in the lamp's colour (#538).
        var lamp = _checking ? RowLamp.Resting : state switch
        {
            IgdbAccessState.Valid => RowLamp.Working,
            IgdbAccessState.Rejected => RowLamp.Problem,
            _ => RowLamp.Resting,
        };
        _clientId.Subtitle = _checking
            ? AppTexts.DataSourcesKeyChecking
            : state switch
            {
                IgdbAccessState.Valid => AppTexts.DataSourcesKeyConnected,
                IgdbAccessState.Rejected => AppTexts.DataSourcesIgdbRejected,
                IgdbAccessState.Unchecked => AppTexts.DataSourcesKeyUnchecked,
                _ when _access.ClientIdEnding is not null => AppTexts.DataSourcesIgdbSecretMissing,
                _ => AppTexts.DataSourcesIgdbNone,
            };
        _clientId.Severity = state == IgdbAccessState.Rejected ? HealthSeverity.Warning : null;
        _clientId.ShowLamp(_access.ClientIdEnding is null ? null : lamp);
        _clientId.Info =
        [
            new InfoText(AppTexts.DataSourcesIgdbInfo),
            new InfoText(AppTexts.DataSourcesIgdbHowTo),
            .. state == IgdbAccessState.Rejected
                ? [new InfoSection(AppTexts.DataSourcesNextStepsHeading, [new InfoText(AppTexts.DataSourcesIgdbRejectedInfo)])]
                : Array.Empty<InfoBlock>(),
            new InfoSection(AppTexts.DataSourcesSendsHeading, [new InfoText(_access.Persists ? AppTexts.DataSourcesIgdbSendsInfo : AppTexts.DataSourcesIgdbSendsInfoInMemory)]),
        ];
        _clientId.Refresh();
        _secret.Subtitle = _access.SecretEnding is null ? AppTexts.DataSourcesIgdbSecretNone
            : state == IgdbAccessState.Rejected ? AppTexts.DataSourcesIgdbRejected
            : state == IgdbAccessState.Valid ? AppTexts.DataSourcesKeyConnected
            : AppTexts.DataSourcesIgdbSecretStored;
        _secret.ShowLamp(_access.SecretEnding is null ? null : lamp);
        _secret.Refresh();

        var hasAccess = state != IgdbAccessState.None;
        var hasAny = hasAccess || _access.ClientIdEnding is not null || _access.SecretEnding is not null;
        _delete.IsDimmed = !hasAny;
        _delete.IsEnabled = hasAny && !_checking;
        _delete.Subtitle = hasAny ? AppTexts.DataSourcesIgdbDeleteSubtitle : AppTexts.DataSourcesIgdbNeedsAccess;
        _test.IsDimmed = !hasAccess;
        _test.IsEnabled = hasAccess && !_testing && !_checking;
        _test.IsWorking = _testing;
        _test.Subtitle = (hasAccess, _access.LastCheck) switch
        {
            (false, _) => AppTexts.DataSourcesIgdbNeedsAccess,
            (true, { Outcome: IgdbOutcome.Rejected } check) => AppTexts.DataSourcesIgdbTestRejected(Services.Formatting.Clock(check.At.ToOffset(_time.GetLocalNow().Offset))),
            (true, { Outcome: IgdbOutcome.NoConnection }) => AppTexts.DataSourcesTestNoConnection,
            (true, { } check) => AppTexts.DataSourcesIgdbTestOk(Services.Formatting.Clock(check.At.ToOffset(_time.GetLocalNow().Offset))),
            _ => AppTexts.DataSourcesIgdbTestNotYet,
        };
        _pasteClientId.IsEnabled = !_checking;
        _pasteSecret.IsEnabled = !_checking;
    }

    private ActionRowViewModel PasteRow(string key, bool isSecret) =>
        new(key, AppTexts.DataSourcesPaste, () => PasteAsync(isSecret).Forget(AppTexts.ErrorContextIgdb))
        {
            Group = Group,
            Indent = 1,
            Subtitle = isSecret ? AppTexts.DataSourcesIgdbPasteSecretSubtitle : AppTexts.DataSourcesIgdbPasteClientIdSubtitle,
            ActionText = AppTexts.DataSourcesPasteAction,
            Info = [new InfoText(AppTexts.DataSourcesIgdbPasteInfo), new InfoText(AppTexts.DataSourcesIgdbPasteInfoNoValue)],
        };

    /// <summary>
    /// A on "Client-ID" or "Client-Secret": the on-screen keyboard over an empty field; an empty field or B changes nothing,
    /// so a value is never lost by opening the field ("Zugang löschen" deletes it).
    /// </summary>
    private void Edit(TextRowViewModel row, bool isSecret)
    {
        if (row.Editor is not null || _checking)
        {
            return;
        }

        TextEditorViewModel? editor = null;
        editor = new TextEditorViewModel(
            string.Empty,
            text => text.Length == 0 || IgdbAccess.Normalize(text) is not null ? null : AppTexts.DataSourcesIgdbValueProblem,
            text =>
            {
                if (text.Length > 0)
                {
                    TakeAsync(text, isSecret, pasted: false).Forget(AppTexts.ErrorContextIgdb);
                }

                return true;
            },
            () => CloseEditor(row, editor!),
            _host.TakeKeyboard,
            _host.GiveBackKeyboard,
            char.IsAsciiLetterOrDigit);
        row.Editor = editor;
        _showEditor(editor);
    }

    private void CloseEditor(TextRowViewModel row, TextEditorViewModel editor)
    {
        row.Editor = null;
        _showEditor(null);
        if (editor.Rejection is { } reason)
        {
            _host.Toast(HealthSeverity.Warning, AppTexts.SettingNotApplied(row: row.Title, reason: reason));
        }

        editor.Dispose();
    }

    private async Task PasteAsync(bool isSecret)
    {
        if (_checking)
        {
            return;
        }

        if (IgdbAccess.Normalize(await _host.ReadClipboardAsync()) is not { } value)
        {
            _host.Toast(HealthSeverity.Warning, isSecret ? AppTexts.DataSourcesIgdbPasteNoSecret : AppTexts.DataSourcesIgdbPasteNoClientId);
            return;
        }

        await TakeAsync(value, isSecret, pasted: true);
    }

    /// <summary>Saves the value; with both there, checks the access. The first usable access loads what is missing at once.</summary>
    private async Task TakeAsync(string value, bool isSecret, bool pasted)
    {
        var first = !_access.IsUsable;
        _checking = true;
        Refresh();
        IgdbOutcome? outcome;
        try
        {
            outcome = isSecret ? await _access.SetSecretAsync(value) : await _access.SetClientIdAsync(value);
        }
        finally
        {
            _checking = false;
            Refresh();
        }

        AppLog.Write($"IGDB: {(isSecret ? "Client-Secret" : "Client-ID …" + IgdbClient.Ending(value))} {(pasted ? "eingefügt" : "eingegeben")}: {outcome?.ToString() ?? "Zugang unvollständig"}");
        switch (outcome)
        {
            case null:
                _host.Toast(HealthSeverity.Info, isSecret ? AppTexts.DataSourcesIgdbSecretSavedNeedsClientId : AppTexts.DataSourcesIgdbClientIdSavedNeedsSecret);
                break;
            case IgdbOutcome.Rejected:
                _host.Toast(HealthSeverity.Warning, AppTexts.DataSourcesIgdbRejectedToast);
                break;
            case IgdbOutcome.NoConnection:
                _host.Toast(HealthSeverity.Info, AppTexts.DataSourcesIgdbKeptNoConnection);
                LoadIfFirst(first);
                break;
            default:
                var loads = LoadIfFirst(first);
                _host.Toast(_access.IsStored ? HealthSeverity.Info : HealthSeverity.Warning, (_access.IsStored, loads) switch
                {
                    (false, _) => AppTexts.DataSourcesIgdbNotStored,
                    (true, true) => AppTexts.DataSourcesIgdbSavedLoading,
                    _ => AppTexts.DataSourcesIgdbSaved,
                });
                break;
        }
    }

    /// <summary>The first usable access asks IGDB at once for every game still missing its answer.</summary>
    private bool LoadIfFirst(bool first) => first && _access.IsUsable && _metadata.FetchMissing();

    private void Delete()
    {
        if (_access.State == IgdbAccessState.None && _access.ClientIdEnding is null && _access.SecretEnding is null)
        {
            return;
        }

        if (_access.Clear())
        {
            AppLog.Write("IGDB: Zugang gelöscht");
            _host.Toast(HealthSeverity.Info, AppTexts.DataSourcesIgdbDeleted);
        }
        else
        {
            AppLog.Write("IGDB: Zugang gelöscht, die Datei ließ sich nicht löschen");
            _host.Toast(HealthSeverity.Warning, AppTexts.DataSourcesIgdbNotDeleted);
        }

        Refresh();
    }

    private async Task TestAsync()
    {
        if (_testing || _checking)
        {
            return;
        }

        _testing = true;
        Refresh();
        IgdbOutcome? outcome;
        try
        {
            outcome = await _access.TestAsync();
        }
        finally
        {
            _testing = false;
            Refresh();
        }

        switch (outcome)
        {
            case IgdbOutcome.Rejected:
                _host.Toast(HealthSeverity.Warning, AppTexts.DataSourcesIgdbRejectedToast);
                break;
            case IgdbOutcome.NoConnection:
                // The access stays; no warning (#494).
                _host.Toast(HealthSeverity.Info, AppTexts.DataSourcesIgdbTestNoConnectionToast);
                break;
            case not null:
                _host.Toast(HealthSeverity.Info, AppTexts.DataSourcesIgdbTestOkToast);
                break;
        }
    }
}
