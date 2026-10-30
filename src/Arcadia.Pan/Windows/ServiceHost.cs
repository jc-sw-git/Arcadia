using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arcadia.Pan.Windows;

/// <summary>
/// Pan as a Windows service, through the service control manager's own API (no framework between): start, stop,
/// pre-shutdown and power events reach <see cref="Guardian"/>. A stop runs the full teardown (review point 4);
/// resume restarts the hang watch (review point 6).
/// </summary>
[SupportedOSPlatform("windows")]
public static class ServiceHost
{
    /// <summary>With no client this long after the last one left (or after start), Pan stops: no footprint without Arcadia.</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// What a teardown may take at most, so Windows waits for it at start (a journal's recovery) and stop: its step
    /// budgets add up to 70 s (ports 10, processes 10, show 30, restart 20) when the PnP lock hangs.
    /// </summary>
    public static readonly TimeSpan TeardownWait = TimeSpan.FromSeconds(80);

    private static nint _statusHandle;
    private static Native.ServiceStatus _status;
    private static readonly CancellationTokenSource Stopping = new();
    private static PanLog? _log;
    private static Guardian? _guardian;

    /// <summary>Hands the process to the service control manager; returns false when not started by it (a console).</summary>
    public static unsafe bool TryRun(PanLog log)
    {
        _log = log;
        fixed (char* name = PanPaths.ServiceName)
        {
            var table = stackalloc Native.ServiceTableEntry[2];
            table[0] = new Native.ServiceTableEntry { Name = name, Main = &ServiceMain };
            table[1] = default;
            if (Native.StartServiceCtrlDispatcherW(table))
            {
                return true;
            }

            var error = Marshal.GetLastPInvokeError();
            if (error == Native.ErrorFailedServiceControllerConnect)
            {
                return false;
            }

            log.Write($"Service dispatcher failed: {Marshal.GetPInvokeErrorMessage(error)}");
            return true;
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void ServiceMain(uint argc, char** argv)
    {
        var log = _log!;
        _statusHandle = Native.RegisterServiceCtrlHandlerExW(PanPaths.ServiceName, &Handler, null);
        if (_statusHandle == 0)
        {
            log.Write($"Service handler not registered: {Native.LastErrorText()}");
            return;
        }

        _status = new Native.ServiceStatus { ServiceType = Native.ServiceWin32OwnProcess };
        // The recovery of a journal runs a teardown before Running is reported.
        Report(Native.ServiceStartPending, waitHintMs: (uint)TeardownWait.TotalMilliseconds);
        try
        {
            RunAsync(log).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // A failure is reported as one, so the recovery actions restart Pan (review, should-fix 6).
            log.Write($"Service failed: {ex}");
            _status.Win32ExitCode = ErrorServiceSpecificError;
            _status.ServiceSpecificExitCode = 1;
        }

        Report(Native.ServiceStopped);
    }

    private const uint ErrorServiceSpecificError = 1066;
    private static readonly TimeSpan NoticeGrace = TimeSpan.FromSeconds(2);

    /// <summary>Also used by <c>--console</c>, where the console's Ctrl+C is the stop.</summary>
    public static async Task RunAsync(PanLog log, CancellationToken? consoleStop = null)
    {
        // The journal and log live there; a folder somebody else owns or may write could point them anywhere, or feed
        // Pan a journal to undo (review, bug 1). Pan makes it anew itself instead of refusing to run (owner,
        // 2026-10-27: Explorer's "Fortsetzen" had given the user full control); what was there is set aside unread.
        IReadOnlyList<string> setAside = [];
        if (ServiceSetup.CheckDataDirectory() is { } problem)
        {
            try
            {
                setAside = ServiceSetup.SecureDataDirectory();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // The log file stays untouched: the line reaches only a console, the SCM sees the exit code.
                log.Write($"Refusing to run: {problem}, and it could not be made anew: {ex.Message}; run Pan.exe --install again");
                throw new InvalidOperationException(problem, ex);
            }
        }

        log.TrustFolder();
        foreach (var folder in setAside)
        {
            log.Write($"Data folder not trusted: {folder}; made anew, its journal not used");
        }

        var cleanup = new WindowsCleanup(log.Write);
        var guardian = new Guardian(new UnbiasedClock(), new JournalFile(PanPaths.JournalPath), cleanup, log.Write, hiding: WindowsHiding.Create())
        {
            // Steam in any session: a firmware hold keeps the Steam Controller hidden while it runs (#242).
            SteamRuns = SteamRuns,
            // The vendors' firmware tools: no controller of theirs is hidden while one runs (#238 D8).
            ProcessRuns = ProcessRuns,
        };
        _guardian = guardian;
        guardian.UnplannedEnd += end => log.Write($"Unplanned end: {end.Reason}, generation {end.Generation}, client {end.ClientPid}, emulating {end.WasEmulating} (crash report: #244)");
        log.Write($"Pan {typeof(ServiceHost).Assembly.GetName().Version} starting (protocol {PanMessage.ProtocolVersion})");
        await guardian.RecoverAsync();
        Report(Native.ServiceRunning);
        // A puck or taken-over controller plugged in while Pan hides is hidden too (part B, #335); without the watch, at the next hide.
        using var arrivals = DeviceArrivalWatch.Start(guardian.DevicesArrivedAsync, guardian.ControllerArrivedAsync, log.Write);

        using var stop = consoleStop is { } console ? CancellationTokenSource.CreateLinkedTokenSource(Stopping.Token, console) : CancellationTokenSource.CreateLinkedTokenSource(Stopping.Token);
        using var serve = new CancellationTokenSource();
        var server = new PipeServer(guardian, log.Write);
        using var idle = new IdleStop(IdleTimeout, () => guardian.ShowRetryPending, stop.Cancel, log.Write);
        server.ClientPresent += idle.ClientPresent;
        idle.ClientPresent(false);
        var serving = server.RunAsync(serve.Token);
        var watch = WatchAsync(guardian, serve.Token);
        var stopRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (stop.Token.Register(() => stopRequested.TrySetResult()))
        {
            await Task.WhenAny(serving, stopRequested.Task);
        }

        // The stop's teardown runs while the client's pipe is still open, so its notice (TornDown, ServiceStopping)
        // reaches Arcadia before the pipe goes (review, bug 3); only then the server is cancelled.
        await guardian.StopAsync();
        await server.FlushNoticesAsync(NoticeGrace);
        serve.Cancel();
        try
        {
            await watch;
        }
        catch (OperationCanceledException)
        {
        }

        try
        {
            // A server that ended on its own (the pipe's name taken) rethrows here: the service fails visibly.
            await serving;
        }
        catch (OperationCanceledException)
        {
        }

        log.Write("Pan stopped");
    }

    /// <summary>The hang watch: ticks at the heartbeat's pace; the guardian ignores ticks while nothing is emulated.</summary>
    private static async Task WatchAsync(Guardian guardian, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(Guardian.HeartbeatInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            guardian.Tick();
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe uint Handler(uint control, uint eventType, void* eventData, void* context)
    {
        switch (control)
        {
            case Native.ServiceControlStop:
            case Native.ServiceControlShutdown:
            case Native.ServiceControlPreshutdown:
                _log?.Write($"Service control {control}: stopping");
                Report(Native.ServiceStopPending, waitHintMs: (uint)TeardownWait.TotalMilliseconds);
                Stopping.Cancel();
                return Native.NoError;
            case Native.ServiceControlInterrogate:
                Report(_status.CurrentState);
                return Native.NoError;
            case Native.ServiceControlPowerEvent:
                if (eventType is Native.PbtApmResumeAutomatic or Native.PbtApmResumeSuspend)
                {
                    _guardian?.PowerResumed();
                }

                return Native.NoError;
            default:
                return Native.ErrorCallNotImplemented;
        }
    }

    private static void Report(uint state, uint waitHintMs = 0)
    {
        if (_statusHandle == 0)
        {
            return;
        }

        _status.CurrentState = state;
        _status.WaitHint = waitHintMs;
        _status.ControlsAccepted = state is Native.ServiceStartPending or Native.ServiceStopPending
            ? 0
            : Native.ServiceAcceptStop | Native.ServiceAcceptShutdown | Native.ServiceAcceptPreshutdown | Native.ServiceAcceptPowerEvent;
        _status.CheckPoint = state is Native.ServiceStartPending or Native.ServiceStopPending ? ++_status.CheckPoint : 0;
        Native.SetServiceStatus(_statusHandle, ref _status);
    }

    private static bool SteamRuns() => ProcessRuns("steam");

    private static bool ProcessRuns(string name)
    {
        var processes = System.Diagnostics.Process.GetProcessesByName(name);
        foreach (var process in processes)
        {
            process.Dispose();
        }

        return processes.Length > 0;
    }
}
