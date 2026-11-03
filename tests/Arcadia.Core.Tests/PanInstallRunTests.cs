using System.Diagnostics;
using System.IO.Pipes;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Pan;

namespace Arcadia.Core.Tests;

/// <summary>
/// Arcadia's side of the hand-over pipe with real processes and a real pipe (#413, T413-16, T413-25): only the Pan it
/// started may take the installers, and a Pan that ends before it made its pipe ends the run at once.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class PanInstallRunTests
{
    private static InstallRun NewRun() =>
        new(InstallRun.NewId(), Environment.ProcessId, Path.Combine(Path.GetTempPath(), "ArcadiaTests", Guid.NewGuid().ToString("N")), Usbip: true, HidHide: false, HidHideRepair: false);

    /// <summary>A stand-in for the started Pan: a process that runs until killed and serves no pipe.</summary>
    private static Process StartIdle() =>
        Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "ping.exe"), "-n 60 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true })!;

    [WindowsFact]
    [Trait("TestCase", "T413-25")]
    public async Task A_pipe_served_by_another_process_than_the_started_Pan_is_refused()
    {
        var run = NewRun();
        using var pan = StartIdle();
        try
        {
            // Another process (here the test itself) made the run's pipe before the started Pan could.
            await using var squatter = new NamedPipeServerStream(InstallHandOver.PipeName(run.Id), PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            var waiting = squatter.WaitForConnectionAsync();

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => PanInstallRun.ConnectAsync(pan, run, CancellationToken.None));

            Assert.Contains("nicht dem gestarteten Pan", error.Message, StringComparison.Ordinal);
            await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            pan.Kill();
        }
    }

    [WindowsFact]
    [Trait("TestCase", "T413-16")]
    public async Task A_Pan_that_ends_before_making_its_pipe_ends_the_run_at_once_with_its_message()
    {
        var run = NewRun();
        // Exit 4: Pan couldn't secure its data folder, so it never made the pipe.
        using var pan = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/c exit 4") { UseShellExecute = false, CreateNoWindow = true })!;
        var clock = Stopwatch.StartNew();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => PanInstallRun.ConnectAsync(pan, run, CancellationToken.None));

        Assert.Equal(PanExitCodes.ErrorFor(PanExitCodes.DataFolder, "--install").Message, error.Message);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"the run ended {clock.Elapsed.TotalSeconds:0.0} s after Pan, not at once");
    }
}
