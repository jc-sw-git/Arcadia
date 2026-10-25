using Arcadia.App.Input;
using Arcadia.Input;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>⧉ and ≡ in Arcadia's normal mode (#64): together they switch the mouse mode, so alone they act only on release or after a moment.</summary>
public sealed class ViewAndMenuTests
{
    private static readonly TimeSpan BeforeSoloDelay = TimeSpan.FromMilliseconds(100);

    private static readonly TimeSpan PastSoloDelay = TimeSpan.FromMilliseconds(300);

    [AvaloniaFact]
    [Trait("Testfall", "T64-10")]
    public async Task View_and_Menu_act_when_let_go_or_held_alone_a_moment()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();

        // Tapped: on the release.
        arcadia.PressButton(GamepadButton.View).Wait(BeforeSoloDelay);
        Assert.False(arcadia.Main.IsGameMenuOpen);
        arcadia.ReleaseButton(GamepadButton.View);
        Assert.True(arcadia.Main.IsGameMenuOpen);
        arcadia.Press(UiAction.Back);

        arcadia.PressButton(GamepadButton.Menu).Wait(BeforeSoloDelay);
        Assert.False(arcadia.Main.IsSettingsOpen);
        arcadia.ReleaseButton(GamepadButton.Menu);
        Assert.True(arcadia.Main.IsSettingsOpen);
        arcadia.Press(UiAction.Back);

        // Held alone: after 0.2 s, still held.
        arcadia.PressButton(GamepadButton.View).Wait(PastSoloDelay);
        Assert.True(arcadia.Main.IsGameMenuOpen);
        arcadia.ReleaseButton(GamepadButton.View).Press(UiAction.Back);
        Assert.False(arcadia.Main.IsGameMenuOpen);

        arcadia.PressButton(GamepadButton.Menu).Wait(PastSoloDelay);
        Assert.True(arcadia.Main.IsSettingsOpen);
        arcadia.ReleaseButton(GamepadButton.Menu);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T64-10")]
    public async Task View_and_Menu_pressed_together_open_nothing()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var focused = arcadia.Main.FocusedGame;

        arcadia.PressButton(GamepadButton.View).Wait(BeforeSoloDelay).PressButton(GamepadButton.Menu).Wait(TimeSpan.FromSeconds(1));
        arcadia.ReleaseButton(GamepadButton.Menu).ReleaseButton(GamepadButton.View).Wait(PastSoloDelay);
        Assert.False(arcadia.Main.IsGameMenuOpen);
        Assert.False(arcadia.Main.IsSettingsOpen);

        arcadia.PressButton(GamepadButton.Menu).PressButton(GamepadButton.View).Wait(TimeSpan.FromSeconds(1));
        arcadia.ReleaseButton(GamepadButton.View).ReleaseButton(GamepadButton.Menu).Wait(PastSoloDelay);
        Assert.False(arcadia.Main.IsGameMenuOpen);
        Assert.False(arcadia.Main.IsSettingsOpen);
        Assert.Same(focused, arcadia.Main.FocusedGame);

        // The other buttons behave as before.
        arcadia.PressButton(GamepadButton.DPadRight).ReleaseButton(GamepadButton.DPadRight);
        Assert.NotSame(focused, arcadia.Main.FocusedGame);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T64-10")]
    public async Task Menu_held_a_second_on_a_search_hit_still_opens_its_details()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.Search);
        var title = arcadia.Main.Tabs[0].Games[0].Title;
        arcadia.Main.TypeInSearch(title);
        var hit = arcadia.Main.Search!.FocusedResult;
        Assert.NotNull(hit);

        arcadia.PressButton(GamepadButton.Menu).Wait(TimeSpan.FromMilliseconds(1300));
        Assert.True(arcadia.Main.IsDetailOpen);
        Assert.Equal(hit.Game.Key, arcadia.Main.Detail!.Current.Tile.Game.Key);
        arcadia.ReleaseButton(GamepadButton.Menu);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T64-10")]
    public async Task With_a_game_in_front_or_in_the_mouse_mode_the_buttons_leave_Arcadia_alone()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var focused = arcadia.Main.FocusedGame;

        foreach (var (inFront, mouseMode) in new[] { (false, false), (true, true) })
        {
            arcadia.IsArcadiaInFront = inFront;
            arcadia.IsMouseModeOn = mouseMode;
            arcadia.PressButton(GamepadButton.DPadRight).ReleaseButton(GamepadButton.DPadRight);
            arcadia.PressButton(GamepadButton.View).ReleaseButton(GamepadButton.View);
            arcadia.PressButton(GamepadButton.Menu).Wait(PastSoloDelay).ReleaseButton(GamepadButton.Menu);
            Assert.Same(focused, arcadia.Main.FocusedGame);
            Assert.False(arcadia.Main.IsGameMenuOpen);
            Assert.False(arcadia.Main.IsSettingsOpen);
        }

        // ⧉ pressed with Arcadia in front and let go after it lost the front (e.g. View held brought it back) opens nothing.
        arcadia.IsArcadiaInFront = true;
        arcadia.IsMouseModeOn = false;
        arcadia.PressButton(GamepadButton.View).Wait(BeforeSoloDelay);
        arcadia.IsArcadiaInFront = false;
        arcadia.ReleaseButton(GamepadButton.View);
        Assert.False(arcadia.Main.IsGameMenuOpen);

        arcadia.IsArcadiaInFront = true;
        arcadia.PressButton(GamepadButton.DPadRight).ReleaseButton(GamepadButton.DPadRight);
        Assert.NotSame(focused, arcadia.Main.FocusedGame);
    }
}
