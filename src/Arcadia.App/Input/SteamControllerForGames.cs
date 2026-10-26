using Arcadia.ControllerSupport;
using Arcadia.Core.Settings;
using Arcadia.App.ViewModels;
using Arcadia.Input;
using Avalonia.Threading;

namespace Arcadia.App.Input;

/// <summary>
/// Wires the Steam Controller's virtual Xbox pad for games (#213) to Arcadia's state: it is wanted while SISR is off
/// (until the per-game mode replaces SISR), a game gets nothing while Arcadia is in front or the mouse mode is on, and
/// Back + Start is withheld while the mouse mode is not "Aus".
/// </summary>
public static class SteamControllerForGames
{
    public static SteamControllerXboxPad Create(
        IVirtualXboxPads pads,
        SettingsService settings,
        ControllerSupportService controllerSupport,
        Func<bool> isArcadiaActive,
        Func<bool> isMouseModeOn,
        ToastViewModel toast,
        TimeProvider time)
    {
        var combos = new CombosFromSettings(settings);
        var pad = new SteamControllerXboxPad(pads, time, Services.AppLog.Write)
        {
            Wanted = () => controllerSupport.State == SisrState.Off,
            ForwardingAllowed = () => !isMouseModeOn() && !isArcadiaActive(),
            Combos = combos.Current,
        };
        pad.GaveUp += message => Dispatcher.UIThread.Post(() => toast.Show(message));
        return pad;
    }

    /// <summary>Read on every poll; rebuilt only when the settings changed.</summary>
    private sealed class CombosFromSettings(SettingsService settings)
    {
        private AppSettings? _from;
        private ComboWithholdingOptions _options = new(false);

        public ComboWithholdingOptions Current()
        {
            var current = settings.Current;
            if (!ReferenceEquals(current, _from))
            {
                _from = current;
                _options = new ComboWithholdingOptions(current.MouseMode.Activation != MouseModeActivation.Off);
            }

            return _options;
        }
    }
}
