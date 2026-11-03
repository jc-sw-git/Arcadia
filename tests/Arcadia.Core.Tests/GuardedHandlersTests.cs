using Arcadia.Input;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>#399 at the SDL events: each handler is a source of its own, the others go on.</summary>
public sealed class GuardedHandlersTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _log = [];
    private readonly List<string> _toasts = [];

    private GuardedHandlers<int> Handlers(Action<string>? log = null, Action<Exception, string, ErrorStreak>? unexpected = null) =>
        new("Controller-Eingabe", unexpected ?? ((error, context, _) => _toasts.Add($"{context}: {error.Message}")), log ?? _log.Add, _time);

    [Fact]
    [Trait("TestCase", "T399-4")]
    public void A_handler_throwing_on_every_poll_at_1_kHz_for_3_minutes_toasts_once_and_the_others_get_every_poll()
    {
        var handlers = Handlers();
        var before = 0;
        var after = 0;
        handlers.Add(_ => before++);
        handlers.Add(_ => throw new InvalidOperationException("poll broke"));
        handlers.Add(_ => after++);

        const int Polls = 3 * 60 * 1000;
        for (var i = 0; i < Polls; i++)
        {
            handlers.Raise(i);
            _time.Advance(TimeSpan.FromMilliseconds(1));
        }

        Assert.Equal(["Controller-Eingabe: poll broke"], _toasts);
        Assert.InRange(_log.Count, 1, 3);
        Assert.All(_log, line => Assert.StartsWith("Unerwarteter Fehler (Controller-Eingabe, GuardedHandlersTests.", line));
        Assert.Equal(Polls, before);
        Assert.Equal(Polls, after);
    }

    [Fact]
    [Trait("TestCase", "T399-8")]
    public void Two_handlers_of_one_event_each_toast_once()
    {
        var handlers = Handlers();
        handlers.Add(_ => throw new InvalidOperationException("first broke"));
        handlers.Add(_ => throw new InvalidOperationException("second broke"));

        handlers.Raise(1);
        handlers.Raise(2);

        Assert.Equal(["Controller-Eingabe: first broke", "Controller-Eingabe: second broke"], _toasts);
    }

    [Fact]
    [Trait("TestCase", "T399-12")]
    public void A_throwing_toast_and_log_are_swallowed_and_the_next_handler_gets_the_event()
    {
        var handlers = Handlers(_ => throw new IOException("log broke"), (_, _, _) => throw new InvalidOperationException("toast broke"));
        var heard = new List<int>();
        handlers.Add(_ => throw new InvalidOperationException("handler broke"));
        handlers.Add(heard.Add);

        handlers.Raise(1);
        _time.Advance(TimeSpan.FromMinutes(1));
        handlers.Raise(2);

        Assert.Equal([1, 2], heard);
    }

    [Fact]
    [Trait("TestCase", "T399-15")]
    public void Raising_to_three_handlers_without_an_error_allocates_nothing()
    {
        var handlers = Handlers();
        var calls = 0;
        handlers.Add(_ => calls++);
        handlers.Add(_ => calls++);
        handlers.Add(_ => calls++);
        handlers.Raise(0);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100_000; i++)
        {
            handlers.Raise(i);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(300_003, calls);
    }
}
