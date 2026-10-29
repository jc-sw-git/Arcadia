using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Arcadia.ControllerSupport;

namespace Arcadia.Core.Tests;

/// <summary>
/// Arcadia's side of VIIPER's authenticated, encrypted connection (#304). The server here does VIIPER's part of the
/// handshake, and encrypts with VIIPER's own MIT client's code (internal in <c>Viiper.Client</c> 0.9.0, reached by
/// reflection), so Arcadia's implementation is checked against the reference, not against itself. Checked once against
/// the fork's real server as well (Linux build, 2026-10-29).
/// </summary>
public sealed class ViiperConnectionTests : IAsyncDisposable
{
    private const string Password = "Test0123456789abcdefABCDEF012345";

    private static readonly Assembly Client = typeof(Viiper.Client.ViiperClient).Assembly;

    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private Task _serving = Task.CompletedTask;

    public ViiperConnectionTests() => _listener.Start();

    private int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        try
        {
            await _serving;
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or IOException or ObjectDisposedException)
        {
        }

        _stop.Dispose();
    }

    [Fact]
    [Trait("TestCase", "T304-4")]
    public void The_key_is_derived_as_VIIPERs_own_client_derives_it()
    {
        Assert.Equal(ReferenceDeriveKey(Password), ViiperConnection.DeriveKey(Password));
    }

    [Fact]
    [Trait("TestCase", "T304-4")]
    public async Task With_the_key_VIIPER_answers_through_the_encrypted_stream()
    {
        _serving = ServeAsync(requireAuth: true);

        Assert.Equal(ViiperAnswer.Answered, await Ping(ViiperConnection.DeriveKey(Password)));
    }

    [Fact]
    [Trait("TestCase", "T304-4")]
    public async Task Without_the_key_VIIPERs_error_counts_as_refused()
    {
        _serving = ServeAsync(requireAuth: true);

        Assert.Equal(ViiperAnswer.Refused, await Ping(null));
    }

    [Fact]
    [Trait("TestCase", "T304-4")]
    public async Task A_wrong_key_that_VIIPER_answers_by_closing_counts_as_refused()
    {
        _serving = ServeAsync(requireAuth: true);

        Assert.Equal(ViiperAnswer.Refused, await Ping(new byte[32]));
    }

    [Fact]
    [Trait("TestCase", "T304-5")]
    public async Task A_server_that_takes_no_key_is_seen_answering_without_one()
    {
        // What the positive check after each start must catch: VIIPER started without --api.require-local-host-auth.
        _serving = ServeAsync(requireAuth: false);

        Assert.Equal(ViiperAnswer.Answered, await Ping(null));
    }

    [Fact]
    [Trait("TestCase", "T304-5")]
    public async Task Silence_is_no_answer_not_a_refusal()
    {
        _serving = Task.Run(async () =>
        {
            using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
            await Task.Delay(Timeout.Infinite, _stop.Token);
        });

        Assert.Equal(ViiperAnswer.NoAnswer, await ViiperConnection.PingAsync(Port, null, TimeSpan.FromMilliseconds(300), CancellationToken.None));
    }

    [Fact]
    [Trait("TestCase", "T304-4")]
    public async Task What_Arcadia_encrypts_VIIPERs_side_reads_and_the_other_way_round()
    {
        var sessionKey = RandomNumberGenerator.GetBytes(32);
        using var arcadiaEnd = new MemoryStream();
        await using (var arcadia = new ViiperEncryptedStream(new NonClosing(arcadiaEnd), sessionKey))
        {
            await arcadia.WriteAsync("bus/1/1\0"u8.ToArray());
            await arcadia.WriteAsync(new byte[] { 1, 2, 3 });
        }

        arcadiaEnd.Position = 0;
        var reference = ReferenceEncryptedStream(arcadiaEnd, sessionKey);
        var read = new byte[11];
        await reference.ReadExactlyAsync(read);
        Assert.Equal([.. "bus/1/1\0"u8.ToArray(), 1, 2, 3], read);

        using var viiperEnd = new MemoryStream();
        var writer = ReferenceEncryptedStream(viiperEnd, sessionKey);
        await writer.WriteAsync(new byte[] { 9, 8, 7 });
        viiperEnd.Position = 0;
        await using var arcadiaReader = new ViiperEncryptedStream(viiperEnd, sessionKey);
        var back = new byte[3];
        await arcadiaReader.ReadExactlyAsync(back);
        Assert.Equal([9, 8, 7], back);
    }

    [Fact]
    [Trait("TestCase", "T304-4")]
    public async Task A_replayed_packet_from_the_server_is_refused()
    {
        var sessionKey = RandomNumberGenerator.GetBytes(32);
        using var viiperEnd = new MemoryStream();
        var writer = ReferenceEncryptedStream(viiperEnd, sessionKey);
        await writer.WriteAsync(new byte[] { 1 });
        var packet = viiperEnd.ToArray();
        viiperEnd.Write(packet);   // the same packet (counter 0) once more
        viiperEnd.Position = 0;
        await using var reader = new ViiperEncryptedStream(viiperEnd, sessionKey);

        await reader.ReadExactlyAsync(new byte[1]);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await reader.ReadExactlyAsync(new byte[1]));
    }

    private Task<ViiperAnswer> Ping(byte[]? key) => ViiperConnection.PingAsync(Port, key, TimeSpan.FromSeconds(5), CancellationToken.None);

    /// <summary>One connection as VIIPER's API server handles it (internal/server/api/server.go and auth/handshake.go).</summary>
    private Task ServeAsync(bool requireAuth) => Task.Run(async () =>
    {
        using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
        Stream stream = client.GetStream();
        var first = new byte[5];
        await stream.ReadExactlyAsync(first, _stop.Token);
        if (Encoding.UTF8.GetString(first) == "eVI1\0")
        {
            var clientNonce = new byte[32];
            var tag = new byte[32];
            await stream.ReadExactlyAsync(clientNonce, _stop.Token);
            await stream.ReadExactlyAsync(tag, _stop.Token);
            var key = ReferenceDeriveKey(Password);
            if (!HMACSHA256.HashData(key, (byte[])[.. "VIIPER-Auth-v1"u8.ToArray(), .. clientNonce]).AsSpan().SequenceEqual(tag))
            {
                // The fork's build closes without a word on a wrong key.
                return;
            }

            var serverNonce = RandomNumberGenerator.GetBytes(32);
            await stream.WriteAsync((byte[])[.. "OK\0"u8.ToArray(), .. serverNonce], _stop.Token);
            stream = ReferenceEncryptedStream(stream, ReferenceSessionKey(key, serverNonce, clientNonce));
            first = [];
        }
        else if (requireAuth)
        {
            await stream.WriteAsync("{\"status\":401,\"title\":\"Unauthorized\",\"detail\":\"authentication required\"}\n"u8.ToArray(), _stop.Token);
            return;
        }

        var request = new List<byte>(first);
        var one = new byte[1];
        while (request.Count == 0 || request[^1] != 0)
        {
            await stream.ReadExactlyAsync(one, _stop.Token);
            request.Add(one[0]);
        }

        Assert.Equal("ping\0", Encoding.UTF8.GetString([.. request]));
        await stream.WriteAsync("{\"server\":\"VIIPER\",\"version\":\"test\"}\n"u8.ToArray(), _stop.Token);
        client.Client.Shutdown(SocketShutdown.Send);
    });

    private static byte[] ReferenceDeriveKey(string password) =>
        (byte[])Auth().GetMethod("DeriveKey")!.Invoke(null, [password])!;

    private static byte[] ReferenceSessionKey(byte[] key, byte[] serverNonce, byte[] clientNonce) =>
        (byte[])Auth().GetMethod("DeriveSessionKey")!.Invoke(null, [key, serverNonce, clientNonce])!;

    private static Stream ReferenceEncryptedStream(Stream inner, byte[] sessionKey) =>
        (Stream)Activator.CreateInstance(Client.GetType("Viiper.Client.EncryptedStream", throwOnError: true)!, inner, sessionKey)!;

    private static Type Auth() => Client.GetType("Viiper.Client.ViiperAuth", throwOnError: true)!;

    /// <summary>Keeps the memory stream readable after the encrypted stream on it is disposed.</summary>
    private sealed class NonClosing(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => inner.CanWrite;

        public override long Length => inner.Length;

        public override long Position { get => inner.Position; set => inner.Position = value; }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
    }
}
