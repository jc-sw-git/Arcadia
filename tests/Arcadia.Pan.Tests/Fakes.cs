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

public static class GuardianExtensions
{
    /// <summary>A command that isn't about hiding, which the guardian answers at once.</summary>
    public static PanMessage HandleNow(this Guardian guardian, PanMessage message)
    {
        var reply = guardian.HandleAsync(message);
        Assert.True(reply.IsCompleted, $"{message} was not answered at once");
        return reply.GetAwaiter().GetResult();
    }
}

/// <summary>HidHide as Pan sees it through the driver: lists, switches, and every way a call can go wrong.</summary>
public sealed class FakeHidHide : IHidHideDriver
{
    public Version? Version = new(1, 5, 230, 0);
    public bool DriverRunning = true;
    public List<string> Hidden = [];
    public List<string> Allowed = [];
    public bool Cloaking;
    public bool Inverse;
    public PanReason? FailAll;
    public string? FailOn;
    public bool UnknownInterface;
    public List<string> Calls = [];

    /// <summary>Runs before every change, e.g. to look at the journal at that moment.</summary>
    public Action<string>? BeforeChange;

    public Version? InstalledVersion() => Version;

    public IReadOnlyList<string> GetHiddenDevices() => Call("GetHidden", () => (IReadOnlyList<string>)[.. Hidden]);

    public void SetHiddenDevices(IReadOnlyList<string> instanceIds) => Change("SetHidden", () => Hidden = [.. instanceIds]);

    public IReadOnlyList<string> GetAllowedApplications() => Call("GetAllowed", () => (IReadOnlyList<string>)[.. Allowed]);

    public void SetAllowedApplications(IReadOnlyList<string> imagePaths) => Change("SetAllowed", () => Allowed = [.. imagePaths]);

    public bool GetCloaking() => Call("GetCloaking", () => Cloaking);

    public void SetCloaking(bool on) => Change(on ? "CloakingOn" : "CloakingOff", () => Cloaking = on);

    public bool GetInverse()
    {
        if (UnknownInterface)
        {
            throw new HidHideException(PanReason.HidHideUnsupported, "HidHide does not know IOCTL 0x80016018: unknown driver interface");
        }

        return Call("GetInverse", () => Inverse);
    }

    private T Call<T>(string name, Func<T> call)
    {
        if (!DriverRunning)
        {
            throw new HidHideException(PanReason.HidHideMissing, "HidHide's driver is not running");
        }

        if (FailAll is { } reason || FailOn == name)
        {
            throw new HidHideException(FailAll ?? PanReason.HidingFailed, $"{name} failed");
        }

        lock (Calls) Calls.Add(name);
        return call();
    }

    private void Change(string name, Action change) => Call(name, () =>
    {
        BeforeChange?.Invoke(name);
        change();
        return 0;
    });
}

/// <summary>The puck's devices: which are connected, and a restart that can fail, hang or need a replug.</summary>
public sealed class FakeDevices : ISteamControllerDevices
{
    public const string Slot2 = @"HID\VID_28DE&PID_1304&MI_02&COL03\8&1A2B3C4D&0&0002";
    public const string Slot3 = @"HID\VID_28DE&PID_1304&MI_03&COL03\8&1A2B3C4D&0&0002";
    public const string Dongle = @"HID\VID_28DE&PID_1304&MI_06\8&5E6F7A8B&0&0000";

    public List<string> Present = [Slot2, Slot3];
    public int Restarts;
    public bool RestartFails;
    public bool RestartHangs;
    public bool ListHangs;
    public List<string> Calls = [];

    public IReadOnlyList<string> PresentHidInstances()
    {
        if (ListHangs)
        {
            Thread.Sleep(TimeSpan.FromSeconds(30));
        }

        return [.. Present];
    }

    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        if (RestartHangs)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        if (RestartFails)
        {
            throw new InvalidOperationException("needs a replug");
        }

        Interlocked.Increment(ref Restarts);
        lock (Calls) Calls.Add("Restart");
    }
}
