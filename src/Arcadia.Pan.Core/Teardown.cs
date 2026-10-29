namespace Arcadia.Pan;

/// <summary>
/// What the teardown does to the machine, done by Windows adapters in Pan and by fakes in the tests. Every call may
/// throw or hang; the runner gives each step its own timeout.
/// </summary>
public interface IEmulationCleanup
{
    /// <summary>The usbip ports of the devices imported from the server on this machine's <paramref name="serverPort"/> (<see cref="UsbipPorts.FromLocalServer"/>).</summary>
    Task<IReadOnlyList<int>> ListImportedPortsAsync(int serverPort, CancellationToken cancellationToken);

    Task DetachPortAsync(int port, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the client's child processes with the expected image name, created after the client, and keeps a handle
    /// to each from now on (review point 3). Never a process id from a message: the client is known from the pipe.
    /// </summary>
    IReadOnlyList<HeldProcess> TakeChildrenOf(int clientPid, long clientStartTime, string imageName);

    /// <summary>Takes a journaled process again after a restart; false if it is gone or its id was reused.</summary>
    bool TryHold(HeldProcess process);

    /// <summary>Ends a held process and waits for it to be gone.</summary>
    Task EndAsync(HeldProcess process, CancellationToken cancellationToken);

    /// <summary>Lets go of a held process without ending it (a planned stop).</summary>
    void Release(HeldProcess process);

    /// <summary>The process's start time, or null when no process with that id runs.</summary>
    long? StartTimeOf(int pid);

    /// <summary>Every TCP listener on this machine, IPv4 and IPv6 (#304); throws when it can't be read.</summary>
    IReadOnlyCollection<TcpListening> Listeners();
}

public enum StepOutcome
{
    Ok,
    Failed,
    TimedOut,
}

public sealed record StepResult(string Name, StepOutcome Outcome, string? Error = null)
{
    public override string ToString() => Outcome == StepOutcome.Ok ? $"{Name}: ok" : $"{Name}: {Outcome}{(Error is null ? string.Empty : $" ({Error})")}";
}

/// <summary>One step of the teardown, run on a worker with its own timeout (review point 9).</summary>
public sealed record TeardownStep(string Name, Func<CancellationToken, Task> Run, TimeSpan Timeout);

public static class TeardownRunner
{
    /// <summary>
    /// Runs the steps in order, each on the thread pool with its own timeout: a stuck step (e.g. on the PnP lock) is
    /// left behind with its token cancelled, logged, and the next step runs. Never throws.
    /// </summary>
    public static async Task<IReadOnlyList<StepResult>> RunAsync(IEnumerable<TeardownStep> steps, Action<string> log)
    {
        var results = new List<StepResult>();
        foreach (var step in steps)
        {
            var result = await RunOneAsync(step);
            log($"Teardown: {result}");
            results.Add(result);
        }

        return results;
    }

    private static async Task<StepResult> RunOneAsync(TeardownStep step)
    {
        using var timeout = new CancellationTokenSource(step.Timeout);
        var token = timeout.Token;
        try
        {
            await Task.Run(() => step.Run(token), CancellationToken.None).WaitAsync(step.Timeout);
            return new(step.Name, StepOutcome.Ok);
        }
        catch (TimeoutException)
        {
            // The step is left behind; its token must fire even when WaitAsync won the race against the timer.
            timeout.Cancel();
            return new(step.Name, StepOutcome.TimedOut, $"after {step.Timeout.TotalSeconds:0} s");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return new(step.Name, StepOutcome.TimedOut, $"after {step.Timeout.TotalSeconds:0} s");
        }
        catch (Exception ex)
        {
            return new(step.Name, StepOutcome.Failed, ex.Message);
        }
    }
}

/// <summary>
/// The teardown order (#243): 1. detach the usbip ports imported from the journaled VIIPER servers (#304), so the game
/// loses the pad first and keeps no last state (review point 14); another program's VIIPER is never touched; 2. end the
/// forwarding processes Pan holds; 3. and 4. (part B) unhide the real
/// controller only once nothing forwards to it, then restart it. Each ended process is reported at once
/// (<paramref name="ended"/>, which the guardian journals under its lock), so a failed step stays listed and is retried at
/// the next trigger, and a step left behind after its timeout never touches the journal itself (review, should-fix 9).
/// A server whose devices are all detached is reported likewise (<paramref name="detached"/>), and only then leaves the
/// journal.
/// </summary>
public sealed class TeardownPlan(
    IEmulationCleanup cleanup, IReadOnlyList<HeldProcess> held, Action<HeldProcess> ended,
    IReadOnlyList<UsbipServer> servers, Action<UsbipServer> detached)
{
    public static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);

    public IEnumerable<TeardownStep> Steps()
    {
        yield return new("detach usbip ports", DetachPortsAsync, StepTimeout);
        yield return new("end forwarding processes", EndProcessesAsync, StepTimeout);
    }

    private async Task DetachPortsAsync(CancellationToken cancellationToken)
    {
        var failed = new List<int>();
        foreach (var server in servers)
        {
            var before = failed.Count;
            foreach (var port in await cleanup.ListImportedPortsAsync(server.Port, cancellationToken))
            {
                try
                {
                    await cleanup.DetachPortAsync(port, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed.Add(port);
                    if (failed.Count == 1)
                    {
                        LastError = ex.Message;
                    }
                }
            }

            if (failed.Count == before)
            {
                detached(server);
            }
        }

        if (failed.Count > 0)
        {
            throw new InvalidOperationException($"port(s) {string.Join(", ", failed)} not detached: {LastError}");
        }
    }

    private string? LastError { get; set; }

    private async Task EndProcessesAsync(CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        foreach (var process in held)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await cleanup.EndAsync(process, cancellationToken);
                ended(process);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"{process.ImageName} ({process.Pid}): {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("; ", errors));
        }
    }
}
