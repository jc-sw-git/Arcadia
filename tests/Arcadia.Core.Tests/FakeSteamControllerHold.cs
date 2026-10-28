using Arcadia.ControllerSupport.Copy;

namespace Arcadia.Tests.Pads;

/// <summary>The copy of the Steam Controller for Steam as the tests see it (#242); shared by unit and acceptance tests.</summary>
public sealed class FakeSteamControllerHold : ISteamControllerHold
{
    public bool CanCopy { get; set; } = true;

    public bool PuckPresent { get; set; } = true;

    /// <summary>Attaching throws with this message, as a VIIPER without the copy's device type would.</summary>
    public string? FailAttach;

    /// <summary>The copy's removal throws with this (e.g. VIIPER not answering).</summary>
    public string? FailDetach;

    public int Attached;
    public int Detached;
    public FakeCopy? Current;

    /// <summary>Shared with the other fakes for the order of events; null: not recorded.</summary>
    public List<string>? Events;

    private void Note(string e)
    {
        if (Events is { } events)
        {
            lock (events) events.Add(e);
        }
    }

    /// <summary>Attaching never finishes until it is cancelled (a hanging VIIPER).</summary>
    public bool HangAttach;

    public async Task<ISteamControllerCopy> AttachAsync(CancellationToken cancellationToken)
    {
        if (FailAttach is { } failure)
        {
            throw new InvalidOperationException(failure);
        }

        if (HangAttach)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        Attached++;
        Note("copy attached");
        Current = new FakeCopy(this);
        return Current;
    }

    public sealed class FakeCopy(FakeSteamControllerHold owner) : ISteamControllerCopy
    {
        private volatile bool _lost;

        public bool IsConnected => !_lost;

        public void Lose() => _lost = true;

        public ValueTask DisposeAsync()
        {
            if (owner.FailDetach is { } why)
            {
                throw new InvalidOperationException(why);
            }

            owner.Detached++;
            owner.Note("copy detached");
            return ValueTask.CompletedTask;
        }
    }
}
