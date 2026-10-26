using Arcadia.App.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// A page of the window (#155): the first start, the settings, the detail page, the search or the library underneath them
/// all. It takes the input while it is on top and says which covers it shows; <see cref="MainViewModel"/> routes the
/// input, keeps the rules between pages and owns every page it opens. A page asks for what leads off it (close, a game's
/// menu, the shared filter dialog) through <see cref="IPageHost"/> (#209, #210).
/// </summary>
/// <remarks>
/// Lifetime and thread (#228): Main calls a page on the UI thread only, and a page raises its events there. Every page goes
/// through one lifecycle, Main's <c>OnPagesChanged</c>: a page is open while Main holds it in its property, covered while
/// another page lies over it, kept while Main holds it closed but brings it back (the detail page behind the search, #127),
/// and closed when Main lets go of it. When a page stops being on top (closed or covered) Main first calls its
/// <see cref="CancelHeldButtons"/> and drops the hold of the one <see cref="GameActions"/>, so a release that now goes
/// elsewhere, or never comes, can't type, start a game or open details late. A covered or kept page stays as it is and is
/// updated by Main after a rescan. A closed page that is <see cref="IDisposable"/> (timers, subscriptions: search,
/// settings) is disposed there, in one place, and every live one when Arcadia closes. The library is never closed.
/// A new page is a property of Main, listed in its <c>OpenPages</c> in the order the pages cover each other, and closed
/// through <see cref="IPageHost.Close"/>.
/// </remarks>
public interface IPage
{
    /// <summary>
    /// A button or key was pressed. False if the page didn't take it: then Main handles it, once, as on every page (≡ opens
    /// the settings, X the search). A page that ignores a button on purpose returns true, so it doesn't reach Main.
    /// </summary>
    bool Handle(UiAction action);

    /// <summary>A button or key was let go. Main passes it to every open page; one that didn't see the press ignores it.</summary>
    void Release(UiAction action);

    /// <summary>Drops held buttons: the page lost the top, or the window its focus, and their release may never arrive.</summary>
    void CancelHeldButtons();

    /// <summary>The covers the page shows, so a game's run and install states reach them wherever it is.</summary>
    IEnumerable<GameTileViewModel> ShownTiles { get; }

    /// <summary>The player switched between controller, keyboard and mouse; the page shows the hints and keyboard for it.</summary>
    void InputKindChanged(InputKind kind);

    /// <summary>
    /// The page covers the whole window, dialogs included (the first start, the settings): it takes every press before an
    /// open dialog does. Other pages leave a dialog over them the press first.
    /// </summary>
    bool CoversWindow => false;
}
