using Arcadia.ControllerSupport.Pan;
using Arcadia.Pan;

namespace Arcadia.Tests.Pads;

/// <summary>Pan as the tests see it (#243): records registrations, can refuse, tear down or be gone; shared by unit and acceptance tests.</summary>
public sealed class FakeEmulationGuardian : IEmulationGuardian, IHidingCheck
{
    private readonly Lock _lock = new();
    private uint _generation;
    private uint _current;
    private uint _tornDown;
    private bool _emulating;

    /// <summary>Why Pan can't serve (e.g. "Pan ist nicht eingerichtet"), or null.</summary>
    public string? Missing;

    /// <summary>Every "emulation started" is refused, as Pan does with a wrong generation or a journal it can't write.</summary>
    public bool Refuse;

    public List<uint> Started = [];
    public List<uint> Stopped = [];
    public List<uint> Acknowledged = [];
    public int Pulses;
    public bool PlannedExit;

    /// <summary>What Pan says about hiding now (#243 B2): None = it could hide; null = no answer (a Pan before part B).</summary>
    public PanReason? Hiding = PanReason.None;

    /// <summary>How often Arcadia asked whether Pan could hide.</summary>
    public int HidingChecks;

    public event Action<uint, PanReason>? TornDown;

    public event Action<string>? Lost;

    public string? Unavailable() => Missing;

    public Task<PanReason?> CheckHidingAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref HidingChecks);
        return Task.FromResult(Missing is null ? Hiding : null);
    }

    public Task<uint> EmulationStartedAsync(CancellationToken cancellationToken)
    {
        if (Missing is not null)
        {
            throw new InvalidOperationException(Missing);
        }

        if (Refuse)
        {
            throw new InvalidOperationException("Pan lehnt die Emulation ab: Rejected(gen 0, TeardownNotAcknowledged)");
        }

        lock (_lock)
        {
            _current = ++_generation;
            _emulating = true;
            Started.Add(_current);
            return Task.FromResult(_current);
        }
    }

    public Task EmulationStoppedAsync(uint generation, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            Stopped.Add(generation);
            if (_current == generation)
            {
                _emulating = false;
            }
        }

        return Task.CompletedTask;
    }

    public Task AcknowledgeTeardownAsync(uint generation, CancellationToken cancellationToken)
    {
        lock (_lock) Acknowledged.Add(generation);
        return Task.CompletedTask;
    }

    public bool IsCurrent(uint generation)
    {
        lock (_lock) return Missing is null && _emulating && _current == generation;
    }

    public bool WasTornDown(uint generation)
    {
        lock (_lock) return _tornDown == generation && generation != 0;
    }

    public void Pulse() => Interlocked.Increment(ref Pulses);

    public Task PlannedExitAsync()
    {
        PlannedExit = true;
        return Task.CompletedTask;
    }

    /// <summary>Pan tore the current emulation down (Arcadia hung); the pad is lost until acknowledged.</summary>
    public void TearDown(PanReason reason = PanReason.Hang)
    {
        uint generation;
        lock (_lock)
        {
            generation = _current;
            _tornDown = generation;
            _emulating = false;
        }

        TornDown?.Invoke(generation, reason);
    }

    /// <summary>Pan is gone (crashed, hung): nothing is current until it is back.</summary>
    public void Lose(string reason)
    {
        Missing = reason;
        lock (_lock) _emulating = false;
        Lost?.Invoke(reason);
    }

    /// <summary>Pan is back.</summary>
    public void Return() => Missing = null;
}
