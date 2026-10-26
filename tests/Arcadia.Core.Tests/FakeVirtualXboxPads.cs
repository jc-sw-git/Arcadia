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
            lock (_sent) _sent.Add(state);
            return Task.CompletedTask;
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
            return ValueTask.CompletedTask;
        }
    }
}
