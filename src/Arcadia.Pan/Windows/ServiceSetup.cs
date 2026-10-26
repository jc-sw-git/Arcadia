using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;

namespace Arcadia.Pan.Windows;

/// <summary>
/// Installs, updates and removes the service (#243): the exe copied into Program Files and its hash checked after
/// the copy, the service with a quoted image path, automatic start, recovery actions, a pre-shutdown stop, its
/// description in the system's language, and a DACL that gives the interactive user only start, query status and
/// interrogate (review point 4). Runs elevated (Arcadia starts it with a UAC prompt).
/// </summary>
[SupportedOSPlatform("windows")]
public static class ServiceSetup
{
    /// <summary>
    /// SYSTEM: everything a service needs; Administrators: full; Interactive users: SERVICE_START (RP), SERVICE_QUERY_STATUS
    /// (LC), SERVICE_INTERROGATE (LO) and READ_CONTROL (RC) to see that, never SERVICE_CHANGE_CONFIG.
    /// </summary>
    public const string ServiceSddl = "D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;RPLCLORC;;;IU)";

    private static readonly TimeSpan StopWait = TimeSpan.FromSeconds(40);

    /// <summary>
    /// Copies <paramref name="source"/> to Program Files (unless it already runs from there), verifies the copy's
    /// SHA-256 against the source's (and <paramref name="expectedHash"/> when Arcadia passed the hash it computed),
    /// replaces the service and starts it.
    /// </summary>
    public static void Install(string source, string? expectedHash, Action<string> log)
    {
        var target = PanPaths.InstalledExecutable;
        var runsFromTarget = string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase);

        using var manager = OpenManager(Native.ScManagerConnect | Native.ScManagerCreateService);
        if (TryOpenService(manager, Native.ServiceStop | Native.ServiceQueryStatus | Native.Delete) is { } existing)
        {
            using (existing)
            {
                // An update: the old Pan stops (its teardown runs if it was emulating) and goes.
                Stop(existing, log);
                if (!Native.DeleteService(existing.Handle))
                {
                    throw new Win32Exception();
                }

                log("Previous service removed");
            }
        }

        if (!runsFromTarget)
        {
            Directory.CreateDirectory(PanPaths.InstallDirectory);
            string sourceHash;
            // The source stays open without write access from the hash to the end of the copy, so it can't be swapped in between.
            using (var from = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                sourceHash = Convert.ToHexString(SHA256.HashData(from));
                from.Position = 0;
                using var to = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
                from.CopyTo(to);
            }

            string targetHash;
            using (var copied = File.OpenRead(target))
            {
                targetHash = Convert.ToHexString(SHA256.HashData(copied));
            }

            if (!string.Equals(sourceHash, targetHash, StringComparison.OrdinalIgnoreCase)
                || (expectedHash is not null && !string.Equals(expectedHash, targetHash, StringComparison.OrdinalIgnoreCase)))
            {
                File.Delete(target);
                throw new InvalidOperationException($"the copy in Program Files doesn't match (expected {expectedHash ?? sourceHash}, got {targetHash}); nothing installed");
            }

            log($"Copied to {target} (SHA-256 {targetHash})");
        }

        ProtectDataDirectory();

        var imagePath = $"\"{target}\"";
        var service = Native.CreateServiceW(
            manager.Handle, PanPaths.ServiceName, PanPaths.DisplayName, Native.ServiceAllAccess,
            Native.ServiceWin32OwnProcess, Native.ServiceAutoStart, Native.ServiceErrorNormal,
            imagePath, null, 0, null, null, null);
        if (service == 0)
        {
            throw new Win32Exception();
        }

        using var created = new ServiceHandle(service);
        Describe(created, ServiceDescriptions.ForSystem());
        SetRecovery(created);
        SetPreshutdown(created, (uint)StopWait.TotalMilliseconds);
        SetSecurity(created, ServiceSddl);
        log($"Service {PanPaths.ServiceName} created ({imagePath})");

        if (!Native.StartServiceW(created.Handle, 0, 0))
        {
            throw new Win32Exception();
        }

        log("Service started");
    }

    /// <summary>Stops the service (its stop is the full teardown), removes it, undoes what the journal still lists, and deletes Pan's files.</summary>
    public static async Task UninstallAsync(Action<string> log)
    {
        using (var manager = OpenManager(Native.ScManagerConnect))
        {
            if (TryOpenService(manager, Native.ServiceStop | Native.ServiceQueryStatus | Native.Delete) is { } service)
            {
                using (service)
                {
                    Stop(service, log);
                    if (!Native.DeleteService(service.Handle))
                    {
                        throw new Win32Exception();
                    }
                }

                log("Service removed");
            }
            else
            {
                log("Service not installed");
            }
        }

        await Restore.RunAsync(log);
        DeleteTree(PanPaths.DataDirectory, log);
        DeleteTree(PanPaths.InstallDirectory, log);
    }

    private static void Stop(ServiceHandle service, Action<string> log)
    {
        var status = new Native.ServiceStatus();
        if (QueryState(service) == Native.ServiceStopped)
        {
            return;
        }

        if (!Native.ControlService(service.Handle, Native.ServiceControlStop, ref status))
        {
            throw new Win32Exception();
        }

        var until = DateTime.UtcNow + StopWait;
        while (QueryState(service) != Native.ServiceStopped)
        {
            if (DateTime.UtcNow > until)
            {
                throw new TimeoutException($"the service didn't stop within {StopWait.TotalSeconds:0} s");
            }

            Thread.Sleep(200);
        }

        log("Service stopped");
    }

    private static unsafe uint QueryState(ServiceHandle service)
    {
        var info = new Native.ServiceStatusProcess();
        return Native.QueryServiceStatusEx(service.Handle, Native.ScStatusProcessInfo, &info, (uint)sizeof(Native.ServiceStatusProcess), out _)
            ? info.CurrentState
            : throw new Win32Exception();
    }

    private static unsafe void Describe(ServiceHandle service, string text)
    {
        fixed (char* description = text)
        {
            var info = new Native.ServiceDescription { Description = description };
            if (!Native.ChangeServiceConfig2W(service.Handle, Native.ServiceConfigDescription, &info))
            {
                throw new Win32Exception();
            }
        }
    }

    /// <summary>Restart after a crash, three times, counted over a day; a non-zero exit counts as a crash too.</summary>
    private static unsafe void SetRecovery(ServiceHandle service)
    {
        var actions = stackalloc Native.ScAction[3];
        for (var i = 0; i < 3; i++)
        {
            actions[i] = new Native.ScAction { Type = Native.ScActionRestart, Delay = 1000 };
        }

        var failure = new Native.ServiceFailureActions { ResetPeriod = 86_400, ActionCount = 3, Actions = actions };
        if (!Native.ChangeServiceConfig2W(service.Handle, Native.ServiceConfigFailureActions, &failure))
        {
            throw new Win32Exception();
        }

        var flag = new Native.ServiceFailureActionsFlag { FailureActionsOnNonCrashFailures = 1 };
        if (!Native.ChangeServiceConfig2W(service.Handle, Native.ServiceConfigFailureActionsFlag, &flag))
        {
            throw new Win32Exception();
        }
    }

    private static unsafe void SetPreshutdown(ServiceHandle service, uint timeoutMs)
    {
        var info = new Native.ServicePreshutdownInfo { PreshutdownTimeout = timeoutMs };
        if (!Native.ChangeServiceConfig2W(service.Handle, Native.ServiceConfigPreshutdownInfo, &info))
        {
            throw new Win32Exception();
        }
    }

    private static void SetSecurity(ServiceHandle service, string sddl)
    {
        if (!Native.ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl, Native.SddlRevision1, out var descriptor, out _))
        {
            throw new Win32Exception();
        }

        try
        {
            if (!Native.SetServiceObjectSecurity(service.Handle, Native.DaclSecurityInformation, descriptor))
            {
                throw new Win32Exception();
            }
        }
        finally
        {
            Native.LocalFree(descriptor);
        }
    }

    /// <summary><c>%ProgramData%\Arcadia\Pan</c> for administrators and SYSTEM only: the journal and log are nobody else's to read or plant.</summary>
    private static void ProtectDataDirectory()
    {
        var directory = Directory.CreateDirectory(PanPaths.DataDirectory);
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        var inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        directory.SetAccessControl(security);
    }

    private static void DeleteTree(string directory, Action<string> log)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
            log($"Deleted {directory}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Pan.exe itself may be the one running this: Windows deletes it at the next boot.
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                Native.MoveFileExW(file, null, Native.MoveFileDelayUntilReboot);
            }

            Native.MoveFileExW(directory, null, Native.MoveFileDelayUntilReboot);
            log($"{directory} is deleted at the next boot ({ex.Message})");
        }
    }

    private static ServiceHandle OpenManager(uint access)
    {
        var manager = Native.OpenSCManagerW(null, null, access);
        return manager == 0 ? throw new Win32Exception() : new ServiceHandle(manager);
    }

    private static ServiceHandle? TryOpenService(ServiceHandle manager, uint access)
    {
        var service = Native.OpenServiceW(manager.Handle, PanPaths.ServiceName, access);
        if (service != 0)
        {
            return new ServiceHandle(service);
        }

        return Marshal.GetLastPInvokeError() == Native.ErrorServiceDoesNotExist ? null : throw new Win32Exception();
    }

    private sealed class ServiceHandle(nint handle) : IDisposable
    {
        public nint Handle { get; } = handle;

        public void Dispose() => Native.CloseServiceHandle(Handle);
    }
}
