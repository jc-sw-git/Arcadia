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
    public const string Group = "LOGGING";
    public const string ListTitle = "Letzte Meldungen";
    public const string ListIntro = "Die letzten 50 Meldungen und Statusänderungen seit dem Start von Arcadia, die neueste oben. Alles davor steht in arcadia.log.";
    public const string EmptyText = "Noch keine Meldungen seit dem Start von Arcadia.";
    public const string AllHiddenText = "Alle Level sind ausgeblendet. Schalte Info, Warnungen oder Fehler an, um Meldungen zu sehen.";
    public const string OpensWithMouseText = "Die Datei öffnet sich in dem Programm, das Windows für .log-Dateien nutzt. Dafür geht der Mausmodus an.";
    public const string PanNotInstalledText = "Pan ist nicht installiert";
    public const string MissingText = "Die Datei gibt es noch nicht";

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

    /// <param name="openLog">Opens a log; it says itself when that fails.</param>
    public LoggingRows(RecentMessages messages, SettingsChanges changes, ILogFiles logs, Action<LogKind> openLog)
    {
        _messages = messages;
        _changes = changes;
        _logs = logs;
        _list = new InfoRowViewModel("logging-list", ListTitle) { Group = Group, ScrollText = "Blättern" };
        _info = Filter("logging-info", "Info", "Hinweise und Bestätigungen", s => s.ListsInfoMessages, (s, on) => s with { ListsInfoMessages = on });
        _warnings = Filter("logging-warnings", "Warnungen", "Etwas hat nicht geklappt und braucht dich", s => s.ListsWarnings, (s, on) => s with { ListsWarnings = on });
        _errors = Filter("logging-errors", "Fehler", "Fehlgeschlagen oder unerwartet", s => s.ListsErrors, (s, on) => s with { ListsErrors = on });
        _clear = new ActionRowViewModel("logging-clear", "Liste leeren", messages.Clear)
        {
            Group = Group,
            Subtitle = "arcadia.log behält alle Meldungen",
            ActionText = "Leeren",
            PanelTitle = ListPanelTitle,
            ScrollText = "Blättern",
        };
        _logRows =
        [
            (LogKind.Arcadia, LogRow(LogKind.Arcadia, "Arcadia-Log öffnen", "ARCADIA-LOG",
                "Alles, was Arcadia tut: Spielstarts, Tasten und Controller-Knöpfe, Fehler mit ihren Details. Diese Datei hilft bei der Fehlersuche.", openLog)),
            (LogKind.Viiper, LogRow(LogKind.Viiper, "VIIPER-Log öffnen", "VIIPER-LOG",
                "Was VIIPER tut, das Programm hinter den emulierten Controllern: welche Controller es anlegt und entfernt, und seine Fehler.", openLog)),
            (LogKind.Pan, LogRow(LogKind.Pan, "Pan-Log öffnen", "PAN-LOG",
                "Was Pan, der Controller-Wächter, tut: Treiber, versteckte Controller und Wiederherstellung. Die Datei gibt es erst, wenn Pan installiert ist.", openLog)),
        ];
        Refresh();
    }

    /// <summary>In the order the controller moves through them.</summary>
    public IReadOnlyList<SettingsRowViewModel> Rows => [_list, _info, _warnings, _errors, _clear, .. _logRows.Select(log => log.Row)];

    /// <summary>The list or a filter changed, or the page shows again (a log may have appeared meanwhile); UI thread.</summary>
    public void Refresh()
    {
        var all = _messages.Entries;
        var settings = _changes.Current;
        var shown = all.Where(entry => IsListed(entry.Level, settings)).ToList();

        _list.Subtitle = all.Count == 0 ? "Noch keine Meldungen"
            : shown.Count == all.Count ? $"{all.Count} seit dem Start von Arcadia"
            : $"{shown.Count} von {all.Count} angezeigt";
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

    private static bool IsListed(HealthSeverity level, AppSettings settings) => level switch
    {
        HealthSeverity.Info => settings.ListsInfoMessages,
        HealthSeverity.Warning => settings.ListsWarnings,
        _ => settings.ListsErrors,
    };

    private SwitchRowViewModel Filter(string key, string title, string subtitle, Func<AppSettings, bool> get, Func<AppSettings, bool, AppSettings> set) =>
        new(key, title, () => get(_changes.Current), on =>
        {
            _changes.Apply(s => set(s, on), $"Letzte Meldungen: {title}");
            Refresh();
        })
        {
            Group = Group,
            Indent = 1,
            Subtitle = subtitle,
            PanelTitle = ListPanelTitle,
            ScrollText = "Blättern",
        };

    private ActionRowViewModel LogRow(LogKind kind, string title, string panelTitle, string holds, Action<LogKind> openLog) =>
        new($"logging-log-{kind.ToString().ToLowerInvariant()}", title, () => openLog(kind))
        {
            Group = Group,
            ActionText = "Öffnen",
            PanelTitle = panelTitle,
            Info =
            [
                new InfoText(holds),
                new InfoText(OpensWithMouseText),
                new InfoSection("SPEICHERORT", [new InfoText(_logs.ShownPathOf(kind))]),
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
        LogKind.Arcadia => "Spielstarts, Tasten, Controller und Fehler",
        LogKind.Viiper => "Die emulierten Controller",
        _ => "Der Controller-Wächter",
    };
}
