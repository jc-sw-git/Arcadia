using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.ServiceProcess;
using Arcadia.Pan.Windows;
using Microsoft.Win32;

namespace Arcadia.Pan.Tests;

/// <summary>
/// Runs only on a Windows CI runner with administrator rights (<c>CI=true</c>, or <c>ARCADIA_PAN_SERVICE_TESTS=1</c> by hand):
/// it installs the real service, so a developer's own Pan is never touched.
/// </summary>
public sealed class ServiceFactAttribute : FactAttribute
{
    public ServiceFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Pan is a Windows service";
        }
        else if (!IsElevated())
        {
            Skip = "installing the service needs administrator rights";
        }
        else if (Environment.GetEnvironmentVariable("CI") != "true" && Environment.GetEnvironmentVariable("ARCADIA_PAN_SERVICE_TESTS") != "1")
        {
            Skip = "installs the real service; set ARCADIA_PAN_SERVICE_TESTS=1 to run it outside CI";
        }
    }

    private static bool IsElevated()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}

/// <summary>
/// The installed service, end to end (#243, T243-7 … T243-11): <c>--install</c> with the hash check, the DACL that keeps
/// users from changing it, the quoted image path, the pipe's ACL, a stop that tears the emulation down, a hang that does
/// the same, and <c>--uninstall</c>. One test, because the steps build on each other on one machine.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class ServiceTests : IDisposable
{
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TeardownTimeout = TimeSpan.FromSeconds(20);
    private readonly string _work = Path.Combine(Path.GetTempPath(), "ArcadiaPanTests", Guid.NewGuid().ToString("N"));
    private readonly List<Process> _children = [];

    public void Dispose()
    {
        foreach (var child in _children)
        {
            try
            {
                if (!child.HasExited)
                {
                    child.Kill();
                }
            }
            catch (InvalidOperationException)
            {
            }

            child.Dispose();
        }

        try
        {
            Directory.Delete(_work, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [ServiceFact]
    public async Task The_installed_service_guards_hands_back_and_goes_again()
    {
        var pan = await PublishPanAsync();
        var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(pan)));

        // T243-7: a hash that isn't the file's (a swapped Pan.exe) installs nothing; with the hash Arcadia computed the
        // copy lands in Program Files and the service runs.
        Assert.Equal(3, Run(pan, "--install", "--expect-hash", new string('0', 64)));
        Assert.False(File.Exists(PanPaths.InstalledExecutable), "nothing installed with a wrong hash");
        Assert.Equal(0, Run(pan, "--install", "--expect-hash", hash));
        Assert.True(File.Exists(PanPaths.InstalledExecutable), "Pan.exe is in Program Files");
        using (var service = new ServiceController(PanPaths.ServiceName))
        {
            service.WaitForStatus(ServiceControllerStatus.Running, ReplyTimeout);
        }

        // T243-7, update: a rejected update leaves the installed Pan running and unchanged (review point 7).
        Assert.Equal(3, Run(pan, "--install", "--expect-hash", new string('0', 64)));
        Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(PanPaths.InstalledExecutable))));
        Assert.False(File.Exists(PanPaths.InstalledExecutable + ".new"), "no staged copy left");
        using (var service = new ServiceController(PanPaths.ServiceName))
        {
            Assert.Equal(ServiceControllerStatus.Running, service.Status);
        }

        // T243-8: a user can start and query it but never change it; the image path is quoted.
        // sc prints the rights in its own order (LCRPLORC), so the ACE is compared as a set of rights. Only the DACL
        // (D:) counts: Windows adds an audit entry for Everyone to the SACL (S:) of every service.
        var sddl = Output("sc.exe", "sdshow", PanPaths.ServiceName);
        var dacl = sddl.IndexOf("S:", StringComparison.Ordinal) is var sacl and >= 0 ? sddl[..sacl] : sddl;
        Assert.Equal(["LC", "LO", "RC", "RP"], RightsOf(dacl, "IU"));
        Assert.DoesNotContain(";;;WD)", dacl);
        Assert.DoesNotContain(";;;BU)", dacl);
        Assert.DoesNotContain(";;;AU)", dacl);
        using (var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{PanPaths.ServiceName}"))
        {
            Assert.Equal($"\"{PanPaths.InstalledExecutable}\"", key?.GetValue("ImagePath"));
            Assert.Equal(ServiceDescriptions.ForSystem(), key?.GetValue("Description"));
        }

        // T243-9: the pipe takes the interactive user and SYSTEM, nobody else; hello answers with the generation.
        uint generation;
        await using (var pipe = await ConnectAsync())
        {
            var rules = pipe.GetAccessControl().GetAccessRules(true, false, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();
            Assert.Contains(rules, r => r.IdentityReference == new SecurityIdentifier(WellKnownSidType.InteractiveSid, null) && r.PipeAccessRights.HasFlag(PipeAccessRights.ReadWrite));
            Assert.DoesNotContain(rules, r => r.IdentityReference == new SecurityIdentifier(WellKnownSidType.WorldSid, null));
            Assert.DoesNotContain(rules, r => r.IdentityReference == new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null));

            var hello = await ExchangeAsync(pipe, PanMessage.Of(PanCommand.Hello));
            Assert.Equal(PanReply.HelloOk, hello.Reply);
            generation = hello.Generation;

            // T243-10: stopping the service is the teardown: the forwarding process Arcadia started is ended.
            var viiper = StartViiper();
            var started = await ExchangeAsync(pipe, PanMessage.Of(PanCommand.EmulationStarted));
            Assert.Equal(PanReply.Started, started.Reply);
            Assert.True(started.Generation > generation);
            using (var service = new ServiceController(PanPaths.ServiceName))
            {
                service.Stop();

                // The client hears why before the pipe closes, so Arcadia can say "Pan wurde beendet" (T243-4).
                var notice = await ReadAsync(pipe, TeardownTimeout);
                Assert.Equal(PanReply.TornDown, notice.Reply);
                Assert.Equal(PanReason.ServiceStopping, notice.Reason);
                Assert.Equal(started.Generation, notice.Generation);
                service.WaitForStatus(ServiceControllerStatus.Stopped, TeardownTimeout);
            }

            Assert.True(viiper.WaitForExit(TeardownTimeout), "stop ended viiper.exe");
        }

        // T243-11: a client that stops its heartbeats for 3 s has hung: Pan ends the forwarding process and says why.
        using (var service = new ServiceController(PanPaths.ServiceName))
        {
            service.Start();
            service.WaitForStatus(ServiceControllerStatus.Running, ReplyTimeout);
        }

        await using (var pipe = await ConnectAsync())
        {
            var hello = await ExchangeAsync(pipe, PanMessage.Of(PanCommand.Hello));
            Assert.Equal(PanReply.HelloOk, hello.Reply);
            var viiper = StartViiper();
            var started = await ExchangeAsync(pipe, PanMessage.Of(PanCommand.EmulationStarted));
            Assert.Equal(PanReply.Started, started.Reply);

            // Part B: the real service reaches HidHide. With HidHide on the runner it hides (no puck there: nothing to
            // hide, but the test host is allow-listed and cloaking turned on) and the hang's teardown restores HidHide.
            // A HidHide the test may not change (a developer's PC, or a CI setup that timed out but still installed it)
            // is only asked about.
            var hidHideBefore = HidHideTests.Enabled ? HidHideTests.Snapshot() : null;
            var check = await ExchangeAsync(pipe, PanMessage.Of(PanCommand.CheckHiding));
            if (hidHideBefore is null && new HidHideDriver().IsServiceRegistered())
            {
                Assert.Equal(PanReply.HidingState, check.Reply);
            }
            else if (hidHideBefore is null)
            {
                var hide = await ExchangeAsync(pipe, PanMessage.Of(PanCommand.Hide, started.Generation));
                Assert.Equal((PanReply.HidingState, PanReason.HidHideMissing), (check.Reply, check.Reason));
                Assert.Equal((PanReply.Rejected, PanReason.HidHideMissing), (hide.Reply, hide.Reason));
            }
            else
            {
                var hide = await ExchangeAsync(pipe, PanMessage.Of(PanCommand.Hide, started.Generation));
                Assert.Equal((PanReply.HidingState, PanReason.None), (check.Reply, check.Reason));
                Assert.Equal(PanReply.Hidden, hide.Reply);
                Assert.True(new HidHideDriver().GetCloaking(), "Pan turned cloaking on");
            }

            var notice = await ReadAsync(pipe, ReplyTimeout);
            Assert.Equal(PanReply.TornDown, notice.Reply);
            Assert.Equal(PanReason.Hang, notice.Reason);
            Assert.Equal(started.Generation, notice.Generation);
            Assert.True(viiper.WaitForExit(TeardownTimeout), "the hang ended viiper.exe");
            if (hidHideBefore is not null)
            {
                Assert.Equal(hidHideBefore, HidHideTests.Snapshot());
            }

            var acknowledged = await ExchangeAsync(pipe, PanMessage.Of(PanCommand.TeardownAcknowledged, started.Generation));
            Assert.Equal(PanReply.Acknowledged, acknowledged.Reply);
        }

        // T243-12: uninstall removes the service and Pan's files.
        Assert.Equal(0, Run(pan, "--uninstall"));
        Assert.Contains("1060", Output("sc.exe", "query", PanPaths.ServiceName));
        Assert.False(Directory.Exists(PanPaths.InstallDirectory), "Program Files\\Arcadia\\Pan is gone");
    }

    /// <summary>The two-letter rights of the allow ACE for <paramref name="sid"/> in an SDDL string, sorted.</summary>
    private static string[] RightsOf(string sddl, string sid)
    {
        var match = System.Text.RegularExpressions.Regex.Match(sddl, $@"\(A;;([A-Z]+);;;{sid}\)");
        Assert.True(match.Success, $"no allow ACE for {sid} in {sddl.Trim()}");
        var rights = match.Groups[1].Value;
        return Enumerable.Range(0, rights.Length / 2).Select(i => rights.Substring(i * 2, 2)).Order().ToArray();
    }

    /// <summary>
    /// A single-file Pan.exe, as the service needs it (the test output's Pan.exe is an apphost that needs Pan.dll beside
    /// it). Not AOT: that takes minutes and the pack step of the CI compiles it anyway.
    /// </summary>
    private async Task<string> PublishPanAsync()
    {
        var root = RepositoryRoot();
        var output = Path.Combine(_work, "publish");
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[]
        {
            "publish", Path.Combine(root, "src", "Arcadia.Pan", "Arcadia.Pan.csproj"), "-c", "Release", "-r", "win-x64", "--self-contained",
            "-p:PublishAot=false", "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true", "-o", output, "-nologo", "-v", "q",
        })
        {
            info.ArgumentList.Add(argument);
        }

        using var publish = Process.Start(info)!;
        var stdout = publish.StandardOutput.ReadToEndAsync();
        var stderr = publish.StandardError.ReadToEndAsync();
        await publish.WaitForExitAsync();
        Assert.True(publish.ExitCode == 0, $"dotnet publish failed:\n{await stdout}\n{await stderr}");
        var pan = Path.Combine(output, PanPaths.ExecutableName);
        Assert.True(File.Exists(pan), $"{pan} published");
        return pan;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Arcadia.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Arcadia.slnx not found above the test output");
    }

    /// <summary>A copy of ping.exe named like VIIPER, pinging for a few minutes: the child Pan must end.</summary>
    private Process StartViiper()
    {
        Directory.CreateDirectory(_work);
        var viiper = Path.Combine(_work, Guardian.ForwardingImageName);
        File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), viiper, overwrite: true);
        var process = Process.Start(new ProcessStartInfo(viiper, "-n 300 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
        _children.Add(process);
        return process;
    }

    private static async Task<NamedPipeClientStream> ConnectAsync()
    {
        var pipe = new NamedPipeClientStream(".", PanPaths.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync((int)ReplyTimeout.TotalMilliseconds);
        return pipe;
    }

    private static async Task<PanMessage> ExchangeAsync(NamedPipeClientStream pipe, PanMessage message)
    {
        await pipe.WriteAsync(message.ToBytes());
        await pipe.FlushAsync();
        return await ReadAsync(pipe, ReplyTimeout);
    }

    private static async Task<PanMessage> ReadAsync(NamedPipeClientStream pipe, TimeSpan timeout)
    {
        using var limit = new CancellationTokenSource(timeout);
        var buffer = new byte[PanMessage.Size];
        await pipe.ReadExactlyAsync(buffer, limit.Token);
        return PanMessage.Read(buffer);
    }

    private static int Run(string exe, params string[] arguments)
    {
        var info = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)!;
        Assert.True(process.WaitForExit(TimeSpan.FromMinutes(2)), $"{exe} {string.Join(' ', arguments)} finished");
        return process.ExitCode;
    }

    private static string Output(string exe, params string[] arguments)
    {
        var info = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
