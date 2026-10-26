using Arcadia.ControllerSupport;
using Arcadia.Core.Settings;
using Arcadia.App.ViewModels;
using Arcadia.Input;
using Avalonia.Threading;

namespace Arcadia.App.Input;

/// <summary>
/// Wires the Steam Controller's virtual Xbox pad for games (#213) to Arcadia's state: the running games and their mode
/// ("Arcadia" or "Steam-Eingabe") decide whether it is plugged in (<see cref="SteamControllerRouting"/>), a game gets
/// nothing while Arcadia is in front or the mouse mode is on, and Back + Start is withheld while the mouse mode is not "Aus".
/// </summary>
public sealed class SteamControllerForGames
{
    private SteamControllerForGames(SteamControllerXboxPad pad, SteamControllerRouting routing)
    {
        Pad = pad;
        Routing = routing;
    }

    public SteamControllerXboxPad Pad { get; }

    public SteamControllerRouting Routing { get; }

    /// <param name="activeGames">The keys of the games that run or are being started (<c>RunningGamesTracker.ActiveGames</c>).</param>
    /// <param name="runningSteamAppId">The Steam game running right now, also one started directly in Steam (#119).</param>
    public static SteamControllerForGames Create(
        IVirtualXboxPads pads,
        SettingsService settings,
        Func<IReadOnlyCollection<string>> activeGames,
        Func<int?> runningSteamAppId,
        Func<bool> isArcadiaActive,
        Func<bool> isMouseModeOn,
        ToastViewModel toast,
        TimeProvider time)
    {
        var routing = new SteamControllerRouting(
            activeGames,
            key => settings.Current.UsesArcadiaPad(key),
            runningSteamAppId,
            () => pads.Unavailable() is null,
            Services.AppLog.Write);
        var combos = new CombosFromSettings(settings);
        var pad = new SteamControllerXboxPad(pads, time, Services.AppLog.Write)
        {
            Wanted = () => routing.Current.PadWanted,
            ForwardingAllowed = () => !isMouseModeOn() && !isArcadiaActive(),
            Combos = combos.Current,
        };
        pad.GaveUp += message => Dispatcher.UIThread.Post(() => toast.Show(message));
        return new SteamControllerForGames(pad, routing);
    }

    /// <summary>Every 2 s, off the UI thread: decides anew, then plugs the pad in or out.</summary>
    public async Task TickAsync()
    {
        await Task.Run(Routing.Update);
        await Pad.TickAsync();
    }

    /// <summary>
    /// Right before a game starts (it is already among the active games): a game on "Steam-Eingabe" must not find
    /// Arcadia's pad when it looks for controllers, so the pad goes now, not at the next tick.
    /// </summary>
    public async Task BeforeGameStartsAsync(CancellationToken cancellationToken)
    {
        // A VIIPER that hangs must not hold up the game: it starts anyway, and the next tick tries again.
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(BeforeStartLimit);
        try
        {
            await Task.Run(Routing.Update, limit.Token);
            await Pad.TickNowAsync(limit.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Services.AppLog.Write($"Xbox-Pad: vor dem Spielstart nicht innerhalb von {BeforeStartLimit.TotalSeconds:0} s angepasst");
        }
    }

    private static readonly TimeSpan BeforeStartLimit = TimeSpan.FromSeconds(3);

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
