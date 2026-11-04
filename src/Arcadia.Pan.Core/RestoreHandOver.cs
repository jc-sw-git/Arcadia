using System.Globalization;
using System.Text;

namespace Arcadia.Pan;

/// <summary>
/// One "Controller wiederherstellen" with the Windows prompt first (#471): <c>Pan.exe --restore --run &lt;id&gt; --client
/// &lt;pid&gt;</c>. Elevated Pan waits for Arcadia's <c>go</c> before it touches anything, so Arcadia lets go of Pan (which
/// ends its emulation) only once the prompt was accepted. Without <c>--run</c> (by hand) the restore runs at once.
/// </summary>
/// <param name="Id">128-bit random, 32 lower-case hex digits (<see cref="InstallRun.NewId"/>): names the pipe.</param>
/// <param name="ClientProcessId">Arcadia's process: the only one that may connect, and Pan ends when it does.</param>
public sealed record RestoreRun(string Id, int ClientProcessId)
{
    /// <summary>The arguments after <c>--restore</c>.</summary>
    public IReadOnlyList<string> ToArguments() =>
        [InstallRun.RunOption, Id, InstallRun.ClientOption, ClientProcessId.ToString(CultureInfo.InvariantCulture)];

    /// <summary>
    /// The run <paramref name="args"/> announce, or null without <c>--run</c> (a restore by hand). Throws on a half or
    /// malformed run, so Pan never restores from a command line it doesn't understand.
    /// </summary>
    public static RestoreRun? Parse(IReadOnlyList<string> args)
    {
        var id = Option(args, InstallRun.RunOption);
        var client = Option(args, InstallRun.ClientOption);
        if (id is null)
        {
            return client is null ? null : throw new InvalidOperationException("--client needs --run <id>");
        }

        if (!InstallRun.IsValidId(id))
        {
            throw new InvalidOperationException("--run needs 32 lower-case hex digits");
        }

        if (!int.TryParse(client, NumberStyles.None, CultureInfo.InvariantCulture, out var pid) || pid <= 0)
        {
            throw new InvalidOperationException("--run needs --client <pid>");
        }

        return new RestoreRun(id, pid);
    }

    private static string? Option(IReadOnlyList<string> args, string name)
    {
        for (var i = 1; i + 1 < args.Count; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}

/// <summary>
/// The restore's hand-over (#471), on the pipe the install uses (<see cref="HandOverWait"/>) under its own name. The
/// message carries nothing but <c>go</c> or <c>abort</c>: what is undone is Pan's own journal, never Arcadia's word.
/// </summary>
public static class RestoreHandOver
{
    public static string PipeName(string runId) => $"Arcadia.Pan.Restore.{runId}";

    public static byte[] Go() => Encoding.UTF8.GetBytes(InstallHandOver.GoLine + "\n");

    /// <summary>Only the exact <c>go</c> or <c>abort</c> line; anything else is refused (exit 3, nothing restored).</summary>
    public static HandOverResult Read(byte[] message)
    {
        if (message.AsSpan().SequenceEqual(Go()))
        {
            return new(HandOverEnd.Go);
        }

        return message.AsSpan().SequenceEqual(InstallHandOver.Abort())
            ? new(HandOverEnd.Abort)
            : new(HandOverEnd.Refused, Reason: "neither go nor abort");
    }

    public static Task<HandOverResult> WaitAsync(IHandOverChannel channel, TimeSpan timeout, TimeProvider time, CancellationToken cancellationToken = default) =>
        HandOverWait.WaitAsync(channel, timeout, time, Read, cancellationToken);
}
