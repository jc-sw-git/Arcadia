using System.Buffers.Binary;
using System.Collections.Concurrent;
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
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private readonly FakePuck _puck = new([Hid]);
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

    private static byte[] StateReport(uint buttons, byte stick)
    {
        var report = new byte[64];
        report[0] = SteamControllerCopyFilter.StateReport;
        report[1] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(report.AsSpan(2), buttons);
        report.AsSpan(6, 12).Fill(stick);
        return report;
    }

    private static byte[] SetReport(HidReportType type) => Setup(0x21, 0x09, (ushort)(((int)type << 8) | 0x01), Hid, 2);

    private static byte[] GetReport(ushort length) => Setup(0xA1, 0x01, ((int)HidReportType.Feature << 8) | 0x01, Hid, length);

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
            HoldFeatures.Wait(Wait);
        }

        public void WriteOutput(byte iface, ReadOnlySpan<byte> report) => OutputQueue.Enqueue(report.ToArray());

        /// <summary>The next GET_REPORT throws, as when the puck doesn't answer.</summary>
        public bool FailGetReport { get; set; }

        public int GetReport(byte iface, HidReportType type, byte id, Span<byte> report)
        {
            if (FailGetReport)
            {
                throw new IOException("Das Gerät antwortet nicht.");
            }

            Answer.CopyTo(report);
            return Answer.Length;
        }

        public void Dispose() => _closed.Cancel();
    }
}
