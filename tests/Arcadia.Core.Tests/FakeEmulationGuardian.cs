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

    /// <summary>What a hide answers (#242): None = hidden.</summary>
    public PanReason HideResult = PanReason.None;

    /// <summary>Windows' answer to the puck restart a hide needs, asked at each hide (#308); null = <see cref="HideResult"/>.</summary>
    public Func<PanReason>? PuckRestart;

    /// <summary>What a check of the hiding generation answers while hidden: None = the hiding holds.</summary>
    public PanReason HidingHolds = PanReason.None;

    /// <summary>The generation the puck is hidden for, or 0.</summary>
    public uint HiddenFor;

    /// <summary>So many hides are followed at once by Pan's teardown (a Pan that ends every new emulation).</summary>
    public int TearDownAfterHides;

    public List<uint> Hidden = [];
    public List<uint> Unhidden = [];

    /// <summary>What a hide of a controller model answers (#335); unset: hidden.</summary>
    public Func<ControllerModel, ControllerHidingResult>? ControllerHide;

    /// <summary>Every controller hide asked for, as "generation model".</summary>
    public List<string> ControllerHides = [];

    /// <summary>Runs while Pan hides a controller, e.g. to see what Arcadia holds meanwhile.</summary>
    public Action? DuringControllerHide;

    public Task<ControllerHidingResult> HideControllerAsync(uint generation, ControllerModel model, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock) ControllerHides.Add($"{generation} {model}");
        Note($"hide {model}");
        DuringControllerHide?.Invoke();
        return Task.FromResult(ControllerHide?.Invoke(model) ?? new ControllerHidingResult(true, PanReason.None));
    }

    /// <summary>Shared with the other fakes, so a test sees the order of everything (e.g. "copy detached" before "unhide 1").</summary>
    public List<string> Events = [];

    private void Note(string e)
    {
        lock (Events) Events.Add(e);
    }

    public event Action<uint, PanReason>? TornDown;

    public event Action<string>? Lost;

    public event Action? PuckRestarting;

    public event Action? PuckRestarted;

    /// <summary>
    /// Pan restarts the puck on its own (a teardown's show, its retry, an arrival; #308): announces it, asks Windows
    /// (<see cref="PuckRestart"/>, None if unset) and says it's over. Returns Windows' answer.
    /// </summary>
    public PanReason RestartPuckItself()
    {
        PuckRestarting?.Invoke();
        Note("pan restarts puck");
        var result = PuckRestart?.Invoke() ?? PanReason.None;
        PuckRestarted?.Invoke();
        return result;
    }

    /// <summary>Only the start of <see cref="RestartPuckItself"/>, to overlap it with something else; <see cref="EndRestart"/> ends it.</summary>
    public void AnnounceRestart() => PuckRestarting?.Invoke();

    public void EndRestart() => PuckRestarted?.Invoke();

    public string? Unavailable() => Missing;

    public Task<PanReason?> CheckHidingAsync(CancellationToken cancellationToken) => CheckHidingAsync(0, cancellationToken);

    public Task<PanReason?> CheckHidingAsync(uint generation, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref HidingChecks);
        if (Missing is not null)
        {
            return Task.FromResult<PanReason?>(null);
        }

        return Task.FromResult(generation != 0 && generation == HiddenFor && Hiding == PanReason.None ? HidingHolds : Hiding);
    }

    public Task<PanReason> HideAsync(uint generation, CancellationToken cancellationToken)
    {
        Note($"hide {generation}");
        var result = PuckRestart?.Invoke() ?? HideResult;
        lock (_lock)
        {
            if (result == PanReason.None)
            {
                Hidden.Add(generation);
                HiddenFor = generation;
            }
        }

        if (TearDownAfterHides > 0)
        {
            TearDownAfterHides--;
            TearDown();
        }

        return Task.FromResult(result);
    }

    public Task<bool> UnhideAsync(uint generation, CancellationToken cancellationToken)
    {
        Note($"unhide {generation}");
        lock (_lock)
        {
            Unhidden.Add(generation);
            if (HiddenFor == generation)
            {
                HiddenFor = 0;
            }
        }

        return Task.FromResult(true);
    }

    /// <summary>The usbip port each start named, in order (#304).</summary>
    public List<int> StartedPorts { get; } = [];

    public Task<uint> EmulationStartedAsync(int usbipPort, CancellationToken cancellationToken)
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
            StartedPorts.Add(usbipPort);
            Note($"started {_current}");
            return Task.FromResult(_current);
        }
    }

    public Task EmulationStoppedAsync(uint generation, CancellationToken cancellationToken)
    {
        Note($"stopped {generation}");
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
        Note($"acknowledged {generation}");
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
            HiddenFor = 0;
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
