using System.Globalization;
using System.Text.RegularExpressions;

namespace Arcadia.Pan;

/// <summary>
/// Reads the output of usbip-win2's <c>usbip port</c>, which lists each imported device as a
/// <c>Port NN:</c> line followed by indented lines, one of them <c>-&gt; usbip://host:port/busid</c>.
/// Used by Arcadia (SISR's leftovers) and by Pan (the teardown, #243).
/// </summary>
public static partial class UsbipPorts
{
    /// <summary>VIIPER's default usbip port; it attaches its controllers from this machine.</summary>
    public const int ViiperPort = 3241;

    /// <summary>How this machine can appear in an imported device's address.</summary>
    private static readonly string[] LocalHosts = ["localhost", "127.0.0.1", "[::1]"];

    /// <summary>The port numbers of the devices imported from VIIPER on this machine.</summary>
    public static IReadOnlyList<int> FromLocalViiper(string portOutput) =>
        ServedBy(portOutput, LocalHosts.Select(host => $"{host}:{ViiperPort}").ToArray());

    /// <summary>The port numbers of the devices imported from any of <paramref name="servers"/> (host:port).</summary>
    private static IReadOnlyList<int> ServedBy(string portOutput, string[] servers)
    {
        var ports = new List<int>();
        int? current = null;
        foreach (var line in portOutput.Split('\n'))
        {
            if (PortLine().Match(line) is { Success: true } port)
            {
                current = int.Parse(port.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            else if (current is { } number
                     && servers.Any(server => line.Contains($"usbip://{server}/", StringComparison.OrdinalIgnoreCase))
                     && !ports.Contains(number))
            {
                ports.Add(number);
            }
        }

        return ports;
    }

    [GeneratedRegex(@"^\s*Port\s+(\d+):", RegexOptions.IgnoreCase)]
    private static partial Regex PortLine();
}
