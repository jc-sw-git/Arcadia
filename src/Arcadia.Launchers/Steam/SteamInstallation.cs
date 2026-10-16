using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32;

namespace Arcadia.Launchers.Steam;

/// <summary>Where Steam is installed, who is logged in, and whether it runs.</summary>
public sealed class SteamInstallation(string rootPath, uint? activeUserId)
{
    public string RootPath { get; } = rootPath;

    /// <summary>The logged-in account (Steam3 account id), or null if nobody is logged in.</summary>
    public uint? ActiveUserId { get; } = activeUserId;

    /// <summary>
    /// While this empty file exists, Steam opens its local CEF debug interface on its next start.
    /// SISR needs it to set the controller layout.
    /// </summary>
    public string CefDebuggingFlagPath => Path.Combine(RootPath, ".cef-enable-remote-debugging");

    public string? ShortcutsPath => UserDataDirectory is { } dir ? Path.Combine(dir, "config", "shortcuts.vdf") : null;

    /// <summary>
    /// The userdata folder of the active account. If nobody is logged in right now (e.g. Steam is
    /// closed) and there is exactly one account, that one.
    /// </summary>
    public string? UserDataDirectory
    {
        get
        {
            var userData = Path.Combine(RootPath, "userdata");
            if (ActiveUserId is { } id and not 0)
            {
                var dir = Path.Combine(userData, id.ToString(CultureInfo.InvariantCulture));
                return Directory.Exists(dir) ? dir : null;
            }

            if (!Directory.Exists(userData))
            {
                return null;
            }

            var accounts = Directory.GetDirectories(userData).Where(d => Path.GetFileName(d) != "0").ToList();
            return accounts.Count == 1 ? accounts[0] : null;
        }
    }

    public static bool IsRunning => ProcessWatch.IsRunning("steam");

    /// <summary>
    /// True when this process was started by Steam (e.g. as a non-Steam game). Steam sets these
    /// variables for everything it launches.
    /// </summary>
    public static bool IsCurrentProcessLaunchedBySteam =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SteamGameId"))
        || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SteamAppId"));

    /// <summary>
    /// The Steam game running right now, by its name in Steam's registry (or its app id), or null.
    /// Steam sets <c>RunningAppID</c> while one of its games runs.
    /// </summary>
    public static string? RunningGameName()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        using var steam = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        if (steam?.GetValue("RunningAppID") is not int appId || appId == 0)
        {
            return null;
        }

        var id = appId.ToString(CultureInfo.InvariantCulture);
        using var app = steam.OpenSubKey(@"Apps\" + id);
        return app?.GetValue("Name") is string name && !string.IsNullOrWhiteSpace(name) ? name : $"App {id}";
    }

    public static SteamInstallation? Find()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        using var steam = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        var root = (steam?.GetValue("SteamPath") as string)?.Replace('/', Path.DirectorySeparatorChar);
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            if (!Directory.Exists(root))
            {
                return null;
            }
        }

        using var process = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
        uint? activeUser = process?.GetValue("ActiveUser") is int value ? unchecked((uint)value) : null;
        return new SteamInstallation(root, activeUser);
    }

    /// <summary>Asks a running Steam to shut down and waits for it.</summary>
    public static async Task<bool> ShutdownAsync(TimeSpan timeout, TimeProvider time, CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
        {
            return true;
        }

        Process.Start(new ProcessStartInfo("steam://exit") { UseShellExecute = true })?.Dispose();
        return await ProcessWatch.WaitUntilAsync(() => !IsRunning, timeout, time, cancellationToken);
    }

    /// <summary>Starts Steam minimized to the tray.</summary>
    public void StartSilently()
    {
        var exe = Path.Combine(RootPath, "steam.exe");
        if (File.Exists(exe))
        {
            Process.Start(new ProcessStartInfo(exe, "-silent") { UseShellExecute = false })?.Dispose();
        }
    }
}
