using Arcadia.App.Input;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>The search over all launchers (#12).</summary>
public sealed class SearchTests
{
    [AvaloniaFact]
    [Trait("Testfall", "T12-3")]
    public async Task Hits_come_from_all_launchers_whatever_the_case()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var expected = arcadia.Main.Tabs.SelectMany(tab => tab.Games)
            .Where(game => game.Title.Contains("hollow", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var launchers = expected.Select(game => game.LauncherId).Distinct().Count();
        Assert.True(launchers > 1, "Die Demo-Daten sollen \"hollow\" in mehreren Launchern haben.");

        arcadia.Press(UiAction.Search);
        arcadia.Main.TypeInSearch("hOLLow");

        var search = arcadia.Main.Search!;
        Assert.Equal(expected.Select(game => game.Key).Order(), search.Hits.Select(game => game.Key).Order());
        Assert.Equal($"{expected.Count} Treffer in {launchers} Launchern", search.ResultsTitle);

        arcadia.Main.TypeInSearch("xyz");
        Assert.Empty(search.Hits);
        Assert.False(search.HasHits);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T12-9")]
    public async Task B_first_puts_the_keyboard_away_then_goes_back_to_the_library_as_it_was_without_erasing()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.NextTab).Press(UiAction.NextPage).Press(UiAction.Right, times: 3);
        var focused = arcadia.Main.FocusedGame!.Game.Key;

        arcadia.Press(UiAction.Search);
        var search = arcadia.Main.Search!;
        Assert.True(arcadia.Main.IsSearchShown);
        Assert.True(search.IsKeyboardOut);
        arcadia.Main.TypeInSearch("iron");

        arcadia.Press(UiAction.Back);
        Assert.True(arcadia.Main.IsSearchShown);
        Assert.False(search.IsKeyboardOut);
        Assert.Equal("iron", search.Text);

        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSearchShown);
        Assert.Equal("Xbox", arcadia.Main.SelectedTab?.Name);
        Assert.Equal("2", arcadia.Main.PageButtons.Single(button => button.IsCurrent).Label);
        Assert.Equal(focused, arcadia.Main.FocusedGame?.Game.Key);
    }
}
