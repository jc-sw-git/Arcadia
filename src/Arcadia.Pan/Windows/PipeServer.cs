using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace Arcadia.Pan.Windows;

/// <summary>
/// The pipe <c>\\.\pipe\ArcadiaPan</c> (#243, review points 1 and 2): created as the first instance, so a process
/// that squatted the name makes Pan fail loudly instead of serving beside it; one instance is always open, so the
/// name is never free while Pan runs; ACL for SYSTEM and the interactive user. The client is known from the pipe
/// and its process (id, start time, session), never from a message, and Pan waits on its process handle to learn
/// of its end without polling. Connections are served side by side, so a second client is refused at once instead of
/// waiting behind the first (review, should-fix 10).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PipeServer(Guardian guardian, Action<string> log)
{
    private static readonly TimeSpan SquatRetry = TimeSpan.FromSeconds(5);
    private const int MaxInstances = 4;
    private readonly List<Task> _notices = [];
    private readonly List<Task> _connections = [];

    /// <summary>A client connected or left; the host uses it to stop Pan when nobody needs it.</summary>
    public event Action<bool>? ClientPresent;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        NamedPipeServerStream? listening = null;
        var first = true;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                listening ??= Create(first);
                first = false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Another process holds the name (FirstPipeInstance then fails with access denied): don't serve beside it.
                log($"Pipe not created ({ex.Message}); is another program squatting \\\\.\\pipe\\{PanPaths.PipeName}? Retrying in {SquatRetry.TotalSeconds:0} s");
                await Task.Delay(SquatRetry, cancellationToken);
                continue;
            }

            var pipe = listening;
            listening = null;
            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                pipe.Dispose();
                break;
            }
            catch (IOException ex)
            {
                log($"Pipe connection failed: {ex.Message}");
                pipe.Dispose();
                continue;
            }

            // The next instance is open before this connection is served, so the name stays ours.
            try
            {
                listening = Create(first: false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log($"Next pipe instance not created: {ex.Message}");
            }

            lock (_connections)
            {
                _connections.RemoveAll(c => c.IsCompleted);
                _connections.Add(ServeGuardedAsync(pipe, cancellationToken));
            }
        }

        listening?.Dispose();
        Task[] open;
        lock (_connections)
        {
            open = [.. _connections];
        }

        await Task.WhenAll(open);
    }

    /// <summary>Notices started so far have been written, or the grace period is over; for the stop.</summary>
    public async Task FlushNoticesAsync(TimeSpan grace)
    {
        Task[] pending;
        lock (_notices)
        {
            _notices.RemoveAll(n => n.IsCompleted);
            pending = [.. _notices];
        }

        if (pending.Length == 0)
        {
            return;
        }

        try
        {
            await Task.WhenAll(pending).WaitAsync(grace);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or OperationCanceledException or ObjectDisposedException)
        {
            log($"Notice not delivered before the stop: {ex.Message}");
        }
    }

    private async Task ServeGuardedAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        try
        {
            await ServeAsync(pipe, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
            log($"Connection ended: {ex.Message}");
        }
        catch (Exception ex)
        {
            log($"Connection failed: {ex}");
        }
    }

    private static NamedPipeServerStream Create(bool first)
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.InteractiveSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
        var options = PipeOptions.Asynchronous | PipeOptions.WriteThrough | (first ? PipeOptions.FirstPipeInstance : PipeOptions.None);
        return NamedPipeServerStreamAcl.Create(PanPaths.PipeName, PipeDirection.InOut, MaxInstances, PipeTransmissionMode.Byte, options, PanMessage.Size * 16, PanMessage.Size * 16, security);
    }

    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        using (pipe)
        {
            if (!Native.GetNamedPipeClientProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out var pid))
            {
                log($"Client's process id not readable: {Native.LastErrorText()}");
                return;
            }

            var process = Native.OpenProcess(Native.ProcessQueryLimitedInformation | Native.Synchronize, false, pid);
            if (process == 0)
            {
                log($"Client {pid} not opened: {Native.LastErrorText()}");
                return;
            }

            using var processHandle = new SafeProcessHandle(process, ownsHandle: true);
            var startTime = WindowsCleanup.StartTimeOf(process) ?? 0;
            var inConsole = Native.ProcessIdToSessionId(pid, out var session) && session == Native.WTSGetActiveConsoleSessionId();
            // Per connection: a notice stuck on a client that doesn't read must not hold up another one's reply.
            var writes = new SemaphoreSlim(1, 1);
            var refused = guardian.Register(new ClientIdentity((int)pid, startTime, inConsole));
            if (refused != PanReason.None)
            {
                await WriteAsync(pipe, writes, PanMessage.Of(PanReply.Refused, reason: refused), cancellationToken);
                return;
            }

            ClientPresent?.Invoke(true);
            using var gone = new CancellationTokenSource();
            using var processWait = new ProcessWaitHandle(processHandle);
            // When Arcadia dies, its pipe breaks before its process object is signalled, so this callback can run
            // after the connection ended and `gone` is disposed; a throw here would end Pan (review, point 1).
            var registration = ThreadPool.RegisterWaitForSingleObject(processWait, (_, _) =>
            {
                try
                {
                    gone.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }, null, -1, executeOnlyOnce: true);
            void Notify(PanMessage message)
            {
                var write = WriteAsync(pipe, writes, message, cancellationToken);
                lock (_notices)
                {
                    _notices.RemoveAll(n => n.IsCompleted);
                    _notices.Add(write);
                }

                write.ContinueWith(t => log($"Notice not sent: {t.Exception?.GetBaseException().Message}"), TaskContinuationOptions.OnlyOnFaulted);
            }

            guardian.Notify += Notify;
            try
            {
                using var either = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, gone.Token);
                await ReadLoopAsync(pipe, writes, either.Token);
            }
            finally
            {
                guardian.Notify -= Notify;
                registration.Unregister(null);
                guardian.ClientGone();
                ClientPresent?.Invoke(false);
            }
        }
    }

    private async Task ReadLoopAsync(NamedPipeServerStream pipe, SemaphoreSlim writes, CancellationToken cancellationToken)
    {
        var buffer = new byte[PanMessage.Size];
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = 0;
            try
            {
                while (read < buffer.Length)
                {
                    var n = await pipe.ReadAsync(buffer.AsMemory(read), cancellationToken);
                    if (n == 0)
                    {
                        return;
                    }

                    read += n;
                }
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
            {
                return;
            }

            var reply = guardian.Handle(PanMessage.Read(buffer));
            try
            {
                await WriteAsync(pipe, writes, reply, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
            {
                return;
            }
        }
    }

    private static async Task WriteAsync(NamedPipeServerStream pipe, SemaphoreSlim writes, PanMessage message, CancellationToken cancellationToken)
    {
        await writes.WaitAsync(cancellationToken);
        try
        {
            await pipe.WriteAsync(message.ToBytes(), cancellationToken);
            await pipe.FlushAsync(cancellationToken);
        }
        finally
        {
            writes.Release();
        }
    }

    private sealed class ProcessWaitHandle : WaitHandle
    {
        public ProcessWaitHandle(SafeProcessHandle process)
        {
            SafeWaitHandle = new SafeWaitHandle(process.DangerousGetHandle(), ownsHandle: false);
        }
    }
}
