using Arcadia.Core.Search;

namespace Arcadia.App.Keyboard;

/// <summary>
/// One on-screen keyboard that short-lived users take in turn (#477): the searches, or the texts written in the settings.
/// Building a keyboard's controls anew for every user took about half a second; one view bound to this keyboard keeps its
/// controls, and each user only takes it over. It types into the user that took it last; once given back, into nothing.
/// </summary>
public sealed class SharedKeyboard : IKeyboardTarget, IDisposable
{
    private readonly Func<KeyboardLayoutChoice> _layouts;
    private IKeyboardTarget? _user;

    /// <param name="layouts">The default layout and the quick selection; they may change in the settings between two users.</param>
    /// <param name="post">Reaches the UI thread from the keyboard's repeat timers.</param>
    public SharedKeyboard(Func<KeyboardLayoutChoice> layouts, TimeProvider time, Action<Action> post)
    {
        _layouts = layouts;
        Keyboard = new OnScreenKeyboardViewModel(layouts(), this, time, post);
    }

    public OnScreenKeyboardViewModel Keyboard { get; }

    /// <summary><paramref name="user"/> takes the keyboard; it starts as a new one would: first key, shift off, the default layout (#553).</summary>
    public OnScreenKeyboardViewModel Take(IKeyboardTarget user, bool usesController)
    {
        _user = user;
        Keyboard.Open(_layouts(), atTop: false);
        Keyboard.FocusStart();
        Keyboard.UsesController = usesController;
        return Keyboard;
    }

    /// <summary><paramref name="user"/> is done; keys pressed meanwhile reach nothing. A newer user keeps the keyboard.</summary>
    public void GiveBack(IKeyboardTarget user)
    {
        if (_user == user)
        {
            _user = null;
            Keyboard.Closed();
        }
    }

    void IKeyboardTarget.Type(char character) => _user?.Type(character);

    void IKeyboardTarget.Press(BoardKeyKind key, bool withShift) => _user?.Press(key, withShift);

    void IKeyboardTarget.Hide() => _user?.Hide();

    void IKeyboardTarget.Dock(bool atTop) => _user?.Dock(atTop);

    public void Dispose() => Keyboard.Dispose();
}
