using System.Collections;
using System.Reflection;
using Arcadia.App.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests.Guards;

/// <summary>
/// #636: Avalonia keeps a record for every style that might match an element (a class selector of the element's type
/// counts, whether the class is set or not), and those records were most of Arcadia's memory. Styles of one view are
/// scoped to that view in the theme, and the keyboards' keys build only their own face.
/// </summary>
public sealed class StyleCostTests
{
    // Measured on the library with the three keyboards built: about 35 per element, 96 before #636.
    private const double MostStyleRecordsPerElement = 50;

    // About 11 per key with the layout menu counted: button, chrome, presenter, the face; 35 before #636.
    private const double MostElementsPerKey = 13;

    [AvaloniaFact]
    [Trait("TestCase", "T636-1")]
    public async Task Elements_carry_few_style_records()
    {
        using var window = await ArcadiaWindow.ShowAsync();
        var elements = window.Window.GetVisualDescendants().OfType<StyledElement>().ToList();

        var perElement = (double)elements.Sum(StyleRecords) / elements.Count;

        Assert.True(perElement <= MostStyleRecordsPerElement, $"{perElement:F0} Stil-Einträge je Element (höchstens {MostStyleRecordsPerElement}).");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T636-2")]
    public async Task An_on_screen_key_builds_only_its_own_face()
    {
        using var window = await ArcadiaWindow.ShowAsync();
        var keyboard = window.Named<OnScreenKeyboardView>("SearchKeyboard");
        var keys = keyboard.GetVisualDescendants().OfType<Button>().Count(button => button.Classes.Contains("boardKey"));

        var perKey = (double)keyboard.GetVisualDescendants().Count() / keys;

        Assert.True(perKey <= MostElementsPerKey, $"{perKey:F1} Elemente je Taste (höchstens {MostElementsPerKey}).");
    }

    /// <summary>The value frames Avalonia keeps on the element: one per style that might match it, plus a few of its own.</summary>
    private static int StyleRecords(StyledElement element)
    {
        var store = typeof(AvaloniaObject).GetField("_values", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(element)!;
        var frames = store.GetType().GetField("_frames", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(store);
        return ((IList)frames!).Count;
    }
}
