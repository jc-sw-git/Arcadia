using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.Input;
using Arcadia.Launchers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests.Guards;

/// <summary>Pages and tabs turn without building the covers again, and every press logs how long it took to show (#229).</summary>
public sealed class LibrarySpeedTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T229-1")]
    public async Task Turning_a_page_or_tab_binds_the_same_covers_to_the_new_games()
    {
        // GOG has 31 games: 24 on page 1, 7 on page 2.
        using var window = await ArcadiaWindow.ShowAsync();
        var arcadia = window.Arcadia;
        var first = window.Tile(arcadia.PageGames[0]);
        var covers = Covers(window);
        Assert.Equal(24, covers.Count);

        window.Press(UiAction.NextPage);
        Assert.Equal(7, arcadia.PageGames.Count);
        Assert.Same(first, window.Tile(arcadia.PageGames[0]));
        Assert.Equal(arcadia.PageGames[0].Title, ToolTip.GetTip(first));
        Assert.Equal(covers, Covers(window));
        Assert.Equal(7, covers.Count(cover => cover.IsEffectivelyVisible));
        // The hidden places take no room: the seven games are one row, nothing to scroll.
        Assert.True(window.Named<ItemsControl>("GameGrid").Bounds.Height < 2 * first.Bounds.Height, "Die leeren Plätze nehmen Zeilen ein.");

        var gog = arcadia.SelectedTab;
        window.Press(UiAction.NextTab);
        Assert.NotSame(gog, arcadia.SelectedTab);
        Assert.Same(first, window.Tile(arcadia.PageGames[0]));
        Assert.Equal(covers, Covers(window));
        Assert.Equal(arcadia.PageGames.Select(tile => tile.Title), covers.Where(cover => cover.IsEffectivelyVisible).Select(cover => ToolTip.GetTip(cover)));
        window.AssertRingAround(first, "erstes Cover nach dem Tabwechsel", ring: "FocusRingLifted");
    }

    /// <summary>T24-5: the largest page, 128 covers, turns as the small ones do: the same covers, bound to the next games.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T24-5")]
    public async Task A_page_of_128_turns_with_the_same_covers()
    {
        var games = Enumerable.Range(1, 300).Select(i => TestGames.Installed(KnownLaunchers.Gog, $"Spiel {i:D3}", DateTimeOffset.UnixEpoch)).ToArray();
        IReadOnlyList<LauncherSpec> launchers = [LauncherSpec.With(KnownLaunchers.Gog, games)];
        using var window = await ArcadiaWindow.ShowAsync(launchers, ArcadiaWindow.WithPageSize(128, launchers));
        var arcadia = window.Arcadia;
        var covers = Covers(window);
        Assert.Equal(128, covers.Count);

        window.Press(UiAction.NextPage).Press(UiAction.NextPage);
        Assert.Equal(300 - 256, arcadia.PageGames.Count);
        Assert.Equal(covers, Covers(window));
        Assert.Equal("Spiel 257", ToolTip.GetTip(covers[0]));

        window.Press(UiAction.PreviousPage);
        Assert.Equal(128, arcadia.PageGames.Count);
        Assert.Equal(covers, Covers(window));
        Assert.Equal(arcadia.PageGames.Select(tile => tile.Title), covers.Select(cover => ToolTip.GetTip(cover)));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T229-2")]
    public async Task Every_key_and_button_press_logs_its_time_to_the_frame_that_shows_it()
    {
        using var window = await ArcadiaWindow.ShowAsync();
        var log = Path.Combine(AppLog.Directory, "arcadia.log");
        // The log folder is shared by the tests: only the lines of these presses count.
        var before = File.Exists(log) ? File.ReadAllLines(log).Length : 0;

        window.Key(Key.Right).Key(Key.E).Key(Key.E, KeyModifiers.Shift).Key(Key.D2);
        var lines = File.ReadAllLines(log).Skip(before).Where(line => line.Contains(" Bild: ", StringComparison.Ordinal)).ToList();
        Assert.Equal(["Right", "NextPage", "NextTab", "Launcher 2"], lines.Select(line => InputLatencyTests.FrameLine().Match(line) is { Success: true } match ? match.Groups["what"].Value : line));

        var arcadia = window.Arcadia;
        arcadia.PressButton(GamepadButton.DPadRight).ReleaseButton(GamepadButton.DPadRight);
        Assert.Matches(InputLatencyTests.FrameLine(), Assert.Single(arcadia.LatencyLines));
        Assert.Equal("Right", InputLatencyTests.FrameLine().Match(arcadia.LatencyLines[0]).Groups["what"].Value);
    }

    private static List<Button> Covers(ArcadiaWindow window) =>
        window.Named<ItemsControl>("GameGrid").GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("tile")).ToList();
}
