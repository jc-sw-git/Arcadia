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
    public void A_hint_on_a_hidden_page_is_built_when_the_page_shows()
    {
        var hint = new KeyHint { Pad = "A", Keys = "Leertaste", Text = "Auswählen" };
        var page = new Panel { IsVisible = false, Children = { hint } };
        var window = Shown(page);
        Assert.Empty(hint.Children);

        page.IsVisible = true;
        Layout(window);
        Assert.Equal(2, hint.Children.Count);
        var glyph = Assert.IsType<PadGlyph>(hint.Children[0]);
        Assert.Equal("A", glyph.Shown?.Text);
        Assert.Equal("Auswählen", Assert.IsType<TextBlock>(hint.Children[1]).Text);
        window.Close();
    }

    [AvaloniaFact]
    [Trait("TestCase", "T637-3")]
    public void A_hint_hidden_for_having_no_keys_comes_back_when_the_input_changes()
    {
        var before = InputHints.UsesController;
        try
        {
            InputHints.UsesController = false;
            var hint = new KeyHint { Pad = "Y", Text = "Ansicht" };
            var window = Shown(new Panel { Children = { hint } });
            Assert.True(hint.Classes.Contains("noKeys"), "Ein Hinweis ohne Taste für die Tastatur bleibt sichtbar.");

            InputHints.UsesController = true;
            Layout(window);
            Assert.False(hint.Classes.Contains("noKeys"), "Der Hinweis kommt mit dem Controller nicht zurück.");
            Assert.Equal("Y", Assert.IsType<PadGlyph>(hint.Children[0]).Shown?.Text);
            window.Close();
        }
        finally
        {
            InputHints.UsesController = before;
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T637-3")]
    public void A_shown_hint_follows_a_changed_text()
    {
        var hint = new KeyHint { Pad = "B", Text = "Zurück" };
        var window = Shown(new Panel { Children = { hint } });

        hint.Text = "Schließen";
        Layout(window);
        Assert.Equal("Schließen", Assert.IsType<TextBlock>(hint.Children[^1]).Text);
        window.Close();
    }

    [Fact]
    [Trait("TestCase", "T637-4")]
    public void The_start_line_names_the_steps_in_the_order_they_happened()
    {
        Assert.Equal("Avalonia 300 ms · Fenster offen 900 ms · Bibliothek im Bild 950 ms",
            StartTimeline.Format([("Bibliothek im Bild", 950), ("Avalonia", 300), ("Fenster offen", 900)]));
    }

    private static Window Shown(Control content)
    {
        var window = new Window { Content = content };
        window.Show();
        Layout(window);
        return window;
    }

    private static void Layout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
