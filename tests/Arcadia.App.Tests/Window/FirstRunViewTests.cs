using Arcadia.App.Input;
using Arcadia.App.Setup;
using Arcadia.App.Views;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>The first start's question which launchers Arcadia uses (#139) as drawn, with every input.</summary>
public sealed class FirstRunViewTests
{
    private static readonly IReadOnlyList<LauncherSpec> TwoFoundOneMissing =
    [
        new(KnownLaunchers.Gog, 31),
        new(KnownLaunchers.Xbox, 62),
        new(KnownLaunchers.Amazon, 6, IsInstalled: false),
    ];

    [AvaloniaTheory]
    [InlineData("Enter")]
    [InlineData("Klick")]
    [Trait("TestCase", "T139-3")]
    public async Task A_launcher_switched_off_then_Fertig_by_Enter_or_a_click_leaves_only_the_others_as_tabs_also_after_a_restart(string how)
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(TwoFoundOneMissing, new AppSettings());
        var firstRun = arcadia.Arcadia.Main.FirstRun!;
        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        Assert.Equal("Aus", ValueShown(arcadia, firstRun.Found[1]));

        if (how == "Enter")
        {
            arcadia.Key(Key.Enter);
        }
        else
        {
            arcadia.Click(Finish(arcadia));
        }

        await AssertLibraryAsync(arcadia, ["GOG Galaxy"], how);

        using var restarted = await arcadia.RestartAsync();
        Assert.False(restarted.Arcadia.Main.IsFirstRunShown);
        Assert.False(View(restarted).IsEffectivelyVisible);
        Assert.Equal(["GOG Galaxy"], restarted.Arcadia.Main.Tabs.Select(tab => tab.Name));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T139-6")]
    public async Task B_does_nothing_and_the_controller_alone_switches_and_finishes()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(TwoFoundOneMissing, new AppSettings());
        var firstRun = arcadia.Arcadia.Main.FirstRun!;
        var (gog, xbox) = (firstRun.Found[0], firstRun.Found[1]);

        arcadia.Press(UiAction.Back);
        AssertStillAsking(arcadia, gog, "B");

        arcadia.Press(UiAction.Down);
        AssertFocused(arcadia, xbox, "▼");
        arcadia.Press(UiAction.Right);
        Assert.Equal("Aus", ValueShown(arcadia, xbox));
        arcadia.Press(UiAction.Left);
        Assert.Equal("An", ValueShown(arcadia, xbox));
        arcadia.Press(UiAction.Accept);
        Assert.Equal("Aus", ValueShown(arcadia, xbox));
        arcadia.Press(UiAction.Up);
        AssertFocused(arcadia, gog, "▲");

        arcadia.Press(UiAction.Settings);
        await AssertLibraryAsync(arcadia, ["GOG Galaxy"], "≡");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T139-6")]
    public async Task Esc_does_nothing_and_one_hand_on_the_keyboard_switches_and_finishes()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(TwoFoundOneMissing, new AppSettings());
        var firstRun = arcadia.Arcadia.Main.FirstRun!;
        var (gog, xbox) = (firstRun.Found[0], firstRun.Found[1]);

        arcadia.Key(Key.Escape);
        AssertStillAsking(arcadia, gog, "Esc");

        arcadia.Key(Key.S);
        AssertFocused(arcadia, xbox, "S");
        arcadia.Key(Key.D);
        Assert.Equal("Aus", ValueShown(arcadia, xbox));
        arcadia.Key(Key.A);
        Assert.Equal("An", ValueShown(arcadia, xbox));
        arcadia.Key(Key.S);
        // A launcher not found is drawn dimmed, its ring too; that it is focused is enough here.
        AssertFocused(arcadia, firstRun.Missing[0], "S", withRing: false);
        arcadia.Key(Key.W, times: 2);
        AssertFocused(arcadia, gog, "W");
        arcadia.Key(Key.D);
        Assert.Equal("Aus", ValueShown(arcadia, gog));

        arcadia.Key(Key.Enter);
        await AssertLibraryAsync(arcadia, ["Xbox"], "Enter");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T139-6")]
    public async Task The_mouse_alone_focuses_by_hovering_switches_with_a_click_on_the_switch_and_finishes_with_Fertig()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(TwoFoundOneMissing, new AppSettings());
        var firstRun = arcadia.Arcadia.Main.FirstRun!;
        var (gog, xbox) = (firstRun.Found[0], firstRun.Found[1]);

        arcadia.MouseMove(arcadia.CentreOf(RowButton(arcadia, xbox)));
        AssertFocused(arcadia, xbox, "Maus über Xbox");
        Assert.Equal("An", ValueShown(arcadia, xbox));

        arcadia.Click(Toggle(arcadia, xbox));
        Assert.Equal("Aus", ValueShown(arcadia, xbox));
        arcadia.Click(Arrows(arcadia, xbox)[1]);
        Assert.Equal("An", ValueShown(arcadia, xbox));

        // A click on the switch of the row not focused yet: the hover focuses it on the way there.
        arcadia.Click(Toggle(arcadia, gog));
        AssertFocused(arcadia, gog, "Klick auf GOGs Schalter");
        Assert.Equal("Aus", ValueShown(arcadia, gog));

        arcadia.Click(Finish(arcadia));
        await AssertLibraryAsync(arcadia, ["Xbox"], "Klick auf Fertig");
    }

    private static FirstRunView View(ArcadiaWindow arcadia) => arcadia.Window.GetVisualDescendants().OfType<FirstRunView>().Single();

    private static Button RowButton(ArcadiaWindow arcadia, FirstRunLauncherViewModel row) => Assert.Single(
        View(arcadia).GetVisualDescendants().OfType<Button>(),
        button => button.Classes.Contains("settingRow") && button.DataContext == row);

    private static string? ValueShown(ArcadiaWindow arcadia, FirstRunLauncherViewModel row) => RowButton(arcadia, row).GetVisualDescendants().OfType<TextBlock>()
        .Single(text => text.Classes.Contains("valueText") && text.IsEffectivelyVisible).Text;

    private static Button Toggle(ArcadiaWindow arcadia, FirstRunLauncherViewModel row) =>
        RowButton(arcadia, row).GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("valueToggle"));

    private static IReadOnlyList<Button> Arrows(ArcadiaWindow arcadia, FirstRunLauncherViewModel row) => RowButton(arcadia, row).GetVisualDescendants().OfType<Button>()
        .Where(button => button.Classes.Contains("valueArrow"))
        .OrderBy(button => arcadia.Bounds(button).Left)
        .ToList();

    private static Button Finish(ArcadiaWindow arcadia) =>
        Assert.Single(View(arcadia).GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("finish") && button.IsEffectivelyVisible);

    private static void AssertFocused(ArcadiaWindow arcadia, FirstRunLauncherViewModel row, string how, bool withRing = true)
    {
        var focused = View(arcadia).GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("settingRow") && button.Classes.Contains("focused"))
            .Select(button => button.DataContext)
            .ToList();
        Assert.True(focused.Count == 1 && focused[0] == row, $"{how}: umrandet sind {string.Join(", ", focused.OfType<FirstRunLauncherViewModel>().Select(r => r.Name))} statt {row.Name}");
        if (withRing)
        {
            arcadia.AssertRingAround(RowButton(arcadia, row), how);
        }
    }

    private static void AssertStillAsking(ArcadiaWindow arcadia, FirstRunLauncherViewModel focused, string how)
    {
        Assert.True(arcadia.Arcadia.Main.IsFirstRunShown, $"{how} hat die Ersteinrichtung geschlossen");
        Assert.True(View(arcadia).IsEffectivelyVisible, how);
        AssertFocused(arcadia, focused, how);
        Assert.All(arcadia.Arcadia.Main.FirstRun!.Found, row => Assert.Equal("An", ValueShown(arcadia, row)));
    }

    private static async Task AssertLibraryAsync(ArcadiaWindow arcadia, IReadOnlyList<string> tabs, string how)
    {
        await arcadia.Arcadia.WaitUntilAsync(() => !arcadia.Arcadia.Main.IsFirstRunShown && arcadia.Arcadia.Main.Tabs.Count > 0, $"{how}: die Bibliothek soll nach \"Fertig\" erscheinen");
        arcadia.Settle();
        Assert.False(View(arcadia).IsEffectivelyVisible, how);
        Assert.Equal(tabs, arcadia.Arcadia.Main.Tabs.Select(tab => tab.Name));
        Assert.Contains("focused", arcadia.Tile(arcadia.Arcadia.Main.FocusedGame!).Classes);
    }
}
