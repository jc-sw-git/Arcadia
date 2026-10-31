using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Arcadia.Launchers;
using Microsoft.Win32;

namespace Arcadia.ControllerSupport;

/// <summary><see cref="ISisrSystem"/> on Windows: processes, SISR's uninstall entry in HKCU, VIIPER's own uninstall.</summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsSisrSystem : ISisrSystem
{
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SISR";
    private const uint QueryLimitedInformation = 0x1000;
    private const int ProcessCommandLineInformation = 60;

    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ViiperUninstallTimeout = TimeSpan.FromSeconds(5);

    public IReadOnlyList<SisrProcess> Processes(string name)
    {
        var found = new List<SisrProcess>();
        foreach (var process in Process.GetProcessesByName(name))
        {
            using (process)
            {
                found.Add(new(process.Id, ProcessWatch.PathOf(process.Id), CommandLineOf(process.Id)));
            }
        }

        return found;
    }

    public bool Stop(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
            return process.WaitForExit(StopTimeout);
        }
        catch (ArgumentException)
        {
            // Already gone.
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            // Not ours to stop (elevated) or just ending: whoever asks looks again.
            return false;
        }
    }

    public bool HasUninstallEntry
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
            return key is not null;
        }
    }

    public void DeleteUninstallEntry() => Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);

    public void UninstallViiper(string viiperExecutable)
    {
        using var uninstall = Process.Start(new ProcessStartInfo(viiperExecutable, "uninstall --quiet") { CreateNoWindow = true, UseShellExecute = false });
        if (uninstall is not null && !uninstall.WaitForExit(ViiperUninstallTimeout))
        {
            uninstall.Kill();
        }
    }

    public void ResetSteamInputLayout() =>
        Process.Start(new ProcessStartInfo("steam://forceinputappid/0") { UseShellExecute = true })?.Dispose();

    public void DetachViiperDevices() => UsbipDriver.DetachViiperDevicesAsync().GetAwaiter().GetResult();

    /// <summary>
    /// The command line through <c>NtQueryInformationProcess</c> (Windows 8.1 and later), which needs only the limited
    /// query right; null when Windows doesn't tell.
    /// </summary>
    private static unsafe string? CommandLineOf(int processId)
    {
        var handle = OpenProcess(QueryLimitedInformation, false, (uint)processId);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var buffer = new byte[64 * 1024];
            fixed (byte* start = buffer)
            {
                if (NtQueryInformationProcess(handle, ProcessCommandLineInformation, start, buffer.Length, out _) != 0)
                {
                    return null;
                }

                // A UNICODE_STRING whose buffer points into ours, right behind it.
                var text = (UnicodeString*)start;
                return text->Buffer == IntPtr.Zero ? null : new string((char*)text->Buffer, 0, text->Length / sizeof(char));
            }
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);

    [LibraryImport("ntdll.dll")]
    private static unsafe partial int NtQueryInformationProcess(IntPtr process, int informationClass, byte* information, int length, out int returnLength);
}
