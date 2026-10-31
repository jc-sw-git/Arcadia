namespace Arcadia.ControllerSupport;

/// <summary>
/// Whether the controllers are to be read every millisecond (#238 D9, <c>IGamepadSource.ReadFast</c>): only while some
/// pad is plugged and its states reach the game, not while every pad rests because Arcadia is in front or the mouse
/// mode is on, so the idle budget holds with controllers connected.
/// </summary>
public sealed class FastReads(Action<bool> readFast)
{
    private readonly Lock _lock = new();
    private bool _others;
    private bool _steamController;
    private bool _fast;

    /// <summary>For <see cref="Pan.TakenOverControllers.Forwarding"/>.</summary>
    public void Others(bool forwarding) => Set(ref _others, forwarding);

    /// <summary>For <see cref="SteamControllerXboxPad.Forwarding"/>.</summary>
    public void SteamController(bool forwarding) => Set(ref _steamController, forwarding);

    private void Set(ref bool field, bool forwarding)
    {
        lock (_lock)
        {
            field = forwarding;
            var fast = _others || _steamController;
            if (fast != _fast)
            {
                _fast = fast;
                readFast(fast);
            }
        }
    }
}
