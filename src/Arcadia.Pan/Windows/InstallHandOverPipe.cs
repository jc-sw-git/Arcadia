using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace Arcadia.Pan.Windows;

/// <summary>
/// The per-run pipe <c>\\.\pipe\Arcadia.Pan.Install.&lt;id&gt;</c> elevated Pan waits on for Arcadia's hand-over (#413,
/// D7 option A). Made as the first instance (a name taken before ends the run), one instance, a DACL that lets only the
/// user of Arcadia's process read and write (not Pan's own user: elevated with another account's credentials, Arcadia
/// still connects), and only a client whose process is Arcadia's is heard. Arcadia's process is opened when the pipe is
/// made and must have started before Pan, so a process id reused after Arcadia ended can't stand in for it.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class InstallHandOverPipe : IHandOverChannel, IDisposable
{
    private readonly NamedPipeServerStream _pipe;
    private readonly SafeProcessHandle _client;
    private readonly uint _clientId;
    private readonly RegisteredWaitHandle _clientWait;
    private readonly TaskCompletionSource _clientGone = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action<string> _log;

    private InstallHandOverPipe(NamedPipeServerStream pipe, SafeProcessHandle client, uint clientId, Action<string> log)
    {
        _pipe = pipe;
        _client = client;
        _clientId = clientId;
        _log = log;
        _clientWait = ThreadPool.RegisterWaitForSingleObject(new ProcessWaitHandle(client), (_, _) => _clientGone.TrySetResult(), null, -1, executeOnlyOnce: true);
    }

    public Task ClientGone => _clientGone.Task;

    /// <summary>The pipe's DACL, for the test that nobody but Arcadia's user may open it.</summary>
    internal PipeSecurity Security => _pipe.GetAccessControl();

    /// <summary>The pipe for <paramref name="run"/>, or null with why not: a name already taken, or no fitting Arcadia.</summary>
    /// <param name="panStart">This Pan's start (FILETIME); tests, where Pan and Arcadia are one process, pass a later one.</param>
    public static InstallHandOverPipe? Create(InstallRun run, Action<string> log, out HandOverEnd? failure, long? panStart = null)
    {
        failure = null;
        var handle = Native.OpenProcess(Native.ProcessQueryLimitedInformation | Native.Synchronize, false, (uint)run.ClientProcessId);
        if (handle == 0)
        {
            log($"Arcadia's process {run.ClientProcessId} not found ({Native.LastErrorText()})");
            failure = HandOverEnd.ArcadiaGone;
            return null;
        }

        var client = new SafeProcessHandle(handle, ownsHandle: true);
        try
        {
            if (WindowsCleanup.StartTimeOf(handle) is not { } clientStart || (panStart ?? OwnStartTime()) is not { } ownStart || clientStart >= ownStart)
            {
                log($"Process {run.ClientProcessId} started after Pan: not the Arcadia that started it");
                failure = HandOverEnd.PipeTaken;
                client.Dispose();
                return null;
            }

            var owner = OwnerOf(handle);
            var security = new PipeSecurity();
            security.AddAccessRule(new PipeAccessRule(owner, PipeAccessRights.ReadWrite, AccessControlType.Allow));
            var pipe = NamedPipeServerStreamAcl.Create(InstallHandOver.PipeName(run.Id), PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance, InstallHandOver.MaxMessageBytes, InstallHandOver.MaxMessageBytes, security);
            return new InstallHandOverPipe(pipe, client, (uint)run.ClientProcessId, log);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log($"Hand-over pipe not created ({ex.Message}): the name is taken");
            failure = HandOverEnd.PipeTaken;
            client.Dispose();
            return null;
        }
    }

    public async Task<byte[]?> ReceiveAsync(int maxBytes, CancellationToken cancellationToken)
    {
        while (true)
        {
            await _pipe.WaitForConnectionAsync(cancellationToken);
            if (Native.GetNamedPipeClientProcessId(_pipe.SafePipeHandle.DangerousGetHandle(), out var pid) && pid == _clientId)
            {
                break;
            }

            // The DACL lets only Arcadia's user in; another process of that user is still not Arcadia.
            _log($"Hand-over: process {pid} connected instead of Arcadia ({_clientId}); refused");
            _pipe.Disconnect();
        }

        var buffer = new byte[maxBytes + 1];
        var read = 0;
        while (read < buffer.Length)
        {
            int n;
            try
            {
                n = await _pipe.ReadAsync(buffer.AsMemory(read), cancellationToken);
            }
            catch (IOException) when (read > 0)
            {
                // Arcadia writes its message and closes the pipe at once; a read after that close reports a broken pipe
                // instead of the end, though the message before it arrived whole.
                break;
            }

            if (n == 0)
            {
                break;
            }

            read += n;
        }

        return read == 0 ? null : buffer[..read];
    }

    public void Dispose()
    {
        _clientWait.Unregister(null);
        _pipe.Dispose();
        _client.Dispose();
    }

    private static long? OwnStartTime()
    {
        var own = Native.OpenProcess(Native.ProcessQueryLimitedInformation, false, (uint)Environment.ProcessId);
        if (own == 0)
        {
            return null;
        }

        try
        {
            return WindowsCleanup.StartTimeOf(own);
        }
        finally
        {
            Native.CloseHandle(own);
        }
    }

    private static SecurityIdentifier OwnerOf(nint process)
    {
        if (!Native.OpenProcessToken(process, Native.TokenQuery, out var token))
        {
            throw new UnauthorizedAccessException($"Arcadia's token not readable: {Native.LastErrorText()}");
        }

        using var tokenHandle = new SafeAccessTokenHandle(token);
        using var identity = new WindowsIdentity(tokenHandle.DangerousGetHandle());
        return identity.User ?? throw new UnauthorizedAccessException("Arcadia's process has no user");
    }

    private sealed class ProcessWaitHandle : WaitHandle
    {
        public ProcessWaitHandle(SafeProcessHandle process)
        {
            SafeWaitHandle = new SafeWaitHandle(process.DangerousGetHandle(), ownsHandle: false);
        }
    }
}
