using System.Runtime.Versioning;
using Arcadia.Input;

namespace Arcadia.ControllerSupport.Copy;

/// <summary>What the copy needs from Arcadia's state: the filter it passes through, and when it passes input.</summary>
/// <param name="Forwarding">The copy passes input now (<see cref="SteamControllerCopyRules.CopyPasses"/>); asked per report.</param>
public sealed record SteamControllerCopyWiring(SteamControllerCopyFilter Filter, Func<bool> Forwarding, Func<ComboWithholdingOptions> Combos);

/// <summary>
/// The copy of the puck on Windows (#242 part C): the real puck read raw (<see cref="WindowsPuck"/>) and VIIPER's
/// <c>hidproxy</c> device (the fork's <c>viiper.exe</c>, <see cref="ViiperRelease"/>) on the server of Arcadia's Xbox pads.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ViiperSteamControllerCopy(ViiperXboxPads viiper, ViiperInstallation installation, SteamControllerCopyWiring wiring, Action<string> log)
    : ISteamControllerHold
{
    /// <summary>A device list costs a few milliseconds; the emulation asks every 2 s and more than once per tick.</summary>
    private static readonly TimeSpan PresenceCache = TimeSpan.FromSeconds(1);

    private readonly Lock _presence = new();
    private (DateTime At, bool Present) _lastPresence;

    /// <summary>Only the fork's <c>viiper.exe</c> knows the copy's device type; another one would make Pan hide in vain.</summary>
    public bool CanCopy => installation.IsPinned();

    public bool PuckPresent
    {
        get
        {
            lock (_presence)
            {
                var now = DateTime.UtcNow;
                if (now - _lastPresence.At > PresenceCache)
                {
                    _lastPresence = (now, WindowsPuck.Present());
                }

                return _lastPresence.Present;
            }
        }
    }

    public async Task<ISteamControllerCopy> AttachAsync(CancellationToken cancellationToken)
    {
        var puck = await Task.Run(() => WindowsPuck.Open(PuckDescriptors.Triton), cancellationToken);
        if (puck.PucksFound > 1)
        {
            log($"Steam-Controller-Kopie: {puck.PucksFound} Pucks angesteckt, nur einer wird für Steam kopiert");
        }

        try
        {
            var device = await viiper.AddRawDeviceAsync("hidproxy", PuckDescriptors.Triton.DeviceSpecific(puck.Serial), cancellationToken);
            var bridge = new SteamControllerCopyBridge(puck, device.Stream, wiring.Filter, wiring.Forwarding, wiring.Combos, PuckDescriptors.Triton, log);
            bridge.Start();
            return new Copy(bridge, device);
        }
        catch
        {
            puck.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The bridge first (nothing reaches the puck any more), then VIIPER's device, also when the bridge couldn't confirm
    /// that; its exception still reaches Pan's emulation, which then tears down in full.
    /// </summary>
    private sealed class Copy(SteamControllerCopyBridge bridge, ViiperRawDevice device) : ISteamControllerCopy
    {
        public bool IsConnected => bridge.IsConnected && !device.ServerEnded;

        public async ValueTask DisposeAsync()
        {
            try
            {
                await bridge.DisposeAsync();
            }
            finally
            {
                await device.DisposeAsync();
            }
        }
    }
}
