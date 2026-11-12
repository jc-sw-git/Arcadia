using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Input;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Arcadia.Tests.Sdl;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The quick selection in "Übersicht" and the dialog "Übersicht anpassen" (#500): the entries act as their rows do, the
/// selection is kept per game, "Für alle Spiele übernehmen" replaces every game's after a question.
/// </summary>
public sealed class QuickSelectionTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private static readonly LauncherSpec[] SteamAndGog = [new(KnownLaunchers.Steam, 3), new(KnownLaunchers.Gog, 3)];

    private static readonly GameActionKind[] Defaults = [GameActionKind.ControllerMode, GameActionKind.MouseMode, GameActionKind.Artwork, GameActionKind.Uninstall];

    [AvaloniaFact]
    [Trait("TestCase", "T500-1")]
    public async Task A_game_never_changed_shows_the_default_entries_with_their_values_and_Uebersicht_anpassen()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;

        Assert.True(page.OpenArea.IsOverview);
        Assert.Equal(Defaults, page.QuickKinds());
        Assert.Equal(page.Row(GameActionKind.ControllerMode).ValueText, page.Overview.Rows[0].ValueText);
        Assert.Equal([true, true, false, false], page.Overview.Rows.Select(entry => entry.IsChoice));
        Assert.False(page.Overview.HasInfoBox);
        Assert.Null(page.InfoRow);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T500-2")]
    public async Task A_in_the_rail_leads_into_the_entries_which_go_round_over_Uebersicht_anpassen_and_B_returns_to_the_rail()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;

        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        Assert.Equal(DetailZone.Rows, page.Zone);
        Assert.Same(page.Overview.Rows[0], page.FocusedRow);
        arcadia.Press(UiAction.Down, times: 3);
        Assert.Same(page.Overview.Rows[3], page.FocusedRow);
        arcadia.Press(UiAction.Down);
        Assert.True(page.IsCustomizeFocused);
        Assert.Null(page.FocusedRow);
        Assert.DoesNotContain(page.Overview.Rows, entry => entry.IsFocused);

        // ◀ ▶ on "Übersicht anpassen" do nothing; ▼ goes round to the first entry, ▲ back.
        arcadia.Press(UiAction.Left).Press(UiAction.Right);
        Assert.True(page.IsCustomizeFocused);
        arcadia.Press(UiAction.Down);
        Assert.Same(page.Overview.Rows[0], page.FocusedRow);
        arcadia.Press(UiAction.Up);
        Assert.True(page.IsCustomizeFocused);

        // A held ▲ stops at the first entry instead of going round.
        arcadia.HoldPad(TimeSpan.FromSeconds(1.2), PadButtons.DPadUp);
        Assert.Same(page.Overview.Rows[0], page.FocusedRow);

        arcadia.Press(UiAction.Back);
        Assert.Equal(DetailZone.Rail, page.Zone);
        Assert.True(page.OpenArea.IsOverview);
        Assert.False(page.IsCustomizeFocused);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T500-3")]
    public async Task Left_and_right_on_an_entry_change_the_value_as_on_its_row_and_it_holds_after_a_restart()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;
        var game = page.Tile.Game;
        var mode = FocusEntry(arcadia, GameActionKind.ControllerMode);
        var standard = mode.ValueText;

        arcadia.Press(UiAction.Right);
        Assert.Equal(ControllerModes.Arcadia, mode.ValueText);
        Assert.Equal(ControllerModes.Arcadia, page.Row(GameActionKind.ControllerMode).ValueText);
        Assert.Equal(AppTexts.DetailControllerLine(ControllerModes.Arcadia), page.Areas[1].ValueLine);
        Assert.True(arcadia.ControllerSupport.ChoiceFor(game));
        arcadia.Press(UiAction.Left, times: 2);
        Assert.Equal(ControllerModes.Steam, mode.ValueText);
        Assert.Equal(ControllerModes.Steam, page.Row(GameActionKind.ControllerMode).ValueText);

        // Under Steam Input the mouse mode is greyed; back to the default to change it.
        arcadia.Press(UiAction.Right);
        Assert.Equal(standard, mode.ValueText);
        var mouse = page.Overview.Rows[1];
        var mouseBefore = mouse.ValueText;
        arcadia.Press(UiAction.Down).Press(UiAction.Right);
        Assert.NotEqual(mouseBefore, mouse.ValueText);
        Assert.Equal(mouse.ValueText, page.Row(GameActionKind.MouseMode).ValueText);
        var mouseAfter = mouse.ValueText;

        using var restarted = await arcadia.RestartAsync();
        restarted.Sdl.Plug(KnownPads.SteamController);
        restarted.StartControllers();
        await restarted.TickSteamControllerAsync();
        restarted.Hold(UiAction.Accept, PastHoldTime);
        Assert.Equal(game.Key, restarted.Detail!.Current.Tile.Game.Key);
        Assert.Equal([standard, mouseAfter], restarted.Detail.Current.Overview.Rows.Take(2).Select(entry => entry.ValueText));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T500-4")]
    public async Task A_on_Artwork_aendern_and_Deinstallieren_does_what_their_rows_do_and_B_returns_to_the_entry()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;

        FocusEntry(arcadia, GameActionKind.Artwork);
        arcadia.Press(UiAction.Accept);
        Assert.NotNull(arcadia.Main.ChangeArtwork);
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.ChangeArtwork);
        Assert.Equal((true, GameActionKind.Artwork), (page.OpenArea.IsOverview, page.FocusedRow?.Kind));

        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        Assert.Equal(AppTexts.GameAskUninstall(page.Tile.Title), arcadia.Main.Confirmation?.Title);
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.Confirmation);
        Assert.Equal((true, GameActionKind.Uninstall), (page.OpenArea.IsOverview, arcadia.Detail!.Current.FocusedRow?.Kind));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T500-5")]
    public async Task An_entry_the_game_doesnt_offer_now_stays_greyed_takes_the_focus_and_says_why_below_the_entries()
    {
        var notInstalled = TestGames.NotInstalled(KnownLaunchers.Gog, "Bravo");
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Gog, notInstalled)]);
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;

        Assert.Equal(Defaults, page.QuickKinds());
        Assert.All(page.Overview.Rows, entry => Assert.True(entry.IsGreyed, $"{entry.Title} nicht ausgegraut"));
        var uninstall = FocusEntry(arcadia, GameActionKind.Uninstall);
        Assert.Equal(page.Row(GameActionKind.Uninstall).Note, uninstall.Note);
        Assert.Equal(AppTexts.NotInstalled, page.QuickNote);

        arcadia.Press(UiAction.Accept).Press(UiAction.Up).Press(UiAction.Accept).Press(UiAction.Up).Press(UiAction.Right);
        Assert.Null(arcadia.Main.Confirmation);
        Assert.Null(arcadia.Main.ChangeArtwork);
        Assert.Empty(arcadia.Settings.Current.MouseMode.GameChoices);
        Assert.Equal(GameActionKind.MouseMode, page.FocusedRow?.Kind);

        // On "Übersicht anpassen" the note goes.
        arcadia.Press(UiAction.Up, times: 2);
        Assert.True(page.IsCustomizeFocused);
        Assert.Null(page.QuickNote);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T500-6")]
    public async Task A_Steam_Controller_connecting_brings_the_entry_alive_in_place_with_the_focus_on_it()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"], PanEnabled = true });
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var mode = FocusEntry(arcadia, GameActionKind.ControllerMode);
        Assert.True(mode.IsGreyed);
        Assert.Equal(AppTexts.DetailModeNoSteamController, arcadia.Detail!.Current.QuickNote);

        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();

        var page = arcadia.Detail!.Current;
        Assert.False(page.Overview.Rows[0].IsGreyed);
        Assert.Equal((DetailZone.Rows, true, GameActionKind.ControllerMode), (page.Zone, page.OpenArea.IsOverview, page.FocusedRow?.Kind));
        Assert.Null(page.QuickNote);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T500-7")]
    public async Task The_button_hints_follow_the_focus_in_the_quick_selection()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;

        arcadia.Press(UiAction.Down);
        Assert.Equal(AppTexts.Select, page.AcceptHint);
        arcadia.Press(UiAction.Accept);
        Assert.Equal((null, true, AppTexts.DetailAreas), (page.AcceptHint, page.ShowsChangeHint, page.BackHint));
        arcadia.Press(UiAction.Down, times: 2);
        Assert.Equal((AppTexts.ActionChangeArtwork, false), (page.AcceptHint, page.ShowsChangeHint));
        arcadia.Press(UiAction.Down, times: 2);
        Assert.Equal((AppTexts.DetailCustomizeOverview, false, AppTexts.DetailAreas), (page.AcceptHint, page.ShowsChangeHint, page.BackHint));

        // A greyed entry names neither A nor ◀ ▶.
        var notInstalled = TestGames.NotInstalled(KnownLaunchers.Gog, "Bravo");
        using var other = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Gog, notInstalled)]);
        other.Hold(UiAction.Accept, PastHoldTime);
        FocusEntry(other, GameActionKind.MouseMode);
        Assert.Equal((null, false), (other.Detail!.Current.AcceptHint, other.Detail.Current.ShowsChangeHint));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T500-8")]
    [Trait("TestCase", "T500-9")]
    public async Task Turning_the_carousel_keeps_Uebersicht_open_and_the_focus_on_its_place_in_the_next_game_s_entries()
    {
        var alpha = TestGames.Installed(KnownLaunchers.Gog, "Alpha", DateTimeOffset.UnixEpoch);
        var bravo = TestGames.Installed(KnownLaunchers.Gog, "Bravo", DateTimeOffset.UnixEpoch);
        var charlie = TestGames.Installed(KnownLaunchers.Gog, "Charlie", DateTimeOffset.UnixEpoch);
        var settings = new AppSettings
        {
            ActiveLaunchers = ["gog", "epic"],
            QuickSelection = new QuickSelectionSettings().WithGame(bravo.Key, ["artwork"]).WithGame(charlie.Key, []),
        };
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Gog, alpha, bravo, charlie), new(KnownLaunchers.Epic, 3)], settings);
        arcadia.FocusTile("Alpha");
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        FocusEntry(arcadia, GameActionKind.MouseMode);

        // Bravo has one entry: the focus goes to it; Charlie none: to "Übersicht anpassen".
        arcadia.Press(UiAction.NextPage);
        var page = arcadia.Detail!.Current;
        Assert.Equal("Bravo", page.Tile.Title);
        Assert.Equal((true, GameActionKind.Artwork), (page.OpenArea.IsOverview, page.FocusedRow?.Kind));
        arcadia.Press(UiAction.NextPage);
        Assert.Equal("Charlie", arcadia.Detail!.Current.Tile.Title);
        Assert.True(arcadia.Detail.Current.IsCustomizeFocused);

        // On "Übersicht anpassen" it stays there, whatever the next game shows.
        arcadia.Press(UiAction.NextPage);
        Assert.Equal("Alpha", arcadia.Detail!.Current.Tile.Title);
        Assert.True(arcadia.Detail.Current.IsCustomizeFocused);
        arcadia.Press(UiAction.Down);
        Assert.Equal(GameActionKind.ControllerMode, arcadia.Detail!.Current.FocusedRow?.Kind);

        // LB/RB: another launcher's game, "Übersicht" open, the same place.
        arcadia.Press(UiAction.Down).Press(UiAction.NextTab);
        page = arcadia.Detail!.Current;
        Assert.Equal(KnownLaunchers.Epic.Id, page.Tile.Game.LauncherId);
        Assert.Equal((true, GameActionKind.MouseMode), (page.OpenArea.IsOverview, page.FocusedRow?.Kind));

        // Opened anew: "Spielen" with "Übersicht" open.
        arcadia.Press(UiAction.Back).Press(UiAction.Back).Hold(UiAction.Accept, PastHoldTime);
        Assert.Equal((DetailZone.Buttons, true), (arcadia.Detail!.Current.Zone, arcadia.Detail.Current.OpenArea.IsOverview));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T500-10")]
    public async Task The_context_menu_offers_no_Uebersicht_anpassen()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.Options);

        var labels = arcadia.Main.GameMenu!.Actions.Select(action => action.Label).ToList();
        Assert.DoesNotContain(AppTexts.DetailCustomizeOverview, labels);
        Assert.Contains(AppTexts.ActionChangeArtworkMore, labels);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T500-11")]
    [Trait("TestCase", "T500-12")]
    public async Task Uebersicht_anpassen_lists_every_entry_by_area_goes_round_over_the_button_and_B_returns_to_it()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;
        var dialog = OpenCustomize(arcadia);

        Assert.Equal(AppTexts.QuickSelectionDialogSubtitle(page.Tile.Title), dialog.Subtitle);
        Assert.Equal([AppTexts.DetailAreaController.ToUpperInvariant(), AppTexts.DetailAreaManage.ToUpperInvariant()], dialog.Groups.Select(group => group.Caption));
        Assert.Equal([AppTexts.DetailControllerModeTitle, AppTexts.DetailMouseModeTitle, AppTexts.ActionChangeArtwork, AppTexts.ActionUninstallMore],
            dialog.Entries.Select(entry => entry.Title));
        Assert.All(dialog.Entries, entry => Assert.True(entry.IsTicked));
        Assert.Equal("4 von 5", dialog.CountText);
        Assert.True(dialog.IsOffered);
        Assert.True(dialog.Entries[0].IsFocused);

        arcadia.Press(UiAction.Down, times: 4);
        Assert.True(dialog.IsApplyFocused);
        Assert.Equal(AppTexts.QuickSelectionApplyToAll, dialog.AcceptText);
        arcadia.Press(UiAction.Down);
        Assert.True(dialog.Entries[0].IsFocused);
        arcadia.Press(UiAction.Up);
        Assert.True(dialog.IsApplyFocused);
        Assert.DoesNotContain(dialog.Entries, entry => entry.IsFocused);

        // A held ▲ stops at the first entry.
        arcadia.HoldPad(TimeSpan.FromSeconds(1.2), PadButtons.DPadUp);
        Assert.True(dialog.Entries[0].IsFocused);

        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.CustomizeOverview);
        Assert.True(page.IsCustomizeFocused);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T500-13")]
    public async Task A_ticks_and_unticks_at_once_the_page_follows_in_the_order_of_the_areas_and_only_this_game_changes()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;
        var game = page.Tile.Game;
        var dialog = OpenCustomize(arcadia);

        arcadia.Press(UiAction.Accept);
        Assert.Equal("3 von 5", dialog.CountText);
        Assert.Equal(Defaults[1..], arcadia.Detail!.Current.QuickKinds());
        arcadia.Press(UiAction.Down, times: 2).Press(UiAction.Accept);
        Assert.Equal([GameActionKind.MouseMode, GameActionKind.Uninstall], arcadia.Detail.Current.QuickKinds());

        // Ticked again last, it still stands first: the order of the areas.
        arcadia.Press(UiAction.Up, times: 2).Press(UiAction.Accept);
        Assert.Equal([GameActionKind.ControllerMode, GameActionKind.MouseMode, GameActionKind.Uninstall], arcadia.Detail.Current.QuickKinds());
        Assert.True(arcadia.Detail.Current.IsCustomizeFocused);

        arcadia.Press(UiAction.Back).Press(UiAction.NextPage);
        Assert.Equal(Defaults, arcadia.Detail.Current.QuickKinds());

        using var restarted = await arcadia.RestartAsync();
        restarted.Hold(UiAction.Accept, PastHoldTime);
        Assert.Equal(game.Key, restarted.Detail!.Current.Tile.Game.Key);
        Assert.Equal([GameActionKind.ControllerMode, GameActionKind.MouseMode, GameActionKind.Uninstall], restarted.Detail.Current.QuickKinds());
    }

    [AvaloniaFact]
    [Trait("TestCase", "T500-14")]
    public async Task With_five_ticked_the_others_are_greyed_passed_over_and_a_note_says_to_untick_one_first()
    {
        // Today there are four entries; the limit is reached with test entries (decision in #500).
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var host = new RecordingHost();
        QuickChoice[] six = [.. Enumerable.Range(1, 6).Select(i => new QuickChoice($"test-{i}", $"Eintrag {i}", i <= 3 ? "Controller" : "Verwalten"))];
        var dialog = new CustomizeOverviewViewModel(arcadia.Detail!.Current.Tile, six, ["test-1", "test-2", "test-3", "test-4"], host);
        Assert.Equal("4 von 5", dialog.CountText);
        Assert.False(dialog.IsFull);

        // The fifth: full, the sixth greyed and passed over.
        dialog.Handle(UiAction.Down);
        dialog.Handle(UiAction.Down);
        dialog.Handle(UiAction.Down);
        dialog.Handle(UiAction.Down);
        dialog.Handle(UiAction.Accept);
        Assert.Equal("5 von 5", dialog.CountText);
        Assert.True(dialog.IsFull);
        Assert.Equal(AppTexts.QuickSelectionFull(5), CustomizeOverviewViewModel.FullNote);
        Assert.True(dialog.Entries[5].IsGreyed);
        Assert.Equal(["test-1", "test-2", "test-3", "test-4", "test-5"], host.Stored[^1]);
        dialog.Handle(UiAction.Down);
        Assert.True(dialog.IsApplyFocused);
        dialog.Handle(UiAction.Up);
        Assert.True(dialog.Entries[4].IsFocused);

        // A click on the greyed one doesn't tick it.
        dialog.Entries[5].ClickCommand.Execute(null);
        Assert.False(dialog.Entries[5].IsTicked);
        Assert.Equal(5, host.Stored[^1].Count);

        // Unticking one makes the others active again.
        dialog.Handle(UiAction.Accept);
        Assert.False(dialog.IsFull);
        Assert.False(dialog.Entries[5].IsGreyed);
        dialog.Handle(UiAction.Down);
        Assert.True(dialog.Entries[5].IsFocused);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T500-15")]
    public async Task Unticking_every_entry_leaves_only_Uebersicht_anpassen_also_after_a_restart()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var dialog = OpenCustomize(arcadia);
        for (var entry = 0; entry < 4; entry++)
        {
            arcadia.Press(UiAction.Accept).Press(UiAction.Down);
        }

        Assert.Equal("0 von 5", dialog.CountText);
        arcadia.Press(UiAction.Back);
        var page = arcadia.Detail!.Current;
        Assert.Empty(page.Overview.Rows);
        Assert.True(page.IsCustomizeFocused);
        arcadia.Press(UiAction.Back).Press(UiAction.Accept);
        Assert.True(page.IsCustomizeFocused);

        using var restarted = await arcadia.RestartAsync();
        restarted.Hold(UiAction.Accept, PastHoldTime).Press(UiAction.Down).Press(UiAction.Accept);
        Assert.Empty(restarted.Detail!.Current.Overview.Rows);
        Assert.True(restarted.Detail.Current.IsCustomizeFocused);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T500-16")]
    public async Task While_the_dialog_is_open_the_page_behind_updates_and_LT_RT_LB_RB_do_nothing()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"], PanEnabled = true });
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var game = arcadia.Detail!.Current.Tile.Game.Key;
        var dialog = OpenCustomize(arcadia);
        Assert.True(arcadia.Detail.Current.Overview.Rows[0].IsGreyed);
        arcadia.Press(UiAction.Down);

        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        Assert.Same(dialog, arcadia.Main.CustomizeOverview);
        Assert.True(dialog.Entries[1].IsFocused);
        Assert.False(arcadia.Detail!.Current.Overview.Rows[0].IsGreyed);

        arcadia.Press(UiAction.NextPage).Press(UiAction.PreviousPage).Press(UiAction.NextTab).Press(UiAction.PreviousTab);
        Assert.Equal(game, arcadia.Detail!.Current.Tile.Game.Key);
        Assert.Same(dialog, arcadia.Main.CustomizeOverview);

        // Also while it asks.
        arcadia.Press(UiAction.Down, times: 3).Press(UiAction.Accept);
        Assert.True(dialog.IsAsking);
        arcadia.Press(UiAction.NextPage).Press(UiAction.NextTab);
        Assert.Equal(game, arcadia.Detail!.Current.Tile.Game.Key);
        Assert.True(dialog.IsAsking);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T500-17")]
    public async Task Fuer_alle_Spiele_uebernehmen_asks_first_and_Abbrechen_or_B_change_nothing()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var dialog = OpenCustomize(arcadia);
        arcadia.Press(UiAction.Up).Press(UiAction.Accept);

        Assert.True(dialog.IsAsking);
        Assert.True(dialog.IsCancelFocused);
        Assert.False(dialog.IsApplyFocused);
        Assert.Equal((AppTexts.Select, AppTexts.Cancel), (dialog.AcceptText, dialog.BackText));
        arcadia.Press(UiAction.Back);
        Assert.Same(dialog, arcadia.Main.CustomizeOverview);
        Assert.Equal((true, true), (dialog.IsOffered, dialog.IsApplyFocused));

        // ◀ ▶ choose the answer; A on "Abbrechen" changes nothing either; ▲ ▼ stay in the question.
        arcadia.Press(UiAction.Accept).Press(UiAction.Right);
        Assert.True(dialog.IsReplaceFocused);
        arcadia.Press(UiAction.Down).Press(UiAction.Left);
        Assert.True(dialog.IsCancelFocused);
        arcadia.Press(UiAction.Accept);
        Assert.True(dialog.IsOffered);
        Assert.Null(arcadia.Settings.Current.QuickSelection.AllGames);
        Assert.Empty(arcadia.Settings.Current.QuickSelection.PerGame);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T500-18")]
    [Trait("TestCase", "T500-19")]
    [Trait("TestCase", "T500-20")]
    public async Task Ersetzen_gives_every_game_this_selection_also_games_added_later_and_one_game_may_differ_again()
    {
        var alpha = TestGames.Installed(KnownLaunchers.Gog, "Alpha", DateTimeOffset.UnixEpoch);
        var bravo = TestGames.Installed(KnownLaunchers.Gog, "Bravo", DateTimeOffset.UnixEpoch);
        var settings = new AppSettings { ActiveLaunchers = ["gog", "epic"], QuickSelection = new QuickSelectionSettings().WithGame(bravo.Key, ["uninstall"]) };
        LauncherSpec[] launchers = [LauncherSpec.With(KnownLaunchers.Gog, alpha, bravo), new(KnownLaunchers.Epic, 3)];
        using var arcadia = await ArcadiaDriver.StartAsync(launchers, settings);
        arcadia.FocusTile("Alpha");
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var dialog = OpenCustomize(arcadia);

        // Only "Artwork ändern" stays ticked, then for all games.
        arcadia.Press(UiAction.Accept).Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.Down, times: 2).Press(UiAction.Accept);
        arcadia.Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.Right).Press(UiAction.Accept);
        Assert.True(dialog.IsApplied);
        Assert.True(dialog.IsApplyFocused);
        Assert.Same(dialog, arcadia.Main.CustomizeOverview);
        Assert.Equal(["artwork"], arcadia.Settings.Current.QuickSelection.AllGames);
        Assert.Empty(arcadia.Settings.Current.QuickSelection.PerGame);

        // Bravo had its own, Epic's games are another tab's.
        arcadia.Press(UiAction.Back).Press(UiAction.NextPage);
        Assert.Equal("Bravo", arcadia.Detail!.Current.Tile.Title);
        Assert.Equal([GameActionKind.Artwork], arcadia.Detail.Current.QuickKinds());
        arcadia.Press(UiAction.NextTab);
        Assert.Equal([GameActionKind.Artwork], arcadia.Detail!.Current.QuickKinds());

        // One game changed afterwards: only it differs.
        Assert.True(arcadia.Detail.Current.IsCustomizeFocused);
        arcadia.Press(UiAction.Accept).Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.Back);
        var epicGame = arcadia.Detail!.Current.Tile.Game;
        Assert.Equal([GameActionKind.MouseMode, GameActionKind.Artwork], arcadia.Detail.Current.QuickKinds());

        // After a restart, with a game added since: the selection for all games, the Epic game its own.
        var charlie = TestGames.Installed(KnownLaunchers.Gog, "Charlie", DateTimeOffset.UnixEpoch);
        using var restarted = await arcadia.RestartAsync([LauncherSpec.With(KnownLaunchers.Gog, alpha, bravo, charlie), new(KnownLaunchers.Epic, 3)]);
        // Arcadia comes back on the tab it closed on, Epic's.
        restarted.Press(UiAction.NextTab);
        restarted.FocusTile("Charlie");
        restarted.Hold(UiAction.Accept, PastHoldTime);
        Assert.Equal([GameActionKind.Artwork], restarted.Detail!.Current.QuickKinds());
        Assert.Equal(["mouse-mode", "artwork"], restarted.Settings.Current.QuickSelection.Of(epicGame.Key, []));
        Assert.Equal(["artwork"], restarted.Settings.Current.QuickSelection.Of(alpha.Key, []));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T500-21")]
    public async Task A_game_never_changed_gets_every_default_entry_while_a_game_with_its_own_selection_keeps_it()
    {
        // As stored by an older version whose defaults knew "Artwork ändern" only: one game chose that.
        var alpha = TestGames.Installed(KnownLaunchers.Gog, "Alpha", DateTimeOffset.UnixEpoch);
        var bravo = TestGames.Installed(KnownLaunchers.Gog, "Bravo", DateTimeOffset.UnixEpoch);
        var settings = new AppSettings { ActiveLaunchers = ["gog"], QuickSelection = new QuickSelectionSettings().WithGame(alpha.Key, ["artwork"]) };
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Gog, alpha, bravo)], settings);
        arcadia.FocusTile("Alpha");
        arcadia.Hold(UiAction.Accept, PastHoldTime);

        Assert.Equal([GameActionKind.Artwork], arcadia.Detail!.Current.QuickKinds());
        arcadia.Press(UiAction.NextPage);
        Assert.Equal(Defaults, arcadia.Detail!.Current.QuickKinds());
    }

    [AvaloniaFact]
    [Trait("TestCase", "T500-22")]
    public async Task An_unreadable_quick_selection_shows_the_defaults_without_a_message_and_logs_it_and_unknown_ids_drop_out()
    {
        using var first = await ArcadiaDriver.StartAsync();
        var key = first.PageGames[0].Game.Key;
        WriteQuickSelection(first, JsonNode.Parse("""{ "PerGame": 7 }"""));
        using var arcadia = await first.RestartAsync();

        arcadia.Hold(UiAction.Accept, PastHoldTime);
        Assert.Equal(Defaults, arcadia.Detail!.Current.QuickKinds());
        Assert.True(arcadia.Settings.Current.PanEnabled);
        Assert.Null(arcadia.Main.Toast.Text);
        Assert.Contains("Schnellauswahl in settings.json nicht lesbar", ArcadiaDriver.Log);

        WriteQuickSelection(arcadia, new JsonObject { ["PerGame"] = new JsonObject { [key] = new JsonArray("from-a-later-version", "artwork") } });
        using var again = await arcadia.RestartAsync();
        again.Hold(UiAction.Accept, PastHoldTime);
        Assert.Equal(key, again.Detail!.Current.Tile.Game.Key);
        Assert.Equal([GameActionKind.Artwork], again.Detail.Current.QuickKinds());
    }

    /// <summary>settings.json as written by hand: its quick selection replaced, everything else kept.</summary>
    private static void WriteQuickSelection(ArcadiaDriver arcadia, JsonNode? quickSelection)
    {
        var settings = JsonNode.Parse(File.ReadAllText(arcadia.SettingsPath))!.AsObject();
        settings["QuickSelection"] = quickSelection;
        File.WriteAllText(arcadia.SettingsPath, settings.ToJsonString());
    }

    private static DetailRowViewModel FocusEntry(ArcadiaDriver arcadia, GameActionKind kind)
    {
        var page = arcadia.Detail!.Current;
        Assert.Equal(DetailZone.Buttons, page.Zone);
        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        for (var steps = 0; page.FocusedRow?.Kind != kind; steps++)
        {
            Assert.True(steps <= page.Overview.Rows.Count, $"Eintrag {kind} nicht erreicht");
            arcadia.Press(UiAction.Down);
        }

        return page.FocusedRow!;
    }

    /// <summary>From the buttons into the rail, A into the entries, ▲ round to "Übersicht anpassen", A.</summary>
    private static CustomizeOverviewViewModel OpenCustomize(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.Up);
        Assert.True(arcadia.Detail!.Current.IsCustomizeFocused);
        arcadia.Press(UiAction.Accept);
        return Assert.IsType<CustomizeOverviewViewModel>(arcadia.Main.CustomizeOverview);
    }

    private static async Task<ArcadiaDriver> StartWithSteamControllerAsync()
    {
        var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings
        {
            ActiveLaunchers = ["steam", "gog"],
            PanEnabled = true,
            MouseMode = new MouseModeSettings { Activation = MouseModeActivation.Manual, InGames = MouseModeInGames.PerGame },
        });
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        return arcadia;
    }

    private sealed class RecordingHost : ICustomizeOverviewHost
    {
        public List<IReadOnlyList<string>> Stored { get; } = [];

        public void StoreQuickSelection(Game game, IReadOnlyList<string> ids) => Stored.Add(ids);

        public void ApplyQuickSelectionToAllGames(IReadOnlyList<string> ids) => Stored.Add(ids);

        public void Close(CustomizeOverviewViewModel dialog)
        {
        }
    }
}
