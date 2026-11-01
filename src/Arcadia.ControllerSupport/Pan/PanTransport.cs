using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.ServiceProcess;
using Arcadia.Pan;

namespace Arcadia.ControllerSupport.Pan;

/// <summary>A connection to Pan: the stream, and who is on the other end for the log.</summary>
public sealed record PanLink(Stream Stream, string Description) : IDisposable
{
    public void Dispose() => Stream.Dispose();
}

/// <summary>The connection can't be made for a reason the user may need to see (Pan not set up, outdated, squatted).</summary>
public sealed class PanUnavailableException(string message, bool permanent = false) : InvalidOperationException(message)
{
    /// <summary>True when retrying soon is pointless (not installed, wrong protocol): the next try waits longer.</summary>
    public bool Permanent { get; } = permanent;
}

public enum PanServiceStartStep
{
    Wait,
    Start,
    Running,

    /// <summary>Started by Arcadia and stopped again: Pan ends at once, and starting it again every 2 s doesn't help.</summary>
    EndedAtOnce,

    /// <summary>Arcadia is to leave Pan alone now (its update began while this start waited): never started (#354).</summary>
    LeftAlone,
}

/// <summary>
/// What Arcadia does with Pan's service state while it waits for it to run. Pan stops itself 30 s after its last
/// client; Arcadia coming back in that moment found it "StopPending", waited without starting it, and gave up after
/// 10 s with "Pan startet nicht (Stopped)" (owner's B2 test, 2026-10-27). A stopped service is started once.
/// </summary>
public static class PanServiceStart
{
    /// <param name="running">The service runs.</param>
    /// <param name="stopped">The service is stopped (not pending either way).</param>
    /// <param name="leftAlone">
    /// Arcadia is to leave Pan alone: Pan's <c>--install</c> may have stopped the service and waits for it to stay stopped.
    /// </param>
    public static PanServiceStartStep Next(bool running, bool stopped, bool startRequested, bool leftAlone = false) =>
        leftAlone ? PanServiceStartStep.LeftAlone
        : running ? PanServiceStartStep.Running
        : stopped ? startRequested ? PanServiceStartStep.EndedAtOnce : PanServiceStartStep.Start
        : PanServiceStartStep.Wait;

    /// <summary>Stopped with a Win32 exit code: Pan reports a failure as 1066 (service-specific), a crash shows as 1067.</summary>
    public static bool Failed(bool stopped, uint win32ExitCode) => stopped && win32ExitCode != 0;
}

/// <summary>Opens the connection to Pan; replaced in tests by a stream pair.</summary>
public interface IPanTransport
{
    /// <summary>
    /// Why a connection is pointless right now (Pan not set up), without trying: the keeper then waits instead of
    /// throwing and catching at every retry. Null means try.
    /// </summary>
    (string Reason, bool Permanent)? Unavailable() => null;

    /// <exception cref="PanUnavailableException">Pan can't be reached and the message says why.</exception>
    Task<PanLink> ConnectAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The pipe <c>\\.\pipe\ArcadiaPan</c> on Windows (#243, review point 1): starts the service if it isn't running
/// (the DACL lets the interactive user do that), connects, and trusts the pipe only if its server is the process
/// the service manager reports for <c>ArcadiaPan</c>; anything else is a squatter and refused.
/// </summary>
[SupportedOSPlatform("windows")]
/// <param name="whyLeftAlone">Why Arcadia is to leave Pan alone right now, null if not; asked again before each service start.</param>
public sealed partial class PanPipeTransport(Action<string> log, Func<string?>? whyLeftAlone = null) : IPanTransport
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ServiceStartTimeout = TimeSpan.FromSeconds(10);

    public static string PipePath => $@"\\.\pipe\{PanPaths.PipeName}";

    private const string NotInstalled = "Pan ist nicht eingerichtet";

    public const string EndsAtOnce = "Pan beendet sich gleich nach dem Start";

    public (string Reason, bool Permanent)? Unavailable() => ServiceExists() ? null : (NotInstalled, true);

    public async Task<PanLink> ConnectAsync(CancellationToken cancellationToken)
    {
        using var service = OpenService();
        if (service is null)
        {
            throw new PanUnavailableException(NotInstalled, permanent: true);
        }

        if (service.Status != ServiceControllerStatus.Running)
        {
            await StartAsync(service, cancellationToken);
        }

        var pipe = new NamedPipeClientStream(".", PanPaths.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ConnectTimeout);
            await pipe.ConnectAsync(timeout.Token);
            pipe.ReadMode = PipeTransmissionMode.Byte;

            var servicePid = ProcessIdOf();
            if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out var serverPid) || serverPid != servicePid)
            {
                var message = $"{PipePath} gehört nicht dem Dienst {PanPaths.ServiceName} (Prozess {serverPid} statt {servicePid})";
                log($"Pan: {message} – ein anderes Programm besetzt die Pipe");
                throw new PanUnavailableException(message, permanent: true);
            }

            return new PanLink(pipe, $"Dienst {PanPaths.ServiceName}, Prozess {servicePid}");
        }
        catch
        {
            pipe.Dispose();
            throw;
        }
    }

    private async Task StartAsync(ServiceController service, CancellationToken cancellationToken)
    {
        try
        {
            var startRequested = false;
            var until = DateTime.UtcNow + ServiceStartTimeout;
            while (true)
            {
                var status = service.Status;
                var leftAlone = whyLeftAlone?.Invoke();
                switch (PanServiceStart.Next(status == ServiceControllerStatus.Running, status == ServiceControllerStatus.Stopped, startRequested, leftAlone is not null))
                {
                    case PanServiceStartStep.LeftAlone:
                        throw new PanUnavailableException(leftAlone!);
                    case PanServiceStartStep.Running:
                        return;
                    case PanServiceStartStep.Start:
                        log($"Pan: Dienst {PanPaths.ServiceName} wird gestartet");
                        service.Start();
                        startRequested = true;
                        break;
                    case PanServiceStartStep.EndedAtOnce:
                        throw new PanUnavailableException(EndsAtOnce, permanent: true);
                }

                if (DateTime.UtcNow > until)
                {
                    throw new PanUnavailableException($"Pan startet nicht ({service.Status})");
                }

                await Task.Delay(100, cancellationToken);
                service.Refresh();
            }
        }
        catch (Exception ex) when (ex is (InvalidOperationException and not PanUnavailableException) or Win32Exception)
        {
            throw new PanUnavailableException($"Pan lässt sich nicht starten: {ex.Message}");
        }
    }

    /// <summary>Whether the service is registered, asked without an exception (the debugger showed one per retry).</summary>
    public static bool ServiceExists()
    {
        var manager = OpenSCManagerW(null, null, ScManagerConnect);
        if (manager == 0)
        {
            return false;
        }

        try
        {
            var service = OpenServiceW(manager, PanPaths.ServiceName, ServiceQueryStatus);
            if (service == 0)
            {
                return false;
            }

            CloseServiceHandle(service);
            return true;
        }
        finally
        {
            CloseServiceHandle(manager);
        }
    }

    /// <summary>The service is in any state but stopped (#326): an old Pan left alone ends itself once it gave everything back.</summary>
    public static bool IsServiceRunning()
    {
        using var service = OpenService();
        try
        {
            return service is not null && service.Status != ServiceControllerStatus.Stopped;
        }
        catch (InvalidOperationException)
        {
            // Removed meanwhile.
            return false;
        }
    }

    private static ServiceController? OpenService()
    {
        if (!ServiceExists())
        {
            return null;
        }

        var service = new ServiceController(PanPaths.ServiceName);
        try
        {
            _ = service.Status;
            return service;
        }
        catch (InvalidOperationException)
        {
            service.Dispose();
            return null;
        }
    }

    private const uint ScManagerConnect = 0x0001;
    private const uint ServiceQueryStatus = 0x0004;
    private const uint ScStatusProcessInfo = 0;

    /// <summary>
    /// The service's process id, opened with <c>SERVICE_QUERY_STATUS</c> only: that is what Pan's DACL gives users, and
    /// <c>ServiceController.ServiceHandle</c> asks for all access, which an unelevated Arcadia never gets (review, bug 2).
    /// </summary>
    private static uint ProcessIdOf() => QueryStatus() is { } status ? status.ProcessId : throw new Win32Exception();

    /// <summary>
    /// Pan's service is stopped with an error from its last run: it refused to start (its data folder not trusted,
    /// owner's B2 test 2026-10-27) or crashed. An idle stop ends without one. Read with <c>SERVICE_QUERY_STATUS</c>.
    /// </summary>
    public static bool LastRunFailed() =>
        QueryStatus() is { } status && PanServiceStart.Failed(status.CurrentState == ServiceStopped, status.Win32ExitCode);

    private const uint ServiceStopped = 1;

    private static unsafe ServiceStatusProcess? QueryStatus()
    {
        var manager = OpenSCManagerW(null, null, ScManagerConnect);
        if (manager == 0)
        {
            return null;
        }

        try
        {
            var service = OpenServiceW(manager, PanPaths.ServiceName, ServiceQueryStatus);
            if (service == 0)
            {
                return null;
            }

            try
            {
                var info = new ServiceStatusProcess();
                return QueryServiceStatusEx(service, ScStatusProcessInfo, &info, (uint)sizeof(ServiceStatusProcess), out _) ? info : null;
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(manager);
        }
    }

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint OpenSCManagerW(string? machine, string? database, uint access);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint OpenServiceW(nint manager, string name, uint access);

    [LibraryImport("advapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseServiceHandle(nint handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatusProcess
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
        public uint ProcessId;
        public uint ServiceFlags;
    }

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool QueryServiceStatusEx(nint service, uint infoLevel, void* buffer, uint size, out uint needed);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetNamedPipeServerProcessId(nint pipe, out uint serverProcessId);
}

/// <summary>Where there is no Pan (not Windows): says so, and the client tries only slowly.</summary>
public sealed class NoPanTransport : IPanTransport
{
    public Task<PanLink> ConnectAsync(CancellationToken cancellationToken) =>
        throw new PanUnavailableException("Pan gibt es nur unter Windows", permanent: true);
}
