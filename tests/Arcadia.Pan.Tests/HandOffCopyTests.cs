using System.Diagnostics;

namespace Arcadia.Pan.Tests;

/// <summary>Finding the running hand-off copy while Pan processes come and go (#434).</summary>
public sealed class HandOffCopyTests
{
    [Fact]
    public void A_process_that_exited_before_its_image_is_read_runs_no_copy()
    {
        using var process = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--version") { RedirectStandardOutput = true })!;
        process.StandardOutput.ReadToEnd();
        Assert.True(process.WaitForExit(TimeSpan.FromSeconds(30)), "the short-lived process ended");

        Assert.False(HandOffCopy.Runs(process, HandOffCopy.Executable));
    }

    [Fact]
    public void A_running_process_runs_its_own_image_and_not_the_copy()
    {
        using var self = Process.GetCurrentProcess();

        Assert.True(HandOffCopy.Runs(self, self.MainModule!.FileName));
        Assert.False(HandOffCopy.Runs(self, HandOffCopy.Executable));
    }
}
