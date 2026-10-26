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

/// <summary>Opens the connection to Pan; replaced in tests by a stream pair.</summary>
public interface IPanTransport
{
    /// <exception cref="PanUnavailableException">Pan can't be reached and the message says why.</exception>
    Task<PanLink> ConnectAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The pipe <c>\\.\pipe\ArcadiaPan</c> on Windows (#243, review point 1): starts the service if it isn't running
/// (the DACL lets the interactive user do that), connects, and trusts the pipe only if its server is the process
/// the service manager reports for <c>ArcadiaPan</c>; anything else is a squatter and refused.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class PanPipeTransport(Action<string> log) : IPanTransport
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ServiceStartTimeout = TimeSpan.FromSeconds(10);

    public static string PipePath => $@"\\.\pipe\{PanPaths.PipeName}";

    public async Task<PanLink> ConnectAsync(CancellationToken cancellationToken)
    {
        using var service = OpenService();
        if (service is null)
        {
            throw new PanUnavailableException("Pan ist nicht eingerichtet", permanent: true);
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

            var servicePid = ProcessIdOf(service);
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
            if (service.Status == ServiceControllerStatus.Stopped)
            {
                log($"Pan: Dienst {PanPaths.ServiceName} wird gestartet");
                service.Start();
            }

            var until = DateTime.UtcNow + ServiceStartTimeout;
            service.Refresh();
            while (service.Status != ServiceControllerStatus.Running)
            {
                if (DateTime.UtcNow > until)
                {
                    throw new PanUnavailableException($"Pan startet nicht ({service.Status})");
                }

                await Task.Delay(100, cancellationToken);
                service.Refresh();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            throw new PanUnavailableException($"Pan lässt sich nicht starten: {ex.Message}");
        }
    }

    private static ServiceController? OpenService()
    {
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

    private static unsafe uint ProcessIdOf(ServiceController service)
    {
        var info = new ServiceStatusProcess();
        if (!QueryServiceStatusEx(service.ServiceHandle.DangerousGetHandle(), 0, &info, (uint)sizeof(ServiceStatusProcess), out _))
        {
            throw new Win32Exception();
        }

        return info.ProcessId;
    }

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
