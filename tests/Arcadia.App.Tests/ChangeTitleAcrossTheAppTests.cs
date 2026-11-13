using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// #496, the quality engineer's additions to <see cref="ChangeTitleTests"/>: an own title moves its game across library
/// pages, the "Original-Titel" line is off after a tab switch, and the waiting list shows the own title.
/// </summary>
public sealed class ChangeTitleAcrossTheAppTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private static readonly DateTimeOffset InstalledAt = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [AvaloniaFact]
    [Trait("TestCase", "T496-8")]
    public async Task A_game_renamed_from_the_last_page_to_the_front_is_focused_on_the_first_page_after_B()
    {
        // 29 games "Spiel 01" … "Spiel 29" and "Zebra": with 24 per page Zebra is on page 2.
        Game[] games =
        [
            .. Enumerable.Range(1, 29).Select(n => TestGames.Installed(KnownLaunchers.Gog, $"Spiel {n:00}", InstalledAt)),
            TestGames.Installed(KnownLaunchers.Gog, "Zebra", InstalledAt),
        ];
        using var arcadia = await ArcadiaDriver.StartAsync([new LauncherSpec(KnownLaunchers.Gog, games.Length, Library: games)]);
        OpenDetailOf(arcadia, "Zebra");
        Assert.Equal("30 von 30", arcadia.Detail!.PositionText);

        TypeTitle(arcadia, "Zebra", "Aal");
        Assert.Equal("1 von 30", arcadia.Detail.PositionText);

        arcadia.Press(UiAction.Back).Press(UiAction.Back);
        Assert.Null(arcadia.Detail);
        Assert.Equal("1", arcadia.CurrentPage);
        Assert.Equal("Aal", arcadia.PageGames[0].Title);
        Assert.Equal("Aal", arcadia.FocusedGame?.Title);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T496-12")]
    public async Task The_original_title_line_is_off_after_switching_the_tab_and_back()
    {
        Game[] gog = [TestGames.Installed(KnownLaunchers.Gog, "Alpha", InstalledAt), TestGames.Installed(KnownLaunchers.Gog, "Zebra", InstalledAt)];
        Game[] epic = [TestGames.Installed(KnownLaunchers.Epic, "Epic Eins", InstalledAt)];
        using var arcadia = await ArcadiaDriver.StartAsync(
            [new LauncherSpec(KnownLaunchers.Gog, gog.Length, Library: gog), new LauncherSpec(KnownLaunchers.Epic, epic.Length, Library: epic)]);
        OpenDetailOf(arcadia, "Zebra");
        TypeTitle(arcadia, "Zebra", "Aal");
        arcadia.Press(UiAction.KeyboardLayout);
        Assert.True(arcadia.Detail!.Current.ShowsOriginalTitle);

        // RB to Epic's tab: its game has no own title, R3 does nothing there; LB back to GOG and to the renamed game.
        arcadia.Press(UiAction.NextTab);
        Assert.Equal("Epic Eins", arcadia.Detail!.Current.Tile.Title);
        Assert.False(arcadia.Detail.Current.HasOriginalTitle);
        arcadia.Press(UiAction.KeyboardLayout);
        Assert.False(arcadia.Detail.Current.ShowsOriginalTitle);
        arcadia.Press(UiAction.PreviousTab);
        while (arcadia.Detail!.Current.Tile.Title != "Aal")
        {
            arcadia.Press(UiAction.NextPage);
        }

        Assert.True(arcadia.Detail.Current.HasOriginalTitle);
        Assert.False(arcadia.Detail.Current.ShowsOriginalTitle);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T496-7")]
    public async Task The_waiting_list_shows_the_own_title_and_searches_by_the_launchers()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([WaitingListTests.Launcher], WaitingListTests.Settings, imageServer: WaitingListTests.Images());
        OpenDetailOf(arcadia, "Echo Drift");
        TypeTitle(arcadia, "Echo Drift", "Aal Drift");
        arcadia.Press(UiAction.Back).Press(UiAction.Back);
        await WaitingListTests.FindNamesakesAsync(arcadia);

        var list = WaitingListTests.OpenList(arcadia);

        Assert.Equal(["Aal Drift", "Golden Crown", "Iron Ember"], list.Games.Select(game => game.Title));
        Assert.Equal("Aal Drift", list.Hits!.Title);
        Assert.Equal("Echo Drift", list.Hits.Waiting.Game.Title);
    }

    /// <summary>From the library: the detail page of the game titled <paramref name="title"/>.</summary>
    private static void OpenDetailOf(ArcadiaDriver arcadia, string title)
    {
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        for (var turns = 0; arcadia.Detail!.Current.Tile.Title != title; turns++)
        {
            Assert.True(turns < 50, $"{title} is not in the carousel.");
            arcadia.Press(UiAction.NextPage);
        }
    }

    /// <summary>On the detail page: "Titel ändern", <paramref name="from"/> erased, <paramref name="to"/> typed, ≡.</summary>
    private static void TypeTitle(ArcadiaDriver arcadia, string from, string to)
    {
        arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.ChangeTitle);
        arcadia.Press(UiAction.Accept);
        arcadia.Press(UiAction.Search, times: from.Length);
        arcadia.Main.ChangeTitle!.Editor.Type(to);
        arcadia.Press(UiAction.Settings);
        Assert.Null(arcadia.Main.ChangeTitle);
        Assert.Equal(to, arcadia.Detail!.Current.Tile.Title);
    }
}
