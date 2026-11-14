using Arcadia.App.Input;
using Arcadia.App.Tests.Fixtures;
using Arcadia.App.ViewModels;
using Arcadia.Core.Tests.Artwork;
using Arcadia.Input;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #565 "Bild ansehen" as the player meets it: Steam games with made-up AppIDs and backgrounds, the detail page left alone
/// for ten seconds, then a button. The order and the timer's details are in <see cref="ImageShowTests"/>.
/// </summary>
public sealed class ImageShowAcceptanceTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);
    private static readonly TimeSpan Idle = ImageShowViewModel.IdleTime;
    private static readonly TimeSpan Moment = TimeSpan.FromMilliseconds(200);

    [AvaloniaFact]
    [Trait("TestCase", "T565-6")]
    [Trait("TestCase", "T565-7")]
    public async Task Left_alone_the_detail_page_gives_way_to_its_background_and_then_to_the_tabs_other_backgrounds()
    {
        using var files = SteamGames(withBackground: ["4200001", "4200002", "4200003"], without: ["4200004"]);
        using var arcadia = await OpenDetailPageAsync(files);
        var own = arcadia.Detail!.Current.Tile.Game;
        arcadia.Press(UiAction.Right);

        arcadia.Wait(Idle - Moment);
        Assert.False(arcadia.Main.ImageShow.IsShowing);
        arcadia.Wait(Moment * 2);
        Assert.True(arcadia.Main.ImageShow.IsShowing);
        Assert.Equal(own.Key, arcadia.Main.ImageShow.Shown?.GameKey);
        Assert.Equal(own.Title, arcadia.Main.ImageShow.Shown?.Title);

        var shown = new List<string>();
        for (var i = 0; i < 2; i++)
        {
            var before = arcadia.Main.ImageShow.Shown!.GameKey;
            arcadia.Wait(Idle);
            await arcadia.WaitUntilAsync(() => arcadia.Main.ImageShow.Shown?.GameKey != before, "das nächste Bild");
            shown.Add(arcadia.Main.ImageShow.Shown!.GameKey);
        }

        // The tab's two other games with a background, each once; the one without never.
        Assert.Equal(arcadia.PageGames.Where(tile => tile.Hero is not null && tile.Game.Key != own.Key).Select(tile => tile.Game.Key).Order(), shown.Order());
    }

    [AvaloniaFact]
    [Trait("TestCase", "T565-8")]
    public async Task A_press_brings_back_the_page_of_the_original_game_with_its_focus_and_does_nothing_else()
    {
        using var files = SteamGames(withBackground: ["4200011", "4200012"], without: []);
        using var arcadia = await OpenDetailPageAsync(files);
        var own = arcadia.Detail!.Current.Tile.Game;
        arcadia.Press(UiAction.Down);
        var focus = arcadia.Detail.Current.Focus;
        arcadia.Wait(Idle * 2);
        await arcadia.WaitUntilAsync(() => arcadia.Main.ImageShow.Shown?.GameKey != own.Key, "das Bild des anderen Spiels");

        // A on the controller: on its release it would start the game; here it only ends the show.
        arcadia.PressButton(GamepadButton.A).ReleaseButton(GamepadButton.A);

        Assert.False(arcadia.Main.ImageShow.IsShowing);
        Assert.Empty(arcadia.Starts.Keys);
        Assert.Equal(own.Key, arcadia.Detail.Current.Tile.Game.Key);
        Assert.Equal(focus, arcadia.Detail.Current.Focus);

        // Each time: LT that ends the show doesn't turn to the game before; the next press acts again.
        arcadia.Wait(Idle);
        arcadia.PressButton(GamepadButton.LeftTrigger);
        arcadia.ReleaseButton(GamepadButton.LeftTrigger);
        Assert.Equal(own.Key, arcadia.Detail.Current.Tile.Game.Key);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsDetailOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T565-9")]
    public async Task It_doesnt_start_under_a_dialog_nor_in_the_library()
    {
        using var files = SteamGames(withBackground: ["4200021", "4200022"], without: []);
        using var arcadia = await OpenDetailPageAsync(files);

        // L3's "Ansicht"; the detail page has no context menu.
        arcadia.Press(UiAction.ViewOptions);
        Assert.True(arcadia.Main.IsViewOptionsOpen);
        arcadia.Wait(Idle * 2);
        Assert.False(arcadia.Main.ImageShow.IsShowing);

        arcadia.Press(UiAction.Back).Press(UiAction.Back);
        Assert.False(arcadia.Main.IsDetailOpen);
        arcadia.Wait(Idle * 2);
        Assert.False(arcadia.Main.ImageShow.IsShowing);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T565-10")]
    public async Task While_another_window_is_in_front_it_neither_runs_nor_starts_and_back_in_front_the_ten_seconds_begin_anew()
    {
        using var files = SteamGames(withBackground: ["4200031", "4200032"], without: []);
        using var arcadia = await OpenDetailPageAsync(files);
        arcadia.Wait(Idle + Moment);
        Assert.True(arcadia.Main.ImageShow.IsShowing);

        arcadia.Main.SetInFront(false);
        Assert.False(arcadia.Main.ImageShow.IsShowing);
        Assert.False(arcadia.Main.ImageShow.IsPresent);
        arcadia.Wait(Idle * 2);
        Assert.False(arcadia.Main.ImageShow.IsShowing);

        arcadia.Main.SetInFront(true);
        arcadia.Wait(Idle - Moment);
        Assert.False(arcadia.Main.ImageShow.IsShowing);
        arcadia.Wait(Moment * 2);
        Assert.True(arcadia.Main.ImageShow.IsShowing);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T565-11")]
    public async Task Without_another_game_with_a_background_in_the_tab_the_own_one_stays()
    {
        using var files = SteamGames(withBackground: ["4200041"], without: ["4200042"]);
        using var arcadia = await OpenDetailPageAsync(files, "4200041");
        var own = arcadia.Detail!.Current.Tile.Game;

        arcadia.Wait(Idle * 4);

        Assert.True(arcadia.Main.ImageShow.IsShowing);
        Assert.Equal(own.Key, arcadia.Main.ImageShow.Shown?.GameKey);
    }

    private static LauncherFiles SteamGames(string[] withBackground, string[] without)
    {
        var files = new LauncherFiles();
        var steam = files.Steam.Install();
        foreach (var appId in withBackground.Concat(without))
        {
            steam.Write(new SteamApp(appId, $"Leuchtfeuer {appId[^2..]}"));
        }

        foreach (var appId in withBackground)
        {
            TestImages.Write(Path.Combine(steam.Root, "appcache", "librarycache", appId, "library_hero.jpg"), TestImages.Png(1920, 620));
        }

        files.ImageServer.Offline = true;
        return files;
    }

    /// <summary>Arcadia with the images in its cache and the detail page of <paramref name="appId"/> (else the first game) open.</summary>
    private static async Task<ArcadiaDriver> OpenDetailPageAsync(LauncherFiles files, string? appId = null)
    {
        var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        for (var round = 0; round < 3; round++)
        {
            Dispatcher.UIThread.RunJobs();
            await arcadia.Artwork.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Dispatcher.UIThread.RunJobs();
        }

        if (appId is not null)
        {
            var index = arcadia.PageGames.ToList().FindIndex(tile => tile.Game.Id == appId);
            arcadia.Press(UiAction.Right, times: index);
        }

        arcadia.Hold(UiAction.Accept, PastHoldTime);
        Assert.True(arcadia.Main.IsDetailOpen);
        return arcadia;
    }
}
