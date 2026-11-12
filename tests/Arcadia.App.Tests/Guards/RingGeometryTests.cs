using Arcadia.App.Input;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests.Guards;

/// <summary>
/// No focus ring runs into a clipping parent, on any page state with focusable buttons (#180, G-32): each visible
/// button counts as focused at once, see <see cref="RingGeometry"/>. The pages are those of <see cref="GuardPages"/>.
/// </summary>
public sealed class RingGeometryTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T180-2")]
    [Trait("TestCase", "G-32")]
    public Task No_ring_is_cut_off_on_any_page_dialog_or_settings_category_in_any_state_of_its_focus() =>
        GuardWalk.EveryAsync(_ => true, (arcadia, page, state) => AssertNoRingCutOff(arcadia, state, page.RingsAtLeast));

    [AvaloniaFact]
    [Trait("TestCase", "T465-14")]
    public async Task No_ring_is_cut_off_in_the_filter_on_its_first_and_last_rows_and_the_right_edge_of_the_type_grid()
    {
        var gog = KnownLaunchers.Gog;
        Game Entry(string title, GameType type, bool installed) =>
            (installed ? TestGames.Installed(gog, title, ArcadiaDriver.Now) : TestGames.NotInstalled(gog, title)) with { Type = type };
        using var arcadia = await ArcadiaWindow.ShowAsync([LauncherSpec.With(gog,
            Entry("Alpha", GameType.Game, true), Entry("Bravo", GameType.Game, false), Entry("Charlie", GameType.Tool, true),
            Entry("Delta", GameType.Software, false), Entry("Echo", GameType.Soundtrack, true))]);

        arcadia.Press(UiAction.KeyboardLayout);
        Assert.True(arcadia.Arcadia.Main.IsFilterOpen);
        AssertNoRingCutOff(arcadia, "Filter, erste Kategorie");
        // "Nur installierte", back up to "Alle Filter zurücksetzen".
        arcadia.Press(UiAction.Accept).Press(UiAction.Down).Press(UiAction.Accept);
        AssertNoRingCutOff(arcadia, "Filter, Wert");
        arcadia.Press(UiAction.Back).Press(UiAction.Up);
        AssertNoRingCutOff(arcadia, "Filter, zurücksetzen");
        // "Typ", the grid's last column and its last value.
        arcadia.Press(UiAction.Down, times: 2).Press(UiAction.Accept).Press(UiAction.Right, times: 2);
        AssertNoRingCutOff(arcadia, "Filter, rechter Rand der Typen");
        arcadia.Press(UiAction.Down);
        AssertNoRingCutOff(arcadia, "Filter, letzter Typ");
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
