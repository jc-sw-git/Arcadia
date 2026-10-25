using Arcadia.App.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// A page of the window (#155): the first start, the settings, the detail page or the search. It takes the input while it
/// is on top and says which covers it shows; <see cref="MainViewModel"/> routes the input, keeps the rules between pages
/// and owns every page it opens. A page asks for what leads off it (close, a game's menu, the shared filter dialog)
/// through <see cref="IPageHost"/> (#209). The library is still part of Main; it becomes a page with #210.
/// </summary>
/// <remarks>
/// Lifetime and thread: Main calls a page on the UI thread only, and a page raises its events there. When a page stops
/// being on top (closed, or covered by another page) Main first calls its <see cref="CancelHeldButtons"/> and drops the
/// hold of the one <see cref="GameActions"/>, so a release that now goes elsewhere, or never comes, can't type, start a
/// game or open details late. A covered page is kept as it is and updated by Main after a rescan; a closed page is
/// disposed by Main if it is <see cref="IDisposable"/>, which stops its timers.
/// </remarks>
public interface IPage
{
    /// <summary>A button or key was pressed. False if the page didn't take it: then Main handles it, once.</summary>
    /// <remarks>
    /// Until the detail page and the settings ask through <see cref="IPageHost"/> too (#210), two pages use this result in
    /// their own way: the settings return false for B at the top and ≡, which Main takes as "close the settings"; the
    /// detail page returns true also for buttons it ignores, as Main only passes it the rest. The search takes every
    /// button and returns true.
    /// </remarks>
    bool Handle(UiAction action);

    /// <summary>A button or key was let go. Main passes it to every open page; one that didn't see the press ignores it.</summary>
    void Release(UiAction action);

    /// <summary>Drops held buttons: the page lost the top, or the window its focus, and their release may never arrive.</summary>
    void CancelHeldButtons();

    /// <summary>The covers the page shows, so a game's run and install states reach them wherever it is.</summary>
    IEnumerable<GameTileViewModel> ShownTiles { get; }

    /// <summary>The player switched between controller, keyboard and mouse; the page shows the hints and keyboard for it.</summary>
    void InputKindChanged(InputKind kind);
}
