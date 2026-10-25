namespace Arcadia.App.ViewModels;

/// <summary>
/// What a page asks of the window around it (#155): the rules between pages and the dialogs shared by several pages
/// belong to <see cref="MainViewModel"/>, so a page only says what the player wants and Main decides where that leads.
/// </summary>
/// <remarks>UI thread only, like the page calling it. A page calls it only while it is on top.</remarks>
public interface IPageHost
{
    /// <summary>B at the page's top level: Main closes it and shows what was there before.</summary>
    void Close(IPage page);

    /// <summary>
    /// Shows the shared filter and sort dialog (<see cref="ViewOptionsViewModel"/>) with the page's options;
    /// <paramref name="create"/> builds them again after every choice.
    /// </summary>
    void ShowViewOptions(IPage page, Func<ViewOptionsViewModel?> create);

    /// <summary>A choice in the dialog changed the page's options: the open dialog shows them again, its focus kept.</summary>
    void ViewOptionsChanged();

    /// <summary>The context menu of a game shown on the page, at its cover or over the library's "Optionen" button.</summary>
    void ShowMenu(GameTileViewModel tile, bool atCover);
}
