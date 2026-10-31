using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arcadia.Pan.Windows;

/// <summary>The Windows calls Pan makes; every one is listed here so a reviewer sees the whole surface at once.</summary>
[SupportedOSPlatform("windows")]
internal static unsafe partial class Native
{
    // Processes
    public const uint ProcessQueryLimitedInformation = 0x1000;
    public const uint ProcessTerminate = 0x0001;
    public const uint Synchronize = 0x00100000;
    public const uint WaitObject0 = 0;
    public const uint WaitTimeout = 0x102;
    public const uint Th32csSnapProcess = 0x2;
    public const uint ErrorNoMoreFiles = 18;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint handle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetProcessTimes(nint process, out long creation, out long exit, out long kernel, out long user);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TerminateProcess(nint process, uint exitCode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial uint WaitForSingleObject(nint handle, uint milliseconds);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool QueryFullProcessImageNameW(nint process, uint flags, char* name, ref uint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint CreateToolhelp32Snapshot(uint flags, uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool Process32FirstW(nint snapshot, ref ProcessEntry entry);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool Process32NextW(nint snapshot, ref ProcessEntry entry);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ProcessEntry
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public nuint DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityClassBase;
        public uint Flags;
        public fixed char ExeFile[260];
    }

    // Sessions
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ProcessIdToSessionId(uint processId, out uint sessionId);

    [LibraryImport("kernel32.dll")]
    public static partial uint WTSGetActiveConsoleSessionId();

    // Pipes
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetNamedPipeClientProcessId(nint pipe, out uint clientProcessId);

    // Clock
    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool QueryUnbiasedInterruptTime(out ulong unbiasedTime);

    // Files
    public const uint MoveFileDelayUntilReboot = 0x4;

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MoveFileExW(string existing, string? @new, uint flags);

    // Service control manager
    public const uint ScManagerConnect = 0x0001;
    public const uint ScManagerCreateService = 0x0002;
    public const uint ServiceQueryStatus = 0x0004;
    public const uint ServiceStart = 0x0010;
    public const uint ServiceStop = 0x0020;
    public const uint ServiceChangeConfig = 0x0002;
    public const uint Delete = 0x00010000;
    public const uint WriteDac = 0x00040000;
    public const uint ReadControl = 0x00020000;
    public const uint ServiceAllAccess = 0xF01FF;

    public const uint ServiceWin32OwnProcess = 0x10;
    public const uint ServiceAutoStart = 0x2;
    public const uint ServiceErrorNormal = 0x1;

    public const uint ServiceStopped = 1;
    public const uint ServiceStartPending = 2;
    public const uint ServiceStopPending = 3;
    public const uint ServiceRunning = 4;

    public const uint ServiceControlStop = 1;
    public const uint ServiceControlInterrogate = 4;
    public const uint ServiceControlShutdown = 5;
    public const uint ServiceControlPowerEvent = 0xD;
    public const uint ServiceControlPreshutdown = 0xF;

    public const uint ServiceAcceptStop = 0x1;
    public const uint ServiceAcceptShutdown = 0x4;
    public const uint ServiceAcceptPowerEvent = 0x40;
    public const uint ServiceAcceptPreshutdown = 0x100;

    public const uint PbtApmResumeSuspend = 0x7;
    public const uint PbtApmResumeAutomatic = 0x12;

    public const uint NoError = 0;
    public const uint ErrorCallNotImplemented = 120;
    public const uint ErrorFailedServiceControllerConnect = 1063;
    public const uint ErrorServiceDoesNotExist = 1060;

    public const uint ServiceConfigDescription = 1;
    public const uint ServiceConfigFailureActions = 2;
    public const uint ServiceConfigFailureActionsFlag = 4;
    public const uint ServiceConfigPreshutdownInfo = 7;
    public const uint ScActionRestart = 1;
    public const uint ScStatusProcessInfo = 0;
    public const uint DaclSecurityInformation = 0x4;
    public const uint SddlRevision1 = 1;

    [StructLayout(LayoutKind.Sequential)]
    public struct ServiceStatus
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ServiceStatusProcess
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
        public uint ProcessId;
        public uint ServiceFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ServiceTableEntry
    {
        public char* Name;
        public delegate* unmanaged<uint, char**, void> Main;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ServiceDescription
    {
        public char* Description;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ScAction
    {
        public uint Type;
        public uint Delay;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ServiceFailureActions
    {
        public uint ResetPeriod;
        public char* RebootMessage;
        public char* Command;
        public uint ActionCount;
        public ScAction* Actions;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ServiceFailureActionsFlag
    {
        public int FailureActionsOnNonCrashFailures;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ServicePreshutdownInfo
    {
        public uint PreshutdownTimeout;
    }

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool StartServiceCtrlDispatcherW(ServiceTableEntry* table);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint RegisterServiceCtrlHandlerExW(string serviceName, delegate* unmanaged<uint, uint, void*, void*, uint> handler, void* context);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetServiceStatus(nint statusHandle, ref ServiceStatus status);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint OpenSCManagerW(string? machine, string? database, uint access);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint OpenServiceW(nint manager, string name, uint access);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateServiceW(
        nint manager, string name, string displayName, uint access, uint type, uint startType, uint errorControl,
        string binaryPath, string? loadOrderGroup, nint tagId, string? dependencies, string? account, string? password);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseServiceHandle(nint handle);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteService(nint service);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool StartServiceW(nint service, uint argc, nint argv);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ControlService(nint service, uint control, ref ServiceStatus status);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool QueryServiceStatusEx(nint service, uint infoLevel, void* buffer, uint size, out uint needed);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ChangeServiceConfig2W(nint service, uint infoLevel, void* info);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetServiceObjectSecurity(nint service, uint securityInformation, nint securityDescriptor);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string sddl, uint revision, out nint securityDescriptor, out uint size);

    [LibraryImport("kernel32.dll")]
    public static partial nint LocalFree(nint memory);

    // Devices: HidHide's control device (part B)
    public const uint GenericRead = 0x80000000;
    public const uint FileShareRead = 0x1;
    public const uint FileShareWrite = 0x2;
    public const uint OpenExisting = 3;
    public const uint ProcessNameNative = 1;

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateFileW(string fileName, uint access, uint share, nint security, uint creation, uint flags, nint template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeviceIoControl(nint device, uint code, void* input, uint inputSize, void* output, uint outputSize, out uint returned, nint overlapped);

    // Devices: the plug and play manager (listing, arrival notification)
    public const uint CrSuccess = 0;
    public const uint CrBufferSmall = 0x1A;
    public const uint CmGetIdListFilterEnumerator = 0x1;
    public const uint CmGetIdListFilterPresent = 0x100;
    public const uint CmNotifyFilterTypeDeviceInterface = 0;
    public const uint CmNotifyActionDeviceInterfaceArrival = 0;

    /// <summary>GUID_DEVINTERFACE_HID.</summary>
    public static readonly Guid HidInterface = new("4d1e55b2-f16f-11cf-88cb-001111000030");

    /// <summary>GUID_DEVINTERFACE_XUSB_DEVICE: an XInput pad's interface (<c>xusb22</c>), which has no HID interface of its own.</summary>
    public static readonly Guid XusbInterface = new("ec87f1e3-c13b-4100-b5f7-8b84d54260cb");

    public const uint CmDrpHardwareId = 0x2;
    public const uint CmDrpCompatibleIds = 0x3;
    public const uint CmDrpClass = 0x8;

    [LibraryImport("cfgmgr32.dll")]
    public static partial uint CM_Get_DevNode_Registry_PropertyW(uint devInst, uint property, out uint type, void* buffer, ref uint length, uint flags);

    [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint CM_Get_Device_ID_List_SizeW(out uint length, string filter, uint flags);

    [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint CM_Get_Device_ID_ListW(string filter, char* buffer, uint length, uint flags);

    public const uint CmLocateDevnodeNormal = 0;

    [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);

    [LibraryImport("cfgmgr32.dll")]
    public static partial uint CM_Get_Parent(out uint parent, uint devInst, uint flags);

    [LibraryImport("cfgmgr32.dll")]
    public static partial uint CM_Get_Device_IDW(uint devInst, char* buffer, uint length, uint flags);

    [LibraryImport("cfgmgr32.dll")]
    public static partial uint CM_Register_Notification(CmNotifyFilter* filter, nint context, delegate* unmanaged<nint, nint, uint, byte*, uint, uint> callback, out nint notification);

    [LibraryImport("cfgmgr32.dll")]
    public static partial uint CM_Unregister_Notification(nint notification);

    /// <summary>CM_NOTIFY_FILTER: 16 bytes of header and a 400-byte union, of which a device interface uses the class GUID.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CmNotifyFilter
    {
        public uint Size;
        public uint Flags;
        public uint FilterType;
        public uint Reserved;
        public Guid ClassGuid;
        public fixed byte Rest[384];
    }

    /// <summary>Where CM_NOTIFY_EVENT_DATA keeps a device interface's symbolic link: after the type, a reserved field and the GUID.</summary>
    public const int CmNotifyEventSymbolicLinkOffset = 24;

    // Devices: setup API (restart)
    public const uint DifPropertyChange = 0x12;
    public const uint DicsPropChange = 3;
    public const uint DicsFlagConfigSpecific = 2;
    public const uint DiNeedRestart = 0x80;
    public const uint DiNeedReboot = 0x100;

    [StructLayout(LayoutKind.Sequential)]
    public struct SpDevInfoData
    {
        public uint Size;
        public Guid ClassGuid;
        public uint DevInst;
        public nuint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SpPropChangeParams
    {
        public uint HeaderSize;
        public uint InstallFunction;
        public uint StateChange;
        public uint Scope;
        public uint HwProfile;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SpDevInstallParams
    {
        public uint Size;
        public uint Flags;
        public uint FlagsEx;
        public nint ParentWindow;
        public nint InstallMsgHandler;
        public nint InstallMsgHandlerContext;
        public nint FileQueue;
        public nuint ClassInstallReserved;
        public uint Reserved;
        public fixed char DriverPath[260];
    }

    [LibraryImport("setupapi.dll", SetLastError = true)]
    public static partial nint SetupDiCreateDeviceInfoList(Guid* classGuid, nint parent);

    [LibraryImport("setupapi.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetupDiOpenDeviceInfoW(nint set, string instanceId, nint parent, uint flags, SpDevInfoData* data);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetupDiSetClassInstallParamsW(nint set, SpDevInfoData* data, SpPropChangeParams* parameters, uint size);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetupDiCallClassInstaller(uint function, nint set, SpDevInfoData* data);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetupDiGetDeviceInstallParamsW(nint set, SpDevInfoData* data, SpDevInstallParams* parameters);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetupDiDestroyDeviceInfoList(nint set);

    public static string LastErrorText() => Marshal.GetPInvokeErrorMessage(Marshal.GetLastPInvokeError());
}
