using Arcadia.App.Keyboard;
using Arcadia.App.Views;
using Arcadia.Core.Search;
using Arcadia.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.App.Tests;

/// <summary>
/// The mouse mode's on-screen keyboard (#110) in its own window, as <c>MouseModeHost</c> opens it over the window in
/// front: its buttons come from the mouse mode, what it types goes to <see cref="Typed"/>. The mouse mode itself (Y,
/// the controller as a mouse, the window in front) is not part of it.
/// </summary>
internal sealed class MouseModeKeyboard : IDisposable
{
    private MouseModeKeyboard(OnScreenKeyboardViewModel keyboard, MouseModeKeyboardWindow window, TypedText typed)
    {
        Keyboard = keyboard;
        Window = window;
        Typed = typed;
    }

    public OnScreenKeyboardViewModel Keyboard { get; }

    public MouseModeKeyboardWindow Window { get; }

    /// <summary>What the keyboard typed into the window in front.</summary>
    public TypedText Typed { get; }

    public static MouseModeKeyboard Show(string layoutCode = "DE")
    {
        var layout = KeyboardLayout.All.Single(candidate => candidate.Code == layoutCode);
        var typed = new TypedText();
        var keyboard = new OnScreenKeyboardViewModel(layout, typed, new FakeTimeProvider(ArcadiaDriver.Now), action => Dispatcher.UIThread.Post(action));
        var window = new MouseModeKeyboardWindow { DataContext = keyboard };
        window.Open(lastAtTop: false);

        // The size the keyboard takes on a 1920 × 1080 monitor. On Windows the window sizes itself with SetWindowPos,
        // which the headless platform ignores; elsewhere Open sets this size already.
        window.Width = 1646;
        window.Height = 384;
        keyboard.Open(layout, atTop: false);
        Dispatcher.UIThread.RunJobs();
        return new MouseModeKeyboard(keyboard, window, typed);
    }

    /// <summary>A controller button tapped while the keyboard is open.</summary>
    public MouseModeKeyboard Press(KeyboardButton button)
    {
        Keyboard.Handle(button, down: true);
        Keyboard.Handle(button, down: false);
        Dispatcher.UIThread.RunJobs();
        return this;
    }

    /// <summary>The button of <paramref name="key"/>.</summary>
    public Button KeyButton(BoardKeyViewModel key) => Window.GetVisualDescendants().OfType<Button>()
        .First(button => button.Classes.Contains("boardKey") && button.DataContext == key);

    public Rect Bounds(Control control) => ArcadiaWindow.Bounds(Window, control);

    public void Dispose()
    {
        Window.Close();
        Keyboard.Dispose();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The window in front, as far as the keyboard types into it.</summary>
    internal sealed class TypedText : IKeyboardTarget
    {
        public string Text { get; private set; } = string.Empty;

        public List<BoardKeyKind> Pressed { get; } = [];

        public bool IsHidden { get; private set; }

        public void Type(char character) => Text += character;

        public void Press(BoardKeyKind key, bool withShift) => Pressed.Add(key);

        public void Hide() => IsHidden = true;

        public void Dock(bool atTop)
        {
        }
    }
}
