using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

public sealed class FakeClock : IPanClock
{
    public long UnbiasedMilliseconds { get; set; }

    public void Advance(TimeSpan by) => UnbiasedMilliseconds += (long)by.TotalMilliseconds;
}

/// <summary>Records what the teardown does; each call can be made to fail or hang.</summary>
public sealed class FakeCleanup : IEmulationCleanup
{
    public List<int> Ports = [];
    public List<int> Detached = [];
    public List<HeldProcess> Children = [];
    public List<HeldProcess> Held = [];
    public List<HeldProcess> Ended = [];
    public List<HeldProcess> Released = [];
    public Dictionary<int, long> StartTimes = [];
    public bool DetachFails;
    public bool DetachHangs;
    public bool ListHangs;
    public bool EndFails;
    public bool EndHangs;
    public Exception? EndThrows;

    public async Task<IReadOnlyList<int>> ListLocalViiperPortsAsync(CancellationToken cancellationToken)
    {
        if (ListHangs)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        return [.. Ports];
    }

    public async Task DetachPortAsync(int port, CancellationToken cancellationToken)
    {
        if (DetachHangs)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        if (DetachFails)
        {
            throw new InvalidOperationException("usbip detach failed");
        }

        lock (Detached) Detached.Add(port);
        Ports.Remove(port);
    }

    public IReadOnlyList<HeldProcess> TakeChildrenOf(int clientPid, long clientStartTime, string imageName)
    {
        var taken = Children.Where(c => c.ImageName == imageName).ToList();
        Held.AddRange(taken);
        return taken;
    }

    public bool TryHold(HeldProcess process)
    {
        if (StartTimes.TryGetValue(process.Pid, out var start) && start == process.StartTime)
        {
            Held.Add(process);
            return true;
        }

        return false;
    }

    public async Task EndAsync(HeldProcess process, CancellationToken cancellationToken)
    {
        if (EndHangs)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        if (EndThrows is not null)
        {
            throw EndThrows;
        }

        if (EndFails)
        {
            throw new InvalidOperationException("access denied");
        }

        lock (Ended) Ended.Add(process);
        Held.Remove(process);
    }

    public void Release(HeldProcess process)
    {
        Released.Add(process);
        Held.Remove(process);
    }

    public long? StartTimeOf(int pid) => StartTimes.TryGetValue(pid, out var start) ? start : null;
}

/// <summary>A journal in a temp folder of its own.</summary>
public sealed class TempJournal : IDisposable
{
    public TempJournal()
    {
        Folder = Path.Combine(Path.GetTempPath(), "arcadia-pan-tests", Guid.NewGuid().ToString("N"));
        File = new JournalFile(Path.Combine(Folder, "journal.json"));
    }

    public string Folder { get; }

    public JournalFile File { get; }

    public string Text => System.IO.File.ReadAllText(File.Path);

    public bool Exists => System.IO.File.Exists(File.Path);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
