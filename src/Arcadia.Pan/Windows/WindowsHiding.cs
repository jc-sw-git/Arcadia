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
    public static SteamControllerHiding Create() => new(new HidHideDriver(), new PuckDevices(), new ControllerDevices());
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
    public IReadOnlyList<string> PresentHidInstances() =>
        PresentDevices("HID").Where(id => HidingList.IsListedHid(id) && !HidingList.OnSoftwareHost(id, ParentOf)).ToList();

    public Task RestartAsync(CancellationToken cancellationToken)
    {
        foreach (var device in PresentDevices("USB").Where(id => HidingList.IsListedUsbParent(id) && !HidingList.OnSoftwareHost(id, ParentOf)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Restart(device);
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<string> PresentBootloaders() => [.. PresentDevices("USB").Where(HidingList.IsBootloader)];

    internal static List<string> PresentDevices(string enumerator)
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

    internal static string? ParentOf(string instanceId)
    {
        const int maxDeviceIdLength = 200;
        if (Native.CM_Locate_DevNodeW(out var device, instanceId, Native.CmLocateDevnodeNormal) != Native.CrSuccess
            || Native.CM_Get_Parent(out var parent, device, 0) != Native.CrSuccess)
        {
            return null;
        }

        var buffer = stackalloc char[maxDeviceIdLength + 1];
        return Native.CM_Get_Device_IDW(parent, buffer, maxDeviceIdLength + 1, 0) == Native.CrSuccess ? new string(buffer) : null;
    }

    /// <exception cref="InvalidOperationException">Windows restarts it only once it is reconnected.</exception>
    internal static void Restart(string instanceId)
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
/// Every other controller's devices (#335) through the plug and play manager. What a game reads: the HID collections
/// (keyboard and mouse ones aside, which HidHide doesn't filter and games don't read) and the XInput interfaces
/// (<c>xusb22</c>, classes <c>XnaComposite</c> and <c>XboxComposite</c>), which HidHide filters as well.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed unsafe class ControllerDevices : IControllerDevices
{
    private static readonly string[] XInputClasses = ["XnaComposite", "XboxComposite"];

    /// <summary>A HID collection of the generic desktop page's joystick or gamepad usage: what SDL and games take for a controller.</summary>
    private static readonly string[] GamepadCollections = ["HID_DEVICE_UP:0001_U:0004", "HID_DEVICE_UP:0001_U:0005"];

    public ControllerScan Scan(ControllerModel model)
    {
        var connected = false;
        var gamepad = false;
        var hideable = new List<ControllerInstance>();
        foreach (var id in PuckDevices.PresentDevices("HID").Where(id => ControllerList.IsOfModel(id, model)))
        {
            connected = true;
            if (HidingList.OnSoftwareHost(id, PuckDevices.ParentOf) || Property(id, Native.CmDrpClass).FirstOrDefault() is "Keyboard" or "Mouse")
            {
                continue;
            }

            hideable.Add(new ControllerInstance(id, ControllerInterface.Hid));
            // Windows lists a collection's usage among its hardware ids; the compatible ids of a HID collection are empty
            // (a DualSense by USB on the owner's PC). Both are read, in case a driver puts it there.
            gamepad |= Property(id, Native.CmDrpHardwareId).Concat(Property(id, Native.CmDrpCompatibleIds))
                .Any(hardwareId => GamepadCollections.Contains(hardwareId, StringComparer.OrdinalIgnoreCase));
        }

        foreach (var id in PuckDevices.PresentDevices("USB").Where(id => ControllerList.IsOfModel(id, model)))
        {
            connected = true;
            if (!HidingList.OnSoftwareHost(id, PuckDevices.ParentOf) && XInputClasses.Contains(Property(id, Native.CmDrpClass).FirstOrDefault(), StringComparer.OrdinalIgnoreCase))
            {
                hideable.Add(new ControllerInstance(id, ControllerInterface.XInput));
                gamepad = true;
            }
        }

        // A model without a gamepad collection (a wheel's pedals, a headset's buttons) isn't a controller Pan hides.
        return new ControllerScan(connected, gamepad ? hideable : []);
    }

    public Task<IReadOnlyList<string>> RestartAsync(IReadOnlyList<string> hidInstances, CancellationToken cancellationToken)
    {
        var replug = new List<string>();
        // The device a collection belongs to: its USB interface (or the USB device), or its Bluetooth device.
        foreach (var device in hidInstances.GroupBy(id => PuckDevices.ParentOf(id) ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (device.Key.Length == 0)
            {
                // Unplugged meanwhile: it announces itself anew when it comes back.
                continue;
            }

            try
            {
                PuckDevices.Restart(device.Key);
            }
            catch (InvalidOperationException) when (CyclePort(device.Key))
            {
                // Windows restarts an XInput pad in place only while no program has it open, and Steam opens a freshly
                // plugged one within a second; the hub enumerating it anew is the reconnect the player would do.
            }
            catch (InvalidOperationException)
            {
                replug.AddRange(device);
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(replug);
    }

    public Task<IReadOnlyList<string>> ReconnectAsync(IReadOnlyList<string> xinputInstances, CancellationToken cancellationToken)
    {
        var left = new List<string>();
        foreach (var instance in xinputInstances)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CyclePort(instance))
            {
                left.Add(instance);
                continue;
            }

            // Answered once it is gone, not once it is back (about 4 s on the owner's PC): the client reads its
            // controllers again right after the answer and takes the pad as a new arrival, while every controller
            // stays let go only for the hide itself.
            var gone = Stopwatch.StartNew();
            while (IsPresent(instance) && gone.Elapsed < GoneWait)
            {
                // The guardian runs this on the thread pool and waits with its timeout; no await in an unsafe class.
                cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(100));
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(left);
    }

    private static readonly TimeSpan GoneWait = TimeSpan.FromSeconds(2);

    private static bool IsPresent(string instanceId) => Native.CM_Locate_DevNodeW(out _, instanceId, Native.CmLocateDevnodeNormal) == Native.CrSuccess;

    /// <summary>Cycles the hub port of the USB device <paramref name="device"/> is part of; false if it has none or the hub refused.</summary>
    private static bool CyclePort(string device)
    {
        if (ControllerList.OnHubPort(device, PuckDevices.ParentOf, IsHub) is not { } onPort
            || PuckDevices.ParentOf(onPort) is not { } hub
            || NumberProperty(onPort, Native.CmDrpAddress) is not { } port
            || HubInterface(hub) is not { } hubInterface)
        {
            return false;
        }

        var handle = Native.CreateFileW(hubInterface, Native.GenericRead | Native.GenericWrite, Native.FileShareRead | Native.FileShareWrite, 0, Native.OpenExisting, 0, 0);
        if (handle == -1)
        {
            return false;
        }

        try
        {
            var parameters = new Native.UsbCyclePortParams { ConnectionIndex = port };
            var size = (uint)sizeof(Native.UsbCyclePortParams);
            return Native.DeviceIoControl(handle, Native.IoctlUsbHubCyclePort, &parameters, size, &parameters, size, out _, 0);
        }
        finally
        {
            Native.CloseHandle(handle);
        }
    }

    private static bool IsHub(string instanceId) =>
        Property(instanceId, Native.CmDrpService).FirstOrDefault() is { } service
        && (service.Equals("USBHUB3", StringComparison.OrdinalIgnoreCase) || service.Equals("usbhub", StringComparison.OrdinalIgnoreCase));

    private static string? HubInterface(string hubInstanceId)
    {
        if (Native.CM_Get_Device_Interface_List_SizeW(out var length, Native.UsbHubInterface, hubInstanceId, 0) != Native.CrSuccess || length <= 1)
        {
            return null;
        }

        var buffer = new char[length];
        fixed (char* chars = buffer)
        {
            if (Native.CM_Get_Device_Interface_ListW(Native.UsbHubInterface, hubInstanceId, chars, length, 0) != Native.CrSuccess)
            {
                return null;
            }
        }

        return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
    }

    /// <summary>A number (DWORD) property of the device; null when it can't be read.</summary>
    private static uint? NumberProperty(string instanceId, uint property)
    {
        if (Native.CM_Locate_DevNodeW(out var device, instanceId, Native.CmLocateDevnodeNormal) != Native.CrSuccess)
        {
            return null;
        }

        uint value = 0;
        var length = (uint)sizeof(uint);
        return Native.CM_Get_DevNode_Registry_PropertyW(device, property, out _, &value, ref length, 0) == Native.CrSuccess ? value : null;
    }

    /// <summary>A string or multi-string property of the device; empty when it can't be read.</summary>
    private static List<string> Property(string instanceId, uint property)
    {
        if (Native.CM_Locate_DevNodeW(out var device, instanceId, Native.CmLocateDevnodeNormal) != Native.CrSuccess)
        {
            return [];
        }

        uint length = 0;
        Native.CM_Get_DevNode_Registry_PropertyW(device, property, out _, null, ref length, 0);
        if (length == 0)
        {
            return [];
        }

        var buffer = new char[length / sizeof(char) + 1];
        fixed (char* chars = buffer)
        {
            if (Native.CM_Get_DevNode_Registry_PropertyW(device, property, out _, chars, ref length, 0) != Native.CrSuccess)
            {
                return [];
            }
        }

        return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}

/// <summary>
/// Windows' notification when a HID or XInput device interface arrives (no polling): the puck's go to the puck's hide,
/// every other model's to the controllers' (#335), which ignores models the client hasn't taken over. The callback runs
/// on a system thread and must return at once, so it only queues the guardian's work.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed unsafe class DeviceArrivalWatch : IDisposable
{
    private static Func<Task>? _puckArrived;
    private static Func<ControllerModel, Task>? _controllerArrived;
    private readonly List<nint> _notifications;

    private DeviceArrivalWatch(List<nint> notifications) => _notifications = notifications;

    /// <summary>Registers the notifications; null (logged) if Windows refuses both, so Pan runs on without them.</summary>
    public static DeviceArrivalWatch? Start(Func<Task> puckArrived, Func<ControllerModel, Task> controllerArrived, Action<string> log)
    {
        _puckArrived = puckArrived;
        _controllerArrived = controllerArrived;
        var notifications = new List<nint>();
        foreach (var (guid, name) in new[] { (Native.HidInterface, "HID"), (Native.XusbInterface, "XInput") })
        {
            var filter = new Native.CmNotifyFilter
            {
                Size = (uint)sizeof(Native.CmNotifyFilter),
                FilterType = Native.CmNotifyFilterTypeDeviceInterface,
                ClassGuid = guid,
            };

            var result = Native.CM_Register_Notification(&filter, 0, &OnEvent, out var notification);
            if (result == Native.CrSuccess)
            {
                notifications.Add(notification);
            }
            else
            {
                log($"{name} device arrivals not watched (CONFIGRET 0x{result:X}): a controller plugged in while hiding is hidden at the next hide");
            }
        }

        return notifications.Count == 0 ? null : new DeviceArrivalWatch(notifications);
    }

    public void Dispose()
    {
        foreach (var notification in _notifications)
        {
            Native.CM_Unregister_Notification(notification);
        }

        _notifications.Clear();
        _puckArrived = null;
        _controllerArrived = null;
    }

    [UnmanagedCallersOnly]
    private static uint OnEvent(nint notification, nint context, uint action, byte* eventData, uint eventDataSize)
    {
        try
        {
            if (action == Native.CmNotifyActionDeviceInterfaceArrival && eventDataSize > Native.CmNotifyEventSymbolicLinkOffset + 2)
            {
                var link = new string((char*)(eventData + Native.CmNotifyEventSymbolicLinkOffset));
                if (link.Contains(HidingList.PuckInterfaceMarker, StringComparison.OrdinalIgnoreCase))
                {
                    if (_puckArrived is { } puckArrived)
                    {
                        Task.Run(puckArrived);
                    }
                }
                else if (ControllerModel.Of(link) is { } model && ControllerList.MayTakeOver(model) && _controllerArrived is { } controllerArrived)
                {
                    Task.Run(() => controllerArrived(model));
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
