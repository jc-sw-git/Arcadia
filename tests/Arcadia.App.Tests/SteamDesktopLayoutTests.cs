using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// "Steams Desktop-Steuerung zulassen" on Steam's launcher page (#315): where the row is, what it says, and that it
/// decides at once what the copy of the Steam Controller passes to Steam, like for every other controller.
/// </summary>
public sealed class SteamDesktopLayoutTests
{
    private static readonly LauncherSpec[] SteamAndGog = [new(KnownLaunchers.Steam, 3), new(KnownLaunchers.Gog, 3)];

    [AvaloniaFact]
    [Trait("TestCase", "T315-1")]
    public async Task The_row_is_on_Steams_page_in_its_own_group_also_while_Steam_is_inactive_and_switches_with_the_controller_alone()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["gog"] });
        var launchers = arcadia.OpenLauncherSettings();
        arcadia.FocusRow("steam");
        arcadia.Press(UiAction.Accept);
        Assert.Equal("steam", launchers.PageLauncherId);

        var row = Assert.IsType<SteamDesktopLayoutRowViewModel>(launchers.Rows[^1]);
        Assert.Equal(("Steams Desktop-Steuerung zulassen", "DESKTOP-STEUERUNG"), (row.Title, row.Group));
        // Off by default: allowing Steam's desktop control is the choice, ignoring it the rule.
        Assert.False(row.IsOn);
        Assert.Equal("Steams Desktop-Steuerung wird momentan ignoriert", row.Subtitle);
        Assert.True(arcadia.Settings.Current.IgnoreSteamDesktopLayout);

        arcadia.FocusRow(SteamDesktopLayoutRowViewModel.RowKey);
        arcadia.Press(UiAction.Left);
        Assert.True(row.IsOn);
        Assert.False(arcadia.Settings.Current.IgnoreSteamDesktopLayout);
        Assert.Equal("Steams Desktop-Steuerung ist momentan aktiviert", row.Subtitle);
        arcadia.Press(UiAction.Accept);
        Assert.True(arcadia.Settings.Current.IgnoreSteamDesktopLayout);
        Assert.Equal("Steams Desktop-Steuerung wird momentan ignoriert", row.Subtitle);

        // B goes back to the list, again B leaves the settings.
        arcadia.Press(UiAction.Back);
        Assert.Null(launchers.PageLauncherId);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T315-1")]
    public async Task The_row_is_on_no_other_launchers_page_and_gone_from_the_controller_settings()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"] });
        var launchers = arcadia.OpenLauncherSettings();
        arcadia.FocusRow("gog");
        arcadia.Press(UiAction.Accept);
        Assert.Equal("gog", launchers.PageLauncherId);
        Assert.DoesNotContain(launchers.Rows, row => row.Key == SteamDesktopLayoutRowViewModel.RowKey);

        arcadia.Press(UiAction.Back).Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
        Assert.DoesNotContain(controller.Rows, row => row.Key == SteamDesktopLayoutRowViewModel.RowKey);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T315-1")]
    public async Task The_info_panel_names_the_buttons_as_glyphs_and_sets_the_Steam_Controllers_doubled_input_apart_in_bold()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam"] });
        var row = new SteamDesktopLayoutRowViewModel(new SettingsChanges(arcadia.Settings));

        var texts = row.Info.OfType<InfoText>().Select(block => block.Text).ToList();
        Assert.Contains(texts, text => text.Contains("Guide-Taste ([Guide]) mit dem rechten Stick ([RS])", StringComparison.Ordinal) && text.Contains("⧉ + ≡", StringComparison.Ordinal));
        Assert.Contains(texts, text => text.StartsWith("Um den Wechsel des Aktionssets (⧉ + ≡)", StringComparison.Ordinal));
        var note = Assert.Single(row.Info.OfType<InfoNote>());
        Assert.True(note.IsInfo && note.IsBold);
        Assert.StartsWith("Beim Steam Controller können darüber hinaus", note.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T315-2")]
    [Trait("TestCase", "T315-3")]
    public async Task Switching_the_setting_changes_at_once_what_the_copy_passes_to_Steam_while_Arcadia_is_in_front()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        arcadia.IsArcadiaInFront = true;
        Assert.False(arcadia.CopyWiring.Forwarding());
        Assert.True(arcadia.CopyWiring.Combos().BackAndStart);

        SwitchOnSteamsPage(arcadia);
        Assert.False(arcadia.Settings.Current.IgnoreSteamDesktopLayout);
        Assert.True(arcadia.CopyWiring.Forwarding());
        Assert.False(arcadia.CopyWiring.Combos().BackAndStart);
        // Arcadia's own pad keeps ⧉ + ≡ from games all the same.
        Assert.True(arcadia.XboxPad.Combos().BackAndStart);

        arcadia.IsArcadiaInFront = false;
        arcadia.IsMouseModeOn = true;
        Assert.True(arcadia.CopyWiring.Forwarding());
        Assert.False(arcadia.XboxPad.ForwardingAllowed());

        arcadia.IsMouseModeOn = false;
        arcadia.IsArcadiaInFront = true;
        SwitchOnSteamsPage(arcadia);
        Assert.False(arcadia.CopyWiring.Forwarding());
        Assert.True(arcadia.CopyWiring.Combos().BackAndStart);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T315-4")]
    public async Task A_game_on_Arcadia_keeps_the_pad_whatever_the_setting_and_its_changes_are_logged()
    {
        using var arcadia = await StartWithSteamControllerAsync(new AppSettings { ActiveLaunchers = ["steam", "gog"], IgnoreSteamDesktopLayout = false });
        arcadia.Press(UiAction.NextTab);
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count == 1, "GOG soll das Spiel starten");
        await arcadia.TickSteamControllerAsync();

        arcadia.IsArcadiaInFront = false;
        Assert.False(arcadia.CopyWiring.Forwarding());
        Assert.True(arcadia.XboxPad.ForwardingAllowed());

        arcadia.IsArcadiaInFront = true;
        Assert.True(arcadia.CopyWiring.Forwarding());
        Assert.False(arcadia.XboxPad.ForwardingAllowed());
        await arcadia.WaitUntilAsync(() => ArcadiaDriver.Log.Contains("Kopie für Steam: gibt Eingaben weiter (Arcadia vorne, Steams Desktop-Belegung aktiv)", StringComparison.Ordinal), "Die Kopie soll ihre Änderung loggen");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T315-3")]
    public async Task On_the_desktop_without_a_game_Steam_gets_the_copy_only_while_its_desktop_layout_is_not_ignored()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        SwitchOnSteamsPage(arcadia);
        Assert.False(arcadia.Settings.Current.IgnoreSteamDesktopLayout);

        arcadia.IsArcadiaInFront = false;
        Assert.True(arcadia.CopyWiring.Forwarding());
        Assert.False(arcadia.XboxPad.ForwardingAllowed());

        // ▶ switches it on again, from the controller alone.
        arcadia.IsArcadiaInFront = true;
        var launchers = arcadia.OpenLauncherSettings();
        arcadia.FocusRow("steam");
        arcadia.Press(UiAction.Accept);
        arcadia.FocusRow(SteamDesktopLayoutRowViewModel.RowKey);
        arcadia.Press(UiAction.Right);
        Assert.True(arcadia.Settings.Current.IgnoreSteamDesktopLayout);
        arcadia.Press(UiAction.Back).Press(UiAction.Back);
        Assert.Null(launchers.PageLauncherId);

        arcadia.IsArcadiaInFront = false;
        Assert.False(arcadia.CopyWiring.Forwarding());
        Assert.True(arcadia.XboxPad.ForwardingAllowed());
    }

    [AvaloniaFact]
    [Trait("TestCase", "T315-4")]
    public async Task Switched_while_a_game_on_Arcadia_runs_the_game_keeps_the_pad_and_only_Arcadia_in_front_follows_the_setting()
    {
        using var arcadia = await StartWithSteamControllerAsync(new AppSettings { ActiveLaunchers = ["steam", "gog"], IgnoreSteamDesktopLayout = false });
        arcadia.Press(UiAction.NextTab);
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count == 1, "GOG soll das Spiel starten");
        await arcadia.TickSteamControllerAsync();

        SwitchOnSteamsPage(arcadia);
        Assert.True(arcadia.Settings.Current.IgnoreSteamDesktopLayout);
        Assert.False(arcadia.CopyWiring.Forwarding());
        await arcadia.WaitUntilAsync(() => ArcadiaDriver.Log.Contains("Kopie für Steam: meldet Ruhe (Arcadia vorne, Steams Desktop-Belegung ignoriert)", StringComparison.Ordinal), "Die Kopie soll ihre Änderung loggen");

        arcadia.IsArcadiaInFront = false;
        Assert.False(arcadia.CopyWiring.Forwarding());
        Assert.True(arcadia.XboxPad.ForwardingAllowed());
        Assert.True(arcadia.XboxPad.IsPlugged);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T315-4")]
    public async Task A_game_on_Steam_Eingabe_gets_the_Steam_Controller_through_the_copy_whatever_the_setting()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        Assert.Equal("Steam", arcadia.SelectedTab?.Name);
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count == 1, "Steam soll das Spiel starten");
        await arcadia.TickSteamControllerAsync();

        arcadia.IsArcadiaInFront = false;
        Assert.True(arcadia.CopyWiring.Forwarding());
        Assert.False(arcadia.XboxPad.ForwardingAllowed());

        arcadia.IsArcadiaInFront = true;
        SwitchOnSteamsPage(arcadia);
        Assert.False(arcadia.Settings.Current.IgnoreSteamDesktopLayout);
        arcadia.IsArcadiaInFront = false;
        Assert.True(arcadia.CopyWiring.Forwarding());
        Assert.False(arcadia.XboxPad.ForwardingAllowed());
        Assert.False(arcadia.XboxPad.IsPlugged);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T315-7")]
    public async Task With_Steam_installed_the_controller_settings_end_with_a_row_that_opens_Steams_page_on_Steam_Controller()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["gog"] });
        arcadia.OpenLauncherSettings();
        arcadia.Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);

        var row = Assert.IsType<ActionRowViewModel>(controller.Rows[^1]);
        Assert.Equal(
            (ControllerSettingsViewModel.SteamLauncherKey, "STEAM", "Controller-Einstellungen für Steam", "Weitere Controller-Einstellungen für Steam.", "Steam-Einstellungen"),
            (row.Key, row.Group, row.Title, row.Subtitle, row.ActionText));
        Assert.StartsWith("Arcadia hat erkannt, dass du Steam installiert hast.", Assert.IsType<InfoText>(Assert.Single(row.Info)).Text, StringComparison.Ordinal);

        arcadia.FocusRow(ControllerSettingsViewModel.SteamLauncherKey);
        arcadia.Press(UiAction.Accept);
        var launchers = Assert.IsType<LauncherSettingsViewModel>(arcadia.Main.Settings!.Selected);
        Assert.Equal("steam", launchers.PageLauncherId);
        Assert.Equal(LauncherControllerModeRowViewModel.RowKey, arcadia.Main.Settings.FocusedRow?.Key);

        // B goes back to the launcher list, as from any launcher page.
        arcadia.Press(UiAction.Back);
        Assert.Null(launchers.PageLauncherId);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T315-7")]
    public async Task Without_Steam_installed_the_controller_settings_have_no_group_Steam()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([new(KnownLaunchers.Gog, 3)], new AppSettings { ActiveLaunchers = ["gog"] });
        arcadia.OpenLauncherSettings();
        arcadia.Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);

        Assert.DoesNotContain(controller.Rows, row => row.Key == ControllerSettingsViewModel.SteamLauncherKey || row.Group == "STEAM");
    }

    private static async Task<ArcadiaDriver> StartWithSteamControllerAsync(AppSettings? settings = null)
    {
        var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, settings ?? new AppSettings { ActiveLaunchers = ["steam", "gog"] });
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        return arcadia;
    }

    /// <summary>≡, Steam's page, A on the row, then out again.</summary>
    private static void SwitchOnSteamsPage(ArcadiaDriver arcadia)
    {
        var launchers = arcadia.OpenLauncherSettings();
        arcadia.FocusRow("steam");
        arcadia.Press(UiAction.Accept);
        Assert.Equal("steam", launchers.PageLauncherId);
        arcadia.FocusRow(SteamDesktopLayoutRowViewModel.RowKey);
        arcadia.Press(UiAction.Accept);
        arcadia.Press(UiAction.Back).Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }
}
