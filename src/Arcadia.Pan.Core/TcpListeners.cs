using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arcadia.Pan;

/// <summary>A process listening on a TCP port: who, on which address (IPv4 or IPv6), on which port.</summary>
public sealed record TcpListening(int Pid, IPAddress Address, int Port);

/// <summary>
/// Every TCP listener on this machine, IPv4 and IPv6, from Windows' TCP table: which ports Arcadia's <c>viiper.exe</c>
/// was given, and that nobody else listens on them (#304). Read by Arcadia and by Pan.
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
    private const int Row4AddressAt = 4;
    private const int Row4PortAt = 8;
    private const int Row4PidAt = 20;

    // MIB_TCP6ROW_OWNER_PID: local address (16), scope, local port, remote address (16), scope, remote port, state, pid.
    private const int Row6Size = 56;
    private const int Row6AddressAt = 0;
    private const int Row6PortAt = 20;
    private const int Row6PidAt = 52;

    /// <summary>All listeners of both families; throws when Windows won't say.</summary>
    public static IReadOnlyList<TcpListening> All() =>
        [.. Read(InterNetwork, Row4Size, Row4AddressAt, 4, Row4PortAt, Row4PidAt), .. Read(InterNetworkV6, Row6Size, Row6AddressAt, 16, Row6PortAt, Row6PidAt)];

    private static List<TcpListening> Read(uint family, int rowSize, int addressAt, int addressLength, int portAt, int pidAt)
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

            var listeners = new List<TcpListening>();
            var rows = Marshal.ReadInt32(table);
            var address = new byte[addressLength];
            for (var row = 0; row < rows; row++)
            {
                var at = 4 + (row * rowSize);
                Marshal.Copy(table + at + addressAt, address, 0, addressLength);
                // The port is in network byte order in the low 16 bits.
                var raw = (uint)Marshal.ReadInt32(table, at + portAt);
                var port = (int)(((raw & 0xFF) << 8) | ((raw >> 8) & 0xFF));
                listeners.Add(new(Marshal.ReadInt32(table, at + pidAt), new IPAddress(address), port));
            }

            return listeners;
        }
        finally
        {
            Marshal.FreeHGlobal(table);
        }
    }

    [LibraryImport("iphlpapi.dll")]
    private static partial uint GetExtendedTcpTable(nint table, ref uint size, [MarshalAs(UnmanagedType.Bool)] bool order, uint family, int tableClass, uint reserved);
}
