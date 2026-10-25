using Arcadia.App.Input;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The window's routing (#210): each press reaches the topmost layer once; what a page leaves (≡, X) opens a page over
/// it, what it ignores on purpose goes nowhere, and an open dialog takes every press before the page under it.
/// </summary>
public sealed class RoutingTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    [AvaloniaFact]
    [Trait("TestCase", "T210-1")]
    public async Task Menu_opens_the_settings_over_a_detail_page_and_Menu_or_B_closes_them_to_the_same_page_once()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.Right, times: 2).Hold(UiAction.Accept, PastHoldTime).Press(UiAction.NextPage);
        var detail = arcadia.Detail!;
        var shown = detail.Current.Tile.Game.Key;

        arcadia.Press(UiAction.Settings);
        Assert.True(arcadia.Main.IsSettingsOpen);
        arcadia.Press(UiAction.Settings);
        Assert.False(arcadia.Main.IsSettingsOpen);
        Assert.Same(detail, arcadia.Detail);
        Assert.Equal(shown, detail.Current.Tile.Game.Key);

        arcadia.Press(UiAction.Settings).Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
        Assert.Same(detail, arcadia.Detail);
        Assert.Equal(shown, detail.Current.Tile.Game.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T210-2")]
    public async Task Options_on_a_detail_page_opens_no_menu_and_leaves_the_library_under_it_alone()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.Right, times: 3);
        var focused = arcadia.FocusedGame!.Game.Key;
        arcadia.Hold(UiAction.Accept, PastHoldTime);

        arcadia.Press(UiAction.Options);

        Assert.False(arcadia.Main.IsGameMenuOpen);
        Assert.True(arcadia.Main.IsDetailOpen);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsGameMenuOpen);
        Assert.Equal(focused, arcadia.FocusedGame?.Game.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T210-3")]
    public async Task An_open_dialog_takes_Menu_and_X_so_neither_the_settings_nor_the_search_open_under_it()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.ViewOptions);
        Assert.True(arcadia.Main.IsViewOptionsOpen);

        arcadia.Press(UiAction.Settings).Press(UiAction.Search);

        Assert.True(arcadia.Main.IsViewOptionsOpen);
        Assert.False(arcadia.Main.IsSettingsOpen);
        Assert.Null(arcadia.Search);
    }
}
