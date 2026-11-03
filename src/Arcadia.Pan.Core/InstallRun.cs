using System.Globalization;

namespace Arcadia.Pan;

/// <summary>
/// One driver install with the Windows prompt first (#413, D7): what Arcadia's command line announces before the prompt.
/// It fixes which installers the run may install; the hand-over after the prompt can only fill in their paths
/// (<see cref="InstallHandOver"/>). <c>Pan.exe --install --expect-hash &lt;sha256&gt; --run &lt;id&gt; --client &lt;pid&gt;
/// --installers &lt;folder&gt; [--usbip] [--hidhide [--hidhide-repair]]</c>.
/// </summary>
/// <param name="Id">128-bit random, 32 lower-case hex digits: names the hand-over pipe and the step report.</param>
/// <param name="ClientProcessId">Arcadia's process: the only one that may connect, and Pan ends when it does.</param>
/// <param name="InstallersFolder">Where Arcadia downloads this run's installers (<c>%TEMP%\Arcadia\&lt;id&gt;</c>).</param>
public sealed record InstallRun(string Id, int ClientProcessId, string InstallersFolder, bool Usbip, bool HidHide, bool HidHideRepair)
{
    public const string RunOption = "--run";
    public const string ClientOption = "--client";
    public const string InstallersOption = "--installers";
    public const string UsbipFlag = "--usbip";
    public const string HidHideFlag = "--hidhide";
    public const string HidHideRepairFlag = "--hidhide-repair";

    /// <summary>A fresh id: 16 random bytes, so no other run (or another program) can guess its pipe.</summary>
    public static string NewId() => Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));

    public static bool IsValidId(string? id) => id is { Length: 32 } && id.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    /// <summary>The arguments after <c>--install --expect-hash …</c>.</summary>
    public IReadOnlyList<string> ToArguments()
    {
        var arguments = new List<string> { RunOption, Id, ClientOption, ClientProcessId.ToString(CultureInfo.InvariantCulture), InstallersOption, InstallersFolder };
        if (Usbip)
        {
            arguments.Add(UsbipFlag);
        }

        if (HidHide)
        {
            arguments.Add(HidHideFlag);
            if (HidHideRepair)
            {
                arguments.Add(HidHideRepairFlag);
            }
        }

        return arguments;
    }

    /// <summary>
    /// The run <paramref name="args"/> announce, or null when they announce none (Pan's update, #326: Pan alone, nothing to
    /// wait for). Throws on a half or malformed run, so Pan never installs from a command line it doesn't understand.
    /// </summary>
    public static InstallRun? Parse(IReadOnlyList<string> args)
    {
        var id = Option(args, RunOption);
        if (id is null)
        {
            if (Has(args, UsbipFlag) || Has(args, HidHideFlag) || Option(args, ClientOption) is not null || Option(args, InstallersOption) is not null)
            {
                throw new InvalidOperationException("installers are only taken in a run (--run <id> --client <pid> --installers <folder>)");
            }

            return null;
        }

        if (!IsValidId(id))
        {
            throw new InvalidOperationException("--run needs 32 lower-case hex digits");
        }

        if (!int.TryParse(Option(args, ClientOption), NumberStyles.None, CultureInfo.InvariantCulture, out var client) || client <= 0)
        {
            throw new InvalidOperationException("--run needs --client <pid>");
        }

        var folder = Option(args, InstallersOption);
        if (folder is null || !Path.IsPathFullyQualified(folder))
        {
            throw new InvalidOperationException("--run needs --installers <absolute folder>");
        }

        var hidHide = Has(args, HidHideFlag);
        var repair = Has(args, HidHideRepairFlag);
        if (repair && !hidHide)
        {
            throw new InvalidOperationException("--hidhide-repair needs --hidhide");
        }

        return new InstallRun(id, client, Path.TrimEndingDirectorySeparator(folder), Has(args, UsbipFlag), hidHide, repair);
    }

    private static bool Has(IReadOnlyList<string> args, string flag) => args.Skip(1).Contains(flag, StringComparer.OrdinalIgnoreCase);

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
