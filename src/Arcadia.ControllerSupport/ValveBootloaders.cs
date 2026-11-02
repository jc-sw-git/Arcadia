using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Arcadia.Pan;

namespace Arcadia.ControllerSupport;

/// <summary>
/// Whether a Steam Controller or its puck is in its bootloader now, i.e. Steam flashes its firmware (#407). Arcadia
/// asks Windows itself, without Pan: before "Treiber installieren" Pan isn't there yet.
/// </summary>
[SupportedOSPlatform("windows")]
public static partial class ValveBootloaders
{
    private const uint FilterEnumerator = 0x1;
    private const uint FilterPresent = 0x100;
    private const uint Success = 0;
    private const uint BufferSmall = 0x1A;

    /// <summary>A bootloader (<see cref="HidingList.IsBootloader"/>) is plugged in; throws when Windows' device list can't be read.</summary>
    public static bool Present() => PresentUsbDevices().Any(HidingList.IsBootloader);

    private static unsafe List<string> PresentUsbDevices()
    {
        const uint flags = FilterEnumerator | FilterPresent;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = CM_Get_Device_ID_List_SizeW(out var length, "USB", flags);
            if (result != Success)
            {
                throw new InvalidOperationException($"device list size not read (CONFIGRET 0x{result:X})");
            }

            var buffer = new char[length];
            fixed (char* chars = buffer)
            {
                result = CM_Get_Device_ID_ListW("USB", chars, length, flags);
            }

            if (result == BufferSmall)
            {
                // A device arrived in between.
                continue;
            }

            if (result != Success)
            {
                throw new InvalidOperationException($"device list not read (CONFIGRET 0x{result:X})");
            }

            return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        throw new InvalidOperationException("device list kept changing");
    }

    [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint CM_Get_Device_ID_List_SizeW(out uint length, string? filter, uint flags);

    [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static unsafe partial uint CM_Get_Device_ID_ListW(string? filter, char* buffer, uint length, uint flags);
}
