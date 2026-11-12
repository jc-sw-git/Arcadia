using System.Text.RegularExpressions;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.Input;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>Every press logs how long it took to show, counted from the press (#229).</summary>
public sealed partial class InputLatencyTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T229-2")]
    public async Task View_and_Menu_count_their_time_from_the_press_not_from_when_they_act()
    {
        // ⧉ and ≡ act only on release or after the solo delay (#64); the user waited from the press. The fake clock runs
        // the solo delay, the sleeps are the real time held.
        using var arcadia = await ArcadiaDriver.StartAsync();
        var lines = arcadia.LatencyLines;

        arcadia.PressButton(GamepadButton.Menu);
        Thread.Sleep(50);
        arcadia.Wait(TimeSpan.FromMilliseconds(100)).ReleaseButton(GamepadButton.Menu);
        Assert.True(arcadia.Main.IsSettingsOpen);
        Assert.InRange(TotalOf(Assert.Single(lines)), 50, 10_000);
        arcadia.Press(UiAction.Back);

        arcadia.PressButton(GamepadButton.View);
        Thread.Sleep(50);
        arcadia.Wait(TimeSpan.FromMilliseconds(300));
        Assert.True(arcadia.Main.IsGameMenuOpen);
        Assert.Equal(2, lines.Count);
        Assert.InRange(TotalOf(lines[^1]), 50, 10_000);
        arcadia.ReleaseButton(GamepadButton.View).Press(UiAction.Back);

        // Together they act not at all, and leave no time behind for the next press.
        arcadia.PressButton(GamepadButton.View).PressButton(GamepadButton.Menu);
        Thread.Sleep(300);
        arcadia.Wait(TimeSpan.FromSeconds(1)).ReleaseButton(GamepadButton.Menu).ReleaseButton(GamepadButton.View).Wait(TimeSpan.FromMilliseconds(300));
        Assert.Equal(2, lines.Count);
        arcadia.PressButton(GamepadButton.Menu).ReleaseButton(GamepadButton.Menu);
        Assert.Equal(3, lines.Count);
        Assert.InRange(TotalOf(lines[^1]), 0, 299);
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

    private static double TotalOf(string line) =>
        double.Parse(FrameLine().Match(line).Groups["total"].Value, System.Globalization.CultureInfo.InvariantCulture);

    [GeneratedRegex(@"Bild: (?<what>.+) nach (?<total>\d+\.\d) ms \(Warten (?<wait>\d+\.\d), Verarbeitung \d+\.\d\)$")]
    internal static partial Regex FrameLine();
}
