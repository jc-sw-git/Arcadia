using System.Text.RegularExpressions;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.Input;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>Pages and tabs turn without building the covers again, and every press logs how long it took to show (#229).</summary>
public sealed partial class LibrarySpeedTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T229-1")]
    public async Task Turning_a_page_or_tab_binds_the_same_covers_to_the_new_games()
    {
        // GOG has 31 games: 25 on page 1, 6 on page 2.
        using var window = await ArcadiaWindow.ShowAsync();
        var arcadia = window.Arcadia;
        var first = window.Tile(arcadia.PageGames[0]);
        var covers = Covers(window);
        Assert.Equal(25, covers.Count);

        window.Press(UiAction.NextPage);
        Assert.Equal(6, arcadia.PageGames.Count);
        Assert.Same(first, window.Tile(arcadia.PageGames[0]));
        Assert.Equal(arcadia.PageGames[0].Title, ToolTip.GetTip(first));
        Assert.Equal(covers, Covers(window));
        Assert.Equal(6, covers.Count(cover => cover.IsEffectivelyVisible));
        // The hidden places take no room: the six games are one row, nothing to scroll.
        Assert.True(window.Named<ItemsControl>("GameGrid").Bounds.Height < 2 * first.Bounds.Height, "Die leeren Plätze nehmen Zeilen ein.");

        var gog = arcadia.SelectedTab;
        window.Press(UiAction.NextTab);
        Assert.NotSame(gog, arcadia.SelectedTab);
        Assert.Same(first, window.Tile(arcadia.PageGames[0]));
        Assert.Equal(covers, Covers(window));
        Assert.Equal(arcadia.PageGames.Select(tile => tile.Title), covers.Where(cover => cover.IsEffectivelyVisible).Select(cover => ToolTip.GetTip(cover)));
        window.AssertRingAround(first, "erstes Cover nach dem Tabwechsel", ring: "FocusRingLifted");
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
        Assert.Equal(["Right", "NextPage", "NextTab", "Launcher 2"], lines.Select(line => FrameLine().Match(line) is { Success: true } match ? match.Groups["what"].Value : line));

        var arcadia = window.Arcadia;
        arcadia.PressButton(GamepadButton.DPadRight).ReleaseButton(GamepadButton.DPadRight);
        Assert.Matches(FrameLine(), Assert.Single(arcadia.LatencyLines));
        Assert.Equal("Right", FrameLine().Match(arcadia.LatencyLines[0]).Groups["what"].Value);
    }

    [Fact]
    public void A_press_is_logged_only_after_its_frame_with_its_wait_and_handling()
    {
        Action? frame = null;
        var lines = new List<string>();
        var latency = new InputLatency(shown => frame = shown, lines.Add);
        var pressedAt = InputLatency.Now() - System.Diagnostics.Stopwatch.Frequency * 30 / 1000;
        var handled = false;

        latency.Measure("NextTab", pressedAt, () => handled = true);
        Assert.True(handled);
        Assert.Empty(lines);

        Assert.NotNull(frame);
        frame();
        var match = FrameLine().Match(Assert.Single(lines));
        Assert.True(match.Success, lines[0]);
        Assert.Equal("NextTab", match.Groups["what"].Value);
        Assert.InRange(double.Parse(match.Groups["wait"].Value, System.Globalization.CultureInfo.InvariantCulture), 30, 10_000);
        Assert.InRange(double.Parse(match.Groups["total"].Value, System.Globalization.CultureInfo.InvariantCulture), 30, 10_000);
    }

    private static List<Button> Covers(ArcadiaWindow window) =>
        window.Named<ItemsControl>("GameGrid").GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("tile")).ToList();

    [GeneratedRegex(@"Bild: (?<what>.+) nach (?<total>\d+\.\d) ms \(Warten (?<wait>\d+\.\d), Verarbeitung \d+\.\d\)$")]
    private static partial Regex FrameLine();
}
