namespace Arcadia.ControllerSupport.Copy;

/// <summary>
/// The copy of the Steam Controller that Steam sees instead of the real one (#242, spike #239): a second virtual device
/// beside Arcadia's Xbox pad that presents itself exactly like the puck. It passes on what
/// <see cref="SteamControllerCopyFilter"/> lets through, and Steam's reports go through the filter to the real puck.
/// Pan hides the real puck first (<see cref="Pan.PanEmulation"/>); this interface is all the emulation knows of the
/// copy, so a Linux copy can stand behind it.
/// </summary>
public interface ISteamControllerHold
{
    /// <summary>
    /// This build can make the copy at all. Until it can, Arcadia doesn't hide the real puck either: Steam would lose the
    /// controller with nothing in its place.
    /// </summary>
    bool CanCopy { get; }

    /// <summary>A Steam Controller's puck is connected, so there is something to copy.</summary>
    bool PuckPresent { get; }

    /// <summary>Attaches the copy of the connected puck; throws when that fails. The copy goes when it is disposed.</summary>
    Task<ISteamControllerCopy> AttachAsync(CancellationToken cancellationToken);
}

/// <summary>One attached copy; disposing it detaches it, after which nothing forwards to the real puck any more.</summary>
public interface ISteamControllerCopy : IAsyncDisposable
{
    /// <summary>False once the copy is gone without being detached (the puck left, VIIPER ended).</summary>
    bool IsConnected { get; }
}

/// <summary>No copy in this build (until the VIIPER fork and its Windows side, #242 parts B and C).</summary>
public sealed class NoSteamControllerCopy : ISteamControllerHold
{
    public bool CanCopy => false;

    public bool PuckPresent => false;

    public Task<ISteamControllerCopy> AttachAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("Diese Arcadia-Version kann den Steam Controller noch nicht für Steam kopieren");
}
