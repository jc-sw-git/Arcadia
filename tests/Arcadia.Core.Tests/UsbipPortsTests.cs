using Arcadia.Pan;
using Arcadia.ControllerSupport;

namespace Arcadia.Core.Tests;

public class UsbipPortsTests
{
    // Layout of usbip-win2's "usbip port" as on the owner's PC (see OwnersPc); the second device comes
    // from another usbip server, the third from VIIPER by IP.
    private const string Output = """
        Imported USB devices
        ====================
        Port 01: device in use at Full Speed(12Mbps)
               Microsoft Corp. : Xbox360 Controller (045e:028e)
               -> usbip://localhost:3241/1-1
                   -> remote bus/dev 001/001
        Port 02: device in use at High Speed(480Mbps)
               unknown vendor : unknown product (1234:5678)
               -> usbip://192.168.1.20:3240/2-1
                   -> remote bus/dev 002/001
        Port 03: device in use at Full Speed(12Mbps)
               Microsoft Corp. : Xbox360 Controller (045e:028e)
               -> usbip://127.0.0.1:3241/1-2
                   -> remote bus/dev 001/002
        """;

    [Fact]
    public void Only_devices_from_VIIPER_are_picked() =>
        Assert.Equal([1, 3], UsbipPorts.FromLocalServer(Output, UsbipPorts.ViiperPort));

    // Copied from the owner's PC while SISR ran (usbip-win2, 2026-10-17).
    private const string OwnersPc = """
        Imported USB devices
        ====================
        Port 01: device in use at Full Speed(12Mbps)
                 Microsoft Corp. : Xbox360 Controller (045e:028e)
                   -> usbip://localhost:3241/1-1
                   -> remote bus/dev: 001/001
                   -> serial:
                   -> mode: zero-copy
        """;

    [Fact]
    public void The_real_output_while_SISR_runs_finds_its_controller() =>
        Assert.Equal([1], UsbipPorts.FromLocalServer(OwnersPc.Replace("\n", "\r\n"), UsbipPorts.ViiperPort));

    [Fact]
    public void Nothing_imported_means_nothing_to_detach() =>
        Assert.Empty(UsbipPorts.FromLocalServer(string.Empty, UsbipPorts.ViiperPort));

    // Arcadia's own VIIPER on a port from Windows (#304), beside another program's VIIPER on its default port.
    private const string BesideAnotherViiper = """
        Imported USB devices
        ====================
        Port 01: device in use at Full Speed(12Mbps)
               Microsoft Corp. : Xbox360 Controller (045e:028e)
               -> usbip://localhost:3241/1-1
                   -> remote bus/dev 001/001
        Port 02: device in use at Full Speed(12Mbps)
               Microsoft Corp. : Xbox360 Controller (045e:028e)
               -> usbip://127.0.0.1:52417/1-1
                   -> remote bus/dev 001/001
        Port 03: device in use at Full Speed(12Mbps)
               Valve Software : unknown product (28de:1304)
               -> usbip://127.0.0.1:52417/2-1
                   -> remote bus/dev 002/001
        Port 04: device in use at Full Speed(12Mbps)
               unknown vendor : unknown product (1234:5678)
               -> usbip://127.0.0.1:5241/1-1
                   -> remote bus/dev 001/001
        """;

    [Fact]
    [Trait("TestCase", "T304-3")]
    public void Only_devices_from_the_given_server_port_are_picked_not_another_VIIPERs_nor_a_port_sharing_its_digits()
    {
        Assert.Equal([2, 3], UsbipPorts.FromLocalServer(BesideAnotherViiper, 52417));
        Assert.Equal([1], UsbipPorts.FromLocalServer(BesideAnotherViiper, UsbipPorts.ViiperPort));
    }
}
