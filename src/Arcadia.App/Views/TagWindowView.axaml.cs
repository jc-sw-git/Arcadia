using Arcadia.App.ViewModels;
using Avalonia.Controls;

namespace Arcadia.App.Views;

/// <summary>"Tags" of one game (#495): keeps the focused tag in sight when there are more than fit.</summary>
public partial class TagWindowView : UserControl
{
    private readonly TagRowsInSight _sight;

    public TagWindowView()
    {
        InitializeComponent();
        _sight = new TagRowsInSight(TagRows, TagFade);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _sight.Follow(DataContext as OwnTagsDialogViewModel, dialog => dialog.Entries.ToList().FindIndex(entry => entry.IsFocused));
    }
}
