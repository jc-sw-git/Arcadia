using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using Arcadia.ControllerSupport.Copy;
using Arcadia.Input;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Tests.Pads;

/// <summary>
/// The copy's bridge (#242 part C) between a puck and VIIPER's <c>hidproxy</c> stream, on a fake puck and a real
/// loopback socket in place of VIIPER: what Steam sends reaches the puck only through the allow list, routine requests
/// of Windows never count as an update, and once the copy closes nothing reaches the puck any more.
/// </summary>
public sealed class SteamControllerCopyBridgeTests : IAsyncLifetime
{
    private const byte Hid = 2;
    private const byte Cdc = 1;
    private const byte EmptySlot = 3;
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private readonly FakePuck _puck = new([Hid, EmptySlot]);
    private readonly List<string> _updates = [];
    private readonly List<string> _log = [];
    private bool _forwarding;
    private SteamControllerCopyBridge _bridge = null!;
    private TcpClient _viiper = null!;
    private NetworkStream _steam = null!;

    public async Task InitializeAsync()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        _viiper = new TcpClient();
        var accept = listener.AcceptTcpClientAsync();
        await _viiper.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        var bridgeSide = await accept;
        _steam = _viiper.GetStream();

        var filter = new SteamControllerCopyFilter(new FakeTimeProvider(), line => { lock (_log) { _log.Add(line); } });
        filter.UpdateTraffic += what => { lock (_updates) { _updates.Add(what); } };
        _bridge = new SteamControllerCopyBridge(_puck, bridgeSide.GetStream(), filter, () => _forwarding, () => new ComboWithholdingOptions(BackAndStart: true),
            PuckDescriptors.Triton, line => { lock (_log) { _log.Add(line); } });
        _bridge.Start();
    }

    public async Task DisposeAsync()
    {
        await _bridge.DisposeAsync();
        _viiper.Dispose();
    }

    [Fact]
    [Trait("TestCase", "T242-2")]
    public async Task While_the_copy_rests_Steam_reads_the_sticks_at_rest_and_while_it_passes_every_input()
    {
        _puck.Send(Hid, StateReport(buttons: 0x00000001, stick: 0x55));
        var resting = await ReadFrameAsync();

        _forwarding = true;
        _puck.Send(Hid, StateReport(buttons: 0x00000001, stick: 0x55));
        var passing = await ReadFrameAsync();

        Assert.Equal((HidProxyFrames.Input, Hid), (resting.Kind, resting.Interface));
        Assert.All(resting.Payload[6..18], value => Assert.Equal(0, value));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(resting.Payload.AsSpan(2)));
        Assert.All(passing.Payload[6..18], value => Assert.Equal(0x55, value));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(passing.Payload.AsSpan(2)));
    }

    [Fact]
    [Trait("TestCase", "T242-1")]
    public async Task An_allowed_feature_reaches_the_puck_and_Steam_reads_its_answer()
    {
        _puck.Answer = [0x01, 0x83, 0x2A];

        var set = await ControlAsync(1, SetReport(HidReportType.Feature), [0x01, 0x83]);
        var get = await ControlAsync(2, GetReport(length: 64));

        Assert.True(set.Done);
        Assert.Empty(set.Data);
        Assert.True(get.Done);
        Assert.Equal([0x01, 0x83, 0x2A], get.Data);
        Assert.Equal([new byte[] { 0x01, 0x83 }], _puck.Features);
        Assert.Empty(_updates);
    }

    [Fact]
    [Trait("TestCase", "T242-1")]
    public async Task A_firmware_message_never_reaches_the_puck_Steam_sees_it_done_and_it_counts_as_an_update()
    {
        var reply = await ControlAsync(1, SetReport(HidReportType.Feature), [0x01, 0xB7, 0x00, 0x10]);

        Assert.True(reply.Done);
        Assert.Empty(reply.Data);
        Assert.Empty(_puck.Features);
        Assert.Single(_updates);
    }

    [Theory]
    [Trait("TestCase", "T242-8")]
    [InlineData(0x21, 0x0A, Hid)] // SET_IDLE
    [InlineData(0x21, 0x0B, Hid)] // SET_PROTOCOL
    [InlineData(0xA1, 0x02, Hid)] // GET_IDLE
    [InlineData(0xA1, 0x03, Hid)] // GET_PROTOCOL
    [InlineData(0x21, 0x20, Cdc)] // CDC SET_LINE_CODING
    [InlineData(0x21, 0x22, Cdc)] // CDC SET_CONTROL_LINE_STATE
    public async Task What_Windows_says_to_any_device_of_the_class_is_answered_and_never_counts_as_an_update(byte requestType, byte request, byte iface)
    {
        var reply = await ControlAsync(1, Setup(requestType, request, 0, iface, 0));

        Assert.True(reply.Done);
        Assert.Empty(_puck.Features);
        Assert.Empty(_puck.Outputs);
        Assert.Empty(_updates);
    }

    [Theory]
    [Trait("TestCase", "T242-1")]
    [InlineData(0x40, 0x01)] // vendor, to the device
    [InlineData(0x41, 0x01)] // vendor, to the interface
    [InlineData(0x21, 0x55)] // an unknown HID class request
    public async Task A_vendor_or_unknown_request_is_refused_and_counts_as_an_update(byte requestType, byte request)
    {
        var reply = await ControlAsync(1, Setup(requestType, request, 0, Hid, 0));

        Assert.False(reply.Done);
        Assert.Empty(_puck.Features);
        Assert.Single(_updates);
    }

    [Fact]
    [Trait("TestCase", "T242-3")]
    public async Task Haptics_reach_the_puck_only_while_the_copy_passes_input()
    {
        await SendAsync(HidProxyFrames.Output, Hid, [0x80, 0x01, 0x02]);
        // VIIPER's frames are handled in order: once SET_IDLE is answered, the first output was decided while resting.
        await ControlAsync(1, Setup(0x21, 0x0A, 0, Hid, 0));
        _forwarding = true;
        await SendAsync(HidProxyFrames.Output, Hid, [0x80, 0x03, 0x04]);

        await WaitUntilAsync(() => _puck.Outputs.Count == 1);
        Assert.Equal([new byte[] { 0x80, 0x03, 0x04 }], _puck.Outputs);
        Assert.Empty(_updates);
    }

    [Fact]
    [Trait("TestCase", "T242-1")]
    public async Task Data_Steam_sends_to_the_serial_interface_never_reaches_the_puck_and_counts_as_an_update()
    {
        _forwarding = true;
        // A haptics id, which would pass on a HID interface: on the serial interface nothing passes.
        await SendAsync(HidProxyFrames.Output, Cdc, [0x80, 0x01, 0x02]);
        await ControlAsync(1, Setup(0x21, 0x0A, 0, Hid, 0));

        Assert.Empty(_puck.Outputs);
        Assert.Single(_updates);
    }

    [Fact]
    [Trait("TestCase", "T242-1")]
    public async Task An_output_report_the_allow_list_doesnt_name_never_reaches_the_puck_and_counts_as_an_update()
    {
        _forwarding = true;
        await SendAsync(HidProxyFrames.Output, Hid, [0x42, 0x01, 0x02]);
        var reply = await ControlAsync(1, Setup(0x21, 0x09, (int)HidReportType.Output << 8 | 0x42, Hid, 3), [0x42, 0x03, 0x04]);

        Assert.True(reply.Done);
        Assert.Empty(_puck.Outputs);
        Assert.Equal(2, _updates.Count);
    }

    [Fact]
    [Trait("TestCase", "T242-6")]
    public async Task A_GET_REPORT_the_puck_fails_is_refused_rather_than_answered_with_nothing()
    {
        _puck.Answer = [0x01, 0x83, 0x05];
        _puck.FailGetReport = true;

        var reply = await ControlAsync(1, GetReport(64));

        Assert.False(reply.Done);
        Assert.Empty(reply.Data);
    }

    [Fact]
    [Trait("TestCase", "T314-1")]
    public async Task A_refusal_Steam_repeats_is_logged_once_naming_interface_report_and_message_and_counted_at_close()
    {
        _puck.FeatureFailure = new Win32Exception(31);
        _puck.FailGetReport = true;

        for (ushort sequence = 1; sequence <= 6; sequence += 2)
        {
            Assert.False((await ControlAsync(sequence, SetReport(HidReportType.Feature), [0x01, 0xAE, 0x01])).Done);
            Assert.False((await ControlAsync((ushort)(sequence + 1), GetReport(64))).Done);
        }

        await _bridge.DisposeAsync();

        var refused = Lines("fehlgeschlagen");
        Assert.Equal(2, refused.Count);
        Assert.Contains("Feature-Report (Interface 2, Report 0x01, GET_STRING_ATTRIBUTE (0xAE)) an den Puck fehlgeschlagen (Fehler 31", refused[0]);
        Assert.Contains("GET_REPORT Feature (Interface 2, Report 0x01, nach GET_STRING_ATTRIBUTE (0xAE)) an den Puck fehlgeschlagen (IOException", refused[1]);
        var summary = Assert.Single(Lines("vom Puck abgelehnt"));
        Assert.Contains("GET_STRING_ATTRIBUTE (0xAE)) Fehler 31: 3×", summary);
        Assert.Contains("nach GET_STRING_ATTRIBUTE (0xAE)) IOException: 3×", summary);
    }

    [Fact]
    [Trait("TestCase", "T314-1")]
    public async Task Another_message_or_another_error_from_the_puck_still_gets_its_own_line()
    {
        _puck.FeatureFailure = new Win32Exception(31);
        await ControlAsync(1, SetReport(HidReportType.Feature), [0x01, 0xAE, 0x01]);
        await ControlAsync(2, SetReport(HidReportType.Feature), [0x01, 0x87, 0x03]);
        _puck.FeatureFailure = new Win32Exception(1167);
        await ControlAsync(3, SetReport(HidReportType.Feature), [0x01, 0xAE, 0x01]);

        var refused = Lines("fehlgeschlagen");
        Assert.Equal(3, refused.Count);
        Assert.Contains("SET_SETTINGS_VALUES (0x87)", refused[1]);
        Assert.Contains("Fehler 1167", refused[2]);
    }

    [Fact]
    [Trait("TestCase", "T314-1")]
    public async Task The_same_refusal_on_another_slot_or_report_gets_its_own_line_and_its_own_count()
    {
        _puck.FeatureFailure = new Win32Exception(31);
        await ControlAsync(1, SetReport(HidReportType.Feature, Hid), [0x01, 0xAE, 0x01]);
        await ControlAsync(2, SetReport(HidReportType.Feature, EmptySlot), [0x01, 0xAE, 0x01]);
        await ControlAsync(3, SetReport(HidReportType.Feature, EmptySlot), [0x01, 0xAE, 0x01]);
        await ControlAsync(4, SetReport(HidReportType.Feature, EmptySlot), [0x02, 0xAE, 0x01]);

        await _bridge.DisposeAsync();

        var refused = Lines("fehlgeschlagen");
        Assert.Equal(3, refused.Count);
        Assert.Contains("(Interface 2, Report 0x01,", refused[0]);
        Assert.Contains("(Interface 3, Report 0x01,", refused[1]);
        Assert.Contains("(Interface 3, Report 0x02,", refused[2]);
        var summary = Assert.Single(Lines("vom Puck abgelehnt"));
        Assert.Contains("(Interface 2, Report 0x01, GET_STRING_ATTRIBUTE (0xAE)) Fehler 31: 1×", summary);
        Assert.Contains("(Interface 3, Report 0x01, GET_STRING_ATTRIBUTE (0xAE)) Fehler 31: 2×", summary);
        Assert.Contains("(Interface 3, Report 0x02, GET_STRING_ATTRIBUTE (0xAE)) Fehler 31: 1×", summary);
    }

    [Fact]
    [Trait("TestCase", "T314-1")]
    public async Task A_failed_GET_REPORT_names_only_a_message_that_reached_the_puck()
    {
        _puck.FailGetReport = true;
        await ControlAsync(1, SetReport(HidReportType.Feature), [0x01, 0x83]);
        // 0xA3 is dropped (answered done): the GET_REPORT after it still reads the answer to 0x83.
        await ControlAsync(2, SetReport(HidReportType.Feature), [0x01, 0xA3]);
        await ControlAsync(3, GetReport(64));

        var refused = Assert.Single(Lines("fehlgeschlagen"));
        Assert.Contains("nach GET_ATTRIBUTES_VALUES (0x83)", refused);
    }

    [Fact]
    [Trait("TestCase", "T314-1")]
    public async Task A_copy_the_puck_refused_nothing_logs_no_refusals_when_it_closes()
    {
        await ControlAsync(1, SetReport(HidReportType.Feature), [0x01, 0x83]);

        await _bridge.DisposeAsync();

        Assert.Empty(Lines("vom Puck abgelehnt"));
    }

    [Fact]
    [Trait("TestCase", "T242-1")]
    public async Task Once_the_copy_closes_nothing_Steam_sent_reaches_the_puck()
    {
        _puck.HoldFeatures.Reset();
        await SendControlAsync(1, SetReport(HidReportType.Feature), [0x01, 0x83]);
        await WaitUntilAsync(() => _puck.FeatureCalls == 1);
        // The second is already on its way when the copy closes.
        await SendControlAsync(2, SetReport(HidReportType.Feature), [0x01, 0x81]);

        var closing = _bridge.DisposeAsync().AsTask();
        await Task.Delay(100);
        _puck.HoldFeatures.Set();
        await closing.WaitAsync(Wait);

        Assert.Equal(1, _puck.FeatureCalls);
        Assert.False(_bridge.IsConnected);
        Assert.True(_puck.Disposed);
    }

    [Fact]
    [Trait("TestCase", "T242-6")]
    public async Task A_puck_that_breaks_off_marks_the_copy_as_gone()
    {
        _puck.Break(Hid);

        await WaitUntilAsync(() => !_bridge.IsConnected);
        Assert.Contains(_log, line => line.Contains("nicht mehr lesbar", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestCase", "T242-6")]
    public async Task VIIPER_ending_the_stream_marks_the_copy_as_gone()
    {
        _viiper.Client.Shutdown(SocketShutdown.Send);

        await WaitUntilAsync(() => !_bridge.IsConnected);
    }

    [Fact]
    [Trait("TestCase", "T242-2")]
    public async Task Reading_an_input_report_directly_gets_what_the_filter_let_through_never_the_puck_itself()
    {
        var before = await ControlAsync(1, GetReport(HidReportType.Input, SteamControllerCopyFilter.StateReport, 64));
        _puck.Send(Hid, StateReport(buttons: 0x00004041, stick: 0x55));
        var resting = await ReadFrameAsync();

        var read = await ControlAsync(2, GetReport(HidReportType.Input, SteamControllerCopyFilter.StateReport, 64));

        Assert.False(before.Done);
        Assert.True(read.Done);
        Assert.Equal(resting.Payload, read.Data);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(read.Data.AsSpan(2)));
        Assert.All(read.Data[6..18], value => Assert.Equal(0, value));
        Assert.Equal(0, _puck.GetReportCalls);
    }

    [Fact]
    [Trait("TestCase", "T242-2")]
    public async Task A_report_that_passed_while_forwarding_is_no_longer_read_once_the_copy_rests()
    {
        _forwarding = true;
        _puck.Send(Hid, [0x44, 0x01, 0x2A]);
        await ReadFrameAsync();
        var passing = await ControlAsync(1, GetReport(HidReportType.Input, 0x44, 64));

        _forwarding = false;
        _puck.Send(Hid, [0x44, 0x01, 0x2B]);
        // The dropped report sends no frame; the battery report after it marks that the first was handled.
        _puck.Send(Hid, [SteamControllerCopyFilter.BatteryReport, 0x01]);
        await ReadFrameAsync();
        var resting = await ControlAsync(2, GetReport(HidReportType.Input, 0x44, 64));

        Assert.Equal([0x44, 0x01, 0x2A], passing.Data);
        Assert.False(resting.Done);
    }

    [Fact]
    [Trait("TestCase", "T242-1")]
    public async Task A_call_to_the_puck_that_doesnt_end_makes_closing_fail_rather_than_report_it_stopped()
    {
        _puck.HoldFeatures.Reset();
        await SendControlAsync(1, SetReport(HidReportType.Feature), [0x01, 0x83]);
        await WaitUntilAsync(() => _puck.FeatureCalls == 1);

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => _bridge.DisposeAsync().AsTask());
            Assert.False(_bridge.IsConnected);
        }
        finally
        {
            _puck.HoldFeatures.Set();
        }
    }

    [Fact]
    [Trait("TestCase", "T242-6")]
    public async Task A_VIIPER_that_stops_reading_breaks_the_copy_instead_of_hanging_it()
    {
        var puck = new FakePuck([Hid]);
        await using var stalled = new StalledStream();
        var filter = new SteamControllerCopyFilter(new FakeTimeProvider(), _ => { });
        var bridge = new SteamControllerCopyBridge(puck, stalled, filter, () => true, () => new ComboWithholdingOptions(BackAndStart: true),
            PuckDescriptors.Triton, _ => { }, sendTimeout: TimeSpan.FromMilliseconds(200));
        bridge.Start();

        puck.Send(Hid, StateReport(buttons: 0x00000001, stick: 0x55));

        await WaitUntilAsync(() => stalled.Writing);
        await WaitUntilAsync(() => !bridge.IsConnected);
        await WaitUntilAsync(() => stalled.IsDisposed);
        await bridge.DisposeAsync();
    }

    private List<string> Lines(string containing)
    {
        lock (_log)
        {
            return [.. _log.Where(line => line.Contains(containing, StringComparison.Ordinal))];
        }
    }

    private static byte[] StateReport(uint buttons, byte stick)
    {
        var report = new byte[64];
        report[0] = SteamControllerCopyFilter.StateReport;
        report[1] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(report.AsSpan(2), buttons);
        report.AsSpan(6, 12).Fill(stick);
        return report;
    }

    private static byte[] SetReport(HidReportType type, byte iface = Hid) => Setup(0x21, 0x09, (ushort)(((int)type << 8) | 0x01), iface, 2);

    private static byte[] GetReport(ushort length) => GetReport(HidReportType.Feature, 0x01, length);

    private static byte[] GetReport(HidReportType type, byte id, ushort length) => Setup(0xA1, 0x01, (ushort)(((int)type << 8) | id), Hid, length);

    private static byte[] Setup(byte requestType, byte request, ushort value, ushort index, ushort length)
    {
        var setup = new byte[SetupPacket.Size];
        setup[0] = requestType;
        setup[1] = request;
        BinaryPrimitives.WriteUInt16LittleEndian(setup.AsSpan(2), value);
        BinaryPrimitives.WriteUInt16LittleEndian(setup.AsSpan(4), index);
        BinaryPrimitives.WriteUInt16LittleEndian(setup.AsSpan(6), length);
        return setup;
    }

    private async Task<(bool Done, byte[] Data)> ControlAsync(ushort sequence, byte[] setup, byte[]? data = null)
    {
        await SendControlAsync(sequence, setup, data);
        var reply = await ReadFrameAsync();
        Assert.Equal(HidProxyFrames.ControlReply, reply.Kind);
        Assert.Equal(sequence, BinaryPrimitives.ReadUInt16LittleEndian(reply.Payload));
        return (reply.Payload[2] == HidProxyFrames.StatusDone, reply.Payload[3..]);
    }

    private Task SendControlAsync(ushort sequence, byte[] setup, byte[]? data = null)
    {
        var payload = new byte[2 + setup.Length + (data?.Length ?? 0)];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, sequence);
        setup.CopyTo(payload, 2);
        data?.CopyTo(payload, 2 + setup.Length);
        return SendAsync(HidProxyFrames.Control, (byte)BinaryPrimitives.ReadUInt16LittleEndian(setup.AsSpan(4)), payload);
    }

    private async Task SendAsync(byte kind, byte iface, byte[] payload)
    {
        var frame = new byte[HidProxyFrames.HeaderSize + payload.Length];
        HidProxyFrames.Write(frame, kind, iface, payload);
        await _steam.WriteAsync(frame);
    }

    private async Task<HidProxyFrame> ReadFrameAsync()
    {
        using var timeout = new CancellationTokenSource(Wait);
        return await HidProxyFrames.ReadAsync(_steam, timeout.Token) ?? throw new EndOfStreamException();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var until = DateTime.UtcNow + Wait;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, "timed out");
            await Task.Delay(10);
        }
    }

    /// <summary>A puck whose reports the test sends, recording what reaches it.</summary>
    private sealed class FakePuck(byte[] interfaces) : IRealPuck
    {
        private readonly ConcurrentDictionary<byte, BlockingCollection<byte[]?>> _reports = new(interfaces.Select(i => KeyValuePair.Create(i, new BlockingCollection<byte[]?>())));
        private readonly CancellationTokenSource _closed = new();
        private int _featureCalls;
        private int _getReportCalls;

        public IReadOnlyCollection<byte> Interfaces => interfaces;

        public ConcurrentQueue<byte[]> FeatureQueue { get; } = new();

        public List<byte[]> Features => [.. FeatureQueue];

        public ConcurrentQueue<byte[]> OutputQueue { get; } = new();

        public List<byte[]> Outputs => [.. OutputQueue];

        public int FeatureCalls => Volatile.Read(ref _featureCalls);

        public ManualResetEventSlim HoldFeatures { get; } = new(true);

        public byte[] Answer { get; set; } = [];

        public bool Disposed => _closed.IsCancellationRequested;

        public void Send(byte iface, byte[] report) => _reports[iface].Add(report);

        /// <summary>The next read throws, as when the puck is unplugged.</summary>
        public void Break(byte iface) => _reports[iface].Add(null);

        public int ReadInput(byte iface, Span<byte> report)
        {
            byte[]? next;
            try
            {
                next = _reports[iface].Take(_closed.Token);
            }
            catch (OperationCanceledException)
            {
                return 0;
            }

            if (next is null)
            {
                throw new IOException("Das Gerät ist nicht verbunden.");
            }

            next.CopyTo(report);
            return next.Length;
        }

        public void SetFeature(byte iface, ReadOnlySpan<byte> report)
        {
            Interlocked.Increment(ref _featureCalls);
            FeatureQueue.Enqueue(report.ToArray());
            if (FeatureFailure is { } failure)
            {
                throw failure;
            }

            // Longer than the bridge waits when it closes (twice 2 s plus the frame loop), so a held call outlasts it.
            HoldFeatures.Wait(TimeSpan.FromSeconds(30));
        }

        public void WriteOutput(byte iface, ReadOnlySpan<byte> report) => OutputQueue.Enqueue(report.ToArray());

        /// <summary>Every feature report throws this, as when the puck refuses it (Windows' error 31 for an empty slot).</summary>
        public Exception? FeatureFailure { get; set; }

        /// <summary>The next GET_REPORT throws, as when the puck doesn't answer.</summary>
        public bool FailGetReport { get; set; }

        public int GetReportCalls => Volatile.Read(ref _getReportCalls);

        public int GetReport(byte iface, HidReportType type, byte id, Span<byte> report)
        {
            Interlocked.Increment(ref _getReportCalls);
            if (FailGetReport)
            {
                throw new IOException("Das Gerät antwortet nicht.");
            }

            Answer.CopyTo(report);
            return Answer.Length;
        }

        public void Dispose() => _closed.Cancel();
    }
    /// <summary>A VIIPER that stopped reading: every write hangs until the stream is closed; reads wait for the close.</summary>
    private sealed class StalledStream : Stream
    {
        private readonly ManualResetEventSlim _closed = new();
        private readonly TaskCompletionSource _closedTask = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private volatile bool _writing;

        public bool Writing => _writing;

        public bool IsDisposed => _closed.IsSet;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _writing = true;
            _closed.Wait();
            throw new IOException("Der Stream wurde geschlossen.");
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _closedTask.Task.WaitAsync(cancellationToken);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            _closed.Set();
            _closedTask.TrySetResult();
            base.Dispose(disposing);
        }
    }
}
