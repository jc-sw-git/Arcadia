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

    // The stop's teardown, then the notices to the client; also Windows' pre-shutdown wait.
    private static readonly TimeSpan StopWait = ServiceHost.TeardownWait + TimeSpan.FromSeconds(10);

    /// <summary>
    /// Installs or updates Pan: stages <paramref name="source"/> as <c>Pan.exe.new</c> in Program Files (unless it
    /// already runs from there) and verifies the copy's SHA-256 against the source's (and <paramref name="expectedHash"/>
    /// when Arcadia passed the hash it computed) before the installed Pan is touched. Only then the old service goes,
    /// the file is swapped and the new service starts; if that fails the old Pan.exe comes back as the service
    /// (review point 7). A rejected or failed update leaves the installed Pan working.
    /// </summary>
    public static void Install(string source, string? expectedHash, Action<string> log)
    {
        var target = PanPaths.InstalledExecutable;
        var runsFromTarget = string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase);
        var staged = runsFromTarget ? null : Stage(source, target, expectedHash, log);

        var backup = target + ".old";
        using var manager = OpenManager(Native.ScManagerConnect | Native.ScManagerCreateService);
        bool hadService;
        try
        {
            // An update: the old Pan stops (its teardown runs if it was emulating) and goes.
            hadService = RemoveService(manager, log);
        }
        catch (Win32Exception) when (staged is not null)
        {
            // A hung old Pan that won't stop: nothing changed, so the staged copy goes again.
            File.Delete(staged);
            throw;
        }

        try
        {
            if (staged is not null)
            {
                File.Delete(backup);
                if (File.Exists(target))
                {
                    File.Move(target, backup);
                }

                File.Move(staged, target);
                log($"Installed {target}");
            }

            CreateAndStart(manager, target, log);
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException)
        {
            // Everything after the old service went is undone together: a half-made service, the file swap, and the
            // old service comes back from its backup (review follow-up 3).
            log($"Installing the new Pan failed ({ex.Message}); restoring the previous one");
            var restored = RollBack(manager, target, staged, backup, hadService, log);
            throw new InvalidOperationException(
                restored
                    ? $"the new Pan could not be installed ({ex.Message}); the previous version is running again"
                    : $"the new Pan could not be installed ({ex.Message}) and the previous one could not be restored; run the install again",
                ex);
        }

        // The new Pan runs: the install is done. The backup is cleanup only (final review, P3): a scanner holding it
        // must not undo a working installation.
        try
        {
            File.Delete(backup);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log($"{backup} could not be removed ({ex.Message}); the next install replaces it");
        }
    }

    /// <summary>Stops and deletes the installed service, if any; true when there was one.</summary>
    private static bool RemoveService(ServiceHandle manager, Action<string> log)
    {
        if (TryOpenService(manager, Native.ServiceStop | Native.ServiceQueryStatus | Native.Delete) is not { } existing)
        {
            return false;
        }

        using (existing)
        {
            Stop(existing, log);
            if (!Native.DeleteService(existing.Handle))
            {
                throw new Win32Exception();
            }
        }

        log("Previous service removed");
        return true;
    }

    /// <summary>
    /// Puts back what <see cref="Install"/> changed after the old service went: a service it half made goes, the old
    /// <c>Pan.exe</c> returns from <paramref name="backup"/> and, if there was a service before, runs again. The new
    /// exe is removed. False when that failed too; the log says what is left.
    /// </summary>
    private static bool RollBack(ServiceHandle manager, string target, string? staged, string backup, bool hadService, Action<string> log)
    {
        try
        {
            RemoveService(manager, log);
            if (staged is not null && File.Exists(backup))
            {
                if (File.Exists(target))
                {
                    File.Move(target, staged, overwrite: true);
                }

                File.Move(backup, target);
                log($"Previous {target} restored");
            }

            if (hadService && File.Exists(target))
            {
                CreateAndStart(manager, target, log);
            }

            if (staged is not null)
            {
                File.Delete(staged);
            }

            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException)
        {
            log($"Rollback failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Copies the source next to the installed Pan.exe (so the swap later is a rename on one volume) and verifies the
    /// copy; a mismatch removes the copy and leaves everything installed as it was.
    /// </summary>
    private static string Stage(string source, string target, string? expectedHash, Action<string> log)
    {
        var staged = target + ".new";
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        string sourceHash;
        // The source stays open without write access from the hash to the end of the copy, so it can't be swapped in between.
        using (var from = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            sourceHash = Convert.ToHexString(SHA256.HashData(from));
            from.Position = 0;
            using var to = new FileStream(staged, FileMode.Create, FileAccess.Write, FileShare.None);
            from.CopyTo(to);
        }

        string stagedHash;
        using (var copied = File.OpenRead(staged))
        {
            stagedHash = Convert.ToHexString(SHA256.HashData(copied));
        }

        if (!string.Equals(sourceHash, stagedHash, StringComparison.OrdinalIgnoreCase)
            || (expectedHash is not null && !string.Equals(expectedHash, stagedHash, StringComparison.OrdinalIgnoreCase)))
        {
            File.Delete(staged);
            throw new InvalidOperationException($"the copy in Program Files doesn't match (expected {expectedHash ?? sourceHash}, got {stagedHash}); nothing changed");
        }

        log($"Staged {staged} (SHA-256 {stagedHash})");
        return staged;
    }

    private static void CreateAndStart(ServiceHandle manager, string target, Action<string> log)
    {
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
        try
        {
            Describe(created, ServiceDescriptions.ForSystem());
            SetRecovery(created);
            SetPreshutdown(created, (uint)StopWait.TotalMilliseconds);
            SetSecurity(created, ServiceSddl);
            log($"Service {PanPaths.ServiceName} created ({imagePath})");

            if (!Native.StartServiceW(created.Handle, 0, 0))
            {
                throw new Win32Exception();
            }
        }
        catch
        {
            // Half a service is worse than none: without it the rollback (or the next install) starts clean.
            Native.DeleteService(created.Handle);
            throw;
        }

        log("Service started");
    }

    /// <summary>
    /// Stops the service (its stop is the full teardown), removes it, then goes on in <see cref="UninstallOrder"/>: undoes
    /// what the journal still lists, removes the drivers if asked, deletes the data folder; a data folder that isn't
    /// ours is left alone. When something could not be undone, the journal and Pan.exe stay too, so
    /// <c>Pan.exe --restore</c> (or the next install) can finish it (review point 8).
    /// </summary>
    public static async Task<UninstallOutcome> UninstallAsync(DataFolder data, Func<Task<bool>>? removeDrivers, Action stopFileLog, Action<string> log)
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

        var outcome = await UninstallOrder.RunAsync(data, () => Restore.RunAsync(log), removeDrivers, stopFileLog, () =>
        {
            DeleteTree(PanPaths.DataDirectory, log);
            DeleteIfEmpty(Path.GetDirectoryName(PanPaths.DataDirectory)!, log);
        }, log);
        if (outcome != UninstallOutcome.RestoreIncomplete)
        {
            DeleteTree(PanPaths.InstallDirectory, log);
        }

        return outcome;
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

    private static readonly SecurityIdentifier LocalSystem = new(WellKnownSidType.LocalSystemSid, null);
    private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);

    /// <summary>
    /// <c>%ProgramData%\Arcadia</c> and its <c>Pan</c>, writable by administrators and SYSTEM only (review, bug 1): everyone
    /// may create folders in <c>%ProgramData%</c> and keep them, so one that exists but isn't ours (a user's, a junction
    /// to anywhere, or one somebody got write rights on) is moved aside and made anew: owner Administrators, nothing
    /// inherited, users may read (the log; owner, 2026-10-27). Before the first log line of <c>--install</c>, because
    /// the log lives there too; also by the service at its start, as SYSTEM.
    /// </summary>
    /// <returns>The folders set aside, with why each wasn't trusted.</returns>
    public static IReadOnlyList<string> SecureDataDirectory()
    {
        var setAside = new List<string>();
        foreach (var directory in DataDirectories())
        {
            if (Exists(directory) && !IsOurs(directory, out var notOurs))
            {
                setAside.Add($"{directory} ({notOurs}) set aside as {MoveAside(directory)}");
            }
            else if (Exists(directory) && !UsersMayRead(directory))
            {
                // A folder from before users could read it: verified ours (admin-owned, no link, parent ours too), so its
                // rules are replaced in place; the owner stays as it is.
                new DirectoryInfo(directory).SetAccessControl(Rules(new DirectorySecurity()));
                continue;
            }

            var security = Rules(new DirectorySecurity());
            security.SetOwner(Administrators);
            // Created with its descriptor in one step and never re-secured by path: a junction somebody plants between
            // the move-aside and the create leaves this a no-op, and the check below fails instead of the elevated
            // process putting an admin-only DACL on the junction's target (review, point 4).
            new DirectoryInfo(directory).Create(security);
            if (!IsOurs(directory, out var problem))
            {
                throw new InvalidOperationException($"{directory} could not be secured: {problem}");
            }
        }

        return setAside;
    }

    private static readonly SecurityIdentifier Users = new(WellKnownSidType.BuiltinUsersSid, null);

    /// <summary>SYSTEM and administrators may do anything, users only read and list.</summary>
    private static DirectorySecurity Rules(DirectorySecurity security)
    {
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        var inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(LocalSystem, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(Administrators, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(Users, FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));
        return security;
    }

    private static bool UsersMayRead(string directory) =>
        new DirectoryInfo(directory).GetAccessControl(AccessControlSections.Access)
            .GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>()
            .Any(rule => rule.AccessControlType == AccessControlType.Allow && rule.IdentityReference == Users
                && rule.FileSystemRights.HasFlag(FileSystemRights.ReadAndExecute));

    /// <summary>Why the data folders can't be trusted (the service and --restore refuse to run then), or null.</summary>
    public static string? CheckDataDirectory()
    {
        foreach (var directory in DataDirectories())
        {
            if (!Exists(directory))
            {
                return $"{directory} is missing";
            }

            if (!IsOurs(directory, out var problem))
            {
                return $"{directory} {problem}";
            }
        }

        return null;
    }

    private static IEnumerable<string> DataDirectories() => [Path.GetDirectoryName(PanPaths.DataDirectory)!, PanPaths.DataDirectory];

    private static bool Exists(string path) => Directory.Exists(path) || File.Exists(path);

    /// <summary>A real folder, owned by Administrators or SYSTEM, that nobody else may change (reading is fine).</summary>
    private static bool IsOurs(string directory, out string problem)
    {
        try
        {
            var info = new DirectoryInfo(directory);
            if (!info.Exists)
            {
                problem = "is not a folder";
                return false;
            }

            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                problem = "is a junction or symbolic link";
                return false;
            }

            var security = info.GetAccessControl(AccessControlSections.Owner | AccessControlSections.Access);
            var owner = security.GetOwner(typeof(SecurityIdentifier));
            if (owner is not SecurityIdentifier sid || (sid != LocalSystem && sid != Administrators))
            {
                problem = $"is owned by {owner}";
                return false;
            }

            if (!security.AreAccessRulesProtected)
            {
                problem = "inherits access rules";
                return false;
            }

            foreach (FileSystemAccessRule rule in security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier)))
            {
                if (rule.AccessControlType == AccessControlType.Allow && rule.IdentityReference is SecurityIdentifier who && who != LocalSystem && who != Administrators
                    && DataFolderRights.Writes((uint)rule.FileSystemRights))
                {
                    problem = $"lets {who} write";
                    return false;
                }
            }

            problem = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problem = $"can't be checked: {ex.Message}";
            return false;
        }
    }

    /// <summary>A link goes as the link alone; a folder is renamed, which the parent's rights allow whatever its own say.</summary>
    /// <returns>Where it went, or "deleted" for a link.</returns>
    private static string MoveAside(string directory)
    {
        var attributes = File.GetAttributes(directory);
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            if (attributes.HasFlag(FileAttributes.Directory))
            {
                Directory.Delete(directory, recursive: false);
            }
            else
            {
                File.Delete(directory);
            }

            return "deleted (a link)";
        }

        var aside = $"{directory}.untrusted-{DateTime.UtcNow:yyyyMMddHHmmss}";
        if (attributes.HasFlag(FileAttributes.Directory))
        {
            Directory.Move(directory, aside);
        }
        else
        {
            File.Move(directory, aside);
        }

        return aside;
    }

    /// <summary>The parent folder Pan created for itself goes when nothing else is in it; never anything inside.</summary>
    private static void DeleteIfEmpty(string directory, Action<string> log)
    {
        try
        {
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory, recursive: false);
                log($"Deleted {directory}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log($"{directory} not deleted: {ex.Message}");
        }
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
