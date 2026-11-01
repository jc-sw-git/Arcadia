using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Arcadia.ControllerSupport;

/// <summary>How VIIPER answered a request: as asked, with an error (e.g. "authentication required"), or not at all.</summary>
public enum ViiperAnswer
{
    Answered,
    Refused,
    NoAnswer,
}

/// <summary>
/// A connection to Arcadia's VIIPER on <c>127.0.0.1</c> (#304), authenticated with its key and encrypted: VIIPER's
/// protocol "VIIPER-Auth-v1" as its MIT client implements it (<c>Viiper.Client</c> 0.9.0, whose classes for it are
/// internal). Arcadia needs it itself for the copy's raw device stream and to tell refused from unanswered.
/// </summary>
public static class ViiperConnection
{
    private const string HandshakeMagic = "eVI1\0";
    private const int NonceSize = 32;
    private const int Pbkdf2Iterations = 100_000;
    private static readonly byte[] AuthContext = Encoding.UTF8.GetBytes("VIIPER-Auth-v1");
    private static readonly byte[] SessionContext = Encoding.UTF8.GetBytes("VIIPER-Session-v1");
    private static readonly byte[] Pbkdf2Salt = Encoding.UTF8.GetBytes("VIIPER-Key-v1");
    private static readonly byte[] Accepted = "OK\0"u8.ToArray();

    /// <summary>The key VIIPER derives from the text in its key file.</summary>
    public static byte[] DeriveKey(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), Pbkdf2Salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);
    }

    /// <summary>
    /// Connects to <c>127.0.0.1:<paramref name="port"/></c>, never "localhost", which may reach another program on
    /// <c>::1</c>. With a key the handshake runs first and the stream is encrypted; throws <see cref="ViiperRefusedException"/>
    /// when VIIPER answers the handshake with an error. The stream owns the connection.
    /// </summary>
    public static async Task<Stream> ConnectAsync(int port, byte[]? key, CancellationToken cancellationToken)
    {
        var tcp = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
        try
        {
            await tcp.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
            Stream stream = new OwningStream(tcp.GetStream(), tcp);
            return key is null ? stream : await HandshakeAsync(stream, key, cancellationToken);
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
    }

    /// <summary>
    /// VIIPER's <c>ping</c>, with or without a key: <see cref="ViiperAnswer.Answered"/> only for VIIPER's own answer,
    /// <see cref="ViiperAnswer.Refused"/> for an error it sent, <see cref="ViiperAnswer.NoAnswer"/> for anything else
    /// (nothing listens, the connection broke, the time ran out, another protocol).
    /// </summary>
    public static async Task<ViiperAnswer> PingAsync(int port, byte[]? key, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            await using var stream = await ConnectAsync(port, key, deadline.Token);
            await stream.WriteAsync("ping\0"u8.ToArray(), deadline.Token);
            var answer = await ReadToEndAsync(stream, deadline.Token);
            return answer.StartsWith("{\"status\":", StringComparison.Ordinal) ? ViiperAnswer.Refused
                : answer.Contains("\"server\":\"VIIPER\"", StringComparison.Ordinal) ? ViiperAnswer.Answered
                : ViiperAnswer.NoAnswer;
        }
        catch (ViiperRefusedException)
        {
            return ViiperAnswer.Refused;
        }
        catch (Exception ex) when (ex is IOException or SocketException or CryptographicException or InvalidDataException
                                       || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return ViiperAnswer.NoAnswer;
        }
    }

    private static async Task<Stream> HandshakeAsync(Stream stream, byte[] key, CancellationToken cancellationToken)
    {
        var clientNonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = HMACSHA256.HashData(key, (byte[])[.. AuthContext, .. clientNonce]);
        await stream.WriteAsync((byte[])[.. Encoding.UTF8.GetBytes(HandshakeMagic), .. clientNonce, .. tag], cancellationToken);

        var prefix = new byte[Accepted.Length];
        if (await stream.ReadAtLeastAsync(prefix, prefix.Length, throwOnEndOfStream: false, cancellationToken) < prefix.Length)
        {
            // VIIPER closes the connection on a wrong key without a word (seen with the fork's build, 2026-10-29).
            throw new ViiperRefusedException();
        }

        if (!prefix.AsSpan().SequenceEqual(Accepted))
        {
            // VIIPER's error is one JSON line; only whether it came matters, not its text.
            await ReadToEndAsync(stream, cancellationToken);
            throw new ViiperRefusedException();
        }

        var serverNonce = new byte[NonceSize];
        await stream.ReadExactlyAsync(serverNonce, cancellationToken);
        var sessionKey = SHA256.HashData((byte[])[.. key, .. serverNonce, .. clientNonce, .. SessionContext]);
        return new ViiperEncryptedStream(stream, sessionKey);
    }

    private static async Task<string> ReadToEndAsync(Stream stream, CancellationToken cancellationToken)
    {
        // A ping answer or an error is one short line; more is not VIIPER.
        var buffer = new byte[4096];
        var length = 0;
        int read;
        while (length < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken)) > 0)
        {
            length += read;
        }

        return Encoding.UTF8.GetString(buffer, 0, length);
    }

    /// <summary>A stream that also closes the connection it reads from.</summary>
    private sealed class OwningStream(NetworkStream inner, IDisposable owner) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);

        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => inner.WriteAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                owner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

/// <summary>VIIPER answered the handshake with an error: the key is wrong, or it takes none.</summary>
public sealed class ViiperRefusedException() : IOException("VIIPER hat die Anmeldung abgelehnt");

/// <summary>
/// VIIPER's encrypted framing after the handshake: each write is one packet (4-byte big-endian length, 12-byte nonce
/// with a big-endian counter in its last 8 bytes, ChaCha20-Poly1305 ciphertext and tag). Packets from VIIPER must come
/// with its counter in order from 0, so a replayed or reordered packet is refused.
/// </summary>
public sealed class ViiperEncryptedStream : Stream
{
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int MaxPacket = 2 * 1024 * 1024;

    private readonly Stream _inner;
    // One each way: an instance isn't documented as safe for a write and a read at the same time.
    private readonly ChaCha20Poly1305 _encrypt;
    private readonly ChaCha20Poly1305 _decrypt;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private ulong _sent;
    private ulong _received;
    private byte[] _plain = [];
    private int _plainAt;

    public ViiperEncryptedStream(Stream inner, byte[] sessionKey)
    {
        _inner = inner;
        _encrypt = new ChaCha20Poly1305(sessionKey);
        _decrypt = new ChaCha20Poly1305(sessionKey);
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        // One packet per write, written whole: the copy's IN reports and the bridge's other writes may come from two tasks.
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            var packet = new byte[4 + NonceLength + buffer.Length + TagLength];
            BinaryPrimitives.WriteUInt32BigEndian(packet, (uint)(NonceLength + buffer.Length + TagLength));
            var nonce = packet.AsSpan(4, NonceLength);
            BinaryPrimitives.WriteUInt64BigEndian(nonce[4..], _sent++);
            _encrypt.Encrypt(nonce, buffer.Span, packet.AsSpan(4 + NonceLength, buffer.Length), packet.AsSpan(4 + NonceLength + buffer.Length, TagLength));
            await _inner.WriteAsync(packet, cancellationToken);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_plainAt >= _plain.Length)
        {
            var header = new byte[4];
            if (await _inner.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken) < header.Length)
            {
                return 0;
            }

            var length = BinaryPrimitives.ReadUInt32BigEndian(header);
            if (length is < NonceLength + TagLength or > MaxPacket)
            {
                throw new InvalidDataException($"VIIPER-Paket mit {length} Bytes");
            }

            var packet = new byte[length];
            await _inner.ReadExactlyAsync(packet, cancellationToken);
            var nonce = packet.AsSpan(0, NonceLength);
            if (BinaryPrimitives.ReadUInt64BigEndian(nonce[4..]) != _received)
            {
                throw new InvalidDataException("VIIPER-Paket außer der Reihe");
            }

            var plain = new byte[length - NonceLength - TagLength];
            _decrypt.Decrypt(nonce, packet.AsSpan(NonceLength, plain.Length), packet.AsSpan(NonceLength + plain.Length), plain);
            _received++;
            _plain = plain;
            _plainAt = 0;
        }

        var count = Math.Min(buffer.Length, _plain.Length - _plainAt);
        _plain.AsMemory(_plainAt, count).CopyTo(buffer);
        _plainAt += count;
        return count;
    }

    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Write(byte[] buffer, int offset, int count) => WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush() => _inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
            _encrypt.Dispose();
            _decrypt.Dispose();
            _writeGate.Dispose();
        }

        base.Dispose(disposing);
    }
}
