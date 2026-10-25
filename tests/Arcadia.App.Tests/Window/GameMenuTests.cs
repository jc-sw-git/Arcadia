using Arcadia.App.Input;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>The context menu of a game in the library (#10), as drawn: how it opens and closes, where it stands, what it lists.</summary>
public sealed class GameMenuTests
{
    private static readonly LauncherInfo Gog = KnownLaunchers.Gog;

    /// <summary>Eight games in one row of the grid, the first with an update, and one not installed in the next row.</summary>
    private static readonly IReadOnlyList<LauncherSpec> Library =
    [
        LauncherSpec.With(
            Gog,
            [
                WithLauncherActions(TestGames.Installed(Gog, "Alpha Strike", ArcadiaDriver.Now.AddDays(-9)) with { UpdateAvailable = true }),
                .. "BCDEFGH".Select(letter => WithLauncherActions(TestGames.Installed(Gog, $"{letter} Game", ArcadiaDriver.Now.AddDays(-9)))),
                TestGames.NotInstalled(Gog, "Zeta Run"),
            ]),
    ];

    [AvaloniaFact]
    [Trait("Testfall", "T10-1")]
    public async Task View_right_click_Options_and_the_menu_key_open_the_menu_B_View_and_a_click_beside_it_close_it()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(Library);
        var main = arcadia.Arcadia.Main;

        arcadia.Press(UiAction.Options);
        AssertMenuOpenFor(arcadia, "Alpha Strike");
        arcadia.Press(UiAction.Back);
        Assert.False(IsMenuShown(arcadia), "B schließt das Menü.");

        arcadia.Press(UiAction.Options).Press(UiAction.Options);
        Assert.False(IsMenuShown(arcadia), "⧉ schließt das Menü.");

        arcadia.Click(arcadia.Tile(main.PageGames[2]), MouseButton.Right);
        AssertMenuOpenFor(arcadia, "C Game");
        arcadia.Click(new Point(arcadia.Bounds(MenuPanel(arcadia)).Right + 100, 40));
        Assert.False(IsMenuShown(arcadia), "Ein Klick daneben schließt das Menü.");

        arcadia.Click(arcadia.Named<Button>("PreviewOptionsButton"));
        AssertMenuOpenFor(arcadia, "C Game");
        arcadia.Key(Key.Escape);
        Assert.False(IsMenuShown(arcadia), "Esc schließt das Menü.");

        arcadia.Key(Key.Apps);
        AssertMenuOpenFor(arcadia, "C Game");

        // Down skips the two disabled entries (favourites, artwork) after "Details anzeigen", Up skips them back.
        Assert.Equal("Spielen", FocusedEntry(arcadia));
        arcadia.Press(UiAction.Down);
        Assert.Equal("Details anzeigen", FocusedEntry(arcadia));
        arcadia.Press(UiAction.Down);
        Assert.Equal("In GOG Galaxy öffnen", FocusedEntry(arcadia));
        arcadia.Press(UiAction.Up);
        Assert.Equal("Details anzeigen", FocusedEntry(arcadia));
        arcadia.Press(UiAction.Back);

        // ≡ opens the settings, not the menu.
        arcadia.Press(UiAction.Settings);
        Assert.False(IsMenuShown(arcadia));
        Assert.True(main.IsSettingsOpen, "≡ öffnet die Einstellungen.");
    }

    [AvaloniaFact]
    [Trait("Testfall", "T10-1")]
    [Trait("Testfall", "G-33")]
    public async Task The_menu_stands_beside_its_cover_left_of_it_in_the_last_column_and_always_inside_the_window()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(Library);
        var main = arcadia.Arcadia.Main;
        var window = new Rect(0, 0, ArcadiaWindow.Width, ArcadiaWindow.Height);

        // A cover in the first column: the menu to its right, top edges level.
        var first = arcadia.Bounds(arcadia.Tile(main.PageGames[0]));
        arcadia.Click(arcadia.Tile(main.PageGames[0]), MouseButton.Right);
        var menu = arcadia.Bounds(MenuPanel(arcadia));
        Assert.True(menu.Left > first.Right && menu.Left - first.Right < 40, $"Menü {menu} rechts neben dem Cover {first}");
        Assert.Equal(first.Top, menu.Top, tolerance: 12);
        Assert.True(window.Contains(menu), $"Menü {menu} ganz im Fenster");
        arcadia.Press(UiAction.Back);

        // The last column: to the left of the cover.
        var last = arcadia.Bounds(arcadia.Tile(main.PageGames[7]));
        arcadia.Click(arcadia.Tile(main.PageGames[7]), MouseButton.Right);
        menu = arcadia.Bounds(MenuPanel(arcadia));
        Assert.True(menu.Right < last.Left && last.Left - menu.Right < 40, $"Menü {menu} links neben dem Cover {last}");
        Assert.True(window.Contains(menu), $"Menü {menu} ganz im Fenster");
        arcadia.Press(UiAction.Back);

        // The second row reaches far down; the menu still ends inside the window.
        arcadia.Click(arcadia.Tile(main.PageGames[8]), MouseButton.Right);
        Assert.True(window.Contains(arcadia.Bounds(MenuPanel(arcadia))), "Menü der zweiten Reihe ganz im Fenster");
        arcadia.Press(UiAction.Back);
    }

    [AvaloniaFact]
    [Trait("Testfall", "G-33")]
    public async Task View_R_and_a_click_on_Options_open_the_menu_over_the_Options_button_on_its_left_and_bottom_edges()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(Library);
        var options = arcadia.Named<Button>("PreviewOptionsButton");

        foreach (var (how, open) in new (string, Action)[]
        {
            ("⧉", () => arcadia.Press(UiAction.Options)),
            ("R", () => arcadia.Key(Key.R)),
            ("Klick auf Optionen", () => arcadia.Click(options)),
        })
        {
            open();
            var button = arcadia.Bounds(options);
            var menu = arcadia.Bounds(MenuPanel(arcadia));
            Assert.True(Math.Abs(menu.Left - button.Left) < 1, $"{how}: linke Kante {menu.Left} statt {button.Left}");
            Assert.True(Math.Abs(menu.Bottom - button.Bottom) < 1, $"{how}: untere Kante {menu.Bottom} statt {button.Bottom}");
            arcadia.Press(UiAction.Back);
        }
    }

    [AvaloniaFact]
    [Trait("Testfall", "T10-2")]
    public async Task An_installed_game_lists_its_actions_with_the_update_note_disabled_ones_greyed_and_uninstall_in_red_after_a_line()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(Library);

        arcadia.Press(UiAction.Options);
        var entries = Entries(arcadia);
        Assert.Equal(
            ["Spielen", "Details anzeigen", "Zu Favoriten hinzufügen", "Artwork ändern …", "In GOG Galaxy öffnen", "Deinstallieren …"],
            entries.Select(entry => entry.Label));
        Assert.Equal([true, true, false, false, true, true], entries.Select(entry => entry.IsEnabled));
        Assert.Equal("GOG Galaxy installiert ggf. vorher das Update", entries[0].Note);
        Assert.All(entries.Where(entry => !entry.IsEnabled), entry => Assert.Equal("Folgt in einer späteren Version", entry.Note));
        Assert.DoesNotContain(entries, entry => entry.Label.Contains("Update", StringComparison.Ordinal) || entry.Label.Contains("Controller-Layout", StringComparison.Ordinal));

        // Disabled entries are drawn faint, "Deinstallieren …" in the danger colour and alone after a line.
        var faint = ArcadiaWindow.ThemeResource<ISolidColorBrush>("TextFaint").Color;
        var danger = ArcadiaWindow.ThemeResource<ISolidColorBrush>("Danger").Color;
        Assert.All(entries.Where(entry => !entry.IsEnabled), entry => Assert.Equal(faint, entry.Colour));
        Assert.All(entries.Where(entry => entry.IsEnabled && entry.Label != "Deinstallieren …"), entry => Assert.NotEqual(faint, entry.Colour));
        Assert.Equal(danger, entries[^1].Colour);
        Assert.Equal([false, false, false, false, false, true], entries.Select(entry => entry.HasLineAbove));
        Assert.True(HasDrawn(arcadia, entries[^1].Text, danger), "„Deinstallieren …“ ist rot zu sehen.");
        arcadia.Press(UiAction.Back);

        // Without an update, "Spielen" has no note.
        arcadia.Press(UiAction.Right).Press(UiAction.Options);
        Assert.Null(Entries(arcadia)[0].Note);
    }

    private static Game WithLauncherActions(Game game) => game with
    {
        OpenInLauncher = new LaunchTarget.ProtocolUri("goggalaxy://openGameView/1"),
        Uninstall = new LaunchTarget.ProtocolUri("goggalaxy://uninstall/1"),
    };

    private static Border MenuPanel(ArcadiaWindow arcadia) => arcadia.Named<Border>("GameMenuPanel");

    private static bool IsMenuShown(ArcadiaWindow arcadia) => MenuPanel(arcadia).IsEffectivelyVisible;

    private static void AssertMenuOpenFor(ArcadiaWindow arcadia, string title)
    {
        Assert.True(IsMenuShown(arcadia), $"Menü für {title} offen");
        Assert.Contains(arcadia.WithText(title.ToUpperInvariant()), text => MenuPanel(arcadia).IsVisualAncestorOf(text));
    }

    private static string FocusedEntry(ArcadiaWindow arcadia) => Assert.Single(Entries(arcadia), entry => entry.IsFocused).Label;

    private sealed record Entry(string Label, string? Note, bool IsEnabled, bool IsFocused, Color Colour, bool HasLineAbove, TextBlock Text);

    /// <summary>The entries of the open menu as drawn, top to bottom.</summary>
    private static List<Entry> Entries(ArcadiaWindow arcadia) => MenuPanel(arcadia).GetVisualDescendants()
        .OfType<Button>()
        .Where(button => button.Classes.Contains("menuItem") && button.IsEffectivelyVisible)
        .Select(button =>
        {
            var texts = button.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).ToList();
            var line = (button.GetVisualParent() as StackPanel)?.Children.OfType<Border>().FirstOrDefault();
            return new Entry(
                texts[0].Text ?? string.Empty,
                texts.Count > 1 ? texts[1].Text : null,
                button.IsEffectivelyEnabled,
                button.Classes.Contains("focused"),
                (texts[0].Foreground as ISolidColorBrush)?.Color ?? default,
                line?.IsVisible == true,
                texts[0]);
        })
        .ToList();

    /// <summary>Some pixels of <paramref name="text"/> are drawn in <paramref name="colour"/>.</summary>
    private static bool HasDrawn(ArcadiaWindow arcadia, Control text, Color colour)
    {
        var bounds = arcadia.Bounds(text);
        using var frame = arcadia.Capture();
        for (var y = bounds.Top; y < bounds.Bottom; y++)
        {
            for (var x = bounds.Left; x < bounds.Right; x++)
            {
                if (ArcadiaWindow.Distance(frame.At(new Point(x, y)), colour) < 30)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
