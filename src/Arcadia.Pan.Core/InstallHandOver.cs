using System.Text;

namespace Arcadia.Pan;

/// <summary>The installers Arcadia hands over after the prompt; each null when the run doesn't install it.</summary>
/// <param name="UsbipSha256">The hash Arcadia checked at the download; Pan runs only that file.</param>
/// <param name="HidHide">HidHide's path only: its hash stays the pinned <see cref="HidHideRelease.Sha256"/>.</param>
public sealed record HandedOverInstallers(string? Usbip, string? UsbipSha256, string? HidHide);

/// <summary>How the wait for Arcadia's hand-over ended.</summary>
public enum HandOverEnd
{
    /// <summary><c>go</c> with valid installers: Pan installs.</summary>
    Go,

    /// <summary>Arcadia sent <c>abort</c>: a step after the prompt failed (download, Steam, a game, a timeout).</summary>
    Abort,

    /// <summary>The pipe broke or Arcadia's process ended before <c>go</c>.</summary>
    ArcadiaGone,

    /// <summary>No <c>go</c> within <see cref="PanTimeouts.HandOver"/>: Arcadia hangs.</summary>
    Timeout,

    /// <summary>The pipe's name was taken before Pan made it, or Arcadia's process isn't the one that started Pan.</summary>
    PipeTaken,

    /// <summary>The message was malformed, too long, or named other installers or paths than announced.</summary>
    Refused,
}

/// <param name="Reason">For <see cref="HandOverEnd.Refused"/>: what was wrong, for <c>pan.log</c>.</param>
public sealed record HandOverResult(HandOverEnd End, HandedOverInstallers? Installers = null, string? Reason = null);

/// <summary>Pan's side of the pipe for one run, as far as it is Windows' (Arcadia.Pan/Windows).</summary>
public interface IHandOverChannel
{
    /// <summary>Completes when Arcadia's process (<see cref="InstallRun.ClientProcessId"/>) has ended.</summary>
    Task ClientGone { get; }

    /// <summary>
    /// Waits for Arcadia's process to connect and reads its one message to the end of the connection: at most
    /// <paramref name="maxBytes"/> bytes plus one, so a longer one shows. Null when the connection broke without a byte.
    /// </summary>
    Task<byte[]?> ReceiveAsync(int maxBytes, CancellationToken cancellationToken);
}

/// <summary>
/// The hand-over of D7 option A (#413): elevated Pan waits for Arcadia's one message over a per-run pipe, then installs
/// what it brings. Ends without changing anything (exit 9) on <c>abort</c>, a gone Arcadia or the timeout; refuses a
/// message that doesn't match the command line (exit 3). The hand-over carries no more trust than the command line did:
/// both come from Arcadia's unelevated process; the installers are still copied and checked by hash and signature.
/// </summary>
public static class InstallHandOver
{
    /// <summary>Pan's exit code when it ended before the hand-over without changing anything.</summary>
    public const int EndedExitCode = 9;

    public const int MaxMessageBytes = 4096;

    public const string GoLine = "go";
    public const string AbortLine = "abort";
    private const string UsbipKey = "usbip=";
    private const string UsbipShaKey = "usbip-sha256=";
    private const string HidHideKey = "hidhide=";

    public static string PipeName(string runId) => $"Arcadia.Pan.Install.{runId}";

    /// <summary>Arcadia's <c>go</c>, one key per line.</summary>
    public static byte[] Go(HandedOverInstallers installers)
    {
        var text = new StringBuilder(GoLine).Append('\n');
        if (installers.Usbip is { } usbip)
        {
            text.Append(UsbipKey).Append(usbip).Append('\n').Append(UsbipShaKey).Append(installers.UsbipSha256).Append('\n');
        }

        if (installers.HidHide is { } hidHide)
        {
            text.Append(HidHideKey).Append(hidHide).Append('\n');
        }

        return Encoding.UTF8.GetBytes(text.ToString());
    }

    public static byte[] Abort() => Encoding.UTF8.GetBytes(AbortLine + "\n");

    /// <summary>
    /// Waits for Arcadia's message; nothing on the machine changes meanwhile. A message wins over Arcadia's end only when
    /// it was read in full first, so an <c>abort</c> right before Arcadia closes still reads as one.
    /// </summary>
    public static async Task<HandOverResult> WaitAsync(IHandOverChannel channel, InstallRun run, TimeSpan timeout, TimeProvider time,
        Func<string, bool> isReparsePoint, CancellationToken cancellationToken = default)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receive = channel.ReceiveAsync(MaxMessageBytes, stop.Token);
        var timer = Task.Delay(timeout, time, stop.Token);
        var first = await Task.WhenAny(receive, channel.ClientGone, timer);
        try
        {
            if (first == receive)
            {
                byte[]? message;
                try
                {
                    message = await receive;
                }
                // A broken or refused pipe gives no installers, so Pan ends as without Arcadia: nothing changed (exit 9).
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    return new(HandOverEnd.ArcadiaGone);
                }

                return message is null ? new(HandOverEnd.ArcadiaGone) : Read(message, run, isReparsePoint);
            }

            return first == timer ? new(HandOverEnd.Timeout) : new(HandOverEnd.ArcadiaGone);
        }
        finally
        {
            await stop.CancelAsync();
            await Task.WhenAll(receive, timer).ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }

    /// <summary>Checks one message against the run's command line; see the class for what is refused.</summary>
    public static HandOverResult Read(byte[] message, InstallRun run, Func<string, bool> isReparsePoint)
    {
        if (message.Length > MaxMessageBytes)
        {
            return Refused($"message over {MaxMessageBytes} bytes");
        }

        string text;
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(message);
        }
        catch (DecoderFallbackException)
        {
            return Refused("message is not UTF-8");
        }

        var lines = text.Split('\n');
        if (lines[^1].Length != 0)
        {
            return Refused("message not ended by a line break");
        }

        lines = lines[..^1];
        if (lines is [AbortLine])
        {
            return new(HandOverEnd.Abort);
        }

        if (lines.Length == 0 || lines[0] != GoLine)
        {
            return Refused("neither go nor abort");
        }

        string? usbip = null, usbipSha = null, hidHide = null;
        foreach (var line in lines.Skip(1))
        {
            if (!TryTake(line, UsbipKey, ref usbip) && !TryTake(line, UsbipShaKey, ref usbipSha) && !TryTake(line, HidHideKey, ref hidHide))
            {
                return Refused($"unknown or repeated line '{Shorten(line)}'");
            }
        }

        if (run.Usbip != (usbip is not null) || (usbip is null) != (usbipSha is null))
        {
            return Refused("usbip-win2's installer differs from what the command line announced");
        }

        if (run.HidHide != (hidHide is not null))
        {
            return Refused("HidHide's installer differs from what the command line announced");
        }

        if (usbipSha is not null && (usbipSha.Length != 64 || !usbipSha.All(Uri.IsHexDigit)))
        {
            return Refused("usbip-win2's hash is not a SHA-256");
        }

        foreach (var path in new[] { usbip, hidHide }.OfType<string>())
        {
            if (WhyNotInFolder(path, run.InstallersFolder, isReparsePoint) is { } why)
            {
                return Refused(why);
            }
        }

        return new(HandOverEnd.Go, new HandedOverInstallers(usbip, usbipSha, hidHide));
    }

    /// <summary>A path straight inside the run's folder, as Arcadia downloads there; never through a link or junction.</summary>
    private static string? WhyNotInFolder(string path, string folder, Func<string, bool> isReparsePoint)
    {
        if (!Path.IsPathFullyQualified(path) || !string.Equals(Path.GetFullPath(path), path, StringComparison.Ordinal))
        {
            return $"path '{Shorten(path)}' is not a plain absolute path";
        }

        if (Path.GetDirectoryName(path) is not { } parent
            || !string.Equals(Path.TrimEndingDirectorySeparator(parent), Path.TrimEndingDirectorySeparator(folder), StringComparison.OrdinalIgnoreCase))
        {
            return $"path '{Shorten(path)}' is outside the run's folder";
        }

        if (isReparsePoint(folder) || isReparsePoint(path))
        {
            return $"path '{Shorten(path)}' goes through a link or junction";
        }

        return null;
    }

    private static bool TryTake(string line, string key, ref string? value)
    {
        if (!line.StartsWith(key, StringComparison.Ordinal) || value is not null || line.Length == key.Length)
        {
            return false;
        }

        value = line[key.Length..];
        return true;
    }

    private static HandOverResult Refused(string reason) => new(HandOverEnd.Refused, Reason: reason);

    private static string Shorten(string text) => text.Length > 80 ? text[..80] + "…" : text;
}
