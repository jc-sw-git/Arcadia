using Arcadia.App.Controls;
using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.Status;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Arcadia.ControllerSupport;

namespace Arcadia.App.Tests;

/// <summary>
/// The status modal and the header's status chip drawn (#166, designs T166Library/T166Modal/T166Empty): the chip's hint
/// follows the input, a click opens the modal, mouse and keyboard work it, the focus ring is whole, a long list fades.
/// </summary>
public sealed class StatusModalViewTests
{
    private static readonly LauncherSpec[] SteamAndGog = [new(KnownLaunchers.Steam, 3), new(KnownLaunchers.Gog, 3)];

    private static async Task<ArcadiaWindow> ShowAsync(FakeSteam steam)
    {
        var arcadia = await ArcadiaWindow.ShowAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"], PanEnabled = true }, steam: steam);
        await arcadia.Arcadia.TickControllerSupportAsync();
        arcadia.Settle();
        return arcadia;
    }

    private static Button Chip(ArcadiaWindow arcadia) => arcadia.Named<Button>("StatusChip");

    private static Border EntryBorder(ArcadiaWindow arcadia, StatusEntryViewModel entry) => Assert.Single(
        arcadia.Window.GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("statusEntry") && border.DataContext == entry);

    private static Button EntryButton(ArcadiaWindow arcadia, StatusEntryViewModel entry, string text) => Assert.Single(
        arcadia.Window.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("statusButton") && button.DataContext == entry
            && button.IsEffectivelyVisible && Equals(button.Content, text));

    /// <summary>Set ARCADIA_CAPTURES to a folder to look at the drawn states (not needed for the test).</summary>
    private static void Keep(ArcadiaWindow arcadia, string name)
    {
        if (Environment.GetEnvironmentVariable("ARCADIA_CAPTURES") is { Length: > 0 } folder)
        {
            using var frame = arcadia.Capture();
            frame.Save(Path.Combine(folder, $"{name}.png"));
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T166-13")]
    public async Task The_chip_shows_Y_or_V_beside_the_symbol_only_with_a_message_and_a_click_opens_the_modal()
    {
        using var empty = await ShowAsync(new FakeSteam());
        Assert.False(Chip(empty).IsEffectivelyVisible);

        using var arcadia = await ShowAsync(new FakeSteam { HidHideVersion = null });
        Assert.True(Chip(arcadia).IsEffectivelyVisible);
        Assert.Equal(["Y"], arcadia.Hints(Chip(arcadia).GetVisualDescendants().OfType<KeyHint>()));
        Keep(arcadia, "status-chip-controller");

        arcadia.Key(Key.W).Settle();
        Assert.Equal(["V"], arcadia.Hints(Chip(arcadia).GetVisualDescendants().OfType<KeyHint>()));

        arcadia.Click(Chip(arcadia)).Settle();
        Assert.True(arcadia.Arcadia.Main.IsStatusModalOpen);
        Assert.False(arcadia.Arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T166-12")]
    public async Task Keyboard_and_mouse_work_the_modal_and_a_click_on_a_button_presses_it()
    {
        using var arcadia = await ShowAsync(new FakeSteam { HidHideVersion = null, UsbipRebootPending = true });
        var main = arcadia.Arcadia.Main;

        // V opens, S chooses the next entry, C switches the order, Esc closes.
        arcadia.Key(Key.V).Settle();
        var modal = Assert.IsType<StatusModalViewModel>(main.StatusModal);
        arcadia.AssertRingAround(EntryBorder(arcadia, modal.Focused!), "erster Eintrag");
        Keep(arcadia, "status-modal-first");
        arcadia.Key(Key.S).Settle();
        Assert.Equal(ControllerSupportTexts.WindowsRestartPendingTitle, modal.Focused?.Title);
        arcadia.AssertRingAround(EntryBorder(arcadia, modal.Focused!), "letzter Eintrag");
        RingGeometry.AssertNoRingCutOff(arcadia.Window, () => arcadia.Settle().WaitForTransitions(), "Status");
        arcadia.Key(Key.C).Settle();
        Assert.Equal(StatusModalViewModel.NewestText, modal.OrderText);
        Assert.Contains("C Sortierung:", arcadia.Hints(arcadia.Window.GetVisualDescendants().OfType<KeyHint>().Where(hint => hint.IsEffectivelyVisible)));
        arcadia.Key(Key.Escape).Settle();
        Assert.False(main.IsStatusModalOpen);

        // The ✕ closes; a click on "Zu den Einstellungen" of an entry opens its row.
        arcadia.Key(Key.V).Settle();
        arcadia.Click(arcadia.Window.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("dialogClose") && button.IsEffectivelyVisible
            && button.DataContext is StatusModalViewModel)).Settle();
        Assert.False(main.IsStatusModalOpen);
        arcadia.Key(Key.V).Settle();
        modal = main.StatusModal!;
        var hidHide = modal.Entries.Single(entry => entry.Title == "HidHide fehlt");
        arcadia.Click(EntryButton(arcadia, hidHide, StatusEntryViewModel.SettingsLabel)).Settle();
        var controller = Assert.IsType<ControllerSettingsViewModel>(main.Settings!.Selected);
        Assert.Equal(PanRows.IssueKey("hidhide-missing"), controller.FocusedRow?.Key);
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T166-15")]
    public async Task Six_messages_fade_at_the_bottom_and_the_last_one_s_ring_is_whole_after_scrolling()
    {
        var steam = new FakeSteam { HidHideVersion = null, UsbipRebootPending = true, IsUsbipReady = false, IsPanReady = false, PanFailing = true };
        steam.Viiper.IsInstalled = false;
        using var arcadia = await ShowAsync(steam);
        arcadia.Press(UiAction.Status).Settle();
        var modal = arcadia.Arcadia.Main.StatusModal!;
        Assert.True(modal.Entries.Count > 5, string.Join(", ", modal.Entries.Select(entry => entry.Title)));
        var fade = Assert.Single(arcadia.Window.GetVisualDescendants().OfType<EdgeFade>(), edge => edge.Name == "EntriesFade");
        Assert.False(fade.ShowsStart);
        Assert.True(fade.ShowsEnd);
        Keep(arcadia, "status-modal-many");

        arcadia.Press(UiAction.Down, times: modal.Entries.Count - 1).Settle();
        Assert.Same(modal.Entries[^1], modal.Focused);
        arcadia.AssertRingAround(EntryBorder(arcadia, modal.Focused!), "letzter von vielen");
        Assert.True(fade.ShowsStart);
        Assert.False(fade.ShowsEnd);
        Keep(arcadia, "status-modal-many-end");
    }
}
