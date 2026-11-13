using Arcadia.App.ViewModels;
using Avalonia.Controls;

namespace Arcadia.App.Views;

/// <summary>"Eigene Tags" (#495): keeps the focused tag or game in sight when there are more than fit.</summary>
public partial class TagListView : UserControl
{
    private readonly TagRowsInSight _tags;
    private readonly TagRowsInSight _games;

    public TagListView()
    {
        InitializeComponent();
        _tags = new TagRowsInSight(TagRows, TagFade);
        _games = new TagRowsInSight(GameRows, GameFade);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        var list = DataContext as TagListViewModel;
        _tags.Follow(list, dialog => dialog is TagListViewModel { ShowsTags: true } ? dialog.Entries.ToList().FindIndex(entry => entry.IsFocused) : -1);
        _games.Follow(list, dialog => dialog is TagListViewModel { Games: { } games } ? games.FocusIndex : -1);
    }
}
