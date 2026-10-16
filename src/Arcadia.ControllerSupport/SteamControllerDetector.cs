using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arcadia.ControllerSupport;

/// <summary>
/// Whether a Steam Controller is plugged in, read from Windows' HID devices. Works whether or not
/// SISR runs: SISR emulates an Xbox 360 pad next to the controller, the controller itself stays a
/// Valve HID device.
/// </summary>
[SupportedOSPlatform("windows")]
public static partial class SteamControllerDetector
{
    private const string ValveVendor = "vid_28de";

    /// <summary>Steam's own virtual gamepad (Steam Input for other pads) also uses Valve's vendor id.</summary>
    private const string SteamVirtualGamepad = "pid_11ff";

    private const uint ListPresentDevices = 0;
    private const uint Success = 0;

    private static readonly Guid HidInterface = new("4D1E55B2-F16F-11CF-88CB-001111000030");

    /// <summary>True if a Valve HID device other than Steam's virtual gamepad is present.</summary>
    /// <remarks>The wireless dongle counts too, even while the controller itself is off.</remarks>
    public static bool IsConnected() =>
        HidDevicePaths().Any(path => path.Contains(ValveVendor, StringComparison.OrdinalIgnoreCase)
                                     && !path.Contains(SteamVirtualGamepad, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> HidDevicePaths()
    {
        var guid = HidInterface;
        if (CM_Get_Device_Interface_List_SizeW(out var length, ref guid, null, ListPresentDevices) != Success || length <= 1)
        {
            return [];
        }

        var buffer = new char[length];
        if (CM_Get_Device_Interface_ListW(ref guid, null, buffer, length, ListPresentDevices) != Success)
        {
            return [];
        }

        // A list of zero-terminated strings, ended by an empty one.
        return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint CM_Get_Device_Interface_List_SizeW(out uint length, ref Guid interfaceClass, string? deviceId, uint flags);

    [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint CM_Get_Device_Interface_ListW(ref Guid interfaceClass, string? deviceId, [Out] char[] buffer, uint length, uint flags);
}
