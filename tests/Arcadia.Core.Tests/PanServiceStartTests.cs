using Arcadia.ControllerSupport.Pan;

namespace Arcadia.Core.Tests;

/// <summary>How Arcadia waits for Pan's service to run (owner's B2 test, 2026-10-27: "Pan startet nicht (Stopped)").</summary>
public sealed class PanServiceStartTests
{
    [Fact]
    [Trait("TestCase", "T243-43")]
    public void Pan_stopping_by_itself_when_Arcadia_comes_back_is_started_again_once_it_has_stopped()
    {
        // Pan's idle stop is under way: wait for it, then start it.
        Assert.Equal(PanServiceStartStep.Wait, PanServiceStart.Next(running: false, stopped: false, startRequested: false));
        Assert.Equal(PanServiceStartStep.Start, PanServiceStart.Next(running: false, stopped: true, startRequested: false));
        Assert.Equal(PanServiceStartStep.Wait, PanServiceStart.Next(running: false, stopped: false, startRequested: true));
        Assert.Equal(PanServiceStartStep.Running, PanServiceStart.Next(running: true, stopped: false, startRequested: true));

        // Started and stopped again (Pan refused to run): reported at once, the client then retries only slowly.
        Assert.Equal(PanServiceStartStep.EndedAtOnce, PanServiceStart.Next(running: false, stopped: true, startRequested: true));
    }

    [Fact]
    [Trait("TestCase", "T243-44")]
    public void A_Pan_stopped_with_an_error_counts_as_failing_an_idle_stop_does_not()
    {
        Assert.True(PanServiceStart.Failed(stopped: true, win32ExitCode: 1066));
        Assert.True(PanServiceStart.Failed(stopped: true, win32ExitCode: 1067));
        Assert.False(PanServiceStart.Failed(stopped: true, win32ExitCode: 0));
        Assert.False(PanServiceStart.Failed(stopped: false, win32ExitCode: 1066));
    }
}
