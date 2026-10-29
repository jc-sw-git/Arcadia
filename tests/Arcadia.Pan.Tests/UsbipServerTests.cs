using System.Net;
using System.Text.Json;
using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

/// <summary>
/// #304: Pan takes an emulation only from a held viiper.exe that alone serves the usbip port Arcadia names, journals that
/// port per generation, and its teardown detaches only devices imported from a journaled port.
/// </summary>
public sealed class UsbipServerTests : IDisposable
{
    private static readonly ClientIdentity Arcadia = new(Pid: 500, StartTime: 1000, InConsoleSession: true);
    private static readonly HeldProcess OwnViiper = new(600, 1500, "viiper.exe");

    private readonly TempJournal _journal = new();
    private readonly FakeCleanup _cleanup = new() { Children = [OwnViiper], Ports = [3] };
    private readonly List<string> _log = [];
    private readonly Guardian _guardian;

    public UsbipServerTests()
    {
        _guardian = new Guardian(new FakeClock(), _journal.File, _cleanup, line => { lock (_log) _log.Add(line); });
    }

    public void Dispose() => _journal.Dispose();

    private string Log() { lock (_log) return string.Join("\n", _log); }

    private PanMessage Start(int port = FakeCleanup.ServerPort)
    {
        Assert.Equal(PanReason.None, _guardian.Register(Arcadia));
        Assert.Equal(PanReply.HelloOk, _guardian.HandleNow(PanMessage.Of(PanCommand.Hello)).Reply);
        return _guardian.HandleNow(PanMessage.Of(PanCommand.EmulationStarted, value: (uint)port));
    }

    private async Task CrashAsync()
    {
        _guardian.ClientGone();
        if (_guardian.Teardown is { } teardown)
        {
            await teardown;
        }
    }

    [Fact]
    [Trait("TestCase", "T304-6")]
    public void A_start_journals_the_usbip_port_with_the_held_viiper_that_serves_it()
    {
        var started = Start();

        Assert.Equal(PanReply.Started, started.Reply);
        Assert.Equal(new UsbipServer(started.Generation, FakeCleanup.ServerPort, OwnViiper.Pid), Assert.Single(_journal.File.Load().State.UsbipServers));
    }

    [Fact]
    [Trait("TestCase", "T304-6")]
    public void Without_a_viiper_of_the_client_the_start_is_refused_and_nothing_is_journaled()
    {
        // Before #304 Pan took it ("Arcadia nutzt ihn mit") and its teardown detached every local VIIPER port.
        _cleanup.Children = [new HeldProcess(601, 1600, "cmd.exe")];

        var refused = Start();

        Assert.Equal((PanReply.Rejected, PanReason.NoForwardingProcess), (refused.Reply, refused.Reason));
        Assert.False(_journal.Exists && _journal.File.Load().State.HasWork);
        Assert.Contains("no viiper.exe of client 500", Log());
    }

    [Theory]
    [Trait("TestCase", "T304-6")]
    [InlineData(0)]
    [InlineData(70000)]
    public void A_start_without_a_valid_port_is_refused_and_the_viiper_let_go(int port)
    {
        var refused = Start(port);

        Assert.Equal((PanReply.Rejected, PanReason.UsbipPortNotServed), (refused.Reply, refused.Reason));
        Assert.Equal([OwnViiper], _cleanup.Released);
        Assert.Empty(_cleanup.Held);
    }

    public static TheoryData<string> ForeignListeners => ["127.0.0.1", "::1", "0.0.0.0", "::"];

    [Theory]
    [Trait("TestCase", "T304-6")]
    [MemberData(nameof(ForeignListeners))]
    public void Another_process_on_the_port_in_either_family_refuses_the_start(string address)
    {
        _cleanup.ListenersOverride =
        [
            new TcpListening(OwnViiper.Pid, IPAddress.Loopback, FakeCleanup.ServerPort),
            new TcpListening(4711, IPAddress.Parse(address), FakeCleanup.ServerPort),
        ];

        var refused = Start();

        Assert.Equal((PanReply.Rejected, PanReason.UsbipPortNotServed), (refused.Reply, refused.Reason));
        Assert.Equal([OwnViiper], _cleanup.Released);
        Assert.Contains("process 4711 listens on port 50123 too", Log());
        Assert.False(_journal.Exists && _journal.File.Load().State.HasWork);
    }

    [Fact]
    [Trait("TestCase", "T304-6")]
    public void A_port_the_held_viiper_does_not_listen_on_refuses_the_start()
    {
        _cleanup.ListenersOverride = [new TcpListening(4711, IPAddress.Loopback, FakeCleanup.ServerPort)];

        Assert.Equal(PanReason.UsbipPortNotServed, Start().Reason);
        Assert.Contains("no held viiper.exe listens on port 50123", Log());
    }

    [Fact]
    [Trait("TestCase", "T304-6")]
    public void Listeners_that_cannot_be_read_refuse_the_start()
    {
        _cleanup.ListenersThrow = true;

        Assert.Equal(PanReason.UsbipPortNotServed, Start().Reason);
        Assert.Contains("listeners not readable", Log());
    }

    [Fact]
    [Trait("TestCase", "T304-3")]
    public async Task A_teardown_detaches_only_what_the_journaled_port_served_never_another_programs_VIIPER()
    {
        _cleanup.ImportedFrom[UsbipPorts.ViiperPort] = [7];
        Start();

        await CrashAsync();

        Assert.Equal([3], _cleanup.Detached);
        Assert.Equal([7], _cleanup.ImportedFrom[UsbipPorts.ViiperPort]);
        Assert.Empty(_journal.File.Load().State.UsbipServers);
    }

    [Fact]
    [Trait("TestCase", "T304-6")]
    public async Task A_server_whose_devices_did_not_detach_stays_journaled_and_goes_at_the_next_trigger()
    {
        var generation = Start().Generation;
        _cleanup.DetachFails = true;
        await CrashAsync();
        Assert.Equal([new UsbipServer(generation, FakeCleanup.ServerPort, OwnViiper.Pid)], _journal.File.Load().State.UsbipServers);

        _cleanup.DetachFails = false;
        var restarted = new Guardian(new FakeClock(), _journal.File, _cleanup, _log.Add);
        await restarted.RecoverAsync();

        Assert.Equal([3], _cleanup.Detached);
        Assert.False(_journal.File.Load().State.HasWork);
    }

    [Fact]
    [Trait("TestCase", "T304-6")]
    public void A_planned_stop_drops_its_generations_port_as_Arcadia_removed_its_devices_itself()
    {
        var generation = Start().Generation;

        Assert.Equal(PanReply.Stopped, _guardian.HandleNow(PanMessage.Of(PanCommand.EmulationStopped, generation)).Reply);

        Assert.Empty(_journal.File.Load().State.UsbipServers);
        Assert.Empty(_cleanup.Detached);
    }

    [Fact]
    [Trait("TestCase", "T304-6")]
    public async Task A_planned_stop_keeps_an_earlier_server_a_teardown_could_not_detach()
    {
        var first = Start().Generation;
        _cleanup.DetachFails = true;
        await CrashAsync();
        _cleanup.DetachFails = false;
        var second = Start().Generation;

        Assert.Equal(PanReply.Stopped, _guardian.HandleNow(PanMessage.Of(PanCommand.EmulationStopped, second)).Reply);

        Assert.Equal([new UsbipServer(first, FakeCleanup.ServerPort, OwnViiper.Pid)], _journal.File.Load().State.UsbipServers);
        var restarted = new Guardian(new FakeClock(), _journal.File, _cleanup, _log.Add);
        await restarted.RecoverAsync();
        Assert.Equal([3], _cleanup.Detached);
        Assert.Empty(_journal.File.Load().State.UsbipServers);
    }

    [Fact]
    [Trait("TestCase", "T304-7")]
    public void The_Pan_before_304_still_reads_a_journal_written_now_with_all_its_state()
    {
        // The update at Arcadia's start may install an older Pan (T297-9): it must keep the generation and what is left
        // to undo, and only miss the usbip ports.
        var generation = Start().Generation;

        var old = JsonSerializer.Deserialize<JournalBefore304>(File.ReadAllText(_journal.File.Path))!;

        Assert.Equal(1, old.Version);
        Assert.Equal(generation, old.Generation);
        Assert.True(old.Emulating);
        Assert.Equal(OwnViiper.Pid, Assert.Single(old.HeldProcesses!).Pid);
        Assert.NotNull(old.HiddenDevices);
        Assert.NotNull(old.AllowListed);
    }

    /// <summary>The journal as the Pan before #304 read it (version 1 only, these members; unknown ones ignored).</summary>
    private sealed record JournalBefore304(int Version, uint Generation, bool Emulating, List<HeldProcess>? HeldProcesses, List<string>? HiddenDevices, List<string>? AllowListed);

    [Fact]
    [Trait("TestCase", "T304-7")]
    public async Task A_journal_from_before_304_detaches_what_VIIPERs_default_port_served_as_that_Pan_did()
    {
        Directory.CreateDirectory(_journal.Folder);
        File.WriteAllText(_journal.File.Path, """
            {"Version":1,"Generation":4,"ClientPid":500,"ClientStartTime":1000,"Emulating":true,
             "HeldProcesses":[{"Pid":600,"StartTime":1500,"ImageName":"viiper.exe"}],"HiddenDevices":[],"AllowListed":[]}
            """);
        _cleanup.StartTimes[600] = 1500;
        _cleanup.Ports = [];
        _cleanup.ImportedFrom[UsbipPorts.ViiperPort] = [9];

        Assert.True(await _guardian.RecoverAsync());

        Assert.Equal([9], _cleanup.Detached);
        Assert.Equal(600, Assert.Single(_cleanup.Ended).Pid);
        var state = _journal.File.Load().State;
        Assert.Equal(JournalState.CurrentVersion, state.Version);
        Assert.False(state.HasWork);
    }

    [Fact]
    [Trait("TestCase", "T304-7")]
    public void A_journal_from_before_304_with_nothing_open_gets_no_port_to_detach()
    {
        Directory.CreateDirectory(_journal.Folder);
        File.WriteAllText(_journal.File.Path, """{"Version":1,"Generation":4,"HeldProcesses":[],"HiddenDevices":[],"AllowListed":[]}""");

        var load = _journal.File.Load();

        Assert.Null(load.Problem);
        Assert.Empty(load.State.UsbipServers);
        Assert.False(load.State.HasWork);
    }
}
