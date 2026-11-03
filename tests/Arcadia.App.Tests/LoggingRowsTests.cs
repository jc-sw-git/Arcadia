using System.ComponentModel;
using Arcadia.App.Messages;
using Arcadia.App.Settings;
using Arcadia.ControllerSupport;
using Arcadia.Core.Settings;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.App.Tests;

/// <summary>#433: the rows of the group "LOGGING" as the view models build them, without a window.</summary>
public sealed class LoggingRowsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("arcadia-logging-").FullName;
    private readonly RecentMessages _messages = new(new FakeTimeProvider(new DateTimeOffset(2026, 11, 3, 10, 41, 0, TimeSpan.Zero)));
    private readonly FakeLogFiles _logs = new();
    private readonly List<LogKind> _opened = [];
    private readonly SettingsService _settings;
    private readonly LoggingRows _rows;

    public LoggingRowsTests()
    {
        _settings = new SettingsService(new SettingsStore(Path.Combine(_folder, "settings.json")));
        _rows = new LoggingRows(_messages, new SettingsChanges(_settings), _logs, _opened.Add);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    [Trait("TestCase", "T433-1")]
    public void The_group_has_its_rows_in_order_with_the_switches_indented()
    {
        Assert.Equal(["Letzte Meldungen", "Info", "Warnungen", "Fehler", "Liste leeren", "Arcadia-Log öffnen", "VIIPER-Log öffnen", "Pan-Log öffnen"],
            _rows.Rows.Select(row => row.Title));
        Assert.All(_rows.Rows, row => Assert.Equal(LoggingRows.Group, row.Group));
        Assert.Equal(["Info", "Warnungen", "Fehler"], _rows.Rows.Where(row => row.IsIndented).Select(row => row.Title));
    }

    [Fact]
    [Trait("TestCase", "T433-10")]
    public void A_level_switched_off_hides_its_entries_at_once_and_the_subtitle_counts_both()
    {
        _messages.Add(HealthSeverity.Info, MessageKind.Toast, "„Sternenpfad“ wird gestartet …");
        _messages.Add(HealthSeverity.Warning, MessageKind.Status, "Wächterdienst Pan fehlt");
        _messages.Add(HealthSeverity.Error, MessageKind.Toast, "„Nebelwald“ konnte nicht gestartet werden");
        _rows.Refresh();
        Assert.Equal("3 seit dem Start von Arcadia", Row("logging-list").Subtitle);

        Row("logging-info").Change(-1);

        Assert.False(_settings.Current.ListsInfoMessages);
        Assert.Equal("2 von 3 angezeigt", Row("logging-list").Subtitle);
        Assert.Equal(["„Nebelwald“ konnte nicht gestartet werden", "Wächterdienst Pan fehlt"], ShownEntries());

        Row("logging-info").Change(-1);
        Assert.True(_settings.Current.ListsInfoMessages);
        Assert.Equal(3, ShownEntries().Count);
    }

    [Fact]
    [Trait("TestCase", "T433-12")]
    public void All_levels_off_says_so_but_an_empty_list_says_it_is_empty()
    {
        foreach (var key in new[] { "logging-info", "logging-warnings", "logging-errors" })
        {
            Row(key).Change(1);
        }

        Assert.Contains(Row("logging-list").Info, block => block == new InfoEmpty(LoggingRows.EmptyText));

        _messages.Add(HealthSeverity.Error, MessageKind.Toast, "Fehler");
        _rows.Refresh();
        Assert.Contains(Row("logging-list").Info, block => block == new InfoEmpty(LoggingRows.AllHiddenText));
    }

    [Fact]
    [Trait("TestCase", "T433-14")]
    public void Clearing_is_greyed_out_while_the_list_is_empty_and_keeps_the_filter()
    {
        var clear = (ActionRowViewModel)Row("logging-clear");
        Assert.False(clear.CanActivate);
        Assert.True(clear.IsDimmed);

        Row("logging-warnings").Change(1);
        _messages.Add(HealthSeverity.Info, MessageKind.Toast, "Info");
        _rows.Refresh();
        Assert.True(clear.CanActivate);

        clear.Activate();
        _rows.Refresh();

        Assert.Empty(_messages.Entries);
        Assert.Equal("Noch keine Meldungen", Row("logging-list").Subtitle);
        Assert.False(_settings.Current.ListsWarnings);
        Assert.False(clear.CanActivate);
    }

    [Fact]
    [Trait("TestCase", "T433-23")]
    public void A_missing_log_is_greyed_out_with_its_reason_and_comes_back_when_the_page_shows_again()
    {
        _logs.States[LogKind.Viiper] = LogFileState.Missing;
        _rows.Refresh();
        var pan = (ActionRowViewModel)Row("logging-log-pan");
        var viiper = (ActionRowViewModel)Row("logging-log-viiper");

        Assert.Equal((false, true, LoggingRows.PanNotInstalledText), (pan.CanActivate, pan.IsDimmed, pan.Subtitle));
        Assert.Equal((false, true, LoggingRows.MissingText), (viiper.CanActivate, viiper.IsDimmed, viiper.Subtitle));
        Assert.Contains(pan.Info, block => block is InfoSection { Title: "SPEICHERORT" });

        _logs.States[LogKind.Pan] = LogFileState.Present;
        _rows.Refresh();
        Assert.True(pan.CanActivate);
        pan.Activate();
        Assert.Equal([LogKind.Pan], _opened);
    }

    [Fact]
    [Trait("TestCase", "T433-11")]
    public void A_settings_file_written_before_the_filter_lists_every_level()
    {
        var path = Path.Combine(_folder, "old.json");
        File.WriteAllText(path, """{ "PanEnabled": false }""");

        var settings = new SettingsService(new SettingsStore(path)).Current;

        Assert.Equal((true, true, true), (settings.ListsInfoMessages, settings.ListsWarnings, settings.ListsErrors));
    }

    [Fact]
    [Trait("TestCase", "T433-25")]
    public void Only_the_three_fixed_logs_have_a_path()
    {
        var logs = new LogFiles();

        Assert.EndsWith("arcadia.log", logs.ShownPathOf(LogKind.Arcadia), StringComparison.Ordinal);
        Assert.EndsWith("viiper.log", logs.ShownPathOf(LogKind.Viiper), StringComparison.Ordinal);
        Assert.EndsWith("pan.log", logs.ShownPathOf(LogKind.Pan), StringComparison.Ordinal);
        Assert.Throws<InvalidEnumArgumentException>(() => logs.Open((LogKind)42));
    }

    private SettingsRowViewModel Row(string key) => _rows.Rows.Single(row => row.Key == key);

    private List<string> ShownEntries() => [.. Row("logging-list").Info.OfType<InfoMessage>().Select(entry => entry.Text)];
}
