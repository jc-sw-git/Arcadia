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

    /// <summary>Steam's log of its client logins, read by <see cref="SteamLoginLog"/>.</summary>
    public string LoginLogPath => Path.Combine(RootPath, "logs", "steamui_login.txt");

    /// <summary>
    /// While this empty file exists, Steam opens its local CEF debug interface on its next start.
    /// SISR needs it to set the controller layout; "SISR entfernen" deletes it (#214).
    /// </summary>
    public string CefDebuggingFlagPath => Path.Combine(RootPath, ".cef-enable-remote-debugging");

    public string? ShortcutsPath => UserDataDirectory is { } dir ? Path.Combine(dir, "config", "shortcuts.vdf") : null;

    /// <summary>The account's settings, among them Steam Input per game (<see cref="SteamInputSetting"/>).</summary>
    public string? LocalConfigPath => UserDataDirectory is { } dir ? Path.Combine(dir, "config", "localconfig.vdf") : null;

    /// <summary>
    /// The userdata folder of the active account. If nobody is logged in right now (e.g. Steam is
    /// closed): the only account, or with several the one that logged in last (<see cref="LastLoggedInUserId"/>).
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
            if (accounts.Count == 1)
            {
                return accounts[0];
            }

            return LastLoggedInUserId() is { } last
                && Path.Combine(userData, last.ToString(CultureInfo.InvariantCulture)) is var lastDir && Directory.Exists(lastDir)
                ? lastDir
                : null;
        }
    }

    /// <summary>The userdata folder of one account, whether or not it is logged in.</summary>
    public string UserDataDirectoryOf(uint accountId) =>
        Path.Combine(RootPath, "userdata", accountId.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// The accounts that can log in to Steam on this PC, from <c>config/loginusers.vdf</c> and named after
    /// their profile, the one that logged in last first. Accounts removed from Steam's login list keep their
    /// userdata folder, so the folders count (named by id) only while that file is missing or unreadable.
    /// </summary>
    public IReadOnlyList<SteamAccount> Accounts()
    {
        var users = LoginUsers();
        IEnumerable<SteamAccount> accounts = users.Count > 0
            ? users.Select(u => new SteamAccount(u.Key, u.Value.GetString("PersonaName") is { Length: > 0 } name ? name : u.Key.ToString(CultureInfo.InvariantCulture)))
            : UserDataAccountIds().Select(id => new SteamAccount(id, id.ToString(CultureInfo.InvariantCulture)));
        var last = LastLoggedInUserId();
        return accounts
            .OrderByDescending(a => a.Id == last)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The <c>shortcuts.vdf</c> of every userdata folder, also of accounts removed from Steam's login list: those
    /// keep their folder, and logging in again brings their non-Steam games back.
    /// </summary>
    public IEnumerable<string> AllShortcutsPaths() =>
        UserDataAccountIds().Select(id => Path.Combine(UserDataDirectoryOf(id), "config", "shortcuts.vdf"));

    private IEnumerable<uint> UserDataAccountIds()
    {
        var userData = Path.Combine(RootPath, "userdata");
        return Directory.Exists(userData)
            ? Directory.GetDirectories(userData)
                .Select(dir => uint.TryParse(Path.GetFileName(dir), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : 0)
                .Where(id => id != 0)
            : [];
    }

    /// <summary>Base of 64-bit Steam ids; the account id is the difference.</summary>
    private const ulong SteamId64Base = 76561197960265728;

    /// <summary>
    /// The account that logged in last, from <c>config/loginusers.vdf</c>: the one marked
    /// <c>MostRecent</c>, else the newest <c>timestamp</c> (with one account Steam writes no
    /// <c>MostRecent</c>; owner's PC, 2026-10-16). Null if the file is missing or unreadable.
    /// </summary>
    public uint? LastLoggedInUserId()
    {
        var last = LoginUsers()
            .OrderByDescending(u => u.Value.GetString("MostRecent") == "1")
            .ThenByDescending(u => long.TryParse(u.Value.GetString("timestamp"), NumberStyles.None, CultureInfo.InvariantCulture, out var t) ? t : 0)
            .FirstOrDefault();
        return last.Value is null ? null : last.Key;
    }

    /// <summary>The entries of <c>config/loginusers.vdf</c> by account id; empty if the file is missing or unreadable.</summary>
    private Dictionary<uint, VdfMap> LoginUsers()
    {
        VdfMap users;
        try
        {
            users = TextVdf.Parse(File.ReadAllText(Path.Combine(RootPath, "config", "loginusers.vdf"))).GetMap("users") ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            return [];
        }

        var byAccount = new Dictionary<uint, VdfMap>();
        foreach (var user in users)
        {
            if (user.Value is VdfMap map && ulong.TryParse(user.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var id64)
                && id64 > SteamId64Base && id64 - SteamId64Base <= uint.MaxValue)
            {
                byAccount[(uint)(id64 - SteamId64Base)] = map;
            }
        }

        return byAccount;
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
        if (!OperatingSystem.IsWindows() || RunningAppId() is not { } appId)
        {
            return null;
        }

        var id = appId.ToString(CultureInfo.InvariantCulture);
        using var app = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\Apps\" + id);
        return app?.GetValue("Name") is string name && !string.IsNullOrWhiteSpace(name) ? name : $"App {id}";
    }

    /// <summary>The app id of the Steam game running right now (<c>RunningAppID</c>), or null.</summary>
    public static int? RunningAppId()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        using var steam = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        return steam?.GetValue("RunningAppID") is int appId && appId != 0 ? appId : null;
    }

    // Set by the user's folder choice (#138). Static, because every part of Arcadia that needs Steam asks Find, and a PC
    // has one Steam; replaced as a whole, so another thread sees the old folder or the new one.
    private static volatile string? _chosenRoot;

    /// <summary>
    /// The folder the user picked for Steam in the launcher settings (#138), one <see cref="RootIn"/> accepted; from then on
    /// <see cref="Find"/> looks only there. Null: Steam is looked for in the registry and the usual place again.
    /// </summary>
    public static string? ChosenRoot
    {
        get => _chosenRoot;
        set => _chosenRoot = value;
    }

    /// <summary>
    /// The Steam folder at <paramref name="folder"/>: the folder itself if it holds <c>steam.exe</c> and <c>steamapps</c>,
    /// or the one above if the user picked <c>steamapps</c> itself. Null if there is no Steam.
    /// </summary>
    public static string? RootIn(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathFullyQualified(folder))
        {
            return null;
        }

        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        if (IsRoot(full))
        {
            return full;
        }

        return string.Equals(Path.GetFileName(full), "steamapps", StringComparison.OrdinalIgnoreCase)
            && Path.GetDirectoryName(full) is { } parent && IsRoot(parent)
            ? parent
            : null;

        static bool IsRoot(string dir) => File.Exists(Path.Combine(dir, "steam.exe")) && Directory.Exists(Path.Combine(dir, "steamapps"));
    }

    public static SteamInstallation? Find()
    {
        if (ChosenRoot is { } chosen)
        {
            return Directory.Exists(chosen) ? new SteamInstallation(chosen, ActiveUserFromRegistry()) : null;
        }

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

        return new SteamInstallation(root, ActiveUserFromRegistry());
    }

    /// <summary>The account logged in to the running Steam; a Steam without registry entry has none, so null.</summary>
    private static uint? ActiveUserFromRegistry()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        using var process = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
        return process?.GetValue("ActiveUser") is int value ? unchecked((uint)value) : null;
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

/// <summary>A Steam account that used Steam on this PC: its Steam3 account id and profile name.</summary>
public sealed record SteamAccount(uint Id, string Name);
