using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.Status;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using Xunit;
using static Arcadia.App.Tests.TestGames;

namespace Arcadia.App.Tests;

/// <summary>B goes back to exactly the page it came from, without exceptions (#586).</summary>
public sealed class BackWhereYouCameFromTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private static readonly Game Alpha = Installed(KnownLaunchers.Gog, "Alpha", ArcadiaDriver.Now.AddDays(-30));
    private static readonly Game Mitte = Installed(KnownLaunchers.Gog, "Mitte", ArcadiaDriver.Now.AddDays(-30));
    private static readonly Game Eisenflut = Installed(KnownLaunchers.Epic, "Eisenflut", ArcadiaDriver.Now.AddDays(-30));

    private static Game Game(LauncherInfo launcher, string title) => Installed(launcher, title, ArcadiaDriver.Now.AddDays(-30));

    /// <summary>"river" hits three games, in two launchers.</summary>
    private static IReadOnlyList<LauncherSpec> RiverLibrary() =>
    [
        new LauncherSpec(KnownLaunchers.Gog, 1, Library: [Game(KnownLaunchers.Gog, "River Crossing"), Game(KnownLaunchers.Gog, "River Raid"), Game(KnownLaunchers.Gog, "Iron Harbor")]),
        new LauncherSpec(KnownLaunchers.Xbox, 2, Library: [Game(KnownLaunchers.Xbox, "Riverbend"), Game(KnownLaunchers.Xbox, "Silent Tides")]),
    ];

    [AvaloniaFact]
    [Trait("TestCase", "T586-1")]
    [Trait("TestCase", "T586-2")]
    public async Task B_on_a_detail_page_from_the_search_returns_to_the_search_on_the_game_shown_last()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(RiverLibrary());
        arcadia.Press(UiAction.Search);
        arcadia.Main.TypeInSearch("river");
        var search = arcadia.Search!;
        var hits = search.Hits.Select(game => game.Key).ToList();
        Assert.True(search.IsKeyboardOut);

        // The second hit's details, turned on to the third.
        arcadia.Press(UiAction.SecondaryRight);
        arcadia.PressDown(UiAction.Settings).Wait(PastHoldTime).LetGo(UiAction.Settings);
        var detail = arcadia.Detail!;
        Assert.True(detail.IsFromSearch);
        arcadia.Press(UiAction.NextPage);
        var shownLast = detail.Current.Tile.Game.Key;

        arcadia.Press(UiAction.Back);

        Assert.Null(arcadia.Detail);
        Assert.Same(search, arcadia.Search);
        Assert.True(arcadia.Main.IsSearchShown);
        Assert.Equal("river", search.Text);
        Assert.Equal(hits, search.Hits.Select(game => game.Key));
        Assert.True(search.IsKeyboardOut);
        Assert.Equal(shownLast, search.FocusedResult?.Game.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T586-3")]
    public async Task The_search_keeps_its_keyboard_put_away_when_B_returns_to_it()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(RiverLibrary());
        arcadia.Press(UiAction.Search);
        arcadia.Main.TypeInSearch("river");
        var search = arcadia.Search!;
        arcadia.Press(UiAction.Back);
        Assert.False(search.IsKeyboardOut);
        arcadia.Press(UiAction.Right);
        var focused = search.FocusedResult!.Game.Key;

        arcadia.Hold(UiAction.Accept, PastHoldTime);
        Assert.True(arcadia.Detail?.IsFromSearch);
        arcadia.Press(UiAction.Back);

        Assert.True(arcadia.Main.IsSearchShown);
        Assert.False(search.IsKeyboardOut);
        Assert.Equal(focused, search.FocusedResult?.Game.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T586-4")]
    public async Task B_leads_from_a_hit_back_to_the_search_and_then_to_the_library_detail_page_it_was_opened_over()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(RiverLibrary());
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var libraryDetail = arcadia.Detail!;
        Assert.True(libraryDetail.IsFromLibrary);
        var libraryGame = libraryDetail.Current.Tile.Game.Key;

        arcadia.Press(UiAction.Search);
        arcadia.Main.TypeInSearch("river");
        arcadia.PressDown(UiAction.Settings).Wait(PastHoldTime).LetGo(UiAction.Settings);
        Assert.True(arcadia.Detail?.IsFromSearch);

        arcadia.Press(UiAction.Back);
        Assert.True(arcadia.Main.IsSearchShown);

        // The first B puts the keyboard away, the second leaves the search.
        arcadia.Press(UiAction.Back, times: 2);
        Assert.Null(arcadia.Search);
        Assert.Same(libraryDetail, arcadia.Detail);
        Assert.Equal(libraryGame, libraryDetail.Current.Tile.Game.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T586-5")]
    public async Task X_on_a_detail_page_from_the_search_also_returns_to_the_search()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(RiverLibrary());
        arcadia.Press(UiAction.Search);
        arcadia.Main.TypeInSearch("river");
        var search = arcadia.Search!;
        arcadia.PressDown(UiAction.Settings).Wait(PastHoldTime).LetGo(UiAction.Settings);
        var shown = arcadia.Detail!.Current.Tile.Game.Key;

        arcadia.Press(UiAction.Search);

        Assert.Null(arcadia.Detail);
        Assert.Same(search, arcadia.Search);
        Assert.Equal(("river", shown), (search.Text, search.FocusedResult?.Game.Key));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T586-6")]
    public async Task Settings_opened_over_a_detail_page_lead_back_to_it_after_a_game_of_the_tag_list() =>
        await SettingsLeadBackOverTheTagListAsync(settingsOverTheTagGame: false);

    [AvaloniaFact]
    [Trait("TestCase", "T586-6")]
    public async Task Settings_opened_again_over_the_tag_lists_game_keep_the_way_back_to_the_first_detail_page() =>
        await SettingsLeadBackOverTheTagListAsync(settingsOverTheTagGame: true);

    private static async Task SettingsLeadBackOverTheTagListAsync(bool settingsOverTheTagGame)
    {
        using var arcadia = await ArcadiaDriver.StartAsync(
            [new LauncherSpec(KnownLaunchers.Gog, 1, Library: [Alpha, Mitte]), new LauncherSpec(KnownLaunchers.Epic, 2, Library: [Eisenflut])]);
        arcadia.Main.Tags.Create("Zu zweit", Mitte.Key);
        arcadia.Main.Tags.Create("Zu zweit", Eisenflut.Key);
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var libraryDetail = arcadia.Detail!;
        Assert.Equal("Alpha", libraryDetail.Current.Tile.Title);

        // ≡ on the detail page, "Datenquellen", the tag list, the tag's games, a game.
        arcadia.Press(UiAction.Settings);
        for (var i = 0; i < 4 && arcadia.Main.Settings!.Selected is not DataSourcesSettingsViewModel; i++)
        {
            arcadia.Press(UiAction.NextTab);
        }

        arcadia.FocusRow(DataSourcesSettingsViewModel.OwnTagsRowKey);
        arcadia.Press(UiAction.Accept);
        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        arcadia.Press(UiAction.Accept);
        var tagGame = arcadia.Detail;
        Assert.NotSame(libraryDetail, tagGame);
        Assert.Null(arcadia.Main.Settings);

        // ≡ and B over the tag's game: back on it, its way back intact.
        if (settingsOverTheTagGame)
        {
            arcadia.Press(UiAction.Settings).Press(UiAction.Back);
            Assert.Same(tagGame, arcadia.Detail);
        }

        // B: the tag's games, the tags, "Datenquellen", then the settings close onto the page they were opened over.
        arcadia.Press(UiAction.Back, times: 3);
        Assert.NotNull(arcadia.Main.Settings);
        Assert.Null(arcadia.Main.TagList);
        arcadia.Press(UiAction.Back);

        Assert.Null(arcadia.Main.Settings);
        Assert.Same(libraryDetail, arcadia.Detail);
        Assert.Equal("Alpha", libraryDetail.Current.Tile.Title);
        Assert.True(arcadia.Main.IsTabStripShown);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T586-9")]
    public async Task B_on_settings_opened_from_the_status_modal_opens_the_modal_again()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(releases: new FakeReleases("0.1.0", ("0.2.0", ["Neu"])));
        await arcadia.Updates.CheckIfDueAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Main.Status.HasArcadiaUpdate, "das Update im Header");
        OpenAboutFromTheStatusModal(arcadia);
        Assert.IsType<AboutSettingsViewModel>(arcadia.Main.Settings?.Selected);
        Assert.Null(arcadia.Main.StatusModal);

        arcadia.Press(UiAction.Back);

        Assert.Null(arcadia.Main.Settings);
        Assert.Contains(arcadia.Main.StatusModal!.Entries, entry => entry.Entry.Target == StatusTarget.ArcadiaUpdate);

        // Settings opened anew with ≡ close onto the page alone.
        arcadia.Press(UiAction.Back).Press(UiAction.Settings).Press(UiAction.Back);
        Assert.Null(arcadia.Main.StatusModal);

        // Settings opened another way while the modal's way back is pending forget it: the launcher settings of an empty tab.
        OpenAboutFromTheStatusModal(arcadia);
        ((ILibraryHost)arcadia.Main).OpenLauncherSettings(null);
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.Settings);
        Assert.Null(arcadia.Main.StatusModal);
    }

    /// <summary>Y, the update's entry, "Zu den Einstellungen" (not the update itself): "Über Arcadia".</summary>
    private static void OpenAboutFromTheStatusModal(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Status);
        var modal = arcadia.Main.StatusModal!;
        while (modal.Focused?.Entry.Target != StatusTarget.ArcadiaUpdate)
        {
            arcadia.Press(UiAction.Down);
        }

        // "Zu den Einstellungen", not the update itself.
        if (modal.Focused.IsFixFocused)
        {
            arcadia.Press(UiAction.Right);
        }

        arcadia.Press(UiAction.Accept);
        Assert.IsType<AboutSettingsViewModel>(arcadia.Main.Settings?.Selected);
    }
}
