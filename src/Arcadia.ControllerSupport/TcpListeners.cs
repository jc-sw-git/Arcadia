using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arcadia.ControllerSupport;

/// <summary>
/// Which processes listen on a TCP port, IPv4 and IPv6 (Windows' TCP table): proof of who serves VIIPER's API on
/// <c>localhost</c>, which resolves to either loopback (review of PR #294).
/// </summary>
[SupportedOSPlatform("windows")]
public static partial class TcpListeners
{
    private const uint InterNetwork = 2;
    private const uint InterNetworkV6 = 23;
    private const int TableOwnerPidListener = 3;
    private const uint ErrorInsufficientBuffer = 122;

    // MIB_TCPROW_OWNER_PID: state, local address, local port, remote address, remote port, owning pid.
    private const int Row4Size = 24;
    private const int Row4PortAt = 8;
    private const int Row4PidAt = 20;

    // MIB_TCP6ROW_OWNER_PID: local address (16), scope, local port, remote address (16), scope, remote port, state, pid.
    private const int Row6Size = 56;
    private const int Row6PortAt = 20;
    private const int Row6PidAt = 52;

    /// <summary>The ids of the processes listening on <paramref name="port"/>; throws when Windows won't say.</summary>
    public static IReadOnlyCollection<int> OwnersOnPort(int port) =>
        [.. Owners(InterNetwork, port, Row4Size, Row4PortAt, Row4PidAt).Concat(Owners(InterNetworkV6, port, Row6Size, Row6PortAt, Row6PidAt)).Distinct()];

    private static List<int> Owners(uint family, int port, int rowSize, int portAt, int pidAt)
    {
        uint size = 0;
        var table = nint.Zero;
        try
        {
            while (true)
            {
                var result = GetExtendedTcpTable(table, ref size, false, family, TableOwnerPidListener, 0);
                if (result == 0)
                {
                    break;
                }

                if (result != ErrorInsufficientBuffer)
                {
                    throw new Win32Exception((int)result);
                }

                Marshal.FreeHGlobal(table);
                table = Marshal.AllocHGlobal((int)size);
            }

            var owners = new List<int>();
            var rows = Marshal.ReadInt32(table);
            for (var row = 0; row < rows; row++)
            {
                var at = 4 + (row * rowSize);
                // The port is in network byte order in the low 16 bits.
                var raw = (uint)Marshal.ReadInt32(table, at + portAt);
                if ((((raw & 0xFF) << 8) | ((raw >> 8) & 0xFF)) == port)
                {
                    owners.Add(Marshal.ReadInt32(table, at + pidAt));
                }
            }

            return owners;
        }
        finally
        {
            Marshal.FreeHGlobal(table);
        }
    }

    [LibraryImport("iphlpapi.dll")]
    private static partial uint GetExtendedTcpTable(nint table, ref uint size, [MarshalAs(UnmanagedType.Bool)] bool order, uint family, int tableClass, uint reserved);
}
