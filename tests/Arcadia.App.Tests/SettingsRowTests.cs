using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>What the rows of the settings page offer and how their values step (#15).</summary>
public sealed class SettingsRowTests
{
    /// <summary>SISR as before #213 part B: these tests cover its switch and setup until #214 removes them.</summary>
    private static readonly StartArguments Sisr = new(Sisr: true);

    [AvaloniaFact]
    [Trait("TestCase", "T15-5")]
    public async Task The_footer_names_only_the_buttons_of_the_focused_row()
    {
        var steam = new FakeSteam { IsSteamRunning = true, HasMarker = false };
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam, arguments: Sisr, settings: new AppSettings
        {
            ActiveLaunchers = [.. ArcadiaDriver.DefaultLaunchers.Select(spec => spec.Launcher.Id)],
            SteamControllerSupport = true,
        });
        await arcadia.TickControllerSupportAsync();
        arcadia.Press(UiAction.Settings);
        var settings = arcadia.Main.Settings!;
        Assert.True(settings.HasSeveralCategories, "Der Footer nennt „LB RB Kategorie“.");

        // A launcher: ◀▶ switch it, A opens its page.
        Assert.Equal(new Footer(Change: true, Activate: "Öffnen"), Footer.Of(settings.FocusedRow!));

        arcadia.Press(UiAction.NextTab);
        await arcadia.WaitUntilAsync(() => settings.Selected.Rows.Any(row => row.Key == "issue:marker-missing"), "die Meldung zum fehlenden Steam-Eintrag");

        // A problem with its fix: only A, named by the fix.
        Assert.Equal(new Footer(Change: false, Activate: "Anlegen"), Footer.Of(arcadia.FocusRow("issue:marker-missing")));

        // A choice: only ◀▶, A counting on goes without saying.
        Assert.Equal(new Footer(Change: true, Activate: null), Footer.Of(arcadia.FocusRow("mouse-mode")));

        // A switch: ◀▶ and "A Umschalten".
        Assert.Equal(new Footer(Change: true, Activate: "Umschalten"), Footer.Of(arcadia.FocusRow("steam-desktop-layout")));

        // A row that only shows something: neither.
        arcadia.Press(UiAction.NextTab);
        Assert.Equal("Über Arcadia", settings.Selected.Name);
        Assert.Equal(new Footer(Change: false, Activate: null), Footer.Of(settings.FocusedRow!));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T15-11")]
    public async Task Choices_wrap_at_their_ends_and_switches_flip_with_either_arrow()
    {
        var settings = new AppSettings
        {
            ActiveLaunchers = [KnownLaunchers.Gog.Id, KnownLaunchers.Xbox.Id],
            MouseMode = new MouseModeSettings { PointerSpeed = 1 },
        };
        using var arcadia = await ArcadiaDriver.StartAsync(
            [new LauncherSpec(KnownLaunchers.Gog, 3), new LauncherSpec(KnownLaunchers.Xbox, 3), new LauncherSpec(KnownLaunchers.Amazon, 3, IsInstalled: false)], settings);
        arcadia.Press(UiAction.Settings);

        // "Aktiv" of a launcher with either arrow; one that isn't installed stays off.
        var gog = arcadia.Main.Settings!.FocusedRow!;
        string[] both = [KnownLaunchers.Gog.Id, KnownLaunchers.Xbox.Id];
        arcadia.Press(UiAction.Left);
        Assert.Equal([KnownLaunchers.Xbox.Id], arcadia.Settings.Current.ActiveLaunchers);
        arcadia.Press(UiAction.Left);
        Assert.Equal(both, arcadia.Settings.Current.ActiveLaunchers!.Order());
        arcadia.Press(UiAction.Right);
        Assert.Equal([KnownLaunchers.Xbox.Id], arcadia.Settings.Current.ActiveLaunchers);
        arcadia.Press(UiAction.Right);
        Assert.Equal("gog", gog.Key);
        var amazon = arcadia.FocusRow("amazon");
        Assert.False(amazon.CanChange);
        arcadia.Press(UiAction.Left).Press(UiAction.Right);
        Assert.Equal(both, arcadia.Settings.Current.ActiveLaunchers!.Order());

        // "Zeigergeschwindigkeit" on 1: ◀ gives 9, the arrow on the left too; ▶ on 9 gives 1. No arrow is greyed out.
        arcadia.Press(UiAction.NextTab);
        var speed = Assert.IsAssignableFrom<ChoiceRowViewModel>(arcadia.FocusRow("mouse-mode-speed"));
        Assert.True(speed.CanDecrease && speed.CanIncrease);
        arcadia.Press(UiAction.Left);
        Assert.Equal("9", speed.ValueText);
        Assert.True(speed.CanDecrease && speed.CanIncrease);
        arcadia.Press(UiAction.Right);
        Assert.Equal("1", speed.ValueText);
        arcadia.Click(speed.StepBackCommand);
        Assert.Equal(9, arcadia.Settings.Current.MouseMode.PointerSpeed);
        arcadia.Click(speed.StepOnCommand);
        Assert.Equal(1, arcadia.Settings.Current.MouseMode.PointerSpeed);

        // A switch ("An" / "Aus") with either arrow.
        var layout = Assert.IsAssignableFrom<SwitchRowViewModel>(arcadia.FocusRow("steam-desktop-layout"));
        var on = layout.IsOn;
        arcadia.Press(UiAction.Right);
        Assert.Equal(!on, arcadia.Settings.Current.IgnoreSteamDesktopLayout);
        arcadia.Press(UiAction.Right);
        Assert.Equal(on, arcadia.Settings.Current.IgnoreSteamDesktopLayout);
        arcadia.Press(UiAction.Left);
        Assert.Equal(!on, arcadia.Settings.Current.IgnoreSteamDesktopLayout);
        arcadia.Press(UiAction.Left);
        Assert.Equal(on, arcadia.Settings.Current.IgnoreSteamDesktopLayout);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T15-11")]
    public async Task Steam_Controller_Kompatibilitaet_switches_with_either_arrow()
    {
        var steam = new FakeSteam { IsSteamRunning = true };
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam, arguments: Sisr);
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        var sisr = Assert.IsType<SisrRowViewModel>(arcadia.Main.Settings!.FocusedRow);

        foreach (var (arrow, on) in new[] { (UiAction.Right, true), (UiAction.Right, false), (UiAction.Left, true), (UiAction.Left, false) })
        {
            arcadia.Press(arrow);
            await arcadia.WaitOnClockUntilAsync(() => arcadia.Settings.Current.SteamControllerSupport == on && !sisr.IsLocked, $"{arrow} schaltet auf {(on ? "An" : "Aus")}", TimeSpan.FromSeconds(10));
            Assert.Equal(on, sisr.IsOn);
        }
    }

    /// <summary>What the footer shows for a row: "◀▶ Ändern", and A with its text, or neither.</summary>
    private sealed record Footer(bool Change, string? Activate)
    {
        public static Footer Of(SettingsRowViewModel row) => new(row.CanChange, row.ShowsActivateHint ? row.ActivateText : null);
    }
}
