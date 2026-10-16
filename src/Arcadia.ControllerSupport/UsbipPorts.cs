using System.Globalization;
using System.Text.RegularExpressions;

namespace Arcadia.ControllerSupport;

/// <summary>
/// Reads the output of usbip-win2's <c>usbip port</c>, which lists each imported device as a
/// <c>Port NN:</c> line followed by indented lines, one of them <c>-&gt; usbip://host:port/busid</c>.
/// </summary>
public static partial class UsbipPorts
{
    /// <summary>VIIPER attaches its controllers from <c>localhost</c> on its default usbip port.</summary>
    public const string ViiperServer = "localhost:3241";

    /// <summary>The port numbers of the devices imported from <paramref name="server"/> (host:port).</summary>
    public static IReadOnlyList<int> ServedBy(string portOutput, string server)
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
                     && line.Contains($"usbip://{server}/", StringComparison.OrdinalIgnoreCase)
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
