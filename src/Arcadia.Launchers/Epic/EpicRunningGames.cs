using System.Text;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Epic;

/// <summary>
/// Epic's running games, from the launcher's current log: a <c>Started tracking playtime</c> line without its
/// <c>Stopped</c> line (see <see cref="EpicLauncherLog"/>). Each poll reads only what the launcher added to the log since
/// the last one, so a poll beside a running game costs one look at the file's length. Epic starts a new log with every
/// launcher start; a log whose first bytes changed is read from its beginning.
/// </summary>
public sealed class EpicRunningGames(string? logDirectory = null) : IRunningGamesMonitor
{
    private const string LogName = "EpicGamesLauncher.log";

    /// <summary>Holds the log's first line with its time, so a new log never starts like the old one.</summary>
    private const int HeadLength = 128;

    private const int ChunkSize = 64 * 1024;

    private static readonly byte[] Marker = Encoding.UTF8.GetBytes(EpicLauncherLog.PlaytimeMarker);

    private readonly string _path = Path.Combine(logDirectory ?? EpicScanner.DefaultLogDirectory(), LogName);
    private readonly Dictionary<string, DateTimeOffset> _open = new(StringComparer.Ordinal);
    private byte[] _head = [];

    // Bytes read up to the end of the last whole line, and the file's length when it was read.
    private long _offset;
    private long _length = -1;

    public string LauncherId => EpicIntegration.Info.Id;

    public IReadOnlyList<RunningGame> Poll(CancellationToken cancellationToken = default)
    {
        try
        {
            ReadNewLines();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The launcher may be rotating its log; what is known stays until the next poll can read it.
        }

        return _open.Select(session => new RunningGame(session.Key, session.Value)).ToList();
    }

    private void ReadNewLines()
    {
        var file = new FileInfo(_path);
        if (!file.Exists)
        {
            StartOver([]);
            _length = -1;
            return;
        }

        if (file.Length == _length)
        {
            return;
        }

        using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var head = new byte[Math.Min(HeadLength, stream.Length)];
        stream.ReadExactly(head);

        // The same log only grows, so what was read of its head before still starts it.
        if (head.Length < _head.Length || !head.AsSpan(0, _head.Length).SequenceEqual(_head) || stream.Length < _offset)
        {
            StartOver(head);
        }

        _head = head;
        _length = stream.Length;
        stream.Position = _offset;
        _offset += ReadWholeLines(stream, OnLine);
    }

    private void StartOver(byte[] head)
    {
        _open.Clear();
        _head = head;
        _offset = 0;
    }

    private void OnLine(ReadOnlySpan<byte> line)
    {
        if (line.IndexOf(Marker) < 0 || EpicLauncherLog.ParsePlaytime(Encoding.UTF8.GetString(line).TrimEnd('\r')) is not { } playtime)
        {
            return;
        }

        if (playtime.Started)
        {
            _open[playtime.AppName] = playtime.Time;
        }
        else
        {
            _open.Remove(playtime.AppName);
        }
    }

    /// <summary>
    /// Hands every line that ends in a line break to <paramref name="line"/>. A last line the launcher is still writing
    /// waits for the next read.
    /// </summary>
    /// <returns>The bytes up to the end of the last whole line.</returns>
    private static long ReadWholeLines(Stream stream, LineHandler line)
    {
        var buffer = new byte[ChunkSize];
        var pending = new MemoryStream();
        long consumed = 0;
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            var chunk = buffer.AsSpan(0, read);
            int end;
            while ((end = chunk.IndexOf((byte)'\n')) >= 0)
            {
                if (pending.Length > 0)
                {
                    pending.Write(chunk[..end]);
                    line(pending.GetBuffer().AsSpan(0, (int)pending.Length));
                    consumed += pending.Length + 1;
                    pending.SetLength(0);
                }
                else
                {
                    line(chunk[..end]);
                    consumed += end + 1;
                }

                chunk = chunk[(end + 1)..];
            }

            pending.Write(chunk);
        }

        return consumed;
    }

    private delegate void LineHandler(ReadOnlySpan<byte> line);
}
