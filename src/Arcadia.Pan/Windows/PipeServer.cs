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
/// of its end without polling.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PipeServer(Guardian guardian, Action<string> log)
{
    private static readonly TimeSpan SquatRetry = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim _writes = new(1, 1);

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
            catch (IOException ex)
            {
                // Another process holds the name: don't serve beside it.
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
            catch (IOException ex)
            {
                log($"Next pipe instance not created: {ex.Message}");
            }

            await ServeAsync(pipe, cancellationToken);
        }

        listening?.Dispose();
    }

    private static NamedPipeServerStream Create(bool first)
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.InteractiveSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
        var options = PipeOptions.Asynchronous | PipeOptions.WriteThrough | (first ? PipeOptions.FirstPipeInstance : PipeOptions.None);
        return NamedPipeServerStreamAcl.Create(PanPaths.PipeName, PipeDirection.InOut, 2, PipeTransmissionMode.Byte, options, PanMessage.Size * 16, PanMessage.Size * 16, security);
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
            var refused = guardian.Register(new ClientIdentity((int)pid, startTime, inConsole));
            if (refused != PanReason.None)
            {
                await WriteAsync(pipe, PanMessage.Of(PanReply.Refused, reason: refused), cancellationToken);
                return;
            }

            ClientPresent?.Invoke(true);
            using var gone = new CancellationTokenSource();
            using var processWait = new ProcessWaitHandle(processHandle);
            var registration = ThreadPool.RegisterWaitForSingleObject(processWait, (_, _) => gone.Cancel(), null, -1, executeOnlyOnce: true);
            void Notify(PanMessage message) => WriteAsync(pipe, message, cancellationToken).ContinueWith(t => log($"Notice not sent: {t.Exception?.GetBaseException().Message}"), TaskContinuationOptions.OnlyOnFaulted);
            guardian.Notify += Notify;
            try
            {
                using var either = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, gone.Token);
                await ReadLoopAsync(pipe, either.Token);
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

    private async Task ReadLoopAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
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
                await WriteAsync(pipe, reply, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
            {
                return;
            }
        }
    }

    private async Task WriteAsync(NamedPipeServerStream pipe, PanMessage message, CancellationToken cancellationToken)
    {
        await _writes.WaitAsync(cancellationToken);
        try
        {
            await pipe.WriteAsync(message.ToBytes(), cancellationToken);
            await pipe.FlushAsync(cancellationToken);
        }
        finally
        {
            _writes.Release();
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
