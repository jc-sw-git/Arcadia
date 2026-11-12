using Arcadia.App.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.App.Tests;

/// <summary>Quick turns of the detail page's carousel stop the running jump instead of queueing theirs (#506, scenario 1).</summary>
public sealed class TurnJumpTests
{
    private static readonly TimeSpan JumpTime = TimeSpan.FromMilliseconds(420);

    [Fact]
    [Trait("TestCase", "T506-1")]
    [Trait("TestCase", "T506-6")]
    public void A_turn_on_its_own_jumps_also_after_a_series_of_quick_ones()
    {
        var time = new FakeTimeProvider();
        var jump = new TurnJump(time, JumpTime);
        Assert.NotNull(jump.Turn());

        time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Null(jump.Turn());
        time.Advance(JumpTime);
        Assert.NotNull(jump.Turn());
    }

    [Fact]
    [Trait("TestCase", "T506-2")]
    [Trait("TestCase", "T506-3")]
    public void Each_quick_turn_stops_the_running_jump_and_none_follows()
    {
        var time = new FakeTimeProvider();
        var jump = new TurnJump(time, JumpTime);
        var first = jump.Turn()!.Value;

        for (var press = 2; press <= 5; press++)
        {
            time.Advance(TimeSpan.FromMilliseconds(80));
            Assert.Null(jump.Turn());
        }

        Assert.True(first.IsCancellationRequested, "Der laufende Sprung läuft weiter.");
    }

    [Fact]
    [Trait("TestCase", "T506-2")]
    public void Leaving_the_page_stops_the_running_jump()
    {
        var jump = new TurnJump(new FakeTimeProvider(), JumpTime);
        var running = jump.Turn()!.Value;
        jump.Stop();
        Assert.True(running.IsCancellationRequested);
    }
}
