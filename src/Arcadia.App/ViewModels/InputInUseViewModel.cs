using Arcadia.App.Input;
using Arcadia.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>
/// Which input is in use (controller, keyboard or mouse) and which controller (#127, #215): the button hints, the ✕ on
/// dialogs and the pages follow it.
/// </summary>
public sealed partial class InputInUseViewModel : ViewModelBase
{
    // A button, key or click since the start; until then the hints follow whether a controller is connected (#215).
    private bool _inputSeen;

    /// <summary>The input in use: the controller until a key, click or the last controller going away says otherwise.</summary>
    public InputKind Kind { get; private set; } = InputKind.Gamepad;

    /// <summary>The input in use changed; the open pages adapt to it (the search's on-screen keyboard and legend).</summary>
    public event Action<InputKind>? KindChanged;

    /// <summary>Dialogs show an ✕ in their corner while mouse or keyboard is in use; with the controller B closes them.</summary>
    [ObservableProperty]
    public partial bool ShowsCloseButtons { get; private set; }

    /// <summary>
    /// The hints show the controller's buttons; after a key press or a click the keyboard's keys and the mouse (#127).
    /// The mouse merely moving changes nothing, so a nudged mouse doesn't swap the hints under the controller.
    /// </summary>
    [ObservableProperty]
    public partial bool UsesController { get; private set; } = true;

    partial void OnUsesControllerChanged(bool value) => InputHints.UsesController = value;

    /// <summary>The printing of the controller in use, which the button hints show (#215); Xbox's until a controller tells otherwise.</summary>
    [ObservableProperty]
    public partial ButtonSymbols ButtonSymbols { get; private set; } = ButtonSymbols.Xbox;

    partial void OnButtonSymbolsChanged(ButtonSymbols value) => InputHints.Symbols = value;

    /// <summary>A button, key or click: that input is in use from now on.</summary>
    public void Note(InputKind kind)
    {
        _inputSeen = true;
        Show(kind);
    }

    /// <summary>The mouse moved: dialogs show their ✕. Unlike a click, it leaves the search's legend alone.</summary>
    public void NotePointerMoved() => ShowsCloseButtons = true;

    /// <summary>
    /// The controller in use changed (#215, <see cref="ControllerInUse"/>): the hints show its printing. With none left the
    /// keyboard is the input in use. Before the first input the hints follow whether a controller is there at all.
    /// </summary>
    public void SetControllerInUse(PadType? type)
    {
        if (type is { } pad)
        {
            ButtonSymbols = ButtonSymbolSets.For(pad);
            if (!_inputSeen)
            {
                Show(InputKind.Gamepad);
            }
        }
        else if (Kind == InputKind.Gamepad)
        {
            Show(InputKind.Keyboard);
        }
    }

    private void Show(InputKind kind)
    {
        Kind = kind;
        ShowsCloseButtons = kind != InputKind.Gamepad;
        UsesController = kind == InputKind.Gamepad;
        KindChanged?.Invoke(kind);
    }
}
