using Arcadia.ControllerSupport;

namespace Arcadia.Core.Tests;

public class UsbipPortsTests
{
    // Layout of usbip-win2's "usbip port" as assumed from its source (not yet seen on the owner's PC);
    // the second device comes from another usbip server, the third from VIIPER by IP.
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
        Assert.Equal([1, 3], UsbipPorts.FromLocalViiper(Output));

    [Fact]
    public void Nothing_imported_means_nothing_to_detach() =>
        Assert.Empty(UsbipPorts.FromLocalViiper(string.Empty));
}
