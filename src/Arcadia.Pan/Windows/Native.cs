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

    public static string LastErrorText() => Marshal.GetPInvokeErrorMessage(Marshal.GetLastPInvokeError());
}
