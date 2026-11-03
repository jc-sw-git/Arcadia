using System.ComponentModel;
using Arcadia.App.Input;
using Arcadia.App.Messages;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Pan;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// #433 as the player meets it: the group "LOGGING" in "Über Arcadia" with the last messages, the level filter, "Liste
/// leeren" and the three logs, driven by the controller. Toasts come from real flows (a failed game start, Pan's
/// notices, an unexpected error); the logs and the shell are faked (<see cref="FakeLogFiles"/>).
/// </summary>
public sealed class RecentMessagesAndLogsTests
{
    private static readonly string[] GroupKeys =
        ["logging-list", "logging-info", "logging-warnings", "logging-errors", "logging-clear", "logging-log-arcadia", "logging-log-viiper", "logging-log-pan"];

    [AvaloniaFact]
    [Trait("TestCase", "T433-1")]
    public async Task Down_reaches_every_row_of_the_group_from_the_version_up_goes_back_and_B_leaves_from_each()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var about = OpenAbout(arcadia);
        var settings = arcadia.Main.Settings!;
        var version = about.Rows.Single(row => row.Key == "arcadia-version");
        Assert.Equal(GroupKeys, about.Rows.SkipWhile(row => row.Group != LoggingRows.Group).Select(row => row.Key));
        Assert.True(about.Rows.IndexOf(version) < about.Rows.IndexOf(Row(arcadia, "logging-list")), "LOGGING steht unter VERSION");

        var reached = new List<string>();
        while (settings.FocusedRow!.Key != "logging-log-pan")
        {
            arcadia.Press(UiAction.Down);
            reached.Add(settings.FocusedRow!.Key);
        }

        Assert.Equal(GroupKeys, reached.SkipWhile(key => key != "logging-list"));
        Assert.True(Row(arcadia, "logging-log-pan").IsDimmed, "Pan-Log öffnen ist ohne Pan ausgegraut und hat doch den Fokus");
        arcadia.Press(UiAction.Up, times: reached.Count);
        Assert.Same(version, settings.FocusedRow);
        arcadia.Press(UiAction.Back);

        foreach (var key in GroupKeys)
        {
            arcadia.Wait(TimeSpan.FromSeconds(1));
            OpenAbout(arcadia);
            arcadia.FocusRow(key);
            arcadia.Press(UiAction.Back);
            Assert.False(arcadia.Main.IsSettingsOpen, $"B auf {key}");
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T433-2")]
    public async Task The_footer_and_the_info_panel_follow_the_focused_row()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        OpenAbout(arcadia);

        var list = arcadia.FocusRow("logging-list");
        Assert.Equal("Blättern", list.ScrollText);
        Assert.False(list.ShowsActivateHint);
        Assert.Equal("LETZTE MELDUNGEN", list.InfoTitle);

        var info = arcadia.FocusRow("logging-info");
        Assert.True(info.CanChange);
        Assert.Equal("Blättern", info.ScrollText);
        Assert.Equal("LETZTE MELDUNGEN", info.InfoTitle);
        Assert.Equal(list.Info, info.Info);

        var clear = arcadia.FocusRow("logging-clear");
        Assert.False(clear.ShowsActivateHint, "Leeren fehlt, solange die Liste leer ist");
        Assert.Equal("LETZTE MELDUNGEN", clear.InfoTitle);
        Assert.Equal(list.Info, clear.Info);

        var log = arcadia.FocusRow("logging-log-arcadia");
        Assert.True(log.ShowsActivateHint);
        Assert.Equal("Öffnen", log.ActivateText);
        Assert.Equal("ARCADIA-LOG", log.InfoTitle);
        Assert.Contains(log.Info.OfType<InfoText>(), text => text.Text == LoggingRows.OpensWithMouseText);
        var where = Assert.Single(log.Info.OfType<InfoSection>(), section => section.Title == "SPEICHERORT");
        Assert.Contains(where.Blocks.OfType<InfoText>(), text => text.Text == @"C:\Beispiel\arcadia.log");

        var pan = arcadia.FocusRow("logging-log-pan");
        Assert.False(pan.ShowsActivateHint, "kein A auf einem ausgegrauten Log");

        // A toast reaches the list: now "Liste leeren" names its A.
        arcadia.Main.Toast.Show(HealthSeverity.Info, "„Sternenpfad“ wird gestartet …");
        await arcadia.WaitUntilAsync(() => Row(arcadia, "logging-clear").ShowsActivateHint, "„Liste leeren“ wird aktiv");
        Assert.Equal("Leeren", Row(arcadia, "logging-clear").ActivateText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T433-5")]
    public async Task A_missed_game_start_failure_is_on_top_of_the_list_with_its_symbol_time_chip_and_text()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var tile = arcadia.FocusedGame!;
        arcadia.Starts.Failure = new IOException("Die Datei ist gesperrt.");
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            arcadia.Press(UiAction.Accept);
            await arcadia.WaitUntilAsync(() => arcadia.Main.Messages.Entries.Count == attempt, $"Fehlstart {attempt} ist in der Liste");
            arcadia.Wait(ToastViewModel.Duration + TimeSpan.FromSeconds(1));
        }

        Assert.Null(arcadia.Main.Toast.Text);
        OpenAbout(arcadia);
        var list = arcadia.FocusRow("logging-list");

        Assert.Equal("3 seit dem Start von Arcadia", list.Subtitle);
        var top = Entries(list)[0];
        Assert.Equal(HealthSeverity.Error, top.Level);
        Assert.True(top.IsError);
        Assert.Equal("Meldung", top.KindText);
        Assert.Matches(@"^\d\d:\d\d$", top.TimeText);
        Assert.Equal($"„{tile.Title}“ konnte nicht gestartet werden: Die Datei ist gesperrt.", top.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T433-6")]
    [Trait("TestCase", "T433-15")]
    public async Task An_unexpected_error_while_the_list_shows_is_on_top_at_once_as_an_error()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        OpenAbout(arcadia);
        var list = arcadia.FocusRow("logging-list");
        Assert.Equal("Noch keine Meldungen", list.Subtitle);

        Services.AppErrors.Report(new InvalidOperationException("kaputt"), "Testablauf 433");
        await arcadia.WaitUntilAsync(() => list.Subtitle == "1 seit dem Start von Arcadia", "der Zähler zählt die neue Meldung");

        var top = Entries(list)[0];
        Assert.Equal(HealthSeverity.Error, top.Level);
        Assert.Contains("Testablauf 433", top.Text);
        Assert.Same(list, arcadia.Main.Settings!.FocusedRow);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T433-7")]
    public async Task After_the_start_and_a_restart_the_list_is_empty_but_arcadia_log_keeps_the_toasts()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        OpenAbout(arcadia);
        var list = arcadia.FocusRow("logging-list");
        Assert.Equal("Noch keine Meldungen", list.Subtitle);
        Assert.Contains(list.Info.OfType<InfoEmpty>(), empty => empty.Text == LoggingRows.EmptyText);

        var tile = arcadia.FocusedGame!;
        arcadia.Press(UiAction.Back);
        arcadia.Starts.Failure = new IOException($"Sperre {Guid.NewGuid():N}");
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Messages.Entries.Count == 1, "der Fehlstart ist in der Liste");
        var text = arcadia.Main.Messages.Entries[0].Text;

        using var restarted = await arcadia.RestartAsync();
        Assert.Empty(restarted.Main.Messages.Entries);
        Assert.Contains(text, ArcadiaDriver.Log);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T433-7")]
    [Trait("TestCase", "T433-17")]
    public async Task A_status_message_present_at_the_start_and_its_end_are_status_entries()
    {
        var steam = new FakeSteam { IsPanReady = false };
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam);
        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Main.Status.HasControllerStatus, "das Dreieck im Header");

        // Arcadia has no status "Steam läuft nicht" (PR #437): the missing Pan stands in for a warning that comes and goes.
        var appeared = Assert.Single(arcadia.Main.Messages.Entries);
        Assert.Equal((HealthSeverity.Warning, MessageKind.Status, "Wächterdienst Pan fehlt"), (appeared.Level, appeared.Kind, appeared.Text));

        var controller = OpenController(arcadia);
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:pan-missing"), "die Zeile „Wächterdienst Pan fehlt“");
        controller.Rows.Single(row => row.Key == "issue:pan-missing").Activate();
        await arcadia.WaitUntilAsync(() => steam.IsPanReady, "Pan installiert");
        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitUntilAsync(() => !arcadia.Main.Status.HasControllerStatus, "der Header ist wieder leer");
        var resolved = arcadia.Main.Messages.Entries[0];
        Assert.Equal((HealthSeverity.Warning, MessageKind.Status, "Behoben: Wächterdienst Pan fehlt"), (resolved.Level, resolved.Kind, resolved.Text));

        arcadia.Press(UiAction.Back);
        arcadia.Wait(TimeSpan.FromSeconds(1));
        OpenAbout(arcadia);
        var shown = Entries(arcadia.FocusRow("logging-list"))[0];
        Assert.Equal("Status", shown.KindText);
        Assert.False(shown.IsError || shown.IsInfo, "das gelbe Dreieck");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T433-8")]
    public async Task With_fifty_entries_a_new_toast_drops_the_oldest()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        for (var i = 1; i <= RecentMessages.Capacity; i++)
        {
            arcadia.Main.Toast.Show(HealthSeverity.Info, $"Meldung {i}");
        }

        OpenAbout(arcadia);
        var list = arcadia.FocusRow("logging-list");
        Assert.Equal("50 seit dem Start von Arcadia", list.Subtitle);

        arcadia.Main.Toast.Show(HealthSeverity.Info, "Meldung 51");
        await arcadia.WaitUntilAsync(() => Entries(list)[0].Text == "Meldung 51", "die neue Meldung oben");
        Assert.Equal(50, Entries(list).Count);
        Assert.Equal("Meldung 2", Entries(list)[^1].Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T433-9")]
    public async Task Pan_notices_from_other_threads_all_reach_the_list_in_their_order()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        OpenAbout(arcadia);
        var list = arcadia.FocusRow("logging-list");

        await Task.WhenAll(Enumerable.Range(0, 4).Select(thread => Task.Run(() =>
        {
            for (var i = 0; i < 10; i++)
            {
                arcadia.PanNotices.Show(HealthSeverity.Warning, $"Faden {thread}: {i}");
            }
        })));
        await arcadia.WaitUntilAsync(() => list.Subtitle == "40 seit dem Start von Arcadia", "alle 40 Hinweise");

        var oldestFirst = Entries(list).Reverse().Select(entry => entry.Text).ToList();
        for (var thread = 0; thread < 4; thread++)
        {
            Assert.Equal(Enumerable.Range(0, 10).Select(i => $"Faden {thread}: {i}"), oldestFirst.Where(text => text.StartsWith($"Faden {thread}:", StringComparison.Ordinal)));
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T433-10")]
    [Trait("TestCase", "T433-12")]
    public async Task Each_level_switch_hides_its_entries_at_once_with_either_arrow_and_all_off_says_so()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        ShowOneOfEachLevel(arcadia);
        OpenAbout(arcadia);
        var list = Row(arcadia, "logging-list");

        foreach (var (key, level) in new[] { ("logging-info", HealthSeverity.Info), ("logging-warnings", HealthSeverity.Warning), ("logging-errors", HealthSeverity.Error) })
        {
            arcadia.FocusRow(key);
            arcadia.Press(UiAction.Left);
            Assert.Equal("2 von 3 angezeigt", list.Subtitle);
            Assert.DoesNotContain(Entries(list), entry => entry.Level == level);
            arcadia.Press(UiAction.Left);
            Assert.Equal("3 seit dem Start von Arcadia", list.Subtitle);
            arcadia.Press(UiAction.Right);
            Assert.Equal("2 von 3 angezeigt", list.Subtitle);
            arcadia.Press(UiAction.Right);
            Assert.Equal("3 seit dem Start von Arcadia", list.Subtitle);
        }

        foreach (var key in new[] { "logging-info", "logging-warnings", "logging-errors" })
        {
            arcadia.FocusRow(key);
            arcadia.Press(UiAction.Left);
        }

        Assert.Equal("0 von 3 angezeigt", list.Subtitle);
        Assert.Equal(LoggingRows.AllHiddenText, Assert.Single(list.Info.OfType<InfoEmpty>()).Text);

        arcadia.FocusRow("logging-clear");
        arcadia.Press(UiAction.Accept);
        Assert.Equal(LoggingRows.EmptyText, Assert.Single(list.Info.OfType<InfoEmpty>()).Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T433-11")]
    public async Task Switched_off_levels_are_still_off_after_a_restart()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        OpenAbout(arcadia);
        arcadia.FocusRow("logging-info");
        arcadia.Press(UiAction.Left);
        arcadia.FocusRow("logging-errors");
        arcadia.Press(UiAction.Right);

        using var restarted = await arcadia.RestartAsync();
        OpenAbout(restarted);
        Assert.Equal(["Aus", "An", "Aus"], new[] { "logging-info", "logging-warnings", "logging-errors" }
            .Select(key => Assert.IsType<SwitchRowViewModel>(Row(restarted, key)).ValueText));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T433-13")]
    public async Task The_filter_only_hides_a_hidden_entry_drops_out_like_a_shown_one()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        for (var i = 1; i <= RecentMessages.Capacity; i++)
        {
            arcadia.Main.Toast.Show(i <= 10 ? HealthSeverity.Info : HealthSeverity.Warning, $"Meldung {i}");
        }

        OpenAbout(arcadia);
        var list = Row(arcadia, "logging-list");
        arcadia.FocusRow("logging-info");
        arcadia.Press(UiAction.Left);
        Assert.Equal("40 von 50 angezeigt", list.Subtitle);

        arcadia.Main.Toast.Show(HealthSeverity.Error, "„Nebelwald“ konnte nicht gestartet werden");
        await arcadia.WaitUntilAsync(() => list.Subtitle == "41 von 50 angezeigt", "der Fehler kommt dazu, die älteste Info fällt heraus");

        arcadia.Press(UiAction.Left);
        Assert.Equal("50 seit dem Start von Arcadia", list.Subtitle);
        Assert.Equal(Enumerable.Range(2, 9).Select(i => $"Meldung {i}"), Entries(list).Where(entry => entry.IsInfo).Select(entry => entry.Text).Reverse());
    }

    [AvaloniaFact]
    [Trait("TestCase", "T433-14")]
    public async Task Liste_leeren_empties_the_list_keeps_the_switches_and_is_greyed_out_when_empty()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        ShowOneOfEachLevel(arcadia);
        OpenAbout(arcadia);
        arcadia.FocusRow("logging-warnings");
        arcadia.Press(UiAction.Left);
        var clear = arcadia.FocusRow("logging-clear");
        Assert.True(clear.CanActivate);

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => Row(arcadia, "logging-list").Subtitle == "Noch keine Meldungen", "die Liste ist leer");
        Assert.Equal(LoggingRows.EmptyText, Assert.Single(clear.Info.OfType<InfoEmpty>()).Text);
        Assert.Equal("Aus", Assert.IsType<SwitchRowViewModel>(Row(arcadia, "logging-warnings")).ValueText);
        Assert.True(clear.IsDimmed);
        Assert.False(clear.CanActivate);

        arcadia.Press(UiAction.Accept);
        Assert.Same(clear, arcadia.Main.Settings!.FocusedRow);
        Assert.Empty(arcadia.Main.Messages.Entries);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T433-15")]
    [Trait("TestCase", "T433-16")]
    public async Task Pan_s_notices_are_entries_with_their_level_also_behind_a_game()
    {
        using var arcadia = await StartWithPanAsync();
        Assert.Equal(PanEmulationState.Running, arcadia.ForGames.Status.State);

        arcadia.Pan.HidingHolds = PanReason.HidingLost;
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(PanEmulation.PausedText, arcadia.Main.Toast.Text);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Messages.Entries.Any(entry => entry.Text == PanEmulation.PausedText), "„pausiert“ ist in der Liste");
        Assert.Equal(HealthSeverity.Warning, arcadia.Main.Messages.Entries.First(entry => entry.Text == PanEmulation.PausedText).Level);

        // Behind a game the notice is drawn over the game, and the list keeps it.
        arcadia.IsArcadiaInFront = false;
        arcadia.Pan.HidingHolds = PanReason.None;
        arcadia.Time.Advance(PanEmulation.HideRetry);
        await arcadia.TickSteamControllerAsync();
        await arcadia.WaitUntilAsync(() => arcadia.OverlayNotices.Contains(PanEmulation.RunningAgainText), "der Hinweis über dem Spiel");
        var again = arcadia.Main.Messages.Entries.First(entry => entry.Text == PanEmulation.RunningAgainText);
        Assert.Equal((HealthSeverity.Info, MessageKind.Toast), (again.Level, again.Kind));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T433-17")]
    [Trait("TestCase", "T433-18")]
    public async Task Pan_s_status_turning_into_an_error_adds_one_entry_and_with_only_errors_on_its_end_still_shows()
    {
        using var arcadia = await StartWithPanAsync();
        OpenAbout(arcadia);
        arcadia.FocusRow("logging-info");
        arcadia.Press(UiAction.Left);
        arcadia.FocusRow("logging-warnings");
        arcadia.Press(UiAction.Left);
        arcadia.Press(UiAction.Back);

        arcadia.SteamControllerCopy.FailAttach = "VIIPER antwortet nicht";
        arcadia.SteamControllerCopy.Current!.Lose();
        await arcadia.TickSteamControllerAsync();
        await arcadia.WaitUntilAsync(() => Statuses(arcadia).Count == 1, "„Pan läuft nicht“ als Warnung");
        Assert.Equal(HealthSeverity.Warning, Statuses(arcadia)[0].Level);

        // Further failures of the same kind change nothing the header shows: no entry, also over minutes.
        await arcadia.TickSteamControllerAsync();
        await arcadia.TickSteamControllerAsync();
        Assert.Single(Statuses(arcadia));
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(PanEmulationState.Stopped, arcadia.ForGames.Status.State);
        await arcadia.WaitUntilAsync(() => Statuses(arcadia).Count == 2, "dieselbe Statusmeldung jetzt als Fehler");
        Assert.Equal((HealthSeverity.Error, StatusBarViewModel.PanNotRunningText), (Statuses(arcadia)[0].Level, Statuses(arcadia)[0].Text));
        for (var minute = 0; minute < 5; minute++)
        {
            arcadia.Time.Advance(TimeSpan.FromMinutes(1));
            await arcadia.TickControllerSupportAsync();
        }

        Assert.Equal(2, Statuses(arcadia).Count);

        arcadia.SteamControllerCopy.FailAttach = null;
        arcadia.Press(UiAction.Back);
        var controller = OpenController(arcadia);
        controller.Rows.Single(r => r.Key == "pan:stopped").Activate();
        await arcadia.TickSteamControllerAsync();
        await arcadia.WaitUntilAsync(() => Statuses(arcadia).Count == 3, "„Behoben: Pan läuft nicht“");
        Assert.Equal((HealthSeverity.Error, StatusMessageChanges.ResolvedPrefix + StatusBarViewModel.PanNotRunningText), (Statuses(arcadia)[0].Level, Statuses(arcadia)[0].Text));

        // Only "Fehler" on: the error and its end show, the warning doesn't.
        arcadia.Press(UiAction.Back);
        OpenAbout(arcadia);
        var shown = Entries(Row(arcadia, "logging-list")).Where(entry => !entry.IsToast).Select(entry => entry.Text).ToList();
        Assert.Equal([StatusMessageChanges.ResolvedPrefix + StatusBarViewModel.PanNotRunningText, StatusBarViewModel.PanNotRunningText], shown);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T433-20")]
    public async Task No_text_of_the_group_says_Toast_and_the_chips_say_Meldung_and_Status()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Main.Toast.Show(HealthSeverity.Info, "„Sternenpfad“ wird gestartet …");
        arcadia.Main.Messages.Add(HealthSeverity.Warning, MessageKind.Status, "Wächterdienst Pan fehlt");
        var about = OpenAbout(arcadia);

        var texts = about.Rows.Where(row => row.Group == LoggingRows.Group)
            .SelectMany(row => new[] { row.Title, row.Subtitle, row.InfoTitle, row.ActivateText, row.ScrollText }.Concat(TextsOf(row.Info)))
            .Where(text => !string.IsNullOrEmpty(text))
            .ToList();
        Assert.DoesNotContain(texts, text => text!.Contains("Toast", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["Status", "Meldung"], Entries(Row(arcadia, "logging-list")).Select(entry => entry.KindText));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T433-21")]
    public async Task A_on_a_log_row_opens_that_log_and_by_controller_asks_for_the_mouse_mode()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Logs.States[LogKind.Pan] = LogFileState.Present;
        var byController = 0;
        arcadia.Main.LogOpenedByController += (_, _) => byController++;
        OpenAbout(arcadia);

        foreach (var (key, kind) in new[] { ("logging-log-arcadia", LogKind.Arcadia), ("logging-log-viiper", LogKind.Viiper), ("logging-log-pan", LogKind.Pan) })
        {
            arcadia.FocusRow(key);
            arcadia.Press(UiAction.Accept);
            await arcadia.WaitUntilAsync(() => arcadia.Logs.Opened.LastOrDefault() == kind && byController == arcadia.Logs.Opened.Count, $"{kind} geöffnet");
        }

        Assert.Equal([LogKind.Arcadia, LogKind.Viiper, LogKind.Pan], arcadia.Logs.Opened);
        Assert.Equal(3, byController);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T433-23")]
    public async Task A_missing_log_is_greyed_out_with_its_reason_A_does_nothing_and_Pan_installed_meanwhile_makes_it_active()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Logs.States[LogKind.Viiper] = LogFileState.Missing;
        OpenAbout(arcadia);

        var pan = arcadia.FocusRow("logging-log-pan");
        Assert.Equal((true, LoggingRows.PanNotInstalledText), (pan.IsDimmed, pan.Subtitle));
        Assert.Contains(pan.Info.OfType<InfoSection>(), section => section.Title == "SPEICHERORT");
        arcadia.Press(UiAction.Accept);
        var viiper = arcadia.FocusRow("logging-log-viiper");
        Assert.Equal((true, LoggingRows.MissingText), (viiper.IsDimmed, viiper.Subtitle));
        arcadia.Press(UiAction.Accept);
        arcadia.Wait(TimeSpan.FromSeconds(1));
        Assert.Empty(arcadia.Logs.Opened);

        // Pan installed while Arcadia runs: back in "Über Arcadia" the row is active.
        arcadia.Logs.States[LogKind.Pan] = LogFileState.Present;
        arcadia.Press(UiAction.Back);
        OpenAbout(arcadia);
        Assert.False(Row(arcadia, "logging-log-pan").IsDimmed);
        Assert.True(Row(arcadia, "logging-log-pan").CanActivate);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T433-23")]
    public async Task Pan_installed_on_the_Controller_page_makes_its_log_active_when_RB_leads_back_to_Ueber_Arcadia()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        OpenAbout(arcadia);
        Assert.True(Row(arcadia, "logging-log-pan").IsDimmed);

        // The same settings page: RB moves between the categories it already built.
        arcadia.Press(UiAction.PreviousTab);
        Assert.Equal("Controller", arcadia.Main.Settings!.Selected.Name);
        arcadia.Logs.States[LogKind.Pan] = LogFileState.Present;
        arcadia.Press(UiAction.NextTab);

        Assert.Equal("Über Arcadia", arcadia.Main.Settings!.Selected.Name);
        Assert.False(Row(arcadia, "logging-log-pan").IsDimmed);
        Assert.True(Row(arcadia, "logging-log-pan").CanActivate);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T433-24")]
    public async Task A_log_Windows_cannot_open_is_an_error_toast_and_entry_and_asks_for_no_mouse_mode()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Logs.Failure = new Win32Exception(1155, "Keine Anwendung ist mit dieser Datei verknüpft.");
        var byController = 0;
        arcadia.Main.LogOpenedByController += (_, _) => byController++;
        OpenAbout(arcadia);
        arcadia.FocusRow("logging-log-arcadia");

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text?.StartsWith("arcadia.log konnte nicht geöffnet werden", StringComparison.Ordinal) == true, "der Fehler als Meldung");

        var entry = arcadia.Main.Messages.Entries[0];
        Assert.Equal((HealthSeverity.Error, MessageKind.Toast), (entry.Level, entry.Kind));
        Assert.Equal(0, byController);
        Assert.True(arcadia.Main.IsSettingsOpen);
    }

    private static async Task<ArcadiaDriver> StartWithPanAsync()
    {
        var arcadia = await ArcadiaDriver.StartAsync(
            [new(KnownLaunchers.Steam, 3), new(KnownLaunchers.Gog, 3)],
            new AppSettings { PanEnabled = true, ActiveLaunchers = ["steam", "gog"] },
            new FakeSteam());
        arcadia.SteamControllerCopy.CanCopy = true;
        arcadia.SteamControllerCopy.PuckPresent = true;
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        return arcadia;
    }

    /// <summary>≡, then LB to the last category.</summary>
    private static SettingsCategoryViewModel OpenAbout(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings).Press(UiAction.PreviousTab);
        var about = arcadia.Main.Settings!.Selected;
        Assert.Equal("Über Arcadia", about.Name);
        return about;
    }

    private static ControllerSettingsViewModel OpenController(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        return Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
    }

    private static void ShowOneOfEachLevel(ArcadiaDriver arcadia)
    {
        arcadia.Main.Toast.Show(HealthSeverity.Info, "„Sternenpfad“ wird gestartet …");
        arcadia.Main.Toast.Show(HealthSeverity.Warning, "Ein anderes Spiel wird gerade gestartet – „Nebelwald“ bitte gleich noch einmal starten.");
        arcadia.Main.Toast.Show(HealthSeverity.Error, "„Nebelwald“ konnte nicht gestartet werden: Die Datei ist gesperrt.");
    }

    private static SettingsRowViewModel Row(ArcadiaDriver arcadia, string key) => arcadia.Main.Settings!.Selected.Rows.Single(row => row.Key == key);

    private static IReadOnlyList<InfoMessage> Entries(SettingsRowViewModel list) => [.. list.Info.OfType<InfoMessage>()];

    private static IReadOnlyList<RecentMessage> Statuses(ArcadiaDriver arcadia) => [.. arcadia.Main.Messages.Entries.Where(entry => entry.Kind == MessageKind.Status)];

    private static IEnumerable<string?> TextsOf(IEnumerable<InfoBlock> blocks) => blocks.SelectMany(block => block switch
    {
        InfoText text => [text.Text],
        InfoSection section => new[] { section.Title }.Concat(TextsOf(section.Blocks)),
        InfoMessage message => [message.KindText, message.Text],
        InfoEmpty empty => [empty.Text],
        _ => [],
    });
}
