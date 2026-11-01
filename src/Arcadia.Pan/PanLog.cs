using System.Globalization;
using System.Text;

namespace Arcadia.Pan;

/// <summary>
/// Pan's log in <see cref="PanPaths.LogPath"/>: one line per event, rotated at 1 MB so it never grows. A log that can't
/// be written never stops Pan. Lines also go to the console when Pan runs there. The file is written only after
/// <see cref="TrustFolder"/>: the folder is where SYSTEM and the elevated commands write, so nothing lands there before
/// this process has secured or checked it (review, bug 1); lines from before are kept and written then. The folder is
/// never created here.
/// </summary>
public sealed class PanLog(string path, bool console)
{
    private const long MaxBytes = 1_000_000;
    private const int KeptLines = 100;
    private readonly Lock _lock = new();
    private readonly Queue<string> _early = new();
    private bool _fileAllowed;
    private bool _fileStopped;

    /// <summary>The data folder is ours (secured or checked in this process): the file may be written from now on.</summary>
    public void TrustFolder()
    {
        lock (_lock)
        {
            _fileAllowed = !_fileStopped;
            while (_early.TryDequeue(out var line))
            {
                Append(line);
            }
        }
    }

    /// <summary>
    /// The folder is about to be deleted: from now on lines go to the console only. Once it is gone anyone may create
    /// it again (or a junction in its place), and an elevated write by path would follow (overall review, finding 1).
    /// </summary>
    public void StopFileLogging()
    {
        lock (_lock)
        {
            _fileAllowed = false;
            _fileStopped = true;
            _early.Clear();
        }
    }

    public void Write(string line)
    {
        var stamped = $"{DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} {line}";
        if (console)
        {
            Console.WriteLine(stamped);
        }

        lock (_lock)
        {
            if (_fileStopped)
            {
                return;
            }

            if (!_fileAllowed)
            {
                _early.Enqueue(stamped);
                if (_early.Count > KeptLines)
                {
                    _early.Dequeue();
                }

                return;
            }

            Append(stamped);
        }
    }

    private void Append(string stamped)
    {
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
            {
                File.Move(path, path + ".old", overwrite: true);
            }

            File.AppendAllText(path, stamped + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nowhere to say so.
        }
    }
}
