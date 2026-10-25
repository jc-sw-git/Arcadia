using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.Input;
using Avalonia.Threading;

namespace Arcadia.App.Input;

/// <summary>
/// The controller's buttons into Arcadia's navigation, from the gamepad's poll thread to the UI thread. ⧉ + ≡ switch the
/// mouse mode, so View and Menu act alone only (<see cref="SoloButtons"/>): on release, or held alone a moment. In the
/// mouse mode the controller is a mouse, and while another window is in front it doesn't drive the library.
/// </summary>
public sealed class GamepadRouting
{
    /// <summary>How long View or Menu must be held alone before they act, so a hold (≡ on a search hit) still works.</summary>
    public static readonly TimeSpan SoloButtonDelay = TimeSpan.FromMilliseconds(200);

    private readonly MainViewModel _viewModel;
    private readonly Func<bool> _isArcadiaActive;
    private readonly Func<bool> _isMouseModeOn;
    private readonly SoloButtons _viewAndMenu;

    public GamepadRouting(MainViewModel viewModel, Func<bool> isArcadiaActive, Func<bool> isMouseModeOn, TimeProvider time)
    {
        _viewModel = viewModel;
        _isArcadiaActive = isArcadiaActive;
        _isMouseModeOn = isMouseModeOn;
        _viewAndMenu = new SoloButtons(GamepadButton.View, GamepadButton.Menu, SoloButtonDelay, time);
        _viewAndMenu.Pressed += Act;
        _viewAndMenu.Released += LetGo;
        _viewAndMenu.Cancelled += _ => Dispatcher.UIThread.Post(viewModel.CancelHold);
    }

    /// <summary>On every poll of the controllers: View or Menu held alone long enough act now.</summary>
    public void Tick() => _viewAndMenu.Tick();

    public void Press(GamepadButton button)
    {
        if (_isMouseModeOn())
        {
            return;
        }

        if (_viewAndMenu.Handles(button))
        {
            _viewAndMenu.Press(button);
        }
        else
        {
            Act(button);
        }
    }

    public void Release(GamepadButton button)
    {
        if (_viewAndMenu.Handles(button))
        {
            _viewAndMenu.Release(button);
        }
        else
        {
            LetGo(button);
        }
    }

    private void Act(GamepadButton button)
    {
        // Checked here, not only on the UI thread: View held in the background brings Arcadia to the
        // front, and the same press must not open the context menu once it got there.
        if (!_isArcadiaActive())
        {
            return;
        }

        if (UiActionMap.FromGamepad(button) is { } action)
        {
            AppLog.Write($"Gamepad: {button} → {action}");
            Dispatcher.UIThread.Post(() =>
            {
                if (_isArcadiaActive())
                {
                    _viewModel.HandleGamepad(action);
                }
            });
        }
    }

    // Releases go through even while another window is in front, so a held A can't get stuck.
    private void LetGo(GamepadButton button)
    {
        if (UiActionMap.FromGamepad(button) is { } action)
        {
            Dispatcher.UIThread.Post(() => _viewModel.Release(action));
        }
    }
}
