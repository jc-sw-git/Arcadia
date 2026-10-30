using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text;
using Arcadia.ControllerSupport;
using Arcadia.Pan;

namespace Arcadia.Core.Tests;

/// <summary>Arcadia's VIIPER: only its own, started and checked before anything goes onto it (#304, before that #242's T242-4).</summary>
[Collection(ViiperPortCollection.Name)]
public sealed class ViiperXboxPadsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "arcadia-viiper-tests", Guid.NewGuid().ToString("N"));
    private readonly List<string> _log = [];
    private readonly FakeViiperHost _host = new();

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string KeyFile => ViiperKeyFile.PathIn(_folder);

    private ViiperServer Server() => new(Path.Combine(_folder, "viiper.exe"), _folder, _folder, _host, line => { lock (_log) _log.Add(line); })
    {
        StartTimeout = TimeSpan.FromMilliseconds(500),
    };

    [Fact]
    [Trait("TestCase", "T304-1")]
    public async Task A_start_binds_loopback_with_ports_from_Windows_a_fresh_key_of_its_own_and_requires_it()
    {
        var running = await Server().StartAsync(CancellationToken.None);

        var start = Assert.Single(_host.Started);
        Assert.Equal(["server", "--no-tray", $"--log.file={Path.Combine(_folder, "viiper.log")}", "--usb.addr=127.0.0.1:0", "--api.addr=127.0.0.1:0", "--api.require-local-host-auth"], start.ArgumentList);
        Assert.Equal(_folder, start.Environment["AppData"]);
        Assert.Equal(_folder, start.WorkingDirectory);
        Assert.Equal((FakeViiperHost.ApiPort, FakeViiperHost.UsbipPort), (running.ApiPort, running.UsbipPort));
        // The key was on disk for VIIPER's start, and is gone once it runs.
        Assert.Equal(running.Key, _host.KeyAtStart);
        Assert.Equal(32, running.Key.Length);
        Assert.False(File.Exists(KeyFile));
        // Every start a new key.
        running.Dispose();
        Assert.NotEqual(running.Key, (await Server().StartAsync(CancellationToken.None)).Key);
    }

    [Fact]
    [Trait("TestCase", "T304-3")]
    public async Task Settings_of_the_users_own_VIIPER_in_the_environment_never_reach_Arcadias()
    {
        Environment.SetEnvironmentVariable("VIIPER_API_AUTO_ATTACH_LOCAL_CLIENT", "false");
        try
        {
            await Server().StartAsync(CancellationToken.None);
        }
        finally
        {
            Environment.SetEnvironmentVariable("VIIPER_API_AUTO_ATTACH_LOCAL_CLIENT", null);
        }

        var environment = Assert.Single(_host.Started).Environment;
        Assert.DoesNotContain(environment.Keys, name => name.Equals("VIIPER_API_AUTO_ATTACH_LOCAL_CLIENT", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("none", environment["VIIPER_UPDATE_NOTIFY"]);
    }

    [Fact]
    [Trait("TestCase", "T304-10")]
    public async Task A_leftover_viiper_from_Arcadias_own_path_is_ended_before_the_start()
    {
        await Server().StartAsync(CancellationToken.None);

        Assert.Equal([Path.Combine(_folder, "viiper.exe")], _host.LeftoversEnded);
    }

    public static TheoryData<string, Action<FakeViiperHost>> Failures => new()
    {
        { "listens on 0.0.0.0", host => host.Listening = [new(FakeViiperHost.Pid, IPAddress.Any, FakeViiperHost.UsbipPort), new(FakeViiperHost.Pid, IPAddress.Loopback, FakeViiperHost.ApiPort)] },
        { "another process on its port (IPv6)", host => host.Listening = [.. host.Listening, new(4711, IPAddress.IPv6Any, FakeViiperHost.ApiPort)] },
        { "a third port", host => host.Listening = [.. host.Listening, new(FakeViiperHost.Pid, IPAddress.Loopback, 50003)] },
        { "only one port", host => host.Listening = [new(FakeViiperHost.Pid, IPAddress.Loopback, FakeViiperHost.ApiPort)] },
        { "ends at once", host => host.ExitsAtOnce = true },
        { "no port answers with the key", host => host.Answers[(FakeViiperHost.ApiPort, "key")] = ViiperAnswer.NoAnswer },
        { "both ports answer", host => host.BothPortsAnswer() },
        { "auth not enforced", host => host.Answers[(FakeViiperHost.ApiPort, "none")] = ViiperAnswer.Answered },
        { "a wrong key taken", host => host.Answers[(FakeViiperHost.ApiPort, "wrong")] = ViiperAnswer.Answered },
        { "no answer without a key", host => host.Answers[(FakeViiperHost.ApiPort, "none")] = ViiperAnswer.NoAnswer },
        { "listeners not readable", host => host.ListenersThrow = true },
    };

    [Theory]
    [Trait("TestCase", "T304-5")]
    [MemberData(nameof(Failures))]
    public async Task Any_check_that_fails_stops_the_viiper_and_leaves_no_key(string failure, Action<FakeViiperHost> arrange)
    {
        arrange(_host);

        await Assert.ThrowsAnyAsync<Exception>(() => Server().StartAsync(CancellationToken.None));

        Assert.True(_host.Process is { Stopped: true, Disposed: true }, $"{failure}: viiper.exe still runs");
        Assert.False(File.Exists(KeyFile), $"{failure}: key file left");
        Assert.Contains(_log, line => line.StartsWith("VIIPER: Start fehlgeschlagen", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestCase", "T304-5")]
    public async Task A_viiper_that_cannot_be_bound_to_Arcadia_never_counts_as_started()
    {
        _host.JobObjectFails = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Server().StartAsync(CancellationToken.None));

        Assert.False(File.Exists(KeyFile));
    }

    [Fact]
    [Trait("TestCase", "T304-5")]
    public async Task A_key_that_cannot_be_written_starts_no_viiper_at_all()
    {
        // VIIPER would make a key of its own and write it to its log in plain text.
        Directory.CreateDirectory(Path.GetDirectoryName(KeyFile)!);
        Directory.CreateDirectory(KeyFile);

        await Assert.ThrowsAnyAsync<Exception>(() => Server().StartAsync(CancellationToken.None));

        Assert.Empty(_host.Started);
    }

    [Fact]
    [Trait("TestCase", "T304-4")]
    public async Task The_key_never_appears_in_Arcadias_log()
    {
        var running = await Server().StartAsync(CancellationToken.None);

        Assert.DoesNotContain(_log, line => line.Contains(running.Key, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestCase", "T304-5")]
    public async Task Listeners_that_change_after_the_start_stop_every_new_device()
    {
        var server = Server();
        var running = await server.StartAsync(CancellationToken.None);
        server.CheckListeners(running);

        _host.Listening = [.. _host.Listening, new(4711, IPAddress.IPv6Loopback, FakeViiperHost.UsbipPort)];

        Assert.Throws<InvalidOperationException>(() => server.CheckListeners(running));
    }

    public static TheoryData<int, string> ForeignListeners => new()
    {
        { FakeViiperHost.UsbipPort, "127.0.0.1" },
        { FakeViiperHost.UsbipPort, "::1" },
        { FakeViiperHost.ApiPort, "0.0.0.0" },
        { FakeViiperHost.ApiPort, "::" },
    };

    [Theory]
    [Trait("TestCase", "T304-5")]
    [MemberData(nameof(ForeignListeners))]
    public async Task A_process_on_a_port_of_the_running_VIIPER_stops_every_new_pad_and_copy(int port, string address)
    {
        var pads = new ViiperXboxPads(Server(), _log.Add);
        await pads.PrepareAsync(CancellationToken.None);

        _host.Listening = [.. _host.Listening, new(4711, IPAddress.Parse(address), port)];

        var pad = await Assert.ThrowsAsync<InvalidOperationException>(() => pads.PlugAsync(CancellationToken.None));
        var copy = await Assert.ThrowsAsync<InvalidOperationException>(() => pads.AddRawDeviceAsync("hidproxy", new Dictionary<string, object?> { ["device"] = "12" }, CancellationToken.None));
        Assert.Equal("VIIPER lauscht nicht wie erwartet", pad.Message);
        Assert.Equal("VIIPER lauscht nicht wie erwartet", copy.Message);
        Assert.Contains(_log, line => line.Contains($"process 4711 listens on port {port} too", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestCase", "T304-5")]
    public async Task A_running_VIIPER_whose_port_another_process_took_is_replaced_at_the_next_emulation()
    {
        // Pan would refuse every start with that port; a new VIIPER gets new ports from Windows.
        var pads = new ViiperXboxPads(Server(), _log.Add);
        await pads.PrepareAsync(CancellationToken.None);
        var first = _host.Process!;
        var foreign = new TcpListening(4711, IPAddress.IPv6Loopback, FakeViiperHost.UsbipPort);
        _host.Listening = [.. _host.Listening, foreign];
        _host.OnStart = () => _host.Listening = [.. _host.Listening.Where(listener => listener != foreign)];

        Assert.Equal(FakeViiperHost.UsbipPort, await pads.PrepareAsync(CancellationToken.None));

        Assert.True(first.Stopped);
        Assert.Equal(2, _host.Started.Count);
    }

    [Fact]
    [Trait("TestCase", "T304-5")]
    public async Task A_running_VIIPER_that_still_checks_out_is_used_again()
    {
        var pads = new ViiperXboxPads(Server(), _log.Add);
        await pads.PrepareAsync(CancellationToken.None);

        await pads.PrepareAsync(CancellationToken.None);

        Assert.Single(_host.Started);
    }

    [Fact]
    [Trait("TestCase", "T304-8")]
    public async Task Nothing_goes_onto_a_VIIPER_before_a_start_made_it_ready_not_even_one_already_listening()
    {
        // Another program's VIIPER on the old fixed API port (both loopbacks), answering everything.
        var listeners = Listen(IPAddress.Loopback, IPAddress.IPv6Loopback);
        var requests = new List<string>();
        using var stop = new CancellationTokenSource();
        var serving = Task.WhenAll(listeners.Select(listener => ServeAsync(listener, requests, stop.Token)));
        var pads = new ViiperXboxPads(Server(), _log.Add);

        var pad = await Assert.ThrowsAsync<InvalidOperationException>(() => pads.PlugAsync(CancellationToken.None));
        var copy = await Assert.ThrowsAsync<InvalidOperationException>(() => pads.AddRawDeviceAsync("hidproxy", new Dictionary<string, object?> { ["device"] = "12" }, CancellationToken.None));

        await stop.CancelAsync();
        await serving;
        listeners.ForEach(listener => listener.Dispose());
        Assert.Equal("VIIPER läuft nicht", pad.Message);
        Assert.Equal("VIIPER läuft nicht", copy.Message);
        Assert.Empty(_host.Started);
        lock (requests)
        {
            Assert.Empty(requests);
        }
    }

    [Fact]
    [Trait("TestCase", "T304-2")]
    public async Task Arcadias_VIIPER_is_only_the_pinned_build_next_to_it_and_without_one_there_is_none()
    {
        var own = Path.Combine(_folder, "viiper.exe");

        Assert.Null(ViiperXboxPads.Own(null, _folder, _folder, _host, _log.Add));
        Assert.Null(ViiperXboxPads.Own(own, _folder, _folder, null, _log.Add));
        await ViiperXboxPads.Own(own, _folder, _folder, _host, _log.Add)!.PrepareAsync(CancellationToken.None);
        var start = Assert.Single(_host.Started);
        Assert.Equal(own, start.FileName);
        Assert.Equal(Path.Combine(_folder, "viiper"), start.Environment["AppData"]);
    }

    [Fact]
    [Trait("TestCase", "T304-2")]
    public async Task Without_a_pinned_build_there_is_no_VIIPER_at_all()
    {
        var none = new NoVirtualXboxPads();

        Assert.NotNull(none.Unavailable());
        Assert.Null(none.ServerPort);
        await Assert.ThrowsAsync<InvalidOperationException>(() => none.PrepareAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => none.PlugAsync(CancellationToken.None));
    }

    [WindowsFact]
    [Trait("TestCase", "T304-1")]
    [SupportedOSPlatform("windows")]
    public void Windows_names_this_process_and_the_address_it_listens_on()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        Assert.Contains(new TcpListening(Environment.ProcessId, IPAddress.Loopback, port), TcpListeners.All());
    }

    /// <summary>A listener on each address the machine has; a container without IPv6 has no ::1.</summary>
    private static List<TcpListener> Listen(params IPAddress[] addresses)
    {
        var listeners = new List<TcpListener>();
        foreach (var address in addresses)
        {
            try
            {
                var listener = new TcpListener(address, 3242);
                listener.Start();
                listeners.Add(listener);
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressFamilyNotSupported && address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // No IPv6 here.
            }
        }

        return listeners;
    }

    /// <summary>Answers every request like a VIIPER would answer a ping, and notes what was asked.</summary>
    private static async Task ServeAsync(TcpListener listener, List<string> requests, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(cancellationToken);
                var stream = client.GetStream();
                var buffer = new byte[4096];
                var read = await stream.ReadAsync(buffer, cancellationToken);
                lock (requests)
                {
                    requests.Add(Encoding.UTF8.GetString(buffer, 0, read).TrimEnd('\0', '\n'));
                }

                await stream.WriteAsync(Encoding.UTF8.GetBytes("{\"server\":\"VIIPER\",\"version\":\"0.0.0\"}\n"), cancellationToken);
                client.Client.Shutdown(SocketShutdown.Send);
            }
        }
        catch (OperationCanceledException)
        {
            // Done.
        }
    }
}

/// <summary>
/// Windows as a VIIPER start sees it: by default a well-behaved viiper.exe on two loopback ports that answers only with
/// the key it was started with. Each piece can be made to misbehave.
/// </summary>
public sealed class FakeViiperHost : IViiperHost
{
    public const int Pid = 4242;
    public const int UsbipPort = 50001;
    public const int ApiPort = 50002;

    public List<ProcessStartInfo> Started { get; } = [];
    public List<string> LeftoversEnded { get; } = [];
    public FakeProcess? Process { get; private set; }
    public string? KeyAtStart { get; private set; }
    public bool JobObjectFails { get; set; }
    public bool ExitsAtOnce { get; set; }
    public bool ListenersThrow { get; set; }
    public Action? OnStart { get; set; }

    /// <summary>The usbip port behaves like a second API: the start can't tell which one is VIIPER's.</summary>
    public void BothPortsAnswer()
    {
        Answers[(UsbipPort, "key")] = ViiperAnswer.Answered;
        Answers[(UsbipPort, "none")] = ViiperAnswer.Refused;
        Answers[(UsbipPort, "wrong")] = ViiperAnswer.Refused;
    }

    public List<TcpListening> Listening { get; set; } =
        [new(Pid, IPAddress.Loopback, UsbipPort), new(Pid, IPAddress.Loopback, ApiPort), new(7, IPAddress.Any, 445)];

    /// <summary>Overrides by (port, "key" | "none" | "wrong"); by default only the API answers, and only with the key.</summary>
    public Dictionary<(int Port, string Key), ViiperAnswer> Answers { get; } = [];

    private byte[]? _key;

    public void EndLeftovers(string executable) => LeftoversEnded.Add(executable);

    public IViiperProcess Start(ProcessStartInfo start)
    {
        Started.Add(start);
        OnStart?.Invoke();
        var keyFile = ViiperKeyFile.PathIn(start.Environment["AppData"]!);
        KeyAtStart = File.ReadAllText(keyFile);
        _key = ViiperConnection.DeriveKey(KeyAtStart);
        Process = new FakeProcess { Exited = ExitsAtOnce };
        if (JobObjectFails)
        {
            Process.Stop();
            Process.Dispose();
            throw new InvalidOperationException("viiper.exe ließe sich nicht mit Arcadia beenden (Fehler 5)");
        }

        return Process;
    }

    public IReadOnlyCollection<TcpListening> Listeners() => ListenersThrow ? throw new System.ComponentModel.Win32Exception(5) : [.. Listening];

    public Task<ViiperAnswer> PingAsync(int port, byte[]? key, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var kind = key is null ? "none" : key.AsSpan().SequenceEqual(_key) ? "key" : "wrong";
        if (Answers.TryGetValue((port, kind), out var answer))
        {
            return Task.FromResult(answer);
        }

        return Task.FromResult(port != ApiPort ? ViiperAnswer.NoAnswer : kind == "key" ? ViiperAnswer.Answered : ViiperAnswer.Refused);
    }

    public sealed class FakeProcess : IViiperProcess
    {
        public int Id => Pid;

        public bool Exited { get; set; }

        public bool Stopped { get; private set; }

        public bool Disposed { get; private set; }

        public bool HasExited => Exited || Stopped;

        public void Stop() => Stopped = true;

        public void Dispose() => Disposed = true;
    }
}

/// <summary>Tests that bind VIIPER's old fixed API port run one at a time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ViiperPortCollection
{
    public const string Name = "VIIPER-Port";
}
