using System.Globalization;
using Arcadia.Launchers.Steam;

namespace Arcadia.App.Tests.Fixtures;

/// <summary>
/// One app manifest (<c>steamapps/appmanifest_&lt;id&gt;.acf</c>) as Steam writes it; the fields and their meaning are in
/// <c>docs/launchers/steam.md</c>. The ids are made up, apart from Steam's own tools, which Arcadia knows by id.
/// </summary>
internal sealed record SteamApp(string AppId, string Name)
{
    /// <summary>4 = installed, 6 = with an update pending; 2 with build 0 = a new install, 1026 downloading or waiting, 1538 paused.</summary>
    public int StateFlags { get; init; } = 4;

    public long BuildId { get; init; } = 1234567;

    public long SizeOnDisk { get; init; } = 12_884_901_888;

    /// <summary>Unix seconds; 0 = never played.</summary>
    public long LastPlayed { get; init; }

    /// <summary>The owner's SteamID64; "0" when nobody owns the app any more.</summary>
    public string LastOwner { get; init; } = SteamFolder.SteamId64(SteamFolder.Account).ToString(CultureInfo.InvariantCulture);

    public long BytesToDownload { get; init; }

    public long BytesDownloaded { get; init; }

    public long BytesToStage { get; init; }

    public long BytesStaged { get; init; }

    public string InstallDir => new(Name.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray());

    public string Acf => $$"""
        "AppState"
        {
        	"appid"		"{{AppId}}"
        	"Universe"		"1"
        	"name"		"{{Name}}"
        	"StateFlags"		"{{StateFlags}}"
        	"installdir"		"{{InstallDir}}"
        	"LastUpdated"		"1775526912"
        	"LastPlayed"		"{{LastPlayed}}"
        	"SizeOnDisk"		"{{SizeOnDisk}}"
        	"buildid"		"{{BuildId}}"
        	"LastOwner"		"{{LastOwner}}"
        	"BytesToDownload"		"{{BytesToDownload}}"
        	"BytesDownloaded"		"{{BytesDownloaded}}"
        	"BytesToStage"		"{{BytesToStage}}"
        	"BytesStaged"		"{{BytesStaged}}"
        	"UserConfig"
        	{
        		"language"		"german"
        	}
        }
        """;
}

/// <summary>
/// A Steam folder as on the owner's PC, built from nothing: <c>steam.exe</c>, <c>steamapps</c> with
/// <c>libraryfolders.vdf</c> and the manifests, libraries on other drives, <c>config/loginusers.vdf</c>, the accounts'
/// <c>userdata/&lt;id&gt;/config</c> and <c>logs/content_log.txt</c>. Nothing exists until <see cref="Install"/>.
/// </summary>
internal sealed class SteamFolder(string root)
{
    /// <summary>The made-up account most tests play with.</summary>
    public const uint Account = 100_000_001;

    private const ulong SteamId64Base = 76561197960265728;

    private readonly List<string> _libraries = [];

    public string Root => root;

    public static ulong SteamId64(uint accountId) => SteamId64Base + accountId;

    /// <summary>Steam is installed: its folder with <c>steam.exe</c> and an empty library.</summary>
    /// <summary>The account signed in to the running Steam (registry <c>ActiveUser</c>); null while Steam is closed.</summary>
    public uint? ActiveUser { get; set; }

    public SteamFolder Install()
    {
        Directory.CreateDirectory(Path.Combine(root, "steamapps"));
        File.WriteAllBytes(Path.Combine(root, "steam.exe"), []);
        WriteLibraryFolders();
        return this;
    }

    /// <summary>A second library, e.g. on another drive; Steam lists it in <c>libraryfolders.vdf</c>.</summary>
    public string AddLibrary(string path)
    {
        Directory.CreateDirectory(Path.Combine(path, "steamapps"));
        _libraries.Add(path);
        WriteLibraryFolders();
        return path;
    }

    /// <summary>
    /// Writes the app's manifest into <paramref name="library"/> (Steam's own folder if null) and, for an installed app, its
    /// game folder, created on <paramref name="installedAt"/>.
    /// </summary>
    public void Write(SteamApp app, string? library = null, DateTime? installedAt = null)
    {
        library ??= root;
        File.WriteAllText(ManifestPath(app.AppId, library), app.Acf);
        if ((app.StateFlags & 4) != 0)
        {
            var folder = Path.Combine(library, "steamapps", "common", app.InstallDir);
            Directory.CreateDirectory(folder);
            if (installedAt is { } date)
            {
                // Not every file system keeps a creation time; where it can't be set, the folder's own counts.
                Directory.SetCreationTimeUtc(folder, date);
            }
        }
    }

    /// <summary>
    /// Steam's app cache <c>appcache/appinfo.vdf</c> (#141, format v29) with each app's type as Steam writes it, e.g.
    /// <c>Tool</c>; the manifests hold none.
    /// </summary>
    public void WriteAppInfo(params (SteamApp App, string Type)[] apps)
    {
        var path = Path.Combine(root, "appcache", "appinfo.vdf");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Arcadia.Core.Tests.AppInfoFile.Build(
            Arcadia.Core.Tests.AppInfoFile.V29,
            apps.Select(app => (uint.Parse(app.App.AppId, CultureInfo.InvariantCulture), app.App.Name, app.Type))));
    }

    /// <summary>Steam deletes the manifest when a new install is cancelled or a game uninstalled.</summary>
    public void Remove(string appId, string? library = null)
    {
        File.Delete(ManifestPath(appId, library ?? root));
    }

    public string GameFolder(SteamApp app, string? library = null) => Path.Combine(library ?? root, "steamapps", "common", app.InstallDir);

    /// <summary>An account that logged in to Steam on this PC: an entry in <c>loginusers.vdf</c> and its userdata folder.</summary>
    public void AddAccount(uint accountId, string personaName, long timestamp, bool mostRecent = false)
    {
        Directory.CreateDirectory(Path.Combine(UserData(accountId), "config"));
        _accounts.Add((accountId, personaName, timestamp, mostRecent));
        var users = string.Concat(_accounts.Select(a => $$"""
            	"{{SteamId64(a.Id)}}"
            	{
            		"AccountName"		"account{{a.Id}}"
            		"PersonaName"		"{{a.Name}}"
            		"RememberPassword"		"1"
            		"MostRecent"		"{{(a.MostRecent ? 1 : 0)}}"
            		"Timestamp"		"{{a.Timestamp}}"
            	}

            """));
        Directory.CreateDirectory(Path.Combine(root, "config"));
        File.WriteAllText(Path.Combine(root, "config", "loginusers.vdf"), $"\"users\"\n{{\n{users}}}\n");
    }

    private readonly List<(uint Id, string Name, long Timestamp, bool MostRecent)> _accounts = [];

    /// <summary>Playtime and last start of each app in the account's <c>localconfig.vdf</c>.</summary>
    public void WritePlayStats(uint accountId, params (string AppId, long LastPlayed, long PlaytimeMinutes)[] apps)
    {
        var entries = string.Concat(apps.Select(app => $$"""
            					"{{app.AppId}}"
            					{
            						"LastPlayed"		"{{app.LastPlayed}}"
            						"Playtime"		"{{app.PlaytimeMinutes}}"
            					}

            """));
        Directory.CreateDirectory(Path.Combine(UserData(accountId), "config"));
        File.WriteAllText(Path.Combine(UserData(accountId), "config", "localconfig.vdf"), $$"""
            "UserLocalConfigStore"
            {
            	"Software"
            	{
            		"Valve"
            		{
            			"Steam"
            			{
            				"apps"
            				{
            {{entries}}				}
            			}
            		}
            	}
            }
            """);
    }

    /// <summary>Lines Steam adds to <c>logs/content_log.txt</c>, e.g. <c>AppID 1 state changed : Update Running,</c>.</summary>
    public void AppendContentLog(params string[] lines)
    {
        Directory.CreateDirectory(Path.Combine(root, "logs"));
        File.AppendAllLines(Path.Combine(root, "logs", "content_log.txt"), lines.Select(line => $"[2026-10-24 20:00:00] {line}"));
    }

    /// <summary>The non-Steam games in the account's Steam library (<c>shortcuts.vdf</c>).</summary>
    public IReadOnlyList<SteamShortcut> ShortcutsOf(uint accountId)
    {
        var path = Path.Combine(UserData(accountId), "config", "shortcuts.vdf");
        return File.Exists(path) ? SteamShortcuts.Read(path) : [];
    }

    public string UserData(uint accountId) => Path.Combine(root, "userdata", accountId.ToString(CultureInfo.InvariantCulture));

    private static string ManifestPath(string appId, string library) => Path.Combine(library, "steamapps", $"appmanifest_{appId}.acf");

    private void WriteLibraryFolders()
    {
        var folders = string.Concat(new[] { root }.Concat(_libraries).Select((path, index) => $$"""
            	"{{index}}"
            	{
            		"path"		"{{path.Replace("\\", "\\\\", StringComparison.Ordinal)}}"
            		"label"		""
            	}

            """));
        File.WriteAllText(Path.Combine(root, "steamapps", "libraryfolders.vdf"), $"\"libraryfolders\"\n{{\n{folders}}}\n");
    }
}
