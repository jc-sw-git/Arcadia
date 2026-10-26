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
        Report(Native.ServiceStartPending, waitHintMs: 5000);
        try
        {
            RunAsync(log).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            log.Write($"Service failed: {ex}");
        }

        Report(Native.ServiceStopped);
    }

    /// <summary>Also used by <c>--console</c>, where the console's Ctrl+C is the stop.</summary>
    public static async Task RunAsync(PanLog log, CancellationToken? consoleStop = null)
    {
        var cleanup = new WindowsCleanup(log.Write);
        var guardian = new Guardian(new UnbiasedClock(), new JournalFile(PanPaths.JournalPath), cleanup, log.Write);
        _guardian = guardian;
        guardian.UnplannedEnd += end => log.Write($"Unplanned end: {end.Reason}, generation {end.Generation}, client {end.ClientPid}, emulating {end.WasEmulating} (crash report: #244)");
        log.Write($"Pan {typeof(ServiceHost).Assembly.GetName().Version} starting (protocol {PanMessage.ProtocolVersion})");
        await guardian.RecoverAsync();
        Report(Native.ServiceRunning);

        using var stop = consoleStop is { } console ? CancellationTokenSource.CreateLinkedTokenSource(Stopping.Token, console) : CancellationTokenSource.CreateLinkedTokenSource(Stopping.Token);
        var server = new PipeServer(guardian, log.Write);
        var idle = new IdleStop(stop, log.Write);
        server.ClientPresent += idle.ClientPresent;
        idle.ClientPresent(false);
        var serving = server.RunAsync(stop.Token);
        var watch = WatchAsync(guardian, stop.Token);
        try
        {
            await serving;
        }
        catch (OperationCanceledException)
        {
        }

        await guardian.StopAsync();
        try
        {
            await watch;
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
                Report(Native.ServiceStopPending, waitHintMs: 30_000);
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

    /// <summary>Stops Pan after <see cref="IdleTimeout"/> without a client; a client that comes meanwhile cancels it.</summary>
    private sealed class IdleStop(CancellationTokenSource stop, Action<string> log)
    {
        private CancellationTokenSource? _countdown;

        public void ClientPresent(bool present)
        {
            lock (this)
            {
                _countdown?.Cancel();
                _countdown?.Dispose();
                _countdown = null;
                if (present)
                {
                    return;
                }

                var countdown = new CancellationTokenSource();
                _countdown = countdown;
                Task.Delay(IdleTimeout, countdown.Token).ContinueWith(_ =>
                {
                    log($"No client for {IdleTimeout.TotalSeconds:0} s: stopping");
                    stop.Cancel();
                }, TaskContinuationOptions.OnlyOnRanToCompletion);
            }
        }
    }
}
