using Arcadia.App.Input;
using Arcadia.Core.Settings;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// No focus ring runs into a clipping parent, on any page state with focusable buttons (#180, G-32): each visible
/// button counts as focused at once, see <see cref="RingGeometry"/>.
/// </summary>
public sealed class RingGeometryTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T180-2")]
    [Trait("TestCase", "G-32")]
    public async Task No_ring_is_cut_off_in_the_library_its_view_options_and_the_game_menu()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        AssertNoRingCutOff(arcadia, "Bibliothek", atLeast: 8);

        arcadia.Press(UiAction.ViewOptions);
        AssertNoRingCutOff(arcadia, "Ansicht");
        arcadia.Press(UiAction.Back);

        arcadia.Press(UiAction.NextPage);
        AssertNoRingCutOff(arcadia, "Bibliothek, zweite Seite", atLeast: 4);

        arcadia.Press(UiAction.Options);
        Assert.True(arcadia.Arcadia.Main.IsGameMenuOpen);
        AssertNoRingCutOff(arcadia, "Spielmenü");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T180-2")]
    public async Task No_ring_is_cut_off_on_the_detail_page_and_in_the_search()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        // Holding A opens the detail page.
        arcadia.Arcadia.PressDown(UiAction.Accept).Wait(TimeSpan.FromSeconds(1.1)).LetGo(UiAction.Accept);
        Assert.True(arcadia.Arcadia.Main.IsDetailOpen, "Detailseite offen");
        AssertNoRingCutOff(arcadia, "Detailseite");
        arcadia.Press(UiAction.Back);

        arcadia.Press(UiAction.Search);
        AssertNoRingCutOff(arcadia, "Suche, leer", atLeast: 30);
        arcadia.Type("a");
        AssertNoRingCutOff(arcadia, "Suche mit Treffern", atLeast: 30);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T180-2")]
    public async Task No_ring_is_cut_off_on_any_settings_category_at_its_top_and_its_end()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        arcadia.Press(UiAction.Settings);
        for (var category = 0; category < 4; category++)
        {
            AssertNoRingCutOff(arcadia, $"Einstellungen {category}, oben");
            arcadia.Press(UiAction.Down, times: 15);
            AssertNoRingCutOff(arcadia, $"Einstellungen {category}, unten");
            arcadia.Press(UiAction.NextTab);
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T180-2")]
    public async Task No_ring_is_cut_off_on_a_launcher_page_and_the_first_start()
    {
        using (var arcadia = await ArcadiaWindow.ShowAsync())
        {
            arcadia.Press(UiAction.Settings).Press(UiAction.Accept);
            Assert.NotNull(arcadia.LauncherCategory().PageLauncherId);
            AssertNoRingCutOff(arcadia, "Launcher-Seite");
        }

        using var firstStart = await ArcadiaWindow.ShowAsync(settings: new AppSettings());
        AssertNoRingCutOff(firstStart, "Ersteinrichtung");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T180-2")]
    public void No_ring_is_cut_off_on_the_mouse_modes_keyboard_and_its_layout_menu()
    {
        using var mouseMode = MouseModeKeyboard.Show();
        RingGeometry.AssertNoRingCutOff(mouseMode.Window, () => ArcadiaWindow.CaptureSettled(mouseMode.Window).Dispose(), "Mausmodus-Tastatur", atLeast: 30);

        mouseMode.Press(Arcadia.Input.KeyboardButton.Layout);
        RingGeometry.AssertNoRingCutOff(mouseMode.Window, () => ArcadiaWindow.CaptureSettled(mouseMode.Window).Dispose(), "Mausmodus-Tastatur, Layoutmenü", atLeast: 30);
    }

    private static void AssertNoRingCutOff(ArcadiaWindow arcadia, string state, int atLeast = 1) =>
        RingGeometry.AssertNoRingCutOff(arcadia.Window, () => arcadia.Settle().WaitForTransitions(), state, atLeast);
}
