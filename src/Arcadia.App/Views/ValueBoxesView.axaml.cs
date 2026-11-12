using Arcadia.App.ViewModels;
using Avalonia;
using Avalonia.Controls;

namespace Arcadia.App.Views;

/// <summary>Draws the <see cref="ValueBox"/>es of a value row in an info box (#506).</summary>
public partial class ValueBoxesView : UserControl
{
    public static readonly StyledProperty<IReadOnlyList<ValueBox>?> BoxesProperty =
        AvaloniaProperty.Register<ValueBoxesView, IReadOnlyList<ValueBox>?>(nameof(Boxes));

    public ValueBoxesView()
    {
        InitializeComponent();
    }

    public IReadOnlyList<ValueBox>? Boxes
    {
        get => GetValue(BoxesProperty);
        set => SetValue(BoxesProperty, value);
    }
}
