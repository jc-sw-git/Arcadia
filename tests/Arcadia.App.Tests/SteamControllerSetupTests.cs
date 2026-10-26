using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.Tests.Fixtures;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// "SISR installieren" writes the "SISR Marker" into the Steam libraries of the accounts in Steam's own files (#85): the
/// real platform reads <c>loginusers.vdf</c> and writes each account's <c>shortcuts.vdf</c>; only Windows is pretended.
/// </summary>
public sealed class SteamControllerSetupTests
{
    /// <summary>SISR as before #213 part B: these tests cover its switch and setup until #214 removes them.</summary>
    private static readonly StartArguments Sisr = new(Sisr: true);

    private const uint Anna = 100_000_011;
    private const uint Bert = 100_000_012;
    private const uint Carla = 100_000_013;

    [AvaloniaFact]
    [Trait("TestCase", "T85-15")]
    public async Task With_one_Steam_account_the_marker_is_written_without_asking_and_SISR_starts()
    {
        using var files = SteamWith((Anna, "Anna", 1_790_000_000, true));
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files, arguments: Sisr);
        var controller = OpenSisrRow(arcadia);

        arcadia.Press(UiAction.Accept);
        Assert.Equal(SettingsPrompt.ConfirmSteamStop, controller.Prompt);
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Settings.Current.SteamControllerSupport, "die Option soll angehen");

        Assert.Single(Markers(files, Anna));
        Assert.Equal(1, files.ControllerSupport!.Sisr.Starts);
        Assert.True(controller.Rows.Single(row => row.Key == "sisr") is SisrRowViewModel { IsSwitch: true, IsOn: true });
    }

    [AvaloniaFact]
    [Trait("TestCase", "T85-15")]
    public async Task Without_any_Steam_account_the_install_says_so_at_once_and_leaves_Steam_and_the_option_alone()
    {
        using var files = SteamWith();
        files.ControllerSupport!.CloseSteam();
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files, arguments: Sisr);
        var controller = OpenSisrRow(arcadia);

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => ErrorOf(controller) is not null, "eine Meldung soll erscheinen");

        Assert.Contains("kein Steam-Konto gefunden", ErrorOf(controller), StringComparison.Ordinal);
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        Assert.False(files.ControllerSupport.Sisr.IsInstalled);
        Assert.False(arcadia.Settings.Current.SteamControllerSupport);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T85-15")]
    public async Task Without_any_Steam_account_a_running_Steam_is_not_closed()
    {
        using var files = SteamWith();
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files, arguments: Sisr);
        var controller = OpenSisrRow(arcadia);

        // Arcadia asks first whether it may close Steam; the check comes before Steam closes.
        arcadia.Press(UiAction.Accept);
        Assert.Equal(SettingsPrompt.ConfirmSteamStop, controller.Prompt);
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => ErrorOf(controller) is not null, "eine Meldung soll erscheinen");

        Assert.Contains("kein Steam-Konto gefunden", ErrorOf(controller), StringComparison.Ordinal);
        Assert.Equal(0, files.ControllerSupport!.SteamStops);
        Assert.True(files.ControllerSupport.IsSteamRunning);
        Assert.False(files.ControllerSupport.Sisr.IsInstalled);
        Assert.False(arcadia.Settings.Current.SteamControllerSupport);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T85-19")]
    public async Task With_several_accounts_the_user_picks_with_the_D_pad_who_gets_the_marker_before_Steam_closes()
    {
        // Written in neither order: Carla logged in last (MostRecent, though Bert has the newer timestamp), Anna has the
        // marker already, Dora's folder is left from an account Steam no longer lists. The list puts the last login first,
        // then goes by name.
        using var files = SteamWith((Bert, "Bert", 1_790_000_300, false), (Carla, "Carla", 1_790_000_100, true), (Anna, "Anna", 1_790_000_200, false));
        const uint dora = 100_000_014;
        Directory.CreateDirectory(Path.Combine(files.Steam.UserData(dora), "config"));
        var sisr = files.ControllerSupport!.Sisr.ExecutablePath;
        Arcadia.Launchers.Steam.SteamShortcuts.Add(Path.Combine(files.Steam.UserData(Anna), "config", "shortcuts.vdf"), "SISR Marker", sisr, "--marker");
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files, arguments: Sisr);
        var controller = OpenSisrRow(arcadia);

        arcadia.Press(UiAction.Accept);
        Assert.Equal(SettingsPrompt.ChooseMarkerAccounts, controller.Prompt);
        Assert.Equal(["Carla", "Anna", "Bert"], controller.MarkerAccounts.Select(a => a.Name));
        Assert.Equal([true, true, false], controller.MarkerAccounts.Select(a => a.IsChecked));
        Assert.True(controller.MarkerAccounts.Single(a => a.Name == "Anna").HasMarker);
        Assert.True(controller.MarkerAccounts.Single(a => a.Name == "Carla").IsFocused);
        Assert.Equal(0, files.ControllerSupport.SteamStops);

        // Anna, who has the marker, is skipped: the D-pad goes to Bert, A ticks him; on to "Weiter".
        arcadia.Press(UiAction.Down);
        Assert.True(controller.MarkerAccounts.Single(a => a.Name == "Bert").IsFocused);
        arcadia.Press(UiAction.Accept);
        Assert.True(controller.MarkerAccounts.Single(a => a.Name == "Bert").IsChecked);
        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        Assert.Equal(SettingsPrompt.ConfirmSteamStop, controller.Prompt);
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Settings.Current.SteamControllerSupport, "die Option soll angehen");

        Assert.Single(Markers(files, Anna));
        Assert.Single(Markers(files, Bert));
        Assert.Single(Markers(files, Carla));
        Assert.Empty(Markers(files, dora));
        Assert.Equal(1, files.ControllerSupport.Sisr.Starts);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T85-19")]
    public async Task Without_a_tick_there_is_no_going_on_and_B_cancels_the_install()
    {
        using var files = SteamWith((Anna, "Anna", 1_790_000_300, true), (Bert, "Bert", 1_790_000_200, false));
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files, arguments: Sisr);
        var controller = OpenSisrRow(arcadia);
        arcadia.Press(UiAction.Accept);

        // A on Anna takes her tick away: "Weiter" does nothing then.
        arcadia.Press(UiAction.Accept);
        Assert.False(controller.CanConfirmMarkerAccounts);
        arcadia.Press(UiAction.Down, times: 2).Press(UiAction.Accept);
        Assert.Equal(SettingsPrompt.ChooseMarkerAccounts, controller.Prompt);

        arcadia.Press(UiAction.Back);

        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        Assert.Equal(0, files.ControllerSupport!.SteamStops);
        Assert.False(files.ControllerSupport.Sisr.IsInstalled);
        Assert.Empty(Markers(files, Anna));
        Assert.Empty(Markers(files, Bert));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T85-19")]
    public async Task With_only_another_account_chosen_SISR_does_not_start_and_the_settings_offer_to_add_the_entry()
    {
        using var files = SteamWith((Anna, "Anna", 1_790_000_300, true), (Bert, "Bert", 1_790_000_200, false));
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files, arguments: Sisr);
        var controller = OpenSisrRow(arcadia);
        arcadia.Press(UiAction.Accept);

        // Anna off, Bert on, "Weiter", "Steam beenden und installieren".
        arcadia.Press(UiAction.Accept).Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.Down).Press(UiAction.Accept);
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Settings.Current.SteamControllerSupport, "die Option soll angehen");

        Assert.Empty(Markers(files, Anna));
        Assert.Single(Markers(files, Bert));
        Assert.Equal(0, files.ControllerSupport!.Sisr.Starts);
        await arcadia.WaitUntilAsync(
            () => controller.Rows.Any(row => row.Title == "Steam-Eintrag für SISR fehlt" && row.ActivateText == "Anlegen"),
            "die Einstellungen sollen „Steam-Eintrag für SISR fehlt“ mit „Anlegen“ zeigen");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T85-19")]
    public async Task With_one_account_listed_the_question_does_not_come_even_if_more_userdata_folders_exist()
    {
        using var files = SteamWith((Anna, "Anna", 1_790_000_300, true));
        Directory.CreateDirectory(Path.Combine(files.Steam.UserData(Bert), "config"));
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files, arguments: Sisr);
        var controller = OpenSisrRow(arcadia);

        arcadia.Press(UiAction.Accept);

        Assert.Equal(SettingsPrompt.ConfirmSteamStop, controller.Prompt);
    }

    private static LauncherFiles SteamWith(params (uint Id, string Name, long Timestamp, bool MostRecent)[] accounts)
    {
        var files = new LauncherFiles { OnWindows = true };
        files.Steam.Install();
        foreach (var account in accounts)
        {
            files.Steam.AddAccount(account.Id, account.Name, account.Timestamp, account.MostRecent);
        }

        return files;
    }

    /// <summary>Settings, RB to "Controller", down to "Steam Controller Kompatibilität".</summary>
    private static ControllerSettingsViewModel OpenSisrRow(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        var settings = arcadia.Main.Settings!;
        var controller = Assert.IsType<ControllerSettingsViewModel>(settings.Selected);
        for (var i = 0; i < 10 && settings.FocusedRow?.Key != "sisr"; i++)
        {
            arcadia.Press(UiAction.Down);
        }

        Assert.Equal("sisr", settings.FocusedRow?.Key);
        return controller;
    }

    private static string? ErrorOf(ControllerSettingsViewModel controller) =>
        controller.Rows.Single(row => row.Key == "sisr").Info.OfType<InfoSection>().SelectMany(section => section.Blocks).OfType<InfoError>().FirstOrDefault()?.Text;

    private static IEnumerable<Arcadia.Launchers.Steam.SteamShortcut> Markers(LauncherFiles files, uint account) =>
        files.Steam.ShortcutsOf(account).Where(s => s.Name == "SISR Marker" && s.LaunchOptions.Contains("--marker", StringComparison.Ordinal));
}
