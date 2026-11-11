using Arcadia.App.Messages;
using Arcadia.ControllerSupport;
using Arcadia.Core.Settings;

namespace Arcadia.App.Settings;

/// <summary>
/// The group "LOGGING" of "Über Arcadia" (#433): the last messages with a filter per level, "Liste leeren" and the three
/// logs. The list is in the info panel of "Letzte Meldungen", its switches and "Liste leeren"; RS scrolls it.
/// </summary>
public sealed class LoggingRows
{
    public static string Group => AppTexts.LoggingGroup;
    public static string ListTitle => AppTexts.LoggingListTitle;
    public static string ListIntro => AppTexts.LoggingListIntro;
    public static string EmptyText => AppTexts.LoggingEmpty;
    public static string AllHiddenText => AppTexts.LoggingAllHidden;
    public static string OpensWithMouseText => AppTexts.LoggingOpensWithMouse;
    public static string PanNotInstalledText => AppTexts.LoggingPanNotInstalled;
    public static string MissingText => AppTexts.LoggingFileMissing;

    private static readonly string ListPanelTitle = ListTitle.ToUpperInvariant();

    private readonly RecentMessages _messages;
    private readonly SettingsChanges _changes;
    private readonly ILogFiles _logs;
    private readonly InfoRowViewModel _list;
    private readonly SwitchRowViewModel _info;
    private readonly SwitchRowViewModel _warnings;
    private readonly SwitchRowViewModel _errors;
    private readonly ActionRowViewModel _clear;
    private readonly IReadOnlyList<(LogKind Kind, ActionRowViewModel Row)> _logRows;
    private long _repeatsShown;

    /// <param name="openLog">Opens a log; it says itself when that fails.</param>
    public LoggingRows(RecentMessages messages, SettingsChanges changes, ILogFiles logs, Action<LogKind> openLog)
    {
        _messages = messages;
        _changes = changes;
        _logs = logs;
        _list = new InfoRowViewModel("logging-list", ListTitle) { Group = Group, ScrollText = AppTexts.Browse };
        _info = Filter("logging-info", AppTexts.LevelInfo, AppTexts.LoggingInfoSubtitle, s => s.ListsInfoMessages, (s, on) => s with { ListsInfoMessages = on });
        _warnings = Filter("logging-warnings", AppTexts.LoggingWarnings, AppTexts.LoggingWarningSubtitle, s => s.ListsWarnings, (s, on) => s with { ListsWarnings = on });
        _errors = Filter("logging-errors", AppTexts.LevelError, AppTexts.LoggingErrorSubtitle, s => s.ListsErrors, (s, on) => s with { ListsErrors = on });
        _clear = new ActionRowViewModel("logging-clear", AppTexts.LoggingClearList, messages.Clear)
        {
            Group = Group,
            Subtitle = AppTexts.LoggingKeepsAll,
            ActionText = AppTexts.Clear,
            PanelTitle = ListPanelTitle,
            ScrollText = AppTexts.Browse,
        };
        _logRows =
        [
            (LogKind.Arcadia, LogRow(LogKind.Arcadia, AppTexts.LoggingOpenArcadiaLog, AppTexts.ArcadiaLogHeading,
                AppTexts.LoggingArcadiaLogInfo, openLog)),
            (LogKind.Viiper, LogRow(LogKind.Viiper, AppTexts.LoggingOpenViiperLog, AppTexts.ViiperLogHeading,
                AppTexts.LoggingViiperLogInfo, openLog)),
            (LogKind.Pan, LogRow(LogKind.Pan, AppTexts.LoggingOpenPanLog, AppTexts.PanLogHeading,
                AppTexts.LoggingPanLogInfo, openLog)),
        ];
        Refresh();
    }

    /// <summary>In the order the controller moves through them.</summary>
    public IReadOnlyList<SettingsRowViewModel> Rows => [_list, _info, _warnings, _errors, _clear, .. _logRows.Select(log => log.Row)];

    /// <summary>
    /// An error came again since the list was built ("×N" grows, #399): only then the list is built anew, so the panel
    /// doesn't jump every second. Reading the counts allocates nothing per entry.
    /// </summary>
    public void RefreshIfRepeated()
    {
        if (RepeatsOf(_messages.Entries) != _repeatsShown)
        {
            Refresh();
        }
    }

    /// <summary>The list or a filter changed, or the page shows again (a log may have appeared meanwhile); UI thread.</summary>
    public void Refresh()
    {
        var all = _messages.Entries;
        _repeatsShown = RepeatsOf(all);
        var settings = _changes.Current;
        var shown = all.Where(entry => IsListed(entry.Level, settings)).ToList();

        _list.Subtitle = all.Count == 0 ? AppTexts.LoggingNoMessagesYet
            : shown.Count == all.Count ? AppTexts.LoggingCountSinceStart(all.Count)
            : AppTexts.LoggingShownOf(shown: shown.Count, total: all.Count);
        IReadOnlyList<InfoBlock> list =
        [
            new InfoText(ListIntro),
            .. all.Count == 0 ? [new InfoEmpty(EmptyText)]
                : shown.Count == 0 ? [new InfoEmpty(AllHiddenText)]
                : shown.Select(entry => (InfoBlock)InfoMessage.Of(entry)),
        ];
        foreach (var row in new SettingsRowViewModel[] { _list, _info, _warnings, _errors, _clear })
        {
            row.Info = list;
        }

        foreach (var filter in new[] { _info, _warnings, _errors })
        {
            filter.Refresh();
        }

        // Greyed out but still focusable: A does nothing and the footer names nothing.
        _clear.IsEnabled = all.Count > 0;
        _clear.IsDimmed = all.Count == 0;

        foreach (var (kind, row) in _logRows)
        {
            ShowLogState(kind, row);
        }
    }

    private static long RepeatsOf(IReadOnlyList<RecentMessage> entries)
    {
        long sum = 0;
        foreach (var entry in entries)
        {
            sum += entry.Repeats?.Repeats ?? 0;
        }

        return sum;
    }

    private static bool IsListed(HealthSeverity level, AppSettings settings) => level switch
    {
        HealthSeverity.Info => settings.ListsInfoMessages,
        HealthSeverity.Warning => settings.ListsWarnings,
        _ => settings.ListsErrors,
    };

    private SwitchRowViewModel Filter(string key, string title, string subtitle, Func<AppSettings, bool> get, Func<AppSettings, bool, AppSettings> set) =>
        new(key, title, () => get(_changes.Current), on =>
        {
            _changes.Apply(s => set(s, on), AppTexts.LoggingRecentMessagesOf(title));
            Refresh();
        })
        {
            Group = Group,
            Indent = 1,
            Subtitle = subtitle,
            PanelTitle = ListPanelTitle,
            ScrollText = AppTexts.Browse,
        };

    private ActionRowViewModel LogRow(LogKind kind, string title, string panelTitle, string holds, Action<LogKind> openLog) =>
        new($"logging-log-{kind.ToString().ToLowerInvariant()}", title, () => openLog(kind))
        {
            Group = Group,
            ActionText = AppTexts.Open,
            PanelTitle = panelTitle,
            Info =
            [
                new InfoText(holds),
                new InfoText(OpensWithMouseText),
                new InfoSection(AppTexts.LocationCaption, [new InfoText(_logs.ShownPathOf(kind))]),
            ],
        };

    private void ShowLogState(LogKind kind, ActionRowViewModel row)
    {
        var state = _logs.StateOf(kind);
        var present = state == LogFileState.Present;
        row.IsEnabled = present;
        row.IsDimmed = !present;
        row.Subtitle = state switch
        {
            LogFileState.PanNotInstalled => PanNotInstalledText,
            LogFileState.Missing => MissingText,
            _ => SubtitleOf(kind),
        };
    }

    private static string SubtitleOf(LogKind kind) => kind switch
    {
        LogKind.Arcadia => AppTexts.LoggingArcadiaLogSubtitle,
        LogKind.Viiper => AppTexts.LoggingViiperLogSubtitle,
        _ => AppTexts.LoggingPanLogSubtitle,
    };
}
