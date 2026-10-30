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
    /// <summary>The usbip port Arcadia's VIIPER listens on in these tests; every held viiper.exe listens there (#304).</summary>
    public const int ServerPort = 50123;

    /// <summary>The usbip ports imported from <see cref="ServerPort"/>.</summary>
    public List<int> Ports = [];

    /// <summary>The usbip ports imported from other servers on this machine, by the server's port.</summary>
    public Dictionary<int, List<int>> ImportedFrom = [];

    /// <summary>Set: the listeners Windows reports instead of the client's viiper.exe children on <see cref="ServerPort"/>.</summary>
    public List<TcpListening>? ListenersOverride;
    public bool ListenersThrow;
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

    public async Task<IReadOnlyList<int>> ListImportedPortsAsync(int serverPort, CancellationToken cancellationToken)
    {
        if (ListHangs)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        lock (Detached)
        {
            return serverPort == ServerPort ? [.. Ports] : [.. ImportedFrom.GetValueOrDefault(serverPort) ?? []];
        }
    }

    public IReadOnlyCollection<TcpListening> Listeners() => ListenersThrow
        ? throw new System.ComponentModel.Win32Exception(5)
        : ListenersOverride ?? [.. Children.Where(p => p.ImageName == Guardian.ForwardingImageName).Select(p => new TcpListening(p.Pid, System.Net.IPAddress.Loopback, ServerPort))];

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

        lock (Detached)
        {
            Detached.Add(port);
            Ports.Remove(port);
            foreach (var imported in ImportedFrom.Values)
            {
                imported.Remove(port);
            }
        }
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

    /// <summary>Runs before every read, e.g. to hold a caller there.</summary>
    public Action<string>? BeforeRead;

    public Version? InstalledVersion() => Version;

    /// <summary>False with <see cref="DriverRunning"/> false: HidHide uninstalled.</summary>
    public bool ServiceRegistered = true;

    public bool IsServiceRegistered() => ServiceRegistered;

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

        BeforeRead?.Invoke(name);
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
public sealed class FakeDevices : ISteamControllerDevices, IDisposable
{
    /// <summary>
    /// The guardian's device timeout in tests whose devices answer: far above what a loaded CI runner needs to schedule
    /// an instant fake (300 ms was not, run 40458024167), and never waited out, as the fake answers at once.
    /// </summary>
    public static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The device timeout in tests whose devices hang until it runs out. Their other device steps must still make it, so
    /// it isn't cut to a few hundred milliseconds; a test that only needs a long step ends it with <see cref="EndRestartHang"/>.
    /// </summary>
    public static readonly TimeSpan HangTimeout = TimeSpan.FromSeconds(1);

    public const string Slot2 = @"HID\VID_28DE&PID_1304&MI_02&COL03\8&1A2B3C4D&0&0002";
    public const string Slot3 = @"HID\VID_28DE&PID_1304&MI_03&COL03\8&1A2B3C4D&0&0002";
    public const string Dongle = @"HID\VID_28DE&PID_1304&MI_06\8&5E6F7A8B&0&0000";

    public List<string> Present = [Slot2, Slot3];
    public int Restarts;
    public bool RestartFails;
    public bool RestartHangs;
    public bool ListHangs;
    public List<string> Calls = [];
    private readonly ManualResetEventSlim _listHangEnds = new();
    private readonly TaskCompletionSource _restartHangEnds = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Runs while the devices are listed, e.g. a change the user makes in HidHide meanwhile.</summary>
    public Action? WhileListing;

    public IReadOnlyList<string> PresentHidInstances()
    {
        WhileListing?.Invoke();
        if (ListHangs)
        {
            // Blocks like Windows' PnP lock, but only until the test ends, so it doesn't starve the thread pool.
            _listHangEnds.Wait(TimeSpan.FromSeconds(30));
        }

        return [.. Present];
    }

    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        if (RestartHangs)
        {
            await _restartHangEnds.Task.WaitAsync(cancellationToken);
        }

        if (RestartFails)
        {
            throw new InvalidOperationException("needs a replug");
        }

        Interlocked.Increment(ref Restarts);
        lock (Calls) Calls.Add("Restart");
    }

    /// <summary>A restart that took long finishes now (and every later one at once).</summary>
    public void EndRestartHang() => _restartHangEnds.TrySetResult();

    public void Dispose()
    {
        _listHangEnds.Set();
        _restartHangEnds.TrySetResult();
    }
}

/// <summary>Every other controller (#335): which models are connected with which devices, and a restart that can fail or need a replug.</summary>
public sealed class FakeControllers : IControllerDevices
{
    public static readonly ControllerModel DualSense = new(0x054C, 0x0CE6);
    public static readonly ControllerModel EightBitDo = new(0x2DC8, 0x310B);
    public const string DualSenseHid = @"HID\VID_054C&PID_0CE6&MI_03\7&2B7F&0&0000";
    public const string DualSenseBluetooth = @"HID\{00001124-0000-1000-8000-00805F9B34FB}_VID&0002054C_PID&0CE6\9&1D2E&0&0000";
    public const string EightBitDoHid = @"HID\VID_2DC8&PID_310B&MI_02\8&3C4D&0&0000";
    public const string EightBitDoXInput = @"USB\VID_2DC8&PID_310B&MI_00\7&5E6F&0&0000";

    public Dictionary<ControllerModel, ControllerScan> Connected = new()
    {
        [DualSense] = new(true, [new(DualSenseHid, ControllerInterface.Hid)]),
        [EightBitDo] = new(true, [new(EightBitDoHid, ControllerInterface.Hid), new(EightBitDoXInput, ControllerInterface.XInput)]),
    };

    public List<string> Restarted = [];
    public bool RestartFails;
    public bool ScanHangs;

    /// <summary>Set: a scan signals <see cref="ScanEntered"/> and waits for it, so a test can act while a hide scans.</summary>
    public ManualResetEventSlim? ScanBlock;
    public readonly TaskCompletionSource ScanEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Instances whose device Windows restarts only once it is reconnected.</summary>
    public HashSet<string> NeedReplug = [];

    public ControllerScan Scan(ControllerModel model)
    {
        if (ScanHangs)
        {
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }

        if (ScanBlock is { } block)
        {
            ScanEntered.TrySetResult();
            block.Wait(TimeSpan.FromSeconds(5));
        }

        return Connected.TryGetValue(model, out var scan) ? scan : new ControllerScan(false, []);
    }

    public Task<IReadOnlyList<string>> RestartAsync(IReadOnlyList<string> hidInstances, CancellationToken cancellationToken)
    {
        if (RestartFails)
        {
            throw new InvalidOperationException("device not opened");
        }

        lock (Restarted) Restarted.AddRange(hidInstances);
        return Task.FromResult<IReadOnlyList<string>>(hidInstances.Where(NeedReplug.Contains).ToList());
    }
}
