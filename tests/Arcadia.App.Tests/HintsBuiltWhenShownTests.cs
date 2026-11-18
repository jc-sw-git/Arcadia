using Arcadia.App.Controls;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #637: the start builds the whole window before its first frame. Button hints build their glyphs when they are measured,
/// so the hints of hidden pages cost nothing until shown, and each one is built once, not at every property XAML sets.
/// </summary>
public sealed class HintsBuiltWhenShownTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T637-3")]
    public void A_hint_on_a_hidden_page_is_built_when_the_page_shows() => WithController(true, () =>
    {
        var hint = new KeyHint { Pad = "A", Keys = "Leertaste", Text = "Auswählen" };
        var page = new Panel { IsVisible = false, Children = { hint } };
        using var window = Shown(page);
        Assert.Empty(hint.Children);

        page.IsVisible = true;
        window.Layout();
        Assert.Equal(2, hint.Children.Count);
        var glyph = Assert.IsType<PadGlyph>(hint.Children[0]);
        Assert.Equal("A", glyph.Button);
        Assert.NotNull(glyph.Shown); // the printing depends on the controller other tests left behind
        Assert.Equal("Auswählen", Assert.IsType<TextBlock>(hint.Children[1]).Text);
    });

    [AvaloniaFact]
    [Trait("TestCase", "T637-3")]
    public void A_hint_hidden_for_having_no_keys_comes_back_when_the_input_changes() => WithController(false, () =>
    {
        var hint = new KeyHint { Pad = "Y", Text = "Ansicht" };
        using var window = Shown(new Panel { Children = { hint } });
        Assert.True(hint.Classes.Contains("noKeys"), "Ein Hinweis ohne Taste für die Tastatur bleibt sichtbar.");

        InputHints.UsesController = true;
        window.Layout();
        Assert.False(hint.Classes.Contains("noKeys"), "Der Hinweis kommt mit dem Controller nicht zurück.");
        var glyph = Assert.IsType<PadGlyph>(hint.Children[0]);
        Assert.Equal("Y", glyph.Button);
        Assert.NotNull(glyph.Shown);
    });

    [AvaloniaFact]
    [Trait("TestCase", "T637-3")]
    public void A_shown_hint_follows_a_changed_text() => WithController(true, () =>
    {
        var hint = new KeyHint { Pad = "B", Text = "Zurück" };
        using var window = Shown(new Panel { Children = { hint } });

        hint.Text = "Schließen";
        window.Layout();
        Assert.Equal("Schließen", Assert.IsType<TextBlock>(hint.Children[^1]).Text);
    });

    [Fact]
    [Trait("TestCase", "T637-4")]
    public void The_start_line_names_the_steps_in_the_order_they_happened()
    {
        Assert.Equal("Avalonia 300 ms · Fenster offen 900 ms · Bibliothek im Bild 950 ms",
            StartTimeline.Format([("Bibliothek im Bild", 950), ("Avalonia", 300), ("Fenster offen", 900)]));
    }

    /// <summary>
    /// Runs <paramref name="test"/> with the input in use set as given; the input is shared by every hint of the process, so
    /// it is set back afterwards.
    /// </summary>
    private static void WithController(bool usesController, Action test)
    {
        var before = InputHints.UsesController;
        try
        {
            InputHints.UsesController = usesController;
            test();
        }
        finally
        {
            InputHints.UsesController = before;
        }
    }

    private static ShownWindow Shown(Control content)
    {
        var window = new ShownWindow(new Window { Content = content });
        window.Layout();
        return window;
    }

    /// <summary>
    /// A window closed at the end of the test also when it fails: a hint left in an open window would hear the input
    /// changes of later tests, on their threads.
    /// </summary>
    private sealed class ShownWindow : IDisposable
    {
        private readonly Window _window;

        public ShownWindow(Window window)
        {
            _window = window;
            window.Show();
        }

        public void Layout()
        {
            Dispatcher.UIThread.RunJobs();
            _window.UpdateLayout();
        }

        public void Dispose() => _window.Close();
    }
}
