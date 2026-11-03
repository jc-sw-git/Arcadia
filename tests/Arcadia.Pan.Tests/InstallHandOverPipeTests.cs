using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Arcadia.Pan.Windows;

namespace Arcadia.Pan.Tests;

/// <summary>
/// The hand-over pipe on Windows (#413, T413-25, T413-26): only Arcadia's user may open it, only Arcadia's process is
/// heard, a name taken before ends the run, and a process that started after Pan can't stand in for Arcadia. Pan and
/// Arcadia are this one process here, so Pan's start is given as a later one where the test needs an older Arcadia.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class InstallHandOverPipeTests
{
    private static readonly long Later = DateTime.UtcNow.AddDays(1).ToFileTimeUtc();

    private static InstallRun Run(int client) => new(InstallRun.NewId(), client, Path.GetTempPath(), Usbip: false, HidHide: false, HidHideRepair: false);

    [WindowsFact]
    [Trait("TestCase", "T413-25")]
    public void Only_Arcadia_s_user_may_read_and_write_the_pipe()
    {
        using var pipe = InstallHandOverPipe.Create(Run(Environment.ProcessId), _ => { }, out var failure, Later);

        Assert.Null(failure);
        var rules = pipe!.Security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();
        using var me = WindowsIdentity.GetCurrent();
        var rule = Assert.Single(rules);
        Assert.Equal(me.User, rule.IdentityReference);
        Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        Assert.Equal(PipeAccessRights.ReadWrite, rule.PipeAccessRights & PipeAccessRights.FullControl & ~PipeAccessRights.Synchronize);
    }

    [WindowsFact]
    [Trait("TestCase", "T413-26")]
    public void A_pipe_name_taken_before_ends_the_run_without_waiting()
    {
        var run = Run(Environment.ProcessId);
        using var first = InstallHandOverPipe.Create(run, _ => { }, out _, Later);

        using var second = InstallHandOverPipe.Create(run, _ => { }, out var failure, Later);

        Assert.Null(second);
        Assert.Equal(HandOverEnd.PipeTaken, failure);
    }

    [WindowsFact]
    [Trait("TestCase", "T413-26")]
    public void A_process_that_started_after_Pan_is_not_the_Arcadia_that_started_it()
    {
        using var newer = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 5 127.0.0.1 > nul") { CreateNoWindow = true, UseShellExecute = false })!;
        try
        {
            using var pipe = InstallHandOverPipe.Create(Run(newer.Id), _ => { }, out var failure);

            Assert.Null(pipe);
            Assert.Equal(HandOverEnd.PipeTaken, failure);
        }
        finally
        {
            newer.Kill();
        }
    }

    [WindowsFact]
    [Trait("TestCase", "T413-25")]
    public async Task Another_process_is_refused_and_Arcadia_still_gets_through()
    {
        var run = Run(Environment.ProcessId);
        var log = new List<string>();
        using var pipe = InstallHandOverPipe.Create(run, line => { lock (log) { log.Add(line); } }, out _, Later)!;
        var receive = pipe.ReceiveAsync(InstallHandOver.MaxMessageBytes, CancellationToken.None);

        // Same user, another process: let in by the DACL, refused by its process id.
        var script = $"$p = New-Object System.IO.Pipes.NamedPipeClientStream('.', '{InstallHandOver.PipeName(run.Id)}', 'InOut'); $p.Connect(10000); " +
                     "$b = [Text.Encoding]::UTF8.GetBytes(\"go`n\"); try { $p.Write($b, 0, $b.Length); $p.Flush() } catch { }; Start-Sleep -Milliseconds 500";
        using (var other = Process.Start(new ProcessStartInfo("powershell.exe", ["-NoProfile", "-Command", script]) { CreateNoWindow = true, UseShellExecute = false })!)
        {
            Assert.True(other.WaitForExit(TimeSpan.FromSeconds(60)), "the other client ended");
        }

        Assert.False(receive.IsCompleted, "nothing was taken from the other process");
        lock (log)
        {
            Assert.Contains(log, line => line.Contains("instead of Arcadia", StringComparison.Ordinal));
        }

        await using (var arcadia = new NamedPipeClientStream(".", InstallHandOver.PipeName(run.Id), PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await arcadia.ConnectAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            await arcadia.WriteAsync(InstallHandOver.Abort());
        }

        Assert.Equal(InstallHandOver.Abort(), await receive.WaitAsync(TimeSpan.FromSeconds(10)));
    }
}
