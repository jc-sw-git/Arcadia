using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.App.Settings;
using Arcadia.App.Status;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Pan;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The header's status messages in a modal with Y (#166): opening it, its entries and order, the way into the settings
/// and the update, the live list and its empty state, and the buttons that moved for it (Ansicht and the launcher order
/// on L3). The real window (chip, ✕, clicks, layout) is in <c>Window/StatusModalViewTests</c>.
/// </summary>
public sealed class StatusModalTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private static readonly LauncherSpec[] SteamAndGog = [new(KnownLaunchers.Steam, 3), new(KnownLaunchers.Gog, 3)];

    private static async Task<ArcadiaDriver> StartAsync(FakeSteam steam, FakeReleases? releases = null, AppSettings? settings = null)
    {
        var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, settings ?? new AppSettings { PanEnabled = true, ActiveLaunchers = ["steam", "gog"] }, steam, releases);
        await arcadia.TickControllerSupportAsync();
        return arcadia;
    }

    private static StatusModalViewModel Modal(ArcadiaDriver arcadia) => Assert.IsType<StatusModalViewModel>(arcadia.Main.StatusModal);

    private static string[] Titles(ArcadiaDriver arcadia) => [.. Modal(arcadia).Entries.Select(entry => entry.Title)];

    [AvaloniaFact]
    [Trait("TestCase", "T166-1")]
    [Trait("TestCase", "T166-4")]
    public async Task Y_opens_Status_in_the_library_with_the_focus_on_the_first_entry_and_each_entry_names_level_title_short_text_and_time()
    {
        using var arcadia = await StartAsync(new FakeSteam { HidHideVersion = null });
        await arcadia.WaitUntilAsync(() => arcadia.Main.Status.HasEntries, "die Warnung im Header");

        arcadia.Press(UiAction.Status);

        var modal = Modal(arcadia);
        var entry = Assert.Single(modal.Entries);
        Assert.Same(entry, modal.Focused);
        Assert.True(entry.IsWarning);
        Assert.Equal("HidHide fehlt", entry.Title);
        Assert.Equal("Versteckt die echten Controller vor anderen Programmen", entry.ShortText);
        Assert.Equal($"seit {Formatting.Clock(arcadia.Time.GetLocalNow())}", entry.SinceText);
        Assert.Equal("Installieren", entry.FixLabel);
        Assert.Equal("1 Meldung", modal.CountText);
        Assert.Equal("Installieren", modal.AcceptText);

        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsStatusModalOpen);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T166-1")]
    public async Task Y_opens_Status_on_the_detail_page_too_and_B_returns_to_it()
    {
        using var arcadia = await StartAsync(new FakeSteam { HidHideVersion = null });
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        Assert.True(arcadia.Main.IsDetailOpen);

        arcadia.Press(UiAction.Status);
        Assert.True(arcadia.Main.IsStatusModalOpen);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsStatusModalOpen);
        Assert.True(arcadia.Main.IsDetailOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T166-2")]
    public async Task Without_a_message_Y_does_nothing_and_the_spinner_alone_is_no_entry()
    {
        using var arcadia = await StartAsync(new FakeSteam());
        Assert.False(arcadia.Main.Status.HasEntries);
        arcadia.Press(UiAction.Status);
        Assert.False(arcadia.Main.IsStatusModalOpen);
        Assert.False(arcadia.Main.IsDialogOpen);

        // "Controller wird eingerichtet …" (Pan still cleans up) is shown, but it is no message.
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        arcadia.Pan.Missing = PanClient.CleaningUp;
        await arcadia.TickSteamControllerAsync();
        Assert.True(arcadia.Main.Status.IsControllerStarting);
        Assert.False(arcadia.Main.Status.HasEntries);
        arcadia.Press(UiAction.Status);
        Assert.False(arcadia.Main.IsStatusModalOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T166-3")]
    public async Task Over_Ansicht_or_the_context_menu_Y_opens_no_modal()
    {
        using var arcadia = await StartAsync(new FakeSteam { HidHideVersion = null });

        arcadia.Press(UiAction.ViewOptions);
        Assert.True(arcadia.Main.IsViewOptionsOpen);
        arcadia.Press(UiAction.Status);
        Assert.False(arcadia.Main.IsStatusModalOpen);
        Assert.True(arcadia.Main.IsViewOptionsOpen);
        arcadia.Press(UiAction.Back);

        arcadia.Press(UiAction.Options);
        Assert.True(arcadia.Main.IsGameMenuOpen);
        arcadia.Press(UiAction.Status);
        Assert.False(arcadia.Main.IsStatusModalOpen);
        arcadia.Press(UiAction.Back);

        // Nor over the settings or the search, which cover the header.
        arcadia.Press(UiAction.Settings).Press(UiAction.Status);
        Assert.False(arcadia.Main.IsStatusModalOpen);
        arcadia.Press(UiAction.Back).Press(UiAction.Search).Press(UiAction.Status);
        Assert.False(arcadia.Main.IsStatusModalOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T166-5")]
    public async Task Installieren_closes_the_modal_opens_Controller_on_its_row_and_asks_there_and_B_stays_on_the_row()
    {
        using var arcadia = await StartAsync(new FakeSteam { HidHideVersion = null });
        arcadia.Press(UiAction.Status).Press(UiAction.Accept);

        Assert.False(arcadia.Main.IsStatusModalOpen);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
        Assert.Equal(PanRows.IssueKey("hidhide-missing"), controller.FocusedRow?.Key);
        Assert.Equal(SettingsPrompt.ConfirmSteamStop, controller.Prompt);
        Assert.Equal(ControllerSettingsViewModel.HidHideInstallTitle, controller.SteamStopTitle);

        arcadia.Press(UiAction.Back);
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        Assert.Equal(PanRows.IssueKey("hidhide-missing"), controller.FocusedRow?.Key);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T166-6")]
    public async Task While_a_fix_runs_Installieren_only_jumps_to_its_row()
    {
        var steam = new FakeSteam { HidHideVersion = null };
        steam.Pan.Running = new();
        using var arcadia = await StartAsync(steam);
        arcadia.Press(UiAction.Status);
        var entry = Modal(arcadia).Focused!.Entry;
        arcadia.Press(UiAction.Accept).Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => steam.Pan.HandOvers == 1, "die Installation läuft");

        // The same entry once more while it runs (the row is busy): the jump, no second question or install.
        ((IStatusModalHost)arcadia.Main).OpenSettingsOf(entry, startFix: true);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
        Assert.Equal(PanRows.IssueKey("hidhide-missing"), controller.FocusedRow?.Key);
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        arcadia.Wait(TimeSpan.FromSeconds(1));
        Assert.Equal((1, 1), (steam.Pan.HandOvers, steam.DriverInstalls));
        steam.Pan.Running.SetResult();
    }

    [AvaloniaFact]
    [Trait("TestCase", "T166-7")]
    public async Task Zu_den_Einstellungen_on_Pan_laeuft_nicht_opens_the_row_that_says_why_and_B_returns_to_the_library()
    {
        using var arcadia = await StartWithPanAsync();
        arcadia.Pan.HidingHolds = PanReason.HidingLost;
        await arcadia.TickSteamControllerAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Main.Status.Entries.Any(entry => entry.Id == StatusBarViewModel.PanStatusId), "„Pan läuft nicht“ im Header");

        arcadia.Press(UiAction.Status);
        var pan = Modal(arcadia).Entries.Single(entry => entry.Id == StatusBarViewModel.PanStatusId);
        Assert.Equal((StatusBarViewModel.PanNotRunningText, StatusBarViewModel.PanShortText, false), (pan.Title, pan.ShortText, pan.HasFix));
        while (Modal(arcadia).Focused != pan)
        {
            arcadia.Press(UiAction.Down);
        }

        Assert.Equal(StatusEntryViewModel.SettingsLabel, Modal(arcadia).AcceptText);
        arcadia.Press(UiAction.Accept);

        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
        Assert.Equal("pan:hide-failed", controller.FocusedRow?.Key);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
        Assert.False(arcadia.Main.IsStatusModalOpen);
        Assert.True(arcadia.Main.IsLibraryShown);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T166-8")]
    public async Task Jetzt_aktualisieren_runs_from_the_modal_and_a_failure_is_an_error_toast_with_the_entry_kept()
    {
        using var arcadia = await StartAsync(new FakeSteam(), new FakeReleases("0.1.0", ("0.3.0", ["Neu"])));
        await arcadia.Updates.CheckIfDueAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Main.Status.HasArcadiaUpdate, "das Update im Header");

        arcadia.Press(UiAction.Status);
        var update = Assert.Single(Modal(arcadia).Entries);
        Assert.True(update.IsUpdate);
        Assert.Equal(("Arcadia 0.3.0 verfügbar", StatusBarViewModel.UpdateShortText, "Jetzt aktualisieren"), (update.Title, update.ShortText, update.FixLabel));

        // The tests' feed refuses every download, as a failing one on the player's PC.
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.IsVisible, "der Fehler als Meldung");
        Assert.StartsWith("Das Update konnte nicht geladen werden", arcadia.Main.Toast.Text, StringComparison.Ordinal);
        Assert.True(arcadia.Main.IsStatusModalOpen);
        Assert.Equal(["Arcadia 0.3.0 verfügbar"], Titles(arcadia));
        Assert.Empty(arcadia.Restarts);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T166-8")]
    public async Task While_the_update_loads_its_entry_shows_the_progress_and_A_cannot_start_it_again()
    {
        var download = new TaskCompletionSource();
        using var arcadia = await StartAsync(new FakeSteam(), new FakeReleases("0.1.0", ("0.3.0", ["Neu"])) { HeldDownload = download });
        await arcadia.Updates.CheckIfDueAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Main.Status.HasArcadiaUpdate, "das Update im Header");

        arcadia.Press(UiAction.Status).Press(UiAction.Accept);
        var update = Assert.Single(Modal(arcadia).Entries);
        await arcadia.WaitUntilAsync(() => update.ShortText == AboutSettingsViewModel.LoadingText("0.3.0", 40), "der Fortschritt im Eintrag");
        Assert.False(update.HasFix);
        Assert.False(Modal(arcadia).FocusedHasFix);
        Assert.Equal(StatusEntryViewModel.SettingsLabel, Modal(arcadia).AcceptText);
        Assert.True(update.IsSettingsFocused);

        // The download fails: the entry offers the update again.
        download.SetResult();
        await arcadia.WaitUntilAsync(() => update.HasFix, "der Knopf wieder da");
        Assert.Equal(StatusBarViewModel.UpdateShortText, update.ShortText);
        Assert.Equal(StatusBarViewModel.UpdateFixLabel, Modal(arcadia).AcceptText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T166-9")]
    public async Task Zu_den_Einstellungen_on_the_update_opens_Ueber_Arcadia_on_Jetzt_aktualisieren()
    {
        using var arcadia = await StartAsync(new FakeSteam(), new FakeReleases("0.1.0", ("0.3.0", ["Neu"])));
        await arcadia.Updates.CheckIfDueAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Main.Status.HasArcadiaUpdate, "das Update im Header");

        arcadia.Press(UiAction.Status).Press(UiAction.Right);
        Assert.Equal(StatusEntryViewModel.SettingsLabel, Modal(arcadia).AcceptText);
        arcadia.Press(UiAction.Accept);

        Assert.IsType<AboutSettingsViewModel>(arcadia.Main.Settings!.Selected);
        Assert.Equal(AboutSettingsViewModel.InstallKey, arcadia.Main.Settings.FocusedRow?.Key);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T166-10")]
    public async Task L3_switches_between_Dringlichkeit_and_Neueste_zuerst_keeps_the_focus_and_the_choice_survives_a_restart()
    {
        // An info at 10:00, a warning at 10:05: the warning is first either way.
        var steam = new FakeSteam { UsbipRebootPending = true };
        using var arcadia = await StartAsync(steam);
        arcadia.Time.Advance(TimeSpan.FromMinutes(5));
        await ChangePartsAsync(arcadia, () => steam.HidHideVersion = null);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Status.Entries.Count == 2, "zwei Meldungen");

        arcadia.Press(UiAction.Status);
        var modal = Modal(arcadia);
        Assert.Equal(StatusModalViewModel.UrgencyText, modal.OrderText);
        Assert.Equal(["HidHide fehlt", "Windows-Neustart ausstehend"], Titles(arcadia));
        arcadia.Press(UiAction.Down);
        Assert.Equal("Windows-Neustart ausstehend", modal.Focused?.Title);

        arcadia.Press(UiAction.ViewOptions);
        Assert.Equal(StatusModalViewModel.NewestText, modal.OrderText);
        Assert.Equal(["HidHide fehlt", "Windows-Neustart ausstehend"], Titles(arcadia));
        Assert.Equal("Windows-Neustart ausstehend", modal.Focused?.Title);
        Assert.True(arcadia.Settings.Current.SortsStatusByNewest);
        arcadia.Press(UiAction.Back);

        using var restarted = await arcadia.RestartAsync();
        Assert.True(restarted.Settings.Current.SortsStatusByNewest);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T166-10")]
    public async Task Neueste_zuerst_puts_a_newer_info_above_an_older_warning()
    {
        var steam = new FakeSteam { HidHideVersion = null };
        using var arcadia = await StartAsync(steam);
        arcadia.Time.Advance(TimeSpan.FromMinutes(5));
        await ChangePartsAsync(arcadia, () => steam.UsbipRebootPending = true);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Status.Entries.Count == 2, "zwei Meldungen");

        arcadia.Press(UiAction.Status);
        Assert.Equal(["HidHide fehlt", "Windows-Neustart ausstehend"], Titles(arcadia));
        Assert.Equal("HidHide fehlt", Modal(arcadia).Focused?.Title);
        arcadia.Press(UiAction.ViewOptions);
        Assert.Equal(["Windows-Neustart ausstehend", "HidHide fehlt"], Titles(arcadia));
        Assert.Equal("HidHide fehlt", Modal(arcadia).Focused?.Title);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T166-11")]
    public async Task Entries_follow_the_header_while_open_and_after_the_last_one_the_empty_state_shows_until_B()
    {
        var steam = new FakeSteam { UsbipRebootPending = true };
        using var arcadia = await StartAsync(steam);
        arcadia.Press(UiAction.Status);
        var modal = Modal(arcadia);
        Assert.Equal(["Windows-Neustart ausstehend"], Titles(arcadia));

        // A warning comes: it stands first, the focus stays on its entry.
        await ChangePartsAsync(arcadia, () => steam.HidHideVersion = null);
        await arcadia.WaitUntilAsync(() => modal.Entries.Count == 2, "die neue Meldung im Modal");
        Assert.Equal(["HidHide fehlt", "Windows-Neustart ausstehend"], Titles(arcadia));
        Assert.Equal("Windows-Neustart ausstehend", modal.Focused?.Title);

        // The focused one goes: the focus moves to the next.
        await ChangePartsAsync(arcadia, () => steam.UsbipRebootPending = false);
        await arcadia.WaitUntilAsync(() => modal.Entries.Count == 1, "die Meldung ist weg");
        Assert.Equal("HidHide fehlt", modal.Focused?.Title);

        // The last one goes: "Alles erledigt!", and only B is left.
        await ChangePartsAsync(arcadia, () => steam.HidHideVersion = HidHideRelease.Version);
        await arcadia.WaitUntilAsync(() => modal.IsEmpty, "der leere Zustand");
        Assert.True(arcadia.Main.IsStatusModalOpen);
        Assert.Null(modal.Focused);
        arcadia.Press(UiAction.Accept).Press(UiAction.ViewOptions).Press(UiAction.Down);
        Assert.True(arcadia.Main.IsStatusModalOpen);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsStatusModalOpen);
        arcadia.Press(UiAction.Status);
        Assert.False(arcadia.Main.IsStatusModalOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T166-12")]
    public async Task Up_down_choose_the_entry_left_right_its_button_and_A_presses_the_chosen_one()
    {
        var steam = new FakeSteam { UsbipRebootPending = true, HidHideVersion = null };
        using var arcadia = await StartAsync(steam);
        arcadia.Press(UiAction.Status);
        var modal = Modal(arcadia);
        Assert.True(modal.HasSeveral);

        Assert.Equal("HidHide fehlt", modal.Focused?.Title);
        Assert.True(modal.Focused!.IsFixFocused);
        arcadia.Press(UiAction.Right);
        Assert.True(modal.Focused.IsSettingsFocused);
        Assert.Equal(StatusEntryViewModel.SettingsLabel, modal.AcceptText);
        arcadia.Press(UiAction.Left);
        Assert.Equal("Installieren", modal.AcceptText);

        // No fix: only "Zu den Einstellungen", ◀ ▶ change nothing; ▲ ▼ stop at the ends.
        arcadia.Press(UiAction.Down, times: 3);
        Assert.Equal("Windows-Neustart ausstehend", modal.Focused?.Title);
        Assert.False(modal.FocusedHasFix);
        arcadia.Press(UiAction.Left);
        Assert.True(modal.Focused!.IsSettingsFocused);
        arcadia.Press(UiAction.Up, times: 3);
        Assert.Equal("HidHide fehlt", modal.Focused?.Title);
        Assert.True(modal.Focused!.IsFixFocused, "ein neu gewählter Eintrag steht auf seinem ersten Knopf");

        // "Zu den Einstellungen" on the info: its row, no question.
        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
        Assert.Equal(PanRows.IssueKey("reboot-pending"), controller.FocusedRow?.Key);
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T166-17")]
    public async Task A_message_gone_during_the_jump_leads_to_the_category_s_top_row()
    {
        var steam = new FakeSteam { HidHideVersion = null };
        using var arcadia = await StartAsync(steam);
        arcadia.Press(UiAction.Status);
        var entry = Modal(arcadia).Focused!.Entry;

        // HidHide arrives between the press and the jump: the host gets the old entry.
        await ChangePartsAsync(arcadia, () => steam.HidHideVersion = HidHideRelease.Version);
        ((IStatusModalHost)arcadia.Main).OpenSettingsOf(entry, startFix: true);

        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
        Assert.Equal(controller.Rows[0].Key, controller.FocusedRow?.Key);
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T166-4")]
    public async Task Seit_keeps_the_time_a_message_appeared_and_a_level_change_counts_as_new()
    {
        using var arcadia = await StartWithPanAsync();
        arcadia.SteamControllerCopy.FailAttach = "VIIPER antwortet nicht";
        arcadia.SteamControllerCopy.Current!.Lose();
        await arcadia.TickSteamControllerAsync();
        await arcadia.WaitUntilAsync(
            () => arcadia.Main.Status.Entries.Any(entry => entry.Id == StatusBarViewModel.PanStatusId), "„Pan läuft nicht“ als Warnung");
        var appeared = $"seit {Formatting.Clock(arcadia.Time.GetLocalNow())}";

        arcadia.Press(UiAction.Status);
        var pan = Modal(arcadia).Entries.Single(entry => entry.Id == StatusBarViewModel.PanStatusId);
        Assert.True(pan.IsWarning);
        Assert.Equal(appeared, pan.SinceText);

        // The same message at the same level keeps its time, also a minute later.
        arcadia.Time.Advance(TimeSpan.FromMinutes(1));
        await arcadia.TickSteamControllerAsync();
        Assert.True(pan.IsWarning);
        Assert.Equal(appeared, pan.SinceText);

        // Pan gives up after further failures: the message turns into an error, a new one with the time of the change.
        await arcadia.TickSteamControllerAsync();
        await arcadia.TickSteamControllerAsync();
        await arcadia.WaitUntilAsync(() => pan.IsError, "„Pan läuft nicht“ als Fehler");
        Assert.Equal($"seit {Formatting.Clock(arcadia.Time.GetLocalNow())}", pan.SinceText);
        Assert.NotEqual(appeared, pan.SinceText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T166-14")]
    public async Task Ansicht_is_on_L3_now_and_Y_does_nothing_in_the_settings()
    {
        using var arcadia = await StartAsync(new FakeSteam());
        Assert.Equal(UiAction.ViewOptions, UiActionMap.FromGamepad(Arcadia.Input.GamepadButton.LeftStickClick));
        Assert.Equal(UiAction.Status, UiActionMap.FromGamepad(Arcadia.Input.GamepadButton.Y));
        Assert.Equal(UiAction.ViewOptions, UiActionMap.FromKey(Avalonia.Input.Key.C, Avalonia.Input.KeyModifiers.None));
        Assert.Equal(UiAction.Status, UiActionMap.FromKey(Avalonia.Input.Key.V, Avalonia.Input.KeyModifiers.None));

        arcadia.Press(UiAction.ViewOptions);
        Assert.True(arcadia.Main.IsViewOptionsOpen);
        arcadia.Press(UiAction.ViewOptions);
        Assert.False(arcadia.Main.IsViewOptionsOpen);

        // In the launcher list Y lifts nothing any more.
        arcadia.Press(UiAction.Settings).Press(UiAction.Status);
        var launchers = Assert.IsType<LauncherSettingsViewModel>(arcadia.Main.Settings!.Selected);
        Assert.False(launchers.IsReordering);
        Assert.True(arcadia.Main.IsSettingsOpen);
        arcadia.Press(UiAction.ViewOptions);
        Assert.True(launchers.IsReordering);
        arcadia.Press(UiAction.Back);
        Assert.False(launchers.IsReordering);
    }

    /// <summary>
    /// A part comes or goes. Outside the settings Arcadia checks the parts only at the start (a check costs a process and
    /// device list), so the test lets it look once more, as the open settings or an install do.
    /// </summary>
    private static async Task ChangePartsAsync(ArcadiaDriver arcadia, Action change)
    {
        change();
        arcadia.ControllerSupport.WatchSetup = true;
        await arcadia.TickControllerSupportAsync();
        arcadia.ControllerSupport.WatchSetup = false;
    }

    private static async Task<ArcadiaDriver> StartWithPanAsync()
    {
        var arcadia = await StartAsync(new FakeSteam());
        arcadia.SteamControllerCopy.CanCopy = true;
        arcadia.SteamControllerCopy.PuckPresent = true;
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        return arcadia;
    }
}
