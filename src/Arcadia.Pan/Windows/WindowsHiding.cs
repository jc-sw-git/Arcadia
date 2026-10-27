using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Arcadia.Pan.Windows;

/// <summary>HidHide and the Steam Controller's devices on Windows (#243 part B).</summary>
[SupportedOSPlatform("windows")]
public static class WindowsHiding
{
    public static SteamControllerHiding Create() => new(new HidHideDriver(), new PuckDevices());
}

/// <summary>
/// HidHide through its control device (<see cref="HidHideWire"/>), not its CLI. HidHide lets one program at a time
/// open the device, so every call opens and closes it, and a busy device is retried briefly before it counts.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed unsafe class HidHideDriver : IHidHideDriver
{
    private const string ServiceKey = @"SYSTEM\CurrentControlSet\Services\HidHide";
    private const int BusyTries = 5;
    private static readonly TimeSpan BusyPause = TimeSpan.FromMilliseconds(200);

    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorAccessDenied = 5;
    private const int ErrorSharingViolation = 32;
    private const int ErrorInvalidFunction = 1;
    private const int ErrorNotSupported = 50;

    public Version? InstalledVersion()
    {
        using var key = Registry.LocalMachine.OpenSubKey(ServiceKey);
        if (key?.GetValue("ImagePath") is not string imagePath)
        {
            return null;
        }

        var file = HidHideWire.DriverFile(imagePath, Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        if (file is null || !File.Exists(file))
        {
            return null;
        }

        var info = FileVersionInfo.GetVersionInfo(file);
        return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
    }

    public bool IsServiceRegistered()
    {
        using var key = Registry.LocalMachine.OpenSubKey(ServiceKey);
        return key is not null;
    }

    public IReadOnlyList<string> GetHiddenDevices() => GetList(HidHideWire.GetHiddenList);

    public void SetHiddenDevices(IReadOnlyList<string> instanceIds) => Set(HidHideWire.SetHiddenList, HidHideWire.EncodeList(instanceIds));

    public IReadOnlyList<string> GetAllowedApplications() => GetList(HidHideWire.GetAllowList);

    public void SetAllowedApplications(IReadOnlyList<string> imagePaths) => Set(HidHideWire.SetAllowList, HidHideWire.EncodeList(imagePaths));

    public bool GetCloaking() => GetSwitch(HidHideWire.GetCloaking);

    public void SetCloaking(bool on) => Set(HidHideWire.SetCloaking, [on ? (byte)1 : (byte)0]);

    public bool GetInverse() => GetSwitch(HidHideWire.GetInverse);

    private static List<string> GetList(uint code)
    {
        var device = Open();
        try
        {
            // HidHide's own client asks for the size first, then for the list.
            if (!Native.DeviceIoControl(device, code, null, 0, null, 0, out var needed, 0))
            {
                throw Failed(code);
            }

            if (needed == 0)
            {
                return [];
            }

            // Twice the size: enough whether HidHide counts bytes or characters.
            var buffer = new byte[needed * 2];
            fixed (byte* output = buffer)
            {
                if (!Native.DeviceIoControl(device, code, null, 0, output, (uint)buffer.Length, out var returned, 0))
                {
                    throw Failed(code);
                }

                return HidHideWire.DecodeList(buffer.AsSpan(0, (int)Math.Min(returned, (uint)buffer.Length)));
            }
        }
        finally
        {
            Native.CloseHandle(device);
        }
    }

    private static bool GetSwitch(uint code)
    {
        var device = Open();
        try
        {
            byte value = 0;
            if (!Native.DeviceIoControl(device, code, null, 0, &value, 1, out var returned, 0) || returned != 1)
            {
                throw Failed(code);
            }

            return value != 0;
        }
        finally
        {
            Native.CloseHandle(device);
        }
    }

    private static void Set(uint code, byte[] input)
    {
        var device = Open();
        try
        {
            fixed (byte* data = input)
            {
                if (!Native.DeviceIoControl(device, code, data, (uint)input.Length, null, 0, out _, 0))
                {
                    throw Failed(code);
                }
            }
        }
        finally
        {
            Native.CloseHandle(device);
        }
    }

    private static nint Open()
    {
        for (var attempt = 1; ; attempt++)
        {
            var device = Native.CreateFileW(HidHideWire.ControlDevice, Native.GenericRead, Native.FileShareRead | Native.FileShareWrite, 0, Native.OpenExisting, 0, 0);
            if (device != -1)
            {
                return device;
            }

            var error = Marshal.GetLastPInvokeError();
            switch (error)
            {
                case ErrorFileNotFound or ErrorPathNotFound:
                    throw new HidHideException(PanReason.HidHideMissing, "HidHide's driver is not running (a restart pending after its setup?)");
                case ErrorAccessDenied or ErrorSharingViolation when attempt < BusyTries:
                    Thread.Sleep(BusyPause);
                    continue;
                case ErrorAccessDenied or ErrorSharingViolation:
                    throw new HidHideException(PanReason.HidHideBusy, "HidHide is open in another program (its configuration client?)");
                default:
                    throw new HidHideException(PanReason.HidingFailed, $"HidHide not opened: {Marshal.GetPInvokeErrorMessage(error)}");
            }
        }
    }

    private static HidHideException Failed(uint code)
    {
        var error = Marshal.GetLastPInvokeError();
        // An IOCTL the driver doesn't know: another driver interface than the one Pan was built for.
        return error is ErrorInvalidFunction or ErrorNotSupported
            ? new HidHideException(PanReason.HidHideUnsupported, $"HidHide does not know IOCTL 0x{code:X8}: unknown driver interface")
            : new HidHideException(PanReason.HidingFailed, $"HidHide IOCTL 0x{code:X8} failed: {Marshal.GetPInvokeErrorMessage(error)}");
    }
}

/// <summary>The puck's devices through the plug and play manager; a restart is what <c>pnputil /restart-device</c> and devcon do.</summary>
[SupportedOSPlatform("windows")]
public sealed unsafe class PuckDevices : ISteamControllerDevices
{
    public IReadOnlyList<string> PresentHidInstances() => PresentDevices("HID").Where(HidingList.IsListedHid).ToList();

    public Task RestartAsync(CancellationToken cancellationToken)
    {
        foreach (var device in PresentDevices("USB").Where(HidingList.IsListedUsbParent))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Restart(device);
        }

        return Task.CompletedTask;
    }

    private static List<string> PresentDevices(string enumerator)
    {
        const uint flags = Native.CmGetIdListFilterEnumerator | Native.CmGetIdListFilterPresent;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = Native.CM_Get_Device_ID_List_SizeW(out var length, enumerator, flags);
            if (result != Native.CrSuccess)
            {
                throw new InvalidOperationException($"device list size not read (CONFIGRET 0x{result:X})");
            }

            var buffer = new char[length];
            fixed (char* chars = buffer)
            {
                result = Native.CM_Get_Device_ID_ListW(enumerator, chars, length, flags);
            }

            if (result == Native.CrBufferSmall)
            {
                // A device arrived in between.
                continue;
            }

            if (result != Native.CrSuccess)
            {
                throw new InvalidOperationException($"device list not read (CONFIGRET 0x{result:X})");
            }

            return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        throw new InvalidOperationException("device list kept changing");
    }

    private static void Restart(string instanceId)
    {
        var set = Native.SetupDiCreateDeviceInfoList(null, 0);
        if (set == -1)
        {
            throw new Win32Exception();
        }

        try
        {
            var data = new Native.SpDevInfoData { Size = (uint)sizeof(Native.SpDevInfoData) };
            if (!Native.SetupDiOpenDeviceInfoW(set, instanceId, 0, 0, &data))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), $"{instanceId} not opened: {Native.LastErrorText()}");
            }

            var change = new Native.SpPropChangeParams
            {
                HeaderSize = 8,
                InstallFunction = Native.DifPropertyChange,
                StateChange = Native.DicsPropChange,
                Scope = Native.DicsFlagConfigSpecific,
            };
            if (!Native.SetupDiSetClassInstallParamsW(set, &data, &change, (uint)sizeof(Native.SpPropChangeParams))
                || !Native.SetupDiCallClassInstaller(Native.DifPropertyChange, set, &data))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), $"{instanceId} not restarted: {Native.LastErrorText()}");
            }

            var install = new Native.SpDevInstallParams { Size = (uint)sizeof(Native.SpDevInstallParams) };
            if (Native.SetupDiGetDeviceInstallParamsW(set, &data, &install) && (install.Flags & (Native.DiNeedRestart | Native.DiNeedReboot)) != 0)
            {
                // pnputil's exit 3010: the device couldn't be restarted in place; a replug does it.
                throw new InvalidOperationException($"{instanceId} needs a replug or a Windows restart to restart");
            }
        }
        finally
        {
            Native.SetupDiDestroyDeviceInfoList(set);
        }
    }
}

/// <summary>
/// Windows' notification when a HID device interface arrives, filtered to the puck's (no polling). The callback runs
/// on a system thread and must return at once, so it only queues the guardian's work.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed unsafe class DeviceArrivalWatch : IDisposable
{
    private static Func<Task>? _arrived;
    private nint _notification;

    private DeviceArrivalWatch(nint notification) => _notification = notification;

    /// <summary>Registers the notification; null (logged) if Windows refuses, so Pan runs on without it.</summary>
    public static DeviceArrivalWatch? Start(Func<Task> arrived, Action<string> log)
    {
        _arrived = arrived;
        var filter = new Native.CmNotifyFilter
        {
            Size = (uint)sizeof(Native.CmNotifyFilter),
            FilterType = Native.CmNotifyFilterTypeDeviceInterface,
            ClassGuid = Native.HidInterface,
        };
        var result = Native.CM_Register_Notification(&filter, 0, &OnEvent, out var notification);
        if (result != Native.CrSuccess)
        {
            log($"Device arrivals not watched (CONFIGRET 0x{result:X}): a Steam Controller plugged in while hiding is hidden at the next hide");
            return null;
        }

        return new DeviceArrivalWatch(notification);
    }

    public void Dispose()
    {
        if (_notification != 0)
        {
            Native.CM_Unregister_Notification(_notification);
            _notification = 0;
        }

        _arrived = null;
    }

    [UnmanagedCallersOnly]
    private static uint OnEvent(nint notification, nint context, uint action, byte* eventData, uint eventDataSize)
    {
        try
        {
            if (action == Native.CmNotifyActionDeviceInterfaceArrival && eventDataSize > Native.CmNotifyEventSymbolicLinkOffset + 2)
            {
                var link = new string((char*)(eventData + Native.CmNotifyEventSymbolicLinkOffset));
                if (link.Contains(HidingList.PuckInterfaceMarker, StringComparison.OrdinalIgnoreCase) && _arrived is { } arrived)
                {
                    Task.Run(arrived);
                }
            }
        }
        catch (Exception)
        {
            // Nothing may escape into Windows' thread; the next arrival or hide catches up.
        }

        return 0;
    }
}
