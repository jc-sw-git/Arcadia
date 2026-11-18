using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Arcadia.App.Controls;

/// <summary>
/// Builds its child from <see cref="Template"/> the first time it is shown, and keeps it from then on (#685). For parts
/// that are hidden nearly always but sit in an element built many times over, such as a cover's badges for a running
/// game or an install: Avalonia measures only what is visible, so a part that is never shown costs nothing.
/// </summary>
public sealed class BuiltWhenShown : Decorator
{
    public static readonly StyledProperty<IDataTemplate?> TemplateProperty =
        AvaloniaProperty.Register<BuiltWhenShown, IDataTemplate?>(nameof(Template));

    /// <summary>Builds the child; its <c>DataContext</c> is this element's.</summary>
    public IDataTemplate? Template
    {
        get => GetValue(TemplateProperty);
        set => SetValue(TemplateProperty, value);
    }

    /// <summary>The child is built (the part was shown once), for tests.</summary>
    public bool IsBuilt => Child is not null;

    protected override Size MeasureOverride(Size availableSize)
    {
        Child ??= Template?.Build(DataContext);
        return base.MeasureOverride(availableSize);
    }
}
