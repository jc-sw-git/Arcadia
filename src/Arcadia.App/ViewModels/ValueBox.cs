using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>
/// One value of a value row in its info box (#506): the value's name and what it does. The box of the current value is
/// highlighted and follows ◀ ▶ at once; the detail page and every settings category draw them alike.
/// </summary>
public sealed partial class ValueBox(string title, string text) : ObservableObject
{
    /// <summary>The value as the row shows it, e.g. "Standard (Arcadia)"; may change with what it names.</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = title;

    public string Text { get; } = text;

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    /// <summary>Highlights the box whose title is the row's value; none if no title matches.</summary>
    public static void ShowCurrent(IReadOnlyList<ValueBox> boxes, string value)
    {
        foreach (var box in boxes)
        {
            box.IsCurrent = box.Title == value;
        }
    }
}
