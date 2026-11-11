using System.Collections.ObjectModel;
using Arcadia.App.Keyboard;
using Arcadia.App.ViewModels;
using Arcadia.Core.Search;

namespace Arcadia.App.Search;

/// <summary>
/// The parts of the search page that outlive each search (#477): its on-screen keyboard and the places of its hits. Building
/// their controls anew for every search took most of the half second between X and the search on screen; each search binds
/// the kept ones instead, as the library's pages do with their covers (#229). The keyboard types into the search open now.
/// </summary>
public sealed class SearchPageParts : IKeyboardTarget, IDisposable
{
    private IKeyboardTarget? _search;

    /// <param name="layoutChosen">A layout was picked on the search's keyboard; it counts everywhere the keyboard shows.</param>
    /// <param name="post">Reaches the UI thread from the keyboard's repeat timers.</param>
    public SearchPageParts(KeyboardLayout layout, Action<KeyboardLayout> layoutChosen, TimeProvider time, Action<Action> post)
    {
        Keyboard = new OnScreenKeyboardViewModel(layout, this, time, post);
        Keyboard.LayoutChosen += layoutChosen;
        // A search field has no tab order to go through, and the keyboard is part of the page, with no edge to move to.
        Keyboard.Configure(key => key.Kind is BoardKeyKind.Tab or BoardKeyKind.Dock ? BoardKeyState.Hidden : BoardKeyState.Shown);
    }

    public OnScreenKeyboardViewModel Keyboard { get; }

    /// <summary>The places the view shows a search's hits in (<see cref="SearchViewModel.Slots"/>).</summary>
    public ObservableCollection<TileSlotViewModel> Slots { get; } = [];

    /// <summary>A new search takes the keyboard: it starts as a new one would, on the given layout.</summary>
    public void Attach(SearchViewModel search, KeyboardLayout layout)
    {
        _search = search;
        Keyboard.Open(layout, atTop: false);
        Keyboard.FocusStart();
    }

    /// <summary>The search closed; keys pressed meanwhile reach nothing. A newer search keeps the keyboard.</summary>
    public void Detach(SearchViewModel search)
    {
        if (_search == search)
        {
            _search = null;
            Keyboard.Closed();
        }
    }

    void IKeyboardTarget.Type(char character) => _search?.Type(character);

    void IKeyboardTarget.Press(BoardKeyKind key, bool withShift) => _search?.Press(key, withShift);

    void IKeyboardTarget.Hide() => _search?.Hide();

    void IKeyboardTarget.Dock(bool atTop) => _search?.Dock(atTop);

    public void Dispose() => Keyboard.Dispose();
}
