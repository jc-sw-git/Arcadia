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
    public bool ShutDown;

    /// <summary>Unplugging throws, as removing the device from a VIIPER that doesn't answer would.</summary>
    public bool FailUnplug;

    /// <summary>A send never ends until cancelled, as a write to a stuck VIIPER would.</summary>
    public bool StallSends;
    public FakePad? Current;

    public string? Unavailable() => Missing;

    public Task<IVirtualXboxPad> PlugAsync(CancellationToken cancellationToken)
    {
        if (FailPlug)
        {
            throw new InvalidOperationException("kaputt");
        }

        Plugged++;
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

        public bool IsConnected => !_lost;

        public void Lose() => _lost = true;

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
            return owner.FailUnplug ? ValueTask.FromException(new IOException("VIIPER antwortet nicht")) : ValueTask.CompletedTask;
        }
    }
}
