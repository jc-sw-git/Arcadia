using System.Net;

namespace Arcadia.Pan;

/// <summary>
/// What the TCP listeners must look like for Arcadia to trust its <c>viiper.exe</c> (#304): exactly its two ports, both
/// on <c>127.0.0.1</c>, and no other process on either port number in IPv4 or IPv6. Checked by Arcadia after each start
/// and before each device, and by Pan for the usbip port of each emulation.
/// </summary>
public static class ViiperListeners
{
    /// <summary>VIIPER listens on two ports: usbip and its API.</summary>
    public const int PortsPerServer = 2;

    /// <summary>
    /// The ports of the server with process id <paramref name="server"/>, or why they can't be trusted. Null ports
    /// with no problem: not both listening yet.
    /// </summary>
    public static (int[]? Ports, string? Problem) OfServer(IReadOnlyCollection<TcpListening> all, int server)
    {
        var own = all.Where(listener => listener.Pid == server).ToList();
        if (own.FirstOrDefault(listener => !IPAddress.Loopback.Equals(listener.Address)) is { } open)
        {
            return (null, $"viiper.exe listens on {Describe(open)}, not only on 127.0.0.1");
        }

        var ports = own.Select(listener => listener.Port).Distinct().ToArray();
        if (ports.Length > PortsPerServer || own.Count > PortsPerServer)
        {
            return (null, $"viiper.exe listens on {own.Count} ports instead of {PortsPerServer}");
        }

        if (ports.Length < PortsPerServer)
        {
            return (null, null);
        }

        return ForeignOn(all, ports, [server]) is { } foreign ? (null, foreign) : (ports, null);
    }

    /// <summary>
    /// Why <paramref name="port"/> isn't served by <paramref name="servers"/> alone, or null when it is: someone of them
    /// listens on it, and nobody else does, on any address of either family.
    /// </summary>
    public static string? OnlyServedBy(IReadOnlyCollection<TcpListening> all, int port, IReadOnlyCollection<int> servers)
    {
        if (!all.Any(listener => listener.Port == port && servers.Contains(listener.Pid)))
        {
            return $"no held viiper.exe listens on port {port}";
        }

        return ForeignOn(all, [port], servers);
    }

    private static string? ForeignOn(IReadOnlyCollection<TcpListening> all, int[] ports, IReadOnlyCollection<int> servers) =>
        all.FirstOrDefault(listener => ports.Contains(listener.Port) && !servers.Contains(listener.Pid)) is { } foreign
            ? $"process {foreign.Pid} listens on port {foreign.Port} too ({Describe(foreign)})"
            : null;

    private static string Describe(TcpListening listener) => new IPEndPoint(listener.Address, listener.Port).ToString();
}
