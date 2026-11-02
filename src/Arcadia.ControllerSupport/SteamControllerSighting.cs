namespace Arcadia.ControllerSupport;

/// <summary>
/// Whether the Steam Controller is connected, as the settings list it ("Spiele sehen", "Controller zuordnen"): what
/// <see cref="SteamControllerXboxPad"/> reads, except while Arcadia lets go of it for Pan (a hide, Pan's restart of the
/// puck) and for <see cref="RereadGrace"/> after. SDL doesn't see it then though it is still there, and a list built in
/// that moment would leave it out.
/// </summary>
public sealed class SteamControllerSighting(TimeProvider time, Action<string> log)
{
    /// <summary>How long after a let-go the controller may take to be read again before it counts as gone.</summary>
    public static readonly TimeSpan RereadGrace = TimeSpan.FromSeconds(3);

    private readonly Lock _lock = new();
    private volatile bool _connected;
    private int _letGo;
    private long _readAgainAt = long.MinValue;

    public bool Connected => _connected;

    /// <summary>Raised on the reading thread when <see cref="Connected"/> changes.</summary>
    public event Action? Changed;

    /// <summary>Every poll, on the reading thread: whether SDL read a Steam Controller. Cheap while nothing changes; never throws.</summary>
    public void Seen(bool connected)
    {
        if (connected == _connected)
        {
            return;
        }

        lock (_lock)
        {
            if (connected == _connected
                || (!connected && (_letGo > 0 || (_readAgainAt != long.MinValue && time.GetElapsedTime(_readAgainAt) < RereadGrace))))
            {
                return;
            }

            _connected = connected;
        }

        Raise();
    }

    /// <summary>Arcadia lets go of the Steam Controller (it stays listed); one <see cref="ReadAgain"/> each.</summary>
    public void LetGo()
    {
        lock (_lock)
        {
            _letGo++;
        }
    }

    /// <summary>Arcadia reads the Steam Controller again: it is gone if it isn't read within <see cref="RereadGrace"/>.</summary>
    public void ReadAgain()
    {
        lock (_lock)
        {
            if (_letGo > 0)
            {
                _letGo--;
            }

            _readAgainAt = time.GetTimestamp();
        }
    }

    /// <summary>The gamepad source stopped for good: nothing reads the controller any more.</summary>
    public void Lost()
    {
        lock (_lock)
        {
            _letGo = 0;
            _readAgainAt = long.MinValue;
            if (!_connected)
            {
                return;
            }

            _connected = false;
        }

        Raise();
    }

    // A throwing subscriber must not cost the poll its read.
    private void Raise()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            log($"Steam Controller: Liste nicht aktualisiert: {ex}");
        }
    }
}
