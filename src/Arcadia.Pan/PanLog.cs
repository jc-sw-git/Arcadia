using System.Globalization;
using System.Text;

namespace Arcadia.Pan;

/// <summary>
/// Pan's log in <see cref="PanPaths.LogPath"/>: one line per event, rotated at 1 MB so it never grows. A log that can't
/// be written never stops Pan. Lines also go to the console when Pan runs there. The folder is never created here: the
/// install secures it (review, bug 1), and a missing one only loses the line.
/// </summary>
public sealed class PanLog(string path, bool console)
{
    private const long MaxBytes = 1_000_000;
    private readonly Lock _lock = new();

    public void Write(string line)
    {
        var stamped = $"{DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} {line}";
        if (console)
        {
            Console.WriteLine(stamped);
        }

        lock (_lock)
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
}
