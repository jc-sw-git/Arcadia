using Arcadia.ControllerSupport;
using Arcadia.Input;

namespace Arcadia.Core.Tests;

/// <summary>
/// The virtual Xbox pad's rumble read (#470): a stream VIIPER ended or broke ends the read without an exception, so
/// none leaves Arcadia's code into Viiper.Client's read loop, where the debugger stops every thread.
/// </summary>
public sealed class XboxPadRumbleReaderTests
{
    private readonly List<XboxRumble> _rumbles = [];
    private int _ended;

    private XboxPadRumbleReader Reader(CancellationToken reading) => new(_rumbles.Add, () => _ended++, reading);

    [Fact]
    [Trait("TestCase", "T470-1")]
    public async Task Two_bytes_from_VIIPER_are_the_large_and_the_small_motor()
    {
        await Reader(CancellationToken.None).ReadAsync(new MemoryStream([200, 30]));

        Assert.Equal([new XboxRumble(200, 30)], _rumbles);
        Assert.Equal(0, _ended);
    }

    [Fact]
    [Trait("TestCase", "T470-1")]
    public async Task A_connection_VIIPER_closed_ends_the_read_without_an_exception()
    {
        await Reader(CancellationToken.None).ReadAsync(new FailingStream(new IOException("Unable to read data from the transport connection")));

        Assert.Empty(_rumbles);
        Assert.Equal(1, _ended);
    }

    [Fact]
    [Trait("TestCase", "T470-1")]
    public async Task A_stream_that_ends_halfway_through_a_report_ends_the_read_without_an_exception()
    {
        await Reader(CancellationToken.None).ReadAsync(new MemoryStream([200]));

        Assert.Empty(_rumbles);
        Assert.Equal(1, _ended);
    }

    [Fact]
    [Trait("TestCase", "T470-1")]
    public async Task A_stream_disposed_under_the_read_ends_it_without_an_exception()
    {
        await Reader(CancellationToken.None).ReadAsync(new FailingStream(new ObjectDisposedException("NetworkStream")));

        Assert.Equal(1, _ended);
    }

    [Fact]
    [Trait("TestCase", "T470-1")]
    public async Task An_unplug_cancels_the_read_quietly_and_doesnt_count_as_the_pad_breaking()
    {
        using var reading = new CancellationTokenSource();
        await reading.CancelAsync();

        await Reader(reading.Token).ReadAsync(new FailingStream(new OperationCanceledException(reading.Token)));

        Assert.Empty(_rumbles);
        Assert.Equal(0, _ended);
    }

    private sealed class FailingStream(Exception error) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw error;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException<int>(error);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
