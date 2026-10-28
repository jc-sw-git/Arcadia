using Arcadia.ControllerSupport;
using Arcadia.Input;

namespace Arcadia.Tests.Pads;

/// <summary>Virtual Xbox pads that record what they get (#213); shared by the unit and the acceptance tests.</summary>
public sealed class FakeVirtualXboxPads : IVirtualXboxPads
{
    public int Plugged;
    public int Unplugged;
    public bool FailPlug;
    public string? Missing;

    /// <summary>A failed plug leaves the pads unavailable for this reason, as Pan does while its teardown runs.</summary>
    public string? MissingAfterFailedPlug;
    public bool ShutDown;

    /// <summary>Unplugging throws, as removing the device from a VIIPER that doesn't answer would.</summary>
    public bool FailUnplug;

    /// <summary>A send never ends until cancelled, as a write to a stuck VIIPER would.</summary>
    public bool StallSends;
    public FakePad? Current;

    /// <summary>How often VIIPER's server was made ready; <see cref="FailPrepare"/> makes it fail.</summary>
    public int Prepared;
    public bool FailPrepare;

    /// <summary>Runs while VIIPER is made ready, e.g. Steam coming to the front meanwhile.</summary>
    public Action? DuringPrepare;

    /// <summary>Shared with the other fakes for the order of events; null: not recorded.</summary>
    public List<string>? Events;

    private void Note(string e)
    {
        if (Events is { } events)
        {
            lock (events) events.Add(e);
        }
    }

    public string? Unavailable() => Missing;

    public Task PrepareAsync(CancellationToken cancellationToken)
    {
        if (FailPrepare)
        {
            throw new TimeoutException("viiper.exe antwortet nicht nach 10 s");
        }

        Prepared++;
        DuringPrepare?.Invoke();
        return Task.CompletedTask;
    }

    public Task<IVirtualXboxPad> PlugAsync(CancellationToken cancellationToken)
    {
        if (FailPlug)
        {
            Missing ??= MissingAfterFailedPlug;
            throw new InvalidOperationException(MissingAfterFailedPlug ?? "kaputt");
        }

        Plugged++;
        Note("pad plugged");
        Current = new FakePad(this);
        return Task.FromResult<IVirtualXboxPad>(Current);
    }

    public Task ShutdownAsync()
    {
        ShutDown = true;
        return Task.CompletedTask;
    }

    public sealed class FakePad(FakeVirtualXboxPads owner) : IVirtualXboxPad
    {
        private readonly List<XboxPadState> _sent = [];
        private volatile bool _lost;

        private volatile bool _takenAway;

        public bool IsConnected => !_lost;

        public bool TakenAway => _takenAway;

        public void Lose() => _lost = true;

        /// <summary>Pan tore the emulation down (or is gone): the pad is lost, but on purpose.</summary>
        public void TakeAway()
        {
            _takenAway = true;
            _lost = true;
        }

        /// <summary>Every state the pad got, in order.</summary>
        public IReadOnlyList<XboxPadState> Sent
        {
            get
            {
                lock (_sent) return [.. _sent];
            }
        }

        public Task SendAsync(XboxPadState state, CancellationToken cancellationToken)
        {
            if (owner.StallSends)
            {
                return StallAsync(cancellationToken);
            }

            lock (_sent) _sent.Add(state);
            return Task.CompletedTask;
        }

        // Like VIIPER's pad: a write cut off by its timeout leaves the pad broken.
        private async Task StallAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            finally
            {
                _lost = true;
            }
        }

        public async Task<XboxPadState> WaitForAsync(Func<XboxPadState, bool> test)
        {
            for (var i = 0; i < 200; i++)
            {
                lock (_sent)
                {
                    if (_sent.Count > 0 && test(_sent[^1]))
                    {
                        return _sent[^1];
                    }
                }

                await Task.Delay(10);
            }

            lock (_sent) throw new Xunit.Sdk.XunitException($"never sent; last: {(_sent.Count > 0 ? _sent[^1] : "nothing")}");
        }

        public ValueTask DisposeAsync()
        {
            owner.Unplugged++;
            owner.Note("pad unplugged");
            return owner.FailUnplug ? ValueTask.FromException(new IOException("VIIPER antwortet nicht")) : ValueTask.CompletedTask;
        }
    }
}
